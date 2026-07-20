using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[module: SkipLocalsInit] // Same semantics as C for inlined functions
namespace Box3D
{
    internal static class Box3DModuleInitializer
    {
        [ModuleInitializer]
        [SuppressMessage("Usage", "CA2255", Justification = "The native resolver must be registered before any Box3D P/Invoke is resolved.")]
        internal static void InitializeNativeLibraryResolver()
        {
            // Register before the Box3D type initializer performs its first P/Invoke.
            // Registering from that type initializer is too late on Apple AOT runtimes,
            // which resolve the import triggered by b3SetAssertFcn before entering the initializer body.
            NativeLibrary.SetDllImportResolver(typeof(Box3D).Assembly, (libraryName, assembly, searchPath) =>
            {
                bool useDebug = AppContext.TryGetSwitch("Box3D.Debug", out bool debug) && debug; // Set in Box3D.targets
                string suffix = useDebug ? "d" : "";

                // Apple frameworks fail to be searched by default - see see https://github.com/dotnet/macios/issues/21238
                if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
                    return NativeLibrary.Load($"@rpath/box3d{suffix}.framework/box3d{suffix}");

                return NativeLibrary.Load($"box3d{suffix}", assembly, searchPath);
            });
        }
    }

    unsafe partial class Box3D
    {
        static Box3D()
        {
            // Route native B3_ASSERT to C# Trace.Assert so asserts are visible
            // through the .NET debug/trace infrastructure without crashing the process.
            // This crash occurs through:
            // - On MSVC: __debugbreak() — raises EXCEPTION_BREAKPOINT (Win32 exception, crash unless a debugger is attached)
            // - On GCC/Clang: __builtin_trap() — raises SIGTRAP or SIGILL, immediate process termination
            // In Release native binaries B3_ASSERT is stripped so this callback is never invoked — no overhead.
            b3SetAssertFcn(&AssertCallback);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int AssertCallback(sbyte* condition, sbyte* fileName, int lineNumber)
        {
            string condStr = Marshal.PtrToStringUTF8((nint)condition) ?? "(null)";
            string fileStr = Marshal.PtrToStringUTF8((nint)fileName) ?? "(null)";
            Console.Error.WriteLine($"Box3D assertion failed: {condStr} at {fileStr}:{lineNumber}");
            if (Debugger.IsAttached) Debugger.Break();
            return 0; // suppress native __debugbreak()/__builtin_trap()
        }

        private static float nextafterf(float x, float y) // polyfill for the C function
        {
            return y < x ? MathF.BitDecrement(x) : MathF.BitIncrement(x);
        }

        /// <summary>Macro to determine if any id is null.</summary>
        public static bool B3_IS_NULL(b3WorldId id) => id.index1 == 0;
        /// <summary>Macro to determine if any id is null.</summary>
        public static bool B3_IS_NULL(b3BodyId id) => id.index1 == 0;
        /// <summary>Macro to determine if any id is null.</summary>
        public static bool B3_IS_NULL(b3ShapeId id) => id.index1 == 0;
        /// <summary>Macro to determine if any id is null.</summary>
        public static bool B3_IS_NULL(b3JointId id) => id.index1 == 0;
        /// <summary>Macro to determine if any id is null.</summary>
        public static bool B3_IS_NULL(b3ContactId id) => id.index1 == 0;
        /// <summary>Macro to determine if any id is non-null.</summary>
        public static bool B3_IS_NON_NULL(b3WorldId id) => id.index1 != 0;
        /// <summary>Macro to determine if any id is non-null.</summary>
        public static bool B3_IS_NON_NULL(b3BodyId id) => id.index1 != 0;
        /// <summary>Macro to determine if any id is non-null.</summary>
        public static bool B3_IS_NON_NULL(b3ShapeId id) => id.index1 != 0;
        /// <summary>Macro to determine if any id is non-null.</summary>
        public static bool B3_IS_NON_NULL(b3JointId id) => id.index1 != 0;
        /// <summary>Macro to determine if any id is non-null.</summary>
        public static bool B3_IS_NON_NULL(b3ContactId id) => id.index1 != 0;
        /// <summary>Compare two ids for equality. Doesn't work for b3WorldId. Don't mix types.</summary>
        public static bool B3_ID_EQUALS(b3BodyId id1, b3BodyId id2) => id1.index1 == id2.index1 && id1.world0 == id2.world0 && id1.generation == id2.generation;
        /// <summary>Compare two ids for equality. Doesn't work for b3WorldId. Don't mix types.</summary>
        public static bool B3_ID_EQUALS(b3ShapeId id1, b3ShapeId id2) => id1.index1 == id2.index1 && id1.world0 == id2.world0 && id1.generation == id2.generation;
        /// <summary>Compare two ids for equality. Doesn't work for b3WorldId. Don't mix types.</summary>
        public static bool B3_ID_EQUALS(b3JointId id1, b3JointId id2) => id1.index1 == id2.index1 && id1.world0 == id2.world0 && id1.generation == id2.generation;
        /// <summary>Compare two ids for equality. Doesn't work for b3WorldId. Don't mix types.</summary>
        public static bool B3_ID_EQUALS(b3ContactId id1, b3ContactId id2) => id1.index1 == id2.index1 && id1.world0 == id2.world0 && id1.generation == id2.generation;
    }
}
