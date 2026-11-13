#!/usr/bin/env bash
# Builds a universal release binary and packages it as dist/BreakReminder.app and a zip.
set -euo pipefail
cd "$(dirname "$0")/.."

ARCHS=(--arch arm64 --arch x86_64)
swift build -c release "${ARCHS[@]}"
BIN_DIR=$(swift build -c release "${ARCHS[@]}" --show-bin-path)

APP=dist/BreakReminder.app
rm -rf dist
mkdir -p "$APP/Contents/MacOS"
cp "$BIN_DIR/BreakReminder" "$APP/Contents/MacOS/"
cp Resources/Info.plist "$APP/Contents/"

# Ad-hoc signature: not notarized, but required to run on Apple silicon.
codesign --force --sign - "$APP"

ditto -c -k --keepParent "$APP" dist/BreakReminder-macos.zip
echo "Built $APP and dist/BreakReminder-macos.zip"
