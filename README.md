<p align="center"><image src="Box2D.png"/><h1 align="center">Box3D</h1></p>

<p align="center"><b>C# bindings for <a href="https://github.com/erincatto/box3d">Box3D</a></b> — Erin Catto's 3D rigid body physics engine for games.</p>

[![NuGet](https://img.shields.io/nuget/v/Box3D.svg?label=NuGet%3A%20Box3D)](https://www.nuget.org/packages/Box3D)
[![NuGet](https://img.shields.io/nuget/v/Box3D.LargeWorlds.svg?label=NuGet%3A%20Box3D.LargeWorlds)](https://www.nuget.org/packages/Box3D.LargeWorlds)
[![Build Status](https://github.com/Happypig375/Box3D/actions/workflows/build.yml/badge.svg)](https://github.com/Happypig375/Box3D/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)

Two NuGet packages provide .NET bindings and prebuilt native binaries for the Box3D C library, so you can use a high-performance 3D physics engine from C# without compiling any C code yourself: **Box3D** (single-precision) and **Box3D.LargeWorlds** (double-precision for simulations spanning 100 km+). The native libraries are built for **12 platforms &times; 2 precisions** and work out of the box on desktop, mobile, and web.

> [!IMPORTANT]
> This is the C# wrapper repository. The physics engine itself is developed by Erin Catto at [erincatto/box3d](https://github.com/erincatto/box3d) and is included here as a git submodule. For physics documentation, tutorials, and the C API reference, refer to [Box3D documentation](https://box2d.org/documentation3d).

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

### Single-precision (Box3D)

Save the following simulation of a box falling onto a ground plane to a C# file (e.g. `Box3D.cs`):

```csharp
// For projects, run: dotnet add package Box3D
#:package Box3D@*
// For projects, add to PropertyGroup in csproj: <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
#:property AllowUnsafeBlocks=true
using Box3D;
using static Box3D.Box3D;
using System;
unsafe {

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
}
```

After running this file (e.g. `dotnet Box3D.cs`), the results show that the box falls from `y = 4` and comes to rest at approximately `y = 1` (sitting on the ground surface at `y = 0`).

> [!TIP]
> Box3D is tuned for **meters, kilograms, seconds**. Keep moving objects between 0.1 m and 10 m for best results. Refer to [Box3D documentation](https://box2d.org/documentation3d) for guidance on units and world size.

### Double-precision (Box3D.LargeWorlds)

For simulations spanning large distances (100 km+), use the `Box3D.LargeWorlds` package.

```bash
dotnet add package Box3D.LargeWorlds
```

There are a few differences compared to single precision:
- All positions use `b3Pos` instead of `b3Vec3`
- The world transform uses `b3WorldTransform` instead of `b3Transform`
- World creation function is called `b3CreateWorldDoublePrecision` instead of `b3CreateWorld`

Otherwise, all other types and functions stay the same. According to Erin Catto, there is a ~3% performance drop using double precision positions compared to single precision.

> [!CAUTION]
> You cannot use both Box3D and Box3D.LargeWorlds together. Native binaries will conflict with each other.
---

## Supported Platforms

Both NuGet packages ship native binaries for the following runtimes — no additional native compilation is required on the consumer side.

| Platform | Runtime Identifier (RID) | Native binary |
|---|---|---|
| Windows x64 | `win-x64` | `box3d.dll` |
| Windows ARM64 | `win-arm64` | `box3d.dll` |
| Linux x64 | `linux-x64` | `libbox3d.so` |
| Linux ARM64 | `linux-arm64` | `libbox3d.so` |
| macOS ARM64 | `osx-arm64` | `libbox3d.dylib` |
| iOS (device) | `ios-arm64` | `box3d.framework` |
| iOS Simulator | `iossimulator-arm64` | `box3d.framework` |
| tvOS (device) | `tvos-arm64` | `box3d.framework` |
| tvOS Simulator | `tvossimulator-arm64` | `box3d.framework` |
| Mac Catalyst | `maccatalyst-arm64` | `box3d.framework` |
| Android ARM64 | `android-arm64` | `libbox3d.so` |
| Android x64 | `android-x64` | `libbox3d.so` |

The correct binary is loaded automatically at runtime via NuGet's RID-based `runtimes/<rid>/native/` convention.

> [!WARNING]
> For iOS/tvOS/Android/Mac Catalyst, only .NET 11+ is supported. For .NET 10 Android, using `<UseMonoRuntime>false</UseMonoRuntime>` property is also supported.

This is because the CoreCLR runtime must be used. Otherwise, the Mono runtime will crash on any invocation of `b3DefaultWorldDef()` because it does not support function pointer fields in struct return types during P/Invoke. This also means we cannot support browser WASM until it migrates to CoreCLR, to be expected for .NET 12.

---

## Usage

The C# API mirrors the Box3D C API one-to-one. All public types live in the `Box3D` (or `Box3D.LargeWorlds`) namespace and all public functions live under the corresponding partial class, so you can call them with `using static Box3D.Box3D;`:

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

> [!TIP]
> Box3D recommends using the **physical core count** (not counting hyper-threads or efficiency cores) as the worker count. The built-in scheduler creates `workerCount - 1` threads, counting the calling thread as the last worker.

```csharp
// Physical core heuristic: divide logical processors by 2 (hyper-threading typically doubles logical count) used in Box3D C samples
int workerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
b3WorldDef worldDef = b3DefaultWorldDef();
worldDef.workerCount = workerCount;
// optionally connect your own task scheduler if you want more control
// worldDef.enqueueTask = ...;
// worldDef.finishTask  = ...;
b3WorldId worldId = b3CreateWorld(&worldDef);
```

See the [Foundations section of Box3D documentation](https://box2d.org/documentation3d/md_foundation.html) for details.

> [!CAUTION]
> When using multithreading, do not perform read or write operations on a Box3D world during `b3World_Step()`. Do not write to the Box3D world from multiple threads. Any operation that wakes a body is not thread-safe.

### Passing user data

You can associate application-specific data (e.g. a game object) with any Box3D object. There are two common approaches:

#### Option A: External dictionary (recommended for most users)

Use a `Dictionary<b3BodyId, T>` (or `b3ShapeId`, `b3JointId`, `b3WorldId`) to map physics objects back to your game entities. Box3D's Id types embed a serial number, so stale Ids won't accidentally collide with newly created objects.

```csharp
record Player(int Health = 100, string Name = "Player 1");
var players = new Dictionary<b3BodyId, Player>();

b3BodyDef bodyDef = b3DefaultBodyDef();
bodyDef.position = new b3Vec3 { x = 0, y = 5, z = 0 };
bodyDef.type = b3BodyType.b3_dynamicBody;
b3BodyId bodyId = b3CreateBody(worldId, &bodyDef);

players[bodyId] = new Player { Health = 100, Name = "Hero" };

// Later, look up from an event
// b3BodyMoveEvent moveEvent = ...;
if (players.TryGetValue(moveEvent.bodyId, out Player player))
{
    Console.WriteLine(player.Name);
}

// Clean up when body is destroyed
// b3BodyDestroy(bodyId);
players.Remove(bodyId);
```

This approach needs no `unsafe`, no pointer casts, and no handle lifetime management. It also works uniformly with all Id types.

#### Option B: `GCHandle` via `void* userData`

The `userData` field on `b3WorldDef`, `b3BodyDef`, `b3ShapeDef`, and `b3JointDef` is a `void*` — a raw pointer. You can also get/set it after creation via `b3World_SetUserData`/`b3World_GetUserData`, `b3Body_SetUserData`/`b3Body_GetUserData`, etc. Event structs (`b3BodyMoveEvent`, `b3JointEvent`) carry the same pointer back to you.

Since you cannot directly cast a C# reference type to `void*`, use `GCHandle` to safely box a managed object:

```csharp
using System.Runtime.InteropServices;

record Player(int Health = 100, string Name = "Player 1");
unsafe
{
    b3BodyDef bodyDef = b3DefaultBodyDef();
    bodyDef.position = new b3Vec3 { x = 0, y = 5, z = 0 };
    bodyDef.type = b3BodyType.b3_dynamicBody;

    // Box a Player instance into a GCHandle and store it in userData
    var player = new Player();
    GCHandle gcHandle = GCHandle.Alloc(player);
    bodyDef.userData = (void*)GCHandle.ToIntPtr(gcHandle);

    b3BodyId bodyId = b3CreateBody(worldId, &bodyDef);

    // Later, retrieve the Player from a body move event
    b3BodyMoveEvent moveEvent = ...;
    Player retrievedPlayer = (Player)GCHandle.FromIntPtr((nint)moveEvent.userData).Target;
    Console.WriteLine(retrievedPlayer.Name);  // "Player 1"
}
```

> [!IMPORTANT]
> You **must** free the `GCHandle` when the associated Box3D object is destroyed, otherwise the managed object will never be garbage collected:

```csharp
unsafe void OnBodyDestroyed(b3BodyId bodyId)
{
    void* userData = b3Body_GetUserData(bodyId);
    if (userData != null)
    {
        GCHandle.FromIntPtr((nint)userData).Free();
    }
}
```

This gives you zero-overhead lookup directly from the pointer, at the cost of `unsafe` code and careful lifetime management. Prefer this only when you are deeply in `unsafe` context and want to avoid the (very cheap) dictionary hash lookup.

---

### Build the LargeWorlds package

Since `Box3D.LargeWorlds/Box3D.LargeWorlds.csproj` is a thin overlay that imports `Box3D.csproj` with two property overrides, the same build command works:

```bash
dotnet build Box3D.LargeWorlds/Box3D.LargeWorlds.csproj
```

This generates the C# bindings with `-D BOX3D_DOUBLE_PRECISION` passed to ClangSharp, producing `NativeMethods.cs` with `double`-precision types and the matching `DllImport` entry points. The native binaries are expected under `native/large-worlds/<platform>/`.

---

## Building from Source

### Prerequisites

- [git](https://git-scm.com/) — for cloning with submodules
- [.NET SDK](https://dotnet.microsoft.com/download) — to build the C# bindings and NuGet package
- [CMake](https://cmake.org/) — only needed if you want to rebuild the native libraries
- A C compiler (MSVC, Clang, or GCC) — only needed for native builds

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

The CI script `build-native.sh` handles all platforms (including iOS) and both precisions. Set `LARGE_WORLDS=true` to build the double-precision variant.

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
| Generate docs | `Scripts/DocGen.cs` | Extracts Doxygen comments from C headers and writes `Box3D.xml` (or `Box3D.LargeWorlds.xml`) for IntelliSense |

### Dual-package architecture

The two NuGet packages share a single codebase:

- **`Box3D.csproj`** is the canonical project. It contains the full binding pipeline and references native binaries from `native/$(Box3DNativeSubdirectory)`. The subdirectory defaults to empty.
- **`Box3D.LargeWorlds/Box3D.LargeWorlds.csproj`** is a 6-line overlay that sets `Box3DNativeSubdirectory=large-worlds/` and `Box3DClangDefines=-a;-D;-a;BOX3D_DOUBLE_PRECISION`, then imports `../Box3D.csproj`. This reuses every target and item group from the canonical project — no duplication.

The CI builds all 12 platforms &times; 2 precisions in a single matrix, producing 24 native binaries that are assembled into the two packages.

The package version is derived automatically from the submodule's `CMakeLists.txt` with a date-based revision suffix, so it always reflects the upstream version you're binding against.

---

## CI/CD

Two GitHub Actions workflows keep the packages up to date:

### `build.yml` — Native binary builds

Triggered on every push, this workflow builds the native Box3D shared library for all 12 platforms &times; 2 precisions using a matrix of GitHub-hosted runners, then packs and validates both NuGet packages.

The build matrix has two dimensions: `large-worlds` (empty for single-precision, `large-worlds` for double-precision) and `platform` (12 target platforms):

- **Windows** (x64, ARM64) — MSVC, static CRT
- **Linux** (x64, ARM64) — Ninja
- **macOS** (ARM64) — Ninja
- **Android** (ARM64, x64) — Android NDK
- **iOS** (ARM64 device, ARM64 simulator) — Xcode with framework packaging
- **tvOS** (ARM64 device, ARM64 simulator) — Xcode with framework packaging
- **Mac Catalyst** (ARM64) — Unix makefiles with framework packaging

Each precision&ndash;platform combination runs the single `build-native.sh` script, which handles Android, iOS, and desktop builds in a single code path. The resulting binaries are compressed and uploaded as named artifacts (`native-{large-worlds}-{platform}.tar`).

The `pack-nuget` job then downloads the matching artifacts, runs the MSBuild binding pipeline (header staging &rarr; ClangSharp &rarr; post-process &rarr; compile &rarr; doc gen), and produces the `.nupkg`. A `validate` job exercises the package on all desktop and mobile platforms.

Both packages are pushed to NuGet.org on every push to `master`.

### `update.yml` — Daily submodule update

Runs daily at 06:00 UTC to check for new commits in the upstream `erincatto/box3d` repository. If the submodule has advanced, the workflow:

1. Updates the `box3d` submodule to the latest upstream commit
2. Runs `dotnet build` to verify the bindings still regenerate and compile cleanly
3. Auto-commits the updated submodule with a reference to the upstream commit

This ensures the C# bindings track the latest Box3D release without manual intervention.

## Contributing

This repository wraps the upstream Box3D engine. Bug reports and feature requests related to the **C# bindings or NuGet packaging** should be filed at [Happypig375/Box3D issues](https://github.com/Happypig375/Box3D/issues).

Issues related to the **physics engine itself** (collision, solver, joints, etc.) should be filed upstream at [erincatto/box3d issues](https://github.com/erincatto/box3d/issues).

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

Box3D is developed by Erin Catto ([@erincatto](https://github.com/erincatto)) and uses the [MIT License](https://github.com/erincatto/box3d/blob/main/LICENSE).

The C# bindings in this repository are developed by Hadrian Tang ([@Happypig375](https://github.com/Happypig375)) and also distributed under the [MIT License](LICENSE.md).

---

## Acknowledgements

- [Erin Catto](https://github.com/erincatto) — author of Box3D and Box2D
- [ClangSharp](https://github.com/dotnet/clangsharp) — .NET P/Invoke binding generator for C libraries