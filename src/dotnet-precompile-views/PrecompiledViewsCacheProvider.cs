using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace dotnet_precompile_views
{
    public class PrecompiledViewsCacheProvider : ICompilerCacheProvider
    {
        public PrecompiledViewsCacheProvider(ICompilerCache cache)
        {
            Cache = cache;
        }

        public ICompilerCache Cache { get; }
    }
}
