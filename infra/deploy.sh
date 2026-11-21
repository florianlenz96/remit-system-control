#!/usr/bin/env bash
# Deploys Remit into a resource group of your Azure subscription (ADR 0003): registry, Container Apps,
# Entra ID sign-in and the key for the operations agent. Needs: Azure CLI, signed in (az login),
# permission to create resource groups, role assignments and app registrations.
#
#   infra/deploy.sh <resource-group> [location] [existing-components.json]
#
# existing-components.json (optional) lists building blocks Remit takes over, e.g.
#   [{ "name": "Catalog cache", "kind": "Cache", "resourceId": "/subscriptions/…/Microsoft.Cache/redis/catalog" }]
# Set MCP_API_KEY to keep the key of an earlier deployment (otherwise a new one is generated).
set -euo pipefail

RG=${1:?usage: infra/deploy.sh <resource-group> [location] [existing-components.json]}
LOCATION=${2:-westeurope}
COMPONENTS=${3:-}
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TAG=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props")
COMPONENTS_JSON='[]'
if [[ -n "$COMPONENTS" ]]; then COMPONENTS_JSON=$(cat "$COMPONENTS"); fi

echo "1/5 Resource group $RG in $LOCATION"
az group create --name "$RG" --location "$LOCATION" --output none

echo "2/5 Registry and environment"
OUT=$(az deployment group create --resource-group "$RG" --template-file "$ROOT/infra/main.bicep" \
  --parameters deployApp=false --query properties.outputs --output json)
REGISTRY=$(jq -r .registryName.value <<<"$OUT")
HOST=$(jq -r .appHost.value <<<"$OUT")

echo "3/5 Building image remit:$TAG in $REGISTRY"
az acr build --registry "$REGISTRY" --image "remit:$TAG" "$ROOT" --output none

echo "4/5 Entra ID app registration for signing in"
NAME="Remit ($RG)"
CLIENT_ID=$(az ad app list --display-name "$NAME" --query "[0].appId" --output tsv)
if [[ -z "$CLIENT_ID" ]]; then
  CLIENT_ID=$(az ad app create --display-name "$NAME" --sign-in-audience AzureADMyOrg --query appId --output tsv)
  az ad sp create --id "$CLIENT_ID" --output none
fi
az ad app update --id "$CLIENT_ID" --enable-id-token-issuance true \
  --web-redirect-uris "https://$HOST/.auth/login/aad/callback"

echo "5/5 App"
MCP_API_KEY=${MCP_API_KEY:-$(openssl rand -base64 36 | tr -d '\n/+=')}
OUT=$(az deployment group create --resource-group "$RG" --template-file "$ROOT/infra/main.bicep" \
  --parameters deployApp=true imageTag="$TAG" entraClientId="$CLIENT_ID" mcpApiKey="$MCP_API_KEY" \
               existingComponents="$COMPONENTS_JSON" \
  --query properties.outputs --output json)
URL=$(jq -r .appUrl.value <<<"$OUT")

cat <<INFO

Remit is running: $URL  (sign in with your Entra ID account)

For the operations agent (docs/sre-agent.md):
  MCP endpoint : $URL/mcp
  Bearer token : $MCP_API_KEY
Store the token only in the agent's connector. To redeploy with the same token:
  MCP_API_KEY='<token>' infra/deploy.sh $RG $LOCATION ${COMPONENTS}
INFO
