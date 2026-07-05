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
  SIGNED_APK=$(find /home/runner/.local/share/dotnet/runfile -name "*-Signed.apk" -o -name "*.apk" -not -path "*/obj/*" 2>/dev/null | head -1)
  echo "Signed APK: $SIGNED_APK"

  echo "--- Look for debug keystore ---"
  for KS in "$HOME/.android/debug.keystore" "$HOME/.local/share/Xamarin/Mono for Android/debug.keystore" "$HOME/.local/share/Xamarin/debug.keystore"; do
    if [ -f "$KS" ]; then
      echo "Found keystore: $KS"
      KEYSTORE="$KS"
      break
    fi
  done
  if [ -z "$KEYSTORE" ]; then
    echo "No debug keystore found at any location"
    find "$HOME/.local/share/Xamarin" "$HOME/.android" -name "*.keystore" 2>/dev/null || true
  fi

  echo "--- Try installing signed APK directly ---"
  if [ -n "$SIGNED_APK" ]; then
    adb install -r -d "$SIGNED_APK" 2>&1 || true
    echo "After signed APK install:"
    adb shell pm list packages 2>/dev/null | grep box2d || echo "STILL NOT INSTALLED"
  fi

  echo "--- Try sign unsigned APK with found keystore ---"
  UNSIGNED_APK=$(find /home/runner/.local/share/dotnet/runfile -name "*.apk" 2>/dev/null | grep "/obj/" | head -1)
  if [ -n "$UNSIGNED_APK" ] && [ -n "$KEYSTORE" ]; then
    APKSIGNER=$(find /usr/local/lib/android/sdk -name "apksigner" 2>/dev/null | head -1)
    if [ -n "$APKSIGNER" ]; then
      cp "$UNSIGNED_APK" "${UNSIGNED_APK}.signed"
      "$APKSIGNER" sign --ks "$KEYSTORE" --ks-pass pass:android "${UNSIGNED_APK}.signed" 2>&1 || echo "Manual sign failed"
      adb install -r -d "${UNSIGNED_APK}.signed" 2>&1 || true
      echo "After manual sign+install:"
      adb shell pm list packages 2>/dev/null | grep box2d || echo "STILL NOT INSTALLED"
    fi
  fi
fi

echo "=== Done ==="
exit 0
