#!/bin/bash
set -e

echo "=== Step 1: Build APK ==="
dotnet build -f "$1" -r "$2" Test.cs -v n 2>&1

echo "=== Step 2: Locate APK ==="
RUNFILE_DIR=$(find /home/runner/.local/share/dotnet/runfile -maxdepth 1 -type d -name "Test-*" 2>/dev/null | head -1)
echo "Runfile dir: $RUNFILE_DIR"
APK=""
if [ -n "$RUNFILE_DIR" ]; then
  APK=$(find "$RUNFILE_DIR" -name "*.apk" 2>/dev/null | head -1)
fi

if [ -n "$APK" ]; then
  echo "APK found: $APK"
  ls -la "$APK"

  echo "=== Step 3: APK lib contents ==="
  unzip -l "$APK" 2>/dev/null | grep "lib/" | head -20

  echo "=== Step 4: APK manifest ==="
  aapt dump badging "$APK" 2>/dev/null || echo "aapt not available"

  echo "=== Step 5: Clear old package ==="
  adb uninstall org.box2d.Box3D 2>/dev/null || true

  echo "=== Step 6: Manual install ==="
  adb install -r -d "$APK" 2>&1
  echo "Install exit code: $?"

  echo "=== Step 7: Check installed ==="
  adb shell pm list packages 2>/dev/null | grep box2d || echo "NOT INSTALLED"

  echo "=== Step 8: am start test ==="
  adb shell am start -S -W -n "org.box2d.Box3D/MainActivity" 2>&1 || true
else
  echo "No APK found, skipping manual steps"
fi

echo "=== Step 9: dotnet run fallback ==="
dotnet run -f "$1" -r "$2" Test.cs 2>&1 || true
