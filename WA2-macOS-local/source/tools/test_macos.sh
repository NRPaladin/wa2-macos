#!/bin/bash
# SPDX-License-Identifier: MIT
# Local-only validation: mute by default; never touch existing game saves.
set -euo pipefail
BASE="$(cd "$(dirname "$0")/.." && pwd)"
APP="${1:-$HOME/Applications/WHITE ALBUM2.app}"
CHAPTER="${2:-2001}"
DRIVER="${3:-Dummy}"
case "$DRIVER" in Dummy|CoreAudio) ;; *) echo "Driver must be Dummy or CoreAudio." >&2; exit 2;; esac
if [[ "$(uname -s)" != Darwin ]]; then echo 'This test must run on macOS.' >&2; exit 2; fi
EXE="$APP/Contents/MacOS/WHITE ALBUM2"
if [[ ! -x "$EXE" ]]; then echo "Missing/non-executable application: $EXE" >&2; exit 2; fi
case "$CHAPTER" in 1001|2001|3001) ;; *) echo 'Chapter must be 1001, 2001 or 3001.' >&2; exit 2;; esac
RESULT="$(mktemp -d "${TMPDIR:-/tmp}/wa2-test-${CHAPTER}.XXXXXX")"
echo "Isolated test results: $RESULT"
printf '%s\n' "$(sw_vers)" "Architecture: $(uname -m)" > "$RESULT/host.txt"
# No global volume changes, no access to original save folders.
ARGS=("$EXE" --audio-driver "$DRIVER" --log-file "$RESULT/godot.log" --
  --smoke-test --save-dir "$RESULT/saves" --smoke-chapter "$CHAPTER")
if [[ "$DRIVER" == CoreAudio ]]; then ARGS+=(--audio-check); fi
"${ARGS[@]}" > "$RESULT/console.log" 2>&1 &
GAME_PID=$!
# Bound failures such as a stopped render loop; only terminate the process we launched.
trap 'kill "$GAME_PID" 2>/dev/null || true' INT TERM
for ((i=0; i<360; i++)); do
  if ! kill -0 "$GAME_PID" 2>/dev/null; then break; fi
  sleep 1
done
if kill -0 "$GAME_PID" 2>/dev/null; then
  kill "$GAME_PID" 2>/dev/null || true
  echo "FAIL: timed out. Preserve $RESULT for diagnosis." >&2
  exit 1
fi
STATUS=0
wait "$GAME_PID" || STATUS=$?
cat "$RESULT/console.log"
if [[ "$STATUS" -ne 0 ]] || ! grep -q '^SMOKE PASS' "$RESULT/console.log"; then
  echo "FAIL: inspect $RESULT" >&2
  exit 1
fi
if grep -Eq '(^ERROR:|SCRIPT ERROR:|Unhandled exception)' "$RESULT/console.log"; then
  echo "FAIL: errors were logged despite completion. Inspect $RESULT" >&2
  exit 1
fi
echo "PASS ($DRIVER). Screenshots and isolated saves retained: $RESULT"
