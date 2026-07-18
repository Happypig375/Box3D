using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Box3D
{
    partial class Box3D
    {
        static Box3D() // Work around https://github.com/dotnet/macios/issues/21238
        {
            // For iOS, tvOS, and Mac Catalyst: load the native library from the framework bundle
            if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
                NativeLibrary.SetDllImportResolver(typeof(Box3D).Assembly, (_, _, _) => NativeLibrary.Load("@rpath/box3d.framework/box3d"));
        }
        private static float nextafterf(float x, float y) // polyfill for the C function
        {
            return y < x ? MathF.BitDecrement(x) : MathF.BitIncrement(x);
        }
    }
}
