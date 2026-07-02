using System;

namespace Box3D
{
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
