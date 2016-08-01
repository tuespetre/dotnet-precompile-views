using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Internal;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;

namespace dotnet_precompile_views
{
    public class DispatcherCommand
    {
        private readonly string publishFolder;
        private readonly string projectPath;
        private readonly string framework;
        private readonly string configuration;

        public DispatcherCommand(string publishFolder, string framework, string configuration, string projectPath)
        {
            this.publishFolder = publishFolder;
            this.projectPath = projectPath;
            this.framework = framework;
            this.configuration = configuration;
        }

        public int Run()
        {
            var projectFile = ProjectReader.GetProject(projectPath ?? string.Empty);
            var targetFrameworks = projectFile
                .GetTargetFrameworks()
                .Select(frameworkInformation => frameworkInformation.FrameworkName);

            NuGetFramework nugetFramework;
            if (!TryResolveFramework(targetFrameworks, out nugetFramework))
            {
                return 0;
            }

            var dispatchArgs = new List<string>
            {
                projectFile.ProjectDirectory,
            };

            if (!string.IsNullOrEmpty(publishFolder))
            {
                dispatchArgs.Add("-p");
                dispatchArgs.Add(publishFolder);
            }

            if (!string.IsNullOrEmpty(framework))
            {
                dispatchArgs.Add("-f");
                dispatchArgs.Add(framework);
            }

            if (!string.IsNullOrEmpty(configuration))
            {
                dispatchArgs.Add("-c");
                dispatchArgs.Add(configuration);
            }

            var dispatchCommand = DotnetToolDispatcher.CreateDispatchCommand(
                dispatchArgs,
                nugetFramework,
                configuration,
                null,
                null,
                projectFile.ProjectDirectory,
                "dotnet-precompile-views");

            var dispatchExitCode = dispatchCommand
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;

            return dispatchExitCode;
        }

        private static bool TryResolveFramework(
            IEnumerable<NuGetFramework> availableFrameworks,
            out NuGetFramework resolvedFramework)
        {
            resolvedFramework = availableFrameworks.First();
            return true;
        }
    }
}
