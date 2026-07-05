#!/bin/bash

echo "=== Step 1: dotnet run (build sign install run) ==="
dotnet run -f "$1" -r "$2" Test.cs 2>&1
DOTNET_EXIT=$?
echo "dotnet run exit code: $DOTNET_EXIT"

echo "=== Step 2: Check installed package ==="
adb shell pm list packages 2>/dev/null | grep box2d || echo "NOT INSTALLED"

echo "=== Step 3: Try direct am start ==="
adb shell am start -S -W -n "org.box2d.Box3D/MainActivity" 2>&1 || true

if [ "$DOTNET_EXIT" -ne 0 ]; then
  echo "=== Step 4: Manual diagnosis ==="
  echo "--- Look for signed APK in bin/ ---"
  find /home/runner/.local/share/dotnet/runfile -name "*.apk" -not -path "*/obj/*" 2>/dev/null | head -5
  echo "--- Check debug keystore ---"
  ls -la "$HOME/.android/debug.keystore" 2>/dev/null || echo "No debug keystore found"
  APK=$(find /home/runner/.local/share/dotnet/runfile -name "*.apk" 2>/dev/null | grep "/obj/" | head -1)
  if [ -n "$APK" ]; then
    echo "--- Try manual sign + install ---"
    APKSIGNER=$(find /usr/local/lib/android/sdk -name "apksigner" 2>/dev/null | head -1)
    if [ -n "$APKSIGNER" ]; then
      "$APKSIGNER" sign --ks "$HOME/.android/debug.keystore" --ks-pass pass:android "$APK" 2>&1 || echo "Manual sign failed"
      adb install -r -d "$APK" 2>&1 || true
    else
      echo "apksigner not found"
    fi
    adb shell pm list packages 2>/dev/null | grep box2d || echo "STILL NOT INSTALLED"
  fi
fi

echo "=== Done ==="
exit 0
