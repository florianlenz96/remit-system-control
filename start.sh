#!/usr/bin/env bash
# Builds the web app and starts Remit on http://localhost:5000.
set -euo pipefail
cd "$(dirname "$0")"
npm --prefix src/Remit.Web ci
npm --prefix src/Remit.Web run build
dotnet run --project src/Remit.App
