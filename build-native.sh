#!/bin/bash
# This script is meant to be run from the .github workflow.
# It builds the box3d library for the target platform and copies the
# resulting binaries to the native/<name>/ directory for NuGet packaging.
set -eu

echo "--- Environment ---"
echo "PATH:       $PATH"
echo "RUNNER_OS:  $RUNNER_OS"
echo "NAME:       $NAME"
echo "FLAGS:      ${FLAGS:-}"

echo "--- Building box3d ---"

# Common CMake flags
CMAKE_FLAGS=""

# Disable samples, tests, and validation for all platforms
CMAKE_FLAGS="-DBOX3D_SAMPLES=OFF -DBOX3D_UNIT_TESTS=OFF -DBOX3D_VALIDATE=OFF"

if [[ "${NAME}" == android-* ]]; then
  # Android cross-compilation via NDK
  TOOLCHAIN="${ANDROID_HOME}/ndk/${ANDROID_NDK_VER}/build/cmake/android.toolchain.cmake"
  CMAKE_FLAGS="${CMAKE_FLAGS} -DCMAKE_TOOLCHAIN_FILE=${TOOLCHAIN} -DANDROID_ABI=${ANDROID_ABI} -DANDROID_PLATFORM=android-${ANDROID_PLATFORM_VER} -DANDROID_STL=c++_static"
fi

cmake -S box3d -B build \
  -DCMAKE_BUILD_TYPE=${BUILD_TYPE} \
  -DBUILD_SHARED_LIBS=ON \
  ${FLAGS:-} \
  ${CMAKE_FLAGS}

cmake --build build --config ${BUILD_TYPE}

echo "--- Copying binaries ---"

# Create output directory
if [[ "${NAME}" == android-* ]]; then
  OUTPUT_DIR="native/android/${ANDROID_ABI}"
else
  OUTPUT_DIR="native/${NAME}"
fi

mkdir -p "${OUTPUT_DIR}"

# Copy the shared library and import library based on platform
if [[ "${RUNNER_OS}" == "Windows" ]]; then
  # Visual Studio puts output in BUILD_TYPE subdirectory
  if [ -f "build/bin/${BUILD_TYPE}/box3d.dll" ]; then
    cp "build/bin/${BUILD_TYPE}/box3d.dll" "${OUTPUT_DIR}/box3d.dll"
  else
    cp "build/bin/box3d.dll" "${OUTPUT_DIR}/box3d.dll"
  fi
elif [[ "${RUNNER_OS}" == "Linux" ]]; then
  cp build/bin/libbox3d.so "${OUTPUT_DIR}/libbox3d.so"
elif [[ "${RUNNER_OS}" == "macOS" ]]; then
  cp build/bin/libbox3d.dylib "${OUTPUT_DIR}/libbox3d.dylib"
fi

echo "--- Done ---"
ls -la "${OUTPUT_DIR}"
