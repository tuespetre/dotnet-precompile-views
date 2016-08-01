using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;

namespace dotnet_precompile_views
{
    public static class PrecompiledViewsServiceCollectionExtensions
    {
        public static void AddPrecompiledViews(this IServiceCollection services)
        {
            services.AddSingleton<ICompilerCacheProvider>(provider =>
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

                var fileProviderAccessor = provider.GetRequiredService<IRazorViewEngineFileProviderAccessor>();

                var cache = new PrecompiledViewsCache(fileProviderAccessor.FileProvider, precompiledViews);

                return new PrecompiledViewsCacheProvider(cache);
            });
        }
    }
}
