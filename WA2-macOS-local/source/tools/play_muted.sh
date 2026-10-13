#!/bin/bash
# SPDX-License-Identifier: MIT
set -euo pipefail
BASE="$(cd "$(dirname "$0")/.." && pwd)"
APP="${1:-$HOME/Applications/WHITE ALBUM2.app}"
[[ "$(uname -s)" == Darwin ]] || { echo 'macOS host required.' >&2; exit 2; }
EXE="$APP/Contents/MacOS/WHITE ALBUM2"
[[ -x "$EXE" ]] || { echo "Missing executable: $EXE" >&2; exit 2; }
RESULT="$(mktemp -d "${TMPDIR:-/tmp}/wa2-play-muted.XXXXXX")"
echo "Muted interactive test; original saves untouched. Test files: $RESULT"
"$EXE" --audio-driver Dummy --log-file "$RESULT/godot.log" -- --save-dir "$RESULT/saves" > "$RESULT/console.log" 2>&1
echo "Test ended. Logs and temporary saves retained: $RESULT"
