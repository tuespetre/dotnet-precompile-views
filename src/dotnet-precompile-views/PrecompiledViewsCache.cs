using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;

namespace dotnet_precompile_views
{
    public class PrecompiledViewsCache : CompilerCache, ICompilerCache
    {
        public PrecompiledViewsCache(IFileProvider fileprovider, IDictionary<string, Type> precompiled)
            : base(fileprovider, precompiled)
        {
        }

        CompilerCacheResult ICompilerCache.GetOrAdd(string relativePath, Func<RelativeFileInfo, CompilationResult> compile)
        {
            return GetOrAdd(relativePath, compile);
        }
    }
}
