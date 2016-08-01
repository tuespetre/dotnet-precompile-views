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
            var services = BuildServiceProvider(applicationBasePath);
            var projectContext = GetProjectContext(applicationBasePath, framework);
            var razorHost = services.GetRequiredService<IMvcRazorHost>();
            var trees = new List<SyntaxTree>();

            foreach (var file in GetViewFilePaths(applicationBasePath))
            {
                using (var stream = File.OpenRead(file.AbsolutePath))
                {
                    var result = razorHost.GenerateCode(file.RelativePath, stream);

                    if (result.Success)
                    {
                        var tree = CSharpSyntaxTree.ParseText(
                            text: result.GeneratedCode,
                            path: file.RelativePath,
                            encoding: Encoding.UTF8);

                        trees.Add(tree);
                    }
                }
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: "precompiledviews",
                syntaxTrees: trees,
                references: GetMetadataReferences(projectContext),
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
                    // TODO: Better handling... VS error reporting?
                    throw new Exception();
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

        private IEnumerable<ViewPathTuple> GetViewFilePaths(string applicationBasePath)
        {
            var matcher = new Matcher();
            var directoryInfo = new DirectoryInfo(applicationBasePath);
            var directoryInfoWrapper = new DirectoryInfoWrapper(directoryInfo);

            matcher.AddInclude("**/*.cshtml");

            var matches = matcher.Execute(directoryInfoWrapper);

            return matches.Files.Select(f => new ViewPathTuple
            {
                RelativePath = f.Path,
                AbsolutePath = Path.Combine(applicationBasePath, f.Path),
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

        private IServiceProvider BuildServiceProvider(string applicationBasePath)
        {
            var services = new ServiceCollection();
            var contentRootPath = applicationBasePath;
            var webRootPath = Path.Combine(contentRootPath, "wwwroot");

            services.AddMvcCore();

            services.AddSingleton<IHostingEnvironment>(new StubHostingEnvironment
            {
                ContentRootFileProvider = new PhysicalFileProvider(contentRootPath),
                ContentRootPath = contentRootPath,
                WebRootFileProvider = new PhysicalFileProvider(webRootPath),
                WebRootPath = webRootPath,
            });

            return services.BuildServiceProvider();
        }

        private IEnumerable<MetadataReference> GetMetadataReferences(ProjectContext projectContext)
        {
            var isPortable = !projectContext.TargetFramework.IsDesktop() && projectContext.IsPortable;
            var compilerOptions = projectContext.ProjectFile.GetCompilerOptions(projectContext.TargetFramework, configuration);
            var applicationName = compilerOptions.OutputName + (isPortable ? ".dll" : ".exe");
            var applicationAssembly = Assembly.Load(new AssemblyName(Path.Combine(publishFolder, applicationName)));
            var dependencyContext = DependencyContext.Load(applicationAssembly);
            var libraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (dependencyContext != null)
            {
                foreach (var library in dependencyContext.CompileLibraries)
                {
                    foreach (var path in library.ResolveReferencePaths())
                    {
                        if (libraryPaths.Add(path))
                        {
                            yield return CreateMetadataReference(path);
                        }
                    }
                }

                yield break;
            }

            yield return CreateMetadataReference(applicationAssembly.Location);
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
