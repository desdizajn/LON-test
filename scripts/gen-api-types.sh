#!/usr/bin/env bash
# Generate OpenAPI schema + TypeScript types from the .NET API.
# Run from repo root: ./scripts/gen-api-types.sh
#
# Outputs:
#   api-contract/swagger.json           — OpenAPI 3 schema
#   frontend/web/src/api/schema.d.ts    — TS types
#
# CI: runs this, then `git diff --exit-code` — non-empty diff means a
# contract changed without committing regenerated types. Build fails.

set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> dotnet tool restore"
dotnet tool restore

echo "==> build LON.API"
dotnet build src/LON.API/LON.API.csproj --nologo --verbosity minimal

echo "==> dump OpenAPI schema"
mkdir -p api-contract
dotnet swagger tofile \
  --output api-contract/swagger.json \
  src/LON.API/bin/Debug/net8.0/LON.API.dll v1

echo "==> generate TypeScript types"
pushd frontend/web >/dev/null
  if [ ! -d node_modules ]; then
    echo "    (node_modules missing — running npm ci)"
    npm ci
  fi
  npm run gen:api-types
popd >/dev/null

echo "==> done"
echo "   api-contract/swagger.json"
echo "   frontend/web/src/api/schema.d.ts"
