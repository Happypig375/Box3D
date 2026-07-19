#!/bin/bash
# Builds the box3d native library for a target platform and copies the
# resulting binaries to native/<name>/ (or native/large-worlds/<name>/
# for double precision) for NuGet packaging.
#
# Works as both a standalone script and from CI.  Apple platforms (iOS,
# iOS Simulator, tvOS, tvOS Simulator, Mac Catalyst) create a .framework
# bundle; all others produce a raw shared library.
#
# Environment variables:
#   NAME       - platform name (e.g. win-x64, linux-arm64, ios-arm64)
#   RUNNER_OS  - Windows / Linux / macOS  (not needed for iOS – auto-detected)
#   FLAGS      - additional CMake flags
#   LARGE_WORLDS  - non-empty to enable BOX3D_DOUBLE_PRECISION (large worlds)
#   BUILD_TYPE - CMake build type (default: Release)
#   BOX3D_VALIDATE - ON to enable BOX3D_VALIDATE (default: OFF, set to ON for Debug builds)
#   Android-only:
#     ANDROID_ABI, ANDROID_NDK_VER, ANDROID_PLATFORM_VER
set -eu

echo "--- Environment ---"
echo "PATH:       $PATH"
echo "RUNNER_OS:  ${RUNNER_OS:-}"
echo "NAME:       $NAME"
echo "FLAGS:      ${FLAGS:-}"
echo "LARGE_WORLDS:  ${LARGE_WORLDS:-false}"
echo "BUILD_TYPE: ${BUILD_TYPE:-Release}"

BUILD_TYPE="${BUILD_TYPE:-Release}"

# Use a separate build directory for Debug to avoid conflicts with Release
# Library suffix matching CMAKE_DEBUG_POSTFIX in box3d/src/CMakeLists.txt ("d")
if [[ "${BUILD_TYPE}" == "Debug" ]]; then
  BUILD_DIR="build-debug"
  LIB_SUFFIX="d"
else
  BUILD_DIR="build"
  LIB_SUFFIX=""
fi

# Precision suffix for output directory
# Validation flag (OFF by default, ON for Debug builds when explicitly set)
BOX3D_VALIDATE="${BOX3D_VALIDATE:-OFF}"
if [[ "${BOX3D_VALIDATE}" == "ON" ]]; then
  VALIDATION_FLAG="-DBOX3D_VALIDATE=ON"
else
  VALIDATION_FLAG="-DBOX3D_VALIDATE=OFF"
fi

if [[ ${LARGE_WORLDS:-} ]]; then
  PRECISION_SUFFIX="/large-worlds"
  PRECISION_FLAG="-DBOX3D_DOUBLE_PRECISION=ON"
else
  PRECISION_SUFFIX=""
  PRECISION_FLAG=""
fi

# Determine output directory upfront (shared by all branches)
if [[ "${NAME}" == android-* ]]; then
  OUTPUT_DIR="native${PRECISION_SUFFIX}/android/${ANDROID_ABI}"
else
  OUTPUT_DIR="native${PRECISION_SUFFIX}/${NAME}"
fi

