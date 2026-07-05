#!/bin/bash

echo "=== Step 1: dotnet run (build+sign+install+run) ==="
dotnet run -f "$1" -r "$2" Test.cs 2>&1
echo "dotnet run exit code: $?"

echo "=== Step 2: Check installed package ==="
adb shell pm list packages 2>/dev/null | grep box2d || echo "NOT INSTALLED"

echo "=== Step 3: am start test ==="
adb shell am start -S -W -n "org.box2d.Box3D/MainActivity" 2>&1 || true
