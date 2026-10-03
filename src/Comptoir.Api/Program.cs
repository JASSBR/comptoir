using System.Text;
using Comptoir.Api;
using Comptoir.Api.Data;
using Comptoir.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ComptoirDb>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("comptoir") ?? throw new InvalidOperationException("Connection string 'comptoir' is missing."),
    sql => sql.EnableRetryOnFailure()));

// Users sign in on the legacy application; the facade turns that session into a short-lived token (ADR 0005).
var signingKey = builder.Configuration["Facade:SigningKey"] ?? throw new InvalidOperationException("Facade:SigningKey is missing.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
{
    jwt.MapInboundClaims = false;
    jwt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = FacadeToken.Issuer,
        ValidAudience = FacadeToken.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        NameClaimType = "sub",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
var api = app.MapGroup("/api").RequireAuthorization();
CatalogEndpoints.Map(api);
OrderEndpoints.Map(api);
QuoteEndpoints.Map(api);

await app.RunAsync();
