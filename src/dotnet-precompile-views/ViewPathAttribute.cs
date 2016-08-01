using System;

namespace dotnet_precompile_views
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ViewPathAttribute : Attribute
    {
        public ViewPathAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
