#!/bin/bash
# SPDX-License-Identifier: MIT
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
[[ "$(uname -s)" == Darwin ]] || { echo 'macOS host required.' >&2; exit 2; }
: "${GODOT_BIN:?Set GODOT_BIN to the Godot 4.5.1 .NET executable inside its .app}"
command -v dotnet >/dev/null || { echo '.NET SDK 9.0.3xx required; see docs/BUILD.md' >&2; exit 2; }
"$GODOT_BIN" --version | grep -q '^4.5.1.stable.mono' || { echo 'Godot 4.5.1 .NET required.' >&2; exit 2; }
mkdir -p build .tools/nuget .tools/export-templates/templates
TEMPLATE="${GODOT_MACOS_TEMPLATE:-$HOME/Library/Application Support/Godot/export_templates/4.5.1.stable.mono/macos.zip}"
if [[ ! -f .tools/export-templates/templates/macos.zip ]]; then
  [[ -f "$TEMPLATE" ]] || { echo "Missing export template: $TEMPLATE" >&2; exit 2; }
  cp "$TEMPLATE" .tools/export-templates/templates/macos.zip
fi
if [[ ! -f .tools/nuget/Godot.NET.Sdk.4.5.1.nupkg ]]; then
  PACKAGES="$(dirname "$GODOT_BIN")/../Resources/GodotSharp/Tools/nupkgs"
  [[ -d "$PACKAGES" ]] || { echo 'Copy editor GodotSharp/Tools/nupkgs/*.nupkg into .tools/nuget.' >&2; exit 2; }
  cp "$PACKAGES"/*.nupkg .tools/nuget/
fi
dotnet build wa2.csproj --configuration Debug > build/dotnet-build-mac.log 2>&1
"$GODOT_BIN" --headless --audio-driver Dummy --editor --path "$ROOT" --import > build/import-mac.log 2>&1
"$GODOT_BIN" --headless --audio-driver Dummy --path "$ROOT" --export-release 'macOS Local' 'build/WHITE ALBUM2-macOS-final.zip' > build/export-macos-final.log 2>&1
if grep -E '^(ERROR:|SCRIPT ERROR:)' build/import-mac.log build/export-macos-final.log | grep -v '!EditorSettings::get_singleton'; then
  echo 'Export logged an error; inspect build logs.' >&2; exit 1
fi
echo 'Built build/WHITE ALBUM2-macOS-final.zip; run bundle verification and the muted integration test.'
