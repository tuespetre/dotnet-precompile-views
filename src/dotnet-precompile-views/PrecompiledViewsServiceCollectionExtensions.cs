using dotnet_precompile_views;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class PrecompiledViewsServiceCollectionExtensions
    {
        /// <summary>
        /// This should be called after <see cref="MvcRazorMvcCoreBuilderExtensions.AddRazorViewEngine(IMvcCoreBuilder)"/>.
        /// </summary>
        public static void AddPrecompiledViews(this IServiceCollection services)
        {
            services.AddSingleton<ICompilerCacheProvider>(provider =>
            {
                var fileProviderAccessor = provider.GetRequiredService<IRazorViewEngineFileProviderAccessor>();

                try
                {
                    var assembly = Assembly.Load(new AssemblyName("precompiledviews"));

                    var precompiledViews =
                    (
                        from type in assembly.ExportedTypes
                        from attribute in type.GetTypeInfo().GetCustomAttributes<ViewPathAttribute>()
                        select new
                        {
                            attribute.Path,
                            type,
                        }
                    )
                    .ToDictionary(x => x.Path, x => x.type);

                    var cache = new CompilerCache(fileProviderAccessor.FileProvider, precompiledViews);

                    return new PrecompiledViewsCacheProvider(cache);
                }
                catch
                {
                    provider.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("dotnet-precompile-views")
                        .LogWarning("Failed to load precompiled views.");

                    return new DefaultCompilerCacheProvider(fileProviderAccessor);
                }
            });
        }
    }
}
