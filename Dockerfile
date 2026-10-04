# syntax=docker/dockerfile:1
# Images of the new side of the migration (the legacy is deployed to IIS, see .github/workflows/ci.yml):
#   docker build --target api    -t comptoir-api .
#   docker build --target facade -t comptoir-facade .     (embeds the Angular application under /app)
# Add --platform linux/amd64 on Apple Silicon for cloud targets.

# Both build stages run on the build machine's own architecture: their output is portable (static files,
# framework-dependent DLLs), and under amd64 emulation on Apple Silicon the build took the better part of an hour.
FROM --platform=$BUILDPLATFORM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN --mount=type=cache,id=npm,target=/root/.npm npm ci --no-audit --no-fund
COPY web/ ./
RUN npx ng build --configuration production

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Comptoir.Api/Comptoir.Api.csproj -c Release -o /out/api \
 && dotnet publish src/Comptoir.Facade/Comptoir.Facade.csproj -c Release -o /out/facade

# Chiseled runtime: no shell, non-root. "-extra" brings ICU: the facade and the API format French data.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comptoir.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS facade
WORKDIR /app
COPY --from=build /out/facade .
COPY --from=web /web/dist/app ./wwwroot/app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comptoir.Facade.dll"]
