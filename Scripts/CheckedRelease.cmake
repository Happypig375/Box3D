# This file is injected after the Box3D project enables its languages.
# Preserve the toolchain's exact Release flags, then remove only NDEBUG so
# Box3D's existing assertions and validation remain active in the checked asset.
if(NOT BOX3D_CHECKED_RELEASE_APPLIED)
  foreach(_box3d_language C CXX)
    set(_box3d_checked_flags "${CMAKE_${_box3d_language}_FLAGS_RELEASE}")
    string(REPLACE "/DNDEBUG" "" _box3d_checked_flags "${_box3d_checked_flags}")
    string(REPLACE "-DNDEBUG" "" _box3d_checked_flags "${_box3d_checked_flags}")
    set(CMAKE_${_box3d_language}_FLAGS_RELEASE
        "${_box3d_checked_flags}"
        CACHE STRING "Flags used by the checked Release build" FORCE)
  endforeach()

  set(BOX3D_CHECKED_RELEASE_APPLIED TRUE CACHE INTERNAL "Checked Release flags applied")
endif()
