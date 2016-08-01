using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;

#if NETCOREAPP1_0
using System.Runtime.Loader;
#endif

namespace dotnet_precompile_views
{
    public class PrecompileViewsCommand
    {
        private readonly string publishFolder;
        private readonly string projectPath;
        private readonly string framework;
        private readonly string configuration;

        public PrecompileViewsCommand(string publishFolder, string framework, string configuration, string projectPath)
        {
            this.publishFolder = publishFolder;
            this.projectPath = projectPath;
            this.framework = framework;
            this.configuration = configuration;
        }

        public int Run()
        {
            var applicationBasePath = GetApplicationBasePath();
            var projectContext = GetProjectContext(applicationBasePath, framework);
            // Needs to be called before BuildServiceProvider so that the
            // DependencyContextRazorViewEngineOptionsSetup doesn't complain
            // about not being able to find the assembly.
            var metadataReferences = GetMetadataReferences(projectContext);
            var services = BuildServiceProvider(projectContext);
            var viewPaths = GetViewFilePaths(projectContext.ProjectDirectory);
            var razorHost = services.GetRequiredService<IMvcRazorHost>();
            var syntaxTrees = GetSyntaxTrees(razorHost, viewPaths);

            var compilation = CSharpCompilation.Create(
                assemblyName: "precompiledviews",
                syntaxTrees: syntaxTrees,
                references: metadataReferences,
                options: new CSharpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary));

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                var rewriter = new ViewPathAttributeSyntaxRewriter(model);

                compilation = compilation.ReplaceSyntaxTree(
                    oldTree: tree, 
                    newTree: tree.WithRootAndOptions(
                        root: rewriter.Visit(tree.GetRoot()),
                        options: new CSharpParseOptions()));
            }

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                var result = compilation.Emit(assemblyStream, pdbStream);

                if (!result.Success)
                {
                    Console.WriteLine(result.Diagnostics.Count());
                    var builder = new StringBuilder();
                    builder.AppendLine("Error creating compilation:");
                    foreach (var error in result.Diagnostics)
                    {
                        builder.AppendLine(error.GetMessage());
                    }
                    // TODO: Better handling... VS error reporting?
                    throw new Exception(builder.ToString());
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);

                using (var file = File.OpenWrite(Path.Combine(publishFolder, "precompiledviews.dll")))
                {
                    assemblyStream.CopyTo(file);
                    file.Flush();
                }
            }

            return 0;
        }

        private IEnumerable<SyntaxTree> GetSyntaxTrees(IMvcRazorHost razorHost, IEnumerable<ViewPathTuple> viewPaths)
        {
            foreach (var file in viewPaths)
            {
                using (var stream = File.OpenRead(file.AbsolutePath))
                {
                    var result = razorHost.GenerateCode(file.RelativePath, stream);

                    if (!result.Success)
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine("Error creating syntax trees:");
                        foreach (var error in result.ParserErrors)
                        {
                            builder.AppendLine(error.Message);
                        }
                        // TODO: Better handling... VS error reporting?
                        throw new Exception(builder.ToString());
                    }

                    var tree = CSharpSyntaxTree.ParseText(
                        text: result.GeneratedCode,
                        path: file.RelativePath,
                        encoding: Encoding.UTF8);

                    yield return tree;
                }
            }
        }

        private IEnumerable<ViewPathTuple> GetViewFilePaths(string projectDirectory)
        {
            var matcher = new Matcher();
            var directoryInfo = new DirectoryInfo(projectDirectory);
            var directoryInfoWrapper = new DirectoryInfoWrapper(directoryInfo);

            matcher.AddInclude("**/*.cshtml");

            var matches = matcher.Execute(directoryInfoWrapper);

            return matches.Files.Select(f => new ViewPathTuple
            {
                RelativePath = f.Path,
                AbsolutePath = Path.Combine(projectDirectory, f.Path),
            });
        }

        private string GetApplicationBasePath()
        {
            if (!string.IsNullOrEmpty(projectPath))
            {
                var fullProjectPath = Path.GetFullPath(projectPath);

                return Path.GetFileName(fullProjectPath) == "project.json"
                    ? Path.GetDirectoryName(fullProjectPath)
                    : fullProjectPath;
            }

            return Directory.GetCurrentDirectory();
        }

        private IServiceProvider BuildServiceProvider(ProjectContext projectContext)
        {
            var services = new ServiceCollection();
            var contentRootPath = projectContext.ProjectDirectory;
            var webRootPath = Path.Combine(contentRootPath, "wwwroot");

            if (!Directory.Exists(webRootPath))
            {
                // Should this be an error?
                webRootPath = contentRootPath;
            }

            services.AddMvcCore().AddViews().AddRazorViewEngine();

            services.AddSingleton<IHostingEnvironment>(new StubHostingEnvironment
            {
                ApplicationName = projectContext.ProjectFile.Name,
                ContentRootFileProvider = new PhysicalFileProvider(contentRootPath),
                ContentRootPath = contentRootPath,
                WebRootFileProvider = new PhysicalFileProvider(webRootPath),
                WebRootPath = webRootPath,
            });

            return services.BuildServiceProvider();
        }

        private IList<MetadataReference> GetMetadataReferences(ProjectContext projectContext)
        {
            var isPortable = !projectContext.TargetFramework.IsDesktop() && projectContext.IsPortable;
            var compilerOptions = projectContext.ProjectFile.GetCompilerOptions(projectContext.TargetFramework, configuration);
            var applicationName = compilerOptions.OutputName + (isPortable ? ".dll" : ".exe");
#if NETCOREAPP1_0
            var applicationAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(publishFolder, applicationName));
#else
            var applicationAssembly = Assembly.LoadFile(Path.Combine(publishFolder, applicationName));
#endif
            var dependencyContext = DependencyContext.Load(applicationAssembly);
            var libraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var references = new List<MetadataReference>();

            foreach (var library in dependencyContext.CompileLibraries)
            {
                foreach (var path in library.ResolveReferencePaths())
                {
                    if (libraryPaths.Add(path))
                    {
                        references.Add(CreateMetadataReference(path));
                    }
                }
            }
            
            references.Add(CreateMetadataReference(typeof(PrecompileViewsCommand).GetTypeInfo().Assembly.Location));

            return references;
        }

        private static MetadataReference CreateMetadataReference(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);

                return assemblyMetadata.GetReference(filePath: path);
            }
        }

        private static ProjectContext GetProjectContext(string applicationBasePath, string framework)
        {
            var project = ProjectReader.GetProject(Path.Combine(applicationBasePath, "project.json"));

            return new ProjectContextBuilder()
                .WithProject(project)
                .WithTargetFramework(framework)
                .Build();
        }

        private class ViewPathTuple
        {
            public string RelativePath { get; set; }

            public string AbsolutePath { get; set; }
        }
    }
}
