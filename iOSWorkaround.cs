namespace Box3D;

using System;
using System.Runtime.InteropServices;
public partial class Box3D
{
    static Box3D() // Work around https://github.com/dotnet/macios/issues/21238
    {
        // For iOS, tvOS, and Mac Catalyst: load the native library from the framework bundle
        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
            NativeLibrary.SetDllImportResolver(typeof(Box3D).Assembly, (_, _, _) => NativeLibrary.Load("@rpath/box3d.framework/box3d"));
    }
}