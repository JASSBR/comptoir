#!/usr/bin/env bash
# Deploys Comptoir to Azure with Terraform (deploy/terraform):
#   1. infrastructure, 2. legacy database scripts (DbUp), 3. the legacy application on App Service Windows,
#   4. the facade and the API on Container Apps.
# Prerequisites: `az login`, Docker, Terraform, deploy/terraform/terraform.tfvars (see terraform.tfvars.example).
# The legacy package is the CI artifact when LEGACY_PACKAGE points to it (built on Windows, views precompiled);
# otherwise it is assembled here from a local build, as an xcopy deployment of 2014 would have been.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.dotnet:$PATH"

TFVARS=deploy/terraform/terraform.tfvars
[ -f "$TFVARS" ] || { echo "✗ Missing $TFVARS"; exit 1; }
az account show >/dev/null 2>&1 || { echo "✗ Not logged in: run 'az login' first."; exit 1; }
ACR=$(sed -nE 's/^registry_name *= *"([^"]+)".*/\1/p' "$TFVARS")
REGISTRY=$(az acr show -n "$ACR" --query loginServer -o tsv)
TAG="$(git rev-parse --short HEAD)-$(date +%H%M%S)"

echo "→ images comptoir-api / comptoir-facade:$TAG (linux/amd64)"
az acr login -n "$ACR" --only-show-errors >/dev/null
docker buildx build --platform linux/amd64 --target api --push -q -t "$REGISTRY/comptoir-api:$TAG" . >/dev/null
docker buildx build --platform linux/amd64 --target facade --push -q -t "$REGISTRY/comptoir-facade:$TAG" . >/dev/null

echo "→ terraform apply"
(cd deploy/terraform && terraform init -input=false >/dev/null && terraform apply -input=false -auto-approve -var "image_tag=$TAG" >/dev/null)
output() { (cd deploy/terraform && terraform output -raw "$1"); }

echo "→ legacy database scripts"
ConnectionStrings__comptoir="$(output sql_connection_string)" dotnet run --project src/Comptoir.DbMigrator >/dev/null

echo "→ legacy application"
PACKAGE="${LEGACY_PACKAGE:-}"
if [ -z "$PACKAGE" ]; then
  STAGING=$(mktemp -d)
  (cd legacy/Comptoir.Web && dotnet build -c Release >/dev/null && cp -R bin Views Scripts Content Global.asax web.config "$STAGING/")
  rm -f "$STAGING"/bin/*.pdb "$STAGING"/bin/Comptoir.Web.dll.config
  PACKAGE="$STAGING.zip"
  (cd "$STAGING" && zip -qr "$PACKAGE" .)
fi
az webapp deploy -g "rg-comptoir" -n "$(output legacy_app_name)" --src-path "$PACKAGE" --type zip --only-show-errors >/dev/null

FACADE=$(output facade_url)
echo "→ waiting for $FACADE"
for _ in $(seq 1 60); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' "$FACADE/health")" = 200 ] && break
  sleep 5
done
echo "✓ facade: $FACADE   (legacy: $(output legacy_url))"