# ---------------------------------------------------------------------------
# iOS / iOS Simulator / Mac Catalyst
# ---------------------------------------------------------------------------
if [[ "${NAME}" == ios-* || "${NAME}" == iossimulator-* || "${NAME}" == tvos-* || "${NAME}" == tvossimulator-* || "${NAME}" == maccatalyst-* ]]; then
  echo "--- Building for Apple platform: ${NAME} ---"

  if [[ "${NAME}" == "maccatalyst-arm64" ]]; then
    # Mac Catalyst: Unix Makefiles with explicit target triple.
    # The Xcode generator always produces iOS binaries regardless of overrides.
    SDKROOT=$(xcrun --sdk iphoneos --show-sdk-path)
    cmake -S box3d -B ${BUILD_DIR} \
      -G "Unix Makefiles" \
      -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" \
      -DCMAKE_C_COMPILER=clang \
      -DCMAKE_CXX_COMPILER=clang++ \
      -DCMAKE_OSX_SYSROOT="$SDKROOT" \
      -DCMAKE_OSX_ARCHITECTURES=arm64 \
      -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
      -DCMAKE_TRY_COMPILE_TARGET_TYPE=STATIC_LIBRARY \
      -DCMAKE_C_FLAGS="-target arm64-apple-ios15.0-macabi" \
      -DCMAKE_CXX_FLAGS="-target arm64-apple-ios15.0-macabi" \
      -DCMAKE_SHARED_LINKER_FLAGS="-target arm64-apple-ios15.0-macabi -Wl,-install_name,@rpath/box3d${LIB_SUFFIX}.framework/box3d${LIB_SUFFIX}" \
      -DCMAKE_EXE_LINKER_FLAGS="-target arm64-apple-ios15.0-macabi" \
      ${PRECISION_FLAG} \
      ${VALIDATION_FLAG} \
      -DBUILD_SHARED_LIBS=ON \
      -DBOX3D_SAMPLES=OFF \
      -DBOX3D_UNIT_TESTS=OFF
    cmake --build ${BUILD_DIR} --config "${BUILD_TYPE}"
  else
    # iOS / iOS Simulator / tvOS / tvOS Simulator: Xcode generator
    APPLE_SDK_NAME="iOS"
    SIMULATOR_FLAG=""
    if [[ "${NAME}" == "iossimulator-arm64" ]]; then
      SIMULATOR_FLAG="-DCMAKE_OSX_SYSROOT=iphonesimulator"
    elif [[ "${NAME}" == tvos-* ]]; then
      APPLE_SDK_NAME="tvOS"
    elif [[ "${NAME}" == tvossimulator-* ]]; then
      APPLE_SDK_NAME="tvOS"
      SIMULATOR_FLAG="-DCMAKE_OSX_SYSROOT=appletvsimulator"
    fi
    cmake -S box3d -B ${BUILD_DIR} \
      -GXcode \
      -DCMAKE_SYSTEM_NAME="${APPLE_SDK_NAME}" \
      -DCMAKE_OSX_ARCHITECTURES=arm64 \
      -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
      ${SIMULATOR_FLAG} \
      ${PRECISION_FLAG} \
      ${VALIDATION_FLAG} \
      -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-install_name,@rpath/box3d${LIB_SUFFIX}.framework/box3d${LIB_SUFFIX}" \
      -DBUILD_SHARED_LIBS=ON \
      -DBOX3D_SAMPLES=OFF \
      -DBOX3D_UNIT_TESTS=OFF
    cmake --build ${BUILD_DIR} --config "${BUILD_TYPE}" -- CODE_SIGNING_ALLOWED=NO
  fi

  # Create .framework bundle (required by Apple platforms)
  echo "--- Creating framework ---"
  mkdir -p "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.framework"
  if [ -f "${BUILD_DIR}/bin/${BUILD_TYPE}/libbox3d${LIB_SUFFIX}.dylib" ]; then
    cp "${BUILD_DIR}/bin/${BUILD_TYPE}/libbox3d${LIB_SUFFIX}.dylib" "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.framework/box3d${LIB_SUFFIX}"
  else
    cp "${BUILD_DIR}/bin/libbox3d${LIB_SUFFIX}.dylib" "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.framework/box3d${LIB_SUFFIX}"
  fi
  cat > "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.framework/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>box3d${LIB_SUFFIX}</string>
  <key>CFBundleIdentifier</key>
  <string>org.box2d.Box3D</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>box3d${LIB_SUFFIX}</string>
  <key>CFBundlePackageType</key>
  <string>FMWK</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>MinimumOSVersion</key>
  <string>15.0</string>
</dict>
</plist>
EOF

# ---------------------------------------------------------------------------
# Desktop (Windows / Linux / macOS) and Android
# ---------------------------------------------------------------------------
else
  echo "--- Building for ${NAME} ---"

  CMAKE_FLAGS="-DBOX3D_SAMPLES=OFF -DBOX3D_UNIT_TESTS=OFF ${VALIDATION_FLAG}"

  if [[ "${NAME}" == android-* ]]; then
    # Android cross-compilation via NDK
    TOOLCHAIN="${ANDROID_HOME}/ndk/${ANDROID_NDK_VER}/build/cmake/android.toolchain.cmake"
    CMAKE_FLAGS="${CMAKE_FLAGS} -DCMAKE_TOOLCHAIN_FILE=${TOOLCHAIN} -DANDROID_ABI=${ANDROID_ABI} -DANDROID_PLATFORM=android-${ANDROID_PLATFORM_VER} -DANDROID_STL=c++_static"
  fi

  cmake -S box3d -B ${BUILD_DIR} \
    -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" \
    -DBUILD_SHARED_LIBS=ON \
    ${PRECISION_FLAG} \
    ${FLAGS:-} \
    ${CMAKE_FLAGS}

  cmake --build ${BUILD_DIR} --config "${BUILD_TYPE}"

  echo "--- Copying binaries ---"
  mkdir -p "${OUTPUT_DIR}"

  if [[ "${RUNNER_OS}" == "Windows" ]]; then
    if [ -f "${BUILD_DIR}/bin/${BUILD_TYPE}/box3d${LIB_SUFFIX}.dll" ]; then
      cp "${BUILD_DIR}/bin/${BUILD_TYPE}/box3d${LIB_SUFFIX}.dll" "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.dll"
    else
      cp "${BUILD_DIR}/bin/box3d${LIB_SUFFIX}.dll" "${OUTPUT_DIR}/box3d${LIB_SUFFIX}.dll"
    fi
  elif [[ "${RUNNER_OS}" == "Linux" ]]; then
    cp "${BUILD_DIR}/bin/libbox3d${LIB_SUFFIX}.so" "${OUTPUT_DIR}/libbox3d${LIB_SUFFIX}.so"
  elif [[ "${RUNNER_OS}" == "macOS" ]]; then
    cp "${BUILD_DIR}/bin/libbox3d${LIB_SUFFIX}.dylib" "${OUTPUT_DIR}/libbox3d${LIB_SUFFIX}.dylib"
  fi
fi

echo "--- Done ---"
ls -la "${OUTPUT_DIR}"
