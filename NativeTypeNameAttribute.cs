using System;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
namespace Box3D
{
    // -c;generate-helper-types can generate this type, but it's incompatible with -c;single-file (default) right now
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    internal sealed class NativeTypeNameAttribute : Attribute
    {
        public NativeTypeNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
