#!/usr/bin/env bash
# Runs the EditMode tests without Unity. Needs the .NET 8 SDK.
#   tools/test.sh              all tests
#   tools/test.sh content      content validation only
set -euo pipefail
cd "$(dirname "$0")/headless/Hearthline.Tests"
if [ "${1:-}" = "content" ]; then
  shift
  exec dotnet test --nologo --filter "FullyQualifiedName~ContentValidation" "$@"
fi
exec dotnet test --nologo "$@"
