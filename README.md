<p align="center"><image src="Box2D.png"/><h1 p align="center">Box3D</h1>

**C# bindings for [Box3D](https://github.com/erincatto/box3d)** — Erin Catto's 3D rigid body physics engine for games.</p>

[![NuGet](https://img.shields.io/nuget/v/Box3D.svg)](https://www.nuget.org/packages/Box3D)
[![Build Status](https://github.com/Happypig375/Box3D/actions/workflows/build.yml/badge.svg)](https://github.com/Happypig375/Box3D/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)

This package provides .NET bindings and prebuilt native binaries for the Box3D C library, so you can use a high-performance 3D physics engine from C# without compiling any C code yourself. The native libraries are built for **10 platforms** and packaged in a single NuGet package that works out of the box on desktop, mobile, and web.

> [!IMPORTANT]
> This is the C# wrapper repository. The physics engine itself is developed by Erin Catto at [erincatto/box3d](https://github.com/erincatto/box3d) and is included here as a git submodule. For physics documentation, tutorials, and the C API reference, see the [upstream docs](#documentation).

---

## Features

Box3D is a feature-complete 3D physics engine. Highlights include:

### Collision
- Continuous collision detection (CCD)
- Convex hulls, capsules, spheres, triangle meshes, and height fields
- Multiple shapes per body (compound shapes)
- Collision filtering
- Ray casts, shape casts, and overlap queries
- Sensor system
- Character mover

### Physics
- Robust *Soft Step* rigid body solver
- Continuous physics for fast translations and rotations
- Island-based sleeping
- Revolute, prismatic, distance, spherical, weld, wheel, motor, parallel, and filter joints
- Joint limits, motors, springs, and friction
- Body movement, contact, sensor, and joint events

### System
- Data-oriented design written in portable C17
- Extensive multithreading and SIMD (SSE2 / NEON)
- Cross-platform determinism
- Recording and replay

---

## Quick Start

Install the [NuGet package](https://www.nuget.org/packages/Box3D):

```bash
dotnet add package Box3D
```

Then simulate a box falling onto a ground plane:

```csharp
using Box3D;
using static Box3D.Box3D;
using System;

// Create a world with gravity pointing down (-Y).
b3WorldDef worldDef = b3DefaultWorldDef();
worldDef.gravity = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };
b3WorldId worldId = b3CreateWorld(&worldDef);

// Static ground body.
b3BodyDef groundDef = b3DefaultBodyDef();
groundDef.position = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };
b3BodyId groundId = b3CreateBody(worldId, &groundDef);

b3BoxHull groundBox = b3MakeBoxHull(50.0f, 10.0f, 50.0f);
b3ShapeDef groundShapeDef = b3DefaultShapeDef();
b3CreateHullShape(groundId, &groundShapeDef, &groundBox.@base);

// Dynamic box that will fall.
b3BodyDef bodyDef = b3DefaultBodyDef();
bodyDef.type = b3BodyType.b3_dynamicBody;
bodyDef.position = new b3Vec3 { x = 0.0f, y = 4.0f, z = 0.0f };
b3BodyId bodyId = b3CreateBody(worldId, &bodyDef);

b3BoxHull dynamicBox = b3MakeCubeHull(1.0f);
b3ShapeDef shapeDef = b3DefaultShapeDef();
shapeDef.density = 1.0f;
shapeDef.baseMaterial.friction = 0.3f;
b3CreateHullShape(bodyId, &shapeDef, &dynamicBox.@base);

// Step the simulation: 1/60 s per step, 4 sub-steps.
float timeStep = 1.0f / 60.0f;
int subStepCount = 4;

for (int i = 0; i < 90; ++i)
{
    b3World_Step(worldId, timeStep, subStepCount);
    b3Vec3 pos = b3Body_GetPosition(bodyId);
    Console.WriteLine($"{pos.y:F2}");
}

b3DestroyWorld(worldId);
```

The box falls from `y = 4` and comes to rest at approximately `y = 1` (sitting on the ground surface at `y = 0`).

> [!TIP]
> Box3D is tuned for **meters, kilograms, seconds**. Keep moving objects between 0.1 m and 10 m for best results. See the [upstream overview](box3d/docs/overview.md) for guidance on units and world size.

---

## Supported Platforms

The NuGet package ships native binaries for the following runtimes — no additional native compilation is required on the consumer side.

| Platform | Runtime Identifier (RID) | Native binary |
|---|---|---|
| Windows x64 | `win-x64` | `box3d.dll` |
| Windows ARM64 | `win-arm64` | `box3d.dll` |
| Linux x64 | `linux-x64` | `libbox3d.so` |
| Linux ARM64 | `linux-arm64` | `libbox3d.so` |
| macOS ARM64 | `osx-arm64` | `libbox3d.dylib` |
| iOS (device) | `ios-arm64` | `box3d.framework` |
| iOS Simulator | `iossimulator-arm64` | `box3d.framework` |
| Mac Catalyst | `maccatalyst-arm64` | `box3d.framework` |
| Android ARM64 | `android-arm64` | `libbox3d.so` |
| Android x64 | `android-x64` | `libbox3d.so` |

The correct binary is loaded automatically at runtime via NuGet's RID-based `runtimes/<rid>/native/` convention.

---

## Usage

The C# API mirrors the Box3D C API one-to-one. All public types and functions live in the `Box3D` namespace under the `Box3D` partial class, so you can call them with `using static Box3D.Box3D;`:

```csharp
using Box3D;
using static Box3D.Box3D;
```

### Core concepts

| Type | Description |
|---|---|
| `b3WorldId` | Opaque handle to a simulation world (pass by value) |
| `b3BodyId` | Opaque handle to a rigid body |
| `b3ShapeId` | Opaque handle to a collision shape |
| `b3JointId` | Opaque handle to a joint constraint |
| `b3Vec3` | 3-component float vector (`x`, `y`, `z`) |
| `b3Quat` | Unit quaternion (`v` vector part, `s` scalar part) |

All ids are small structs passed by value. Use `b3*_IsValid(id)` to check validity and `B3_IS_NULL(id)` / zero-initialization for null ids.

### Pointers and `unsafe`

Because Box3D is a C library, many functions take pointers to definition structs (e.g. `b3CreateWorld(&worldDef)`). The C# bindings preserve this, so calling code must be in an `unsafe` context:

```xml
<!-- In your .csproj -->
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

### Multithreading

By default Box3D runs single-threaded. To enable multithreading, set `workerCount` and provide task callbacks on the world definition before creating the world:

```csharp
b3WorldDef worldDef = b3DefaultWorldDef();
worldDef.workerCount = 4;
// worldDef.enqueueTask = ...;
// worldDef.finishTask  = ...;
b3WorldId worldId = b3CreateWorld(&worldDef);
```

See the [upstream documentation](box3d/docs/foundation.md) for details.

---

## Building from Source

### Prerequisites

- [git](https://git-scm.com/) — for cloning with submodules
- [.NET 10 SDK](https://dotnet.microsoft.com/download) — to build the C# bindings and NuGet package
- [CMake](https://cmake.org/) 3.22+ — only needed if you want to rebuild the native libraries
- A C17 compiler (MSVC, Clang, or GCC) — only needed for native builds

### Clone with the submodule

```bash
git clone --recurse-submodules https://github.com/Happypig375/Box3D.git
cd Box3D
```

If you already cloned without `--recurse-submodules`:

```bash
git submodule update --init --recursive
```

### Build the C# package

```bash
dotnet build
```

This will:
1. **Stage** the C headers from `box3d/include/box3d/` into `obj/headers/`, preprocessing `math_functions.h` to replace C compound literals (`B3_LITERAL`) with C#-compatible member initialization.
2. **Generate** `NativeMethods.cs` using [ClangSharpPInvokeGenerator](https://github.com/dotnet/clangsharp) (pinned via `dotnet-tools.json`).
3. **Post-process** the generated code to remap C math functions (`sqrtf` → `System.MathF.Sqrt`), fix `bool != 0` comparisons, and remove duplicate declarations.
4. **Compile** the package and produce `Box3D.dll` + `Box3D.xml` (XML documentation).

> [!NOTE]
> `NativeMethods.cs` is generated at build time and is not committed to the repository. It is listed in `.gitignore`.

> [!CAUTION]
> `dotnet build` will not build native binaries. You must build them separately, or download them from GitHub Actions runs.

### Rebuild native libraries (optional)

If you need to rebuild the native shared libraries for your platform instead of using the ones shipped in the NuGet package, use the build script:

```bash
# Linux/macOS
cd box3d
cmake --preset linux-release   # or: macos
cmake --build --preset linux-release
```

```powershell
# Windows
cd box3d
cmake --preset windows
cmake --build --preset windows-release
```

The CI script `build-native.sh` shows how the release binaries are built for all 10 platforms.

---

## How It Works

This repository wraps the upstream Box3D C engine with automatically generated C# bindings. The pipeline runs entirely within MSBuild:

```
PreprocessHeaders  →  GenerateNativeBindings  →  PostProcessNativeMethods  →  Build
```

| Stage | Script | Purpose |
|---|---|---|
| Preprocess headers | `Scripts/StageHeaders.cs` | Copies `.h` files to `obj/`, rewrites `B3_LITERAL` compound literals and injects assertion macro stubs so ClangSharp can parse them |
| Generate bindings | ClangSharpPInvokeGenerator | Produces `NativeMethods.cs` with P/Invoke declarations and inline function bodies |
| Post-process | `Scripts/PostProcessNativeMethods.cs` | Remaps C math functions to `System.MathF`, fixes boolean comparisons, deduplicates declarations |
| Generate docs | `Scripts/DocGen.cs` | Extracts Doxygen comments from C headers and writes `Box3D.xml` for IntelliSense |

The package version is derived automatically from the submodule's `CMakeLists.txt` (currently `0.1.0`) with a date-based revision suffix, so it always reflects the upstream version you're binding against.

---

## CI/CD

Two GitHub Actions workflows keep the package up to date:

### `build.yml` — Native binary builds

Triggered on every push, this workflow builds the native Box3D shared library for all 10 platforms using a matrix of GitHub-hosted runners:

- **Windows** (x64, ARM64) — MSVC, static CRT
- **Linux** (x64, ARM64) — Ninja
- **macOS** (ARM64) — Xcode
- **Android** (ARM64, x64) — Android NDK
- **iOS** (device, simulator, Mac Catalyst) — Xcode with framework packaging

Each platform's binary is uploaded as a build artifact and later assembled into the NuGet package's `runtimes/` folder.

### `update.yml` — Daily submodule update

Runs daily at 06:00 UTC to check for new commits in the upstream `erincatto/box3d` repository. If the submodule has advanced, the workflow:

1. Updates the `box3d` submodule to the latest upstream commit
2. Runs `dotnet build` to verify the bindings still regenerate and compile cleanly
3. Auto-commits the updated submodule with a reference to the upstream commit

This ensures the C# bindings track the latest Box3D release without manual intervention.

---

## Documentation

The full Box3D user manual and API reference live in the submodule at [`box3d/docs/`](box3d/docs/). Key documents:

| Document | Contents |
|---|---|
| [Overview](box3d/docs/overview.md) | Core concepts: bodies, shapes, constraints, joints, solver, CCD, events |
| [Hello World](box3d/docs/hello.md) | Minimal first program (C tutorial) |
| [Foundations](box3d/docs/foundation.md) | Assertions, allocators, vector math (`b3Vec3`, `b3Quat`, `b3Matrix3`) |
| [Collision](box3d/docs/collision.md) | Shape primitives, convex hulls, meshes, dynamic tree |
| [Simulation](box3d/docs/simulation.md) | Worlds, bodies, shapes, contacts, joints, events |
| [Samples](box3d/docs/samples.md) | The samples application (sokol + imgui) |
| [Character](box3d/docs/character.md) | Character mover usage |
| [Large Worlds](box3d/docs/large_worlds.md) | Double precision and large coordinate support |
| [Recording](box3d/docs/recording.md) | Simulation recording and replay |
| [FAQ](box3d/docs/faq.md) | Frequently asked questions |

The C# API is a direct translation of the C API, so the C documentation applies directly — just use the C# syntax shown in the [Quick Start](#quick-start) and [Usage](#usage) sections above.

XML documentation (IntelliSense) is generated from the C header comments and packaged as `Box3D.xml`, so you get inline API documentation in your editor.

---

## Samples

The upstream Box3D repository includes a samples application written in C++ using [sokol](https://github.com/floooh/sokol) for graphics and [imgui](https://github.com/ocornut/imgui) for the UI. It runs with D3D11 on Windows, Metal on macOS, and OpenGL 4.5 on Linux.

To build and run the samples (requires a C++20 compiler):

```bash
cd box3d
cmake --preset windows          # or: linux-release / macos
cmake --build --preset windows-release
./build/bin/Release/samples     # Windows: .\build\bin\Release\samples.exe
```

See [`box3d/docs/samples.md`](box3d/docs/samples.md) for details.

---

## Contributing

This repository wraps the upstream Box3D engine. Bug reports and feature requests related to the **C# bindings or NuGet packaging** should be filed at [Happypig375/Box3D/issues](https://github.com/Happypig375/Box3D/issues).

Issues related to the **physics engine itself** (collision, solver, joints, etc.) should be filed upstream at [erincatto/box3d/issues](https://github.com/erincatto/box3d/issues).

### Submodule update workflow

The `box3d` submodule is updated automatically by the daily [`update.yml`](.github/workflows/update.yml) workflow. To update it manually:

```bash
git submodule update --remote box3d
dotnet build   # verify bindings regenerate cleanly
git add box3d
git commit -m "Update box3d to <upstream commit>"
```

---

## License

Box3D is developed by Erin Catto and uses the [MIT License](https://en.wikipedia.org/wiki/MIT_License).

The C# bindings in this repository are Copyright © 2026 Hadrian Tang and also distributed under the [MIT License](LICENSE.md).

---

## Acknowledgements

- [Erin Catto](https://github.com/erincatto) — author of Box3D and Box2D
- [ClangSharp](https://github.com/dotnet/clangsharp) — .NET P/Invoke binding generator for C libraries