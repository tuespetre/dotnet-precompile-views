using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Internal;

namespace dotnet_precompile_views
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "dotnet-precompile-views",
                FullName = "ASP.NET Core MVC View Precompiler",
                Description = "A tool for precompilation of Razor views within an MVC project"
            };

            app.HelpOption("-h|--help");

            var publishFolderOption = app.Option(
                "-p|--publish-folder", 
                "The path to the publish output folder", 
                CommandOptionType.SingleValue);

            var frameworkOption = app.Option(
                "-f|--framework <FRAMEWORK>", 
                "Target framework of application being published", 
                CommandOptionType.SingleValue);

            var configurationOption = app.Option(
                "-c|--configuration <CONFIGURATION>", 
                "Target configuration of application being published", 
                CommandOptionType.SingleValue);

            var projectPathArgument = app.Argument(
                "<PROJECT>", 
                "The path to the project (project folder or project.json) being published. If empty the current directory is used.");

            var isDispatcher = DotnetToolDispatcher.IsDispatcher(args);

            if (!isDispatcher)
            {
                DotnetToolDispatcher.EnsureValidDispatchRecipient(ref args);
            }

            app.OnExecute(() =>
            {
                var publishFolder = publishFolderOption.Value();
                var framework = frameworkOption.Value();
                var configuration = configurationOption.Value();
                var projectPath = projectPathArgument.Value;

                if (publishFolder == null || framework == null)
                {
                    app.ShowHelp();

                    return 2;
                }

                if (isDispatcher)
                {
                    var dispatcherCommand = new DispatcherCommand(
                        publishFolder,
                        framework,
                        configuration,
                        projectPath);

                    return dispatcherCommand.Run();
                }

                Reporter.Output.WriteLine($"Precompiling MVC views for the following project: '{publishFolder}'");

                var precompileViewsCommand = new PrecompileViewsCommand(
                    publishFolder,
                    framework,
                    configuration,
                    projectPath);

                var exitCode = precompileViewsCommand.Run();

                if (exitCode == 0)
                {
                    Reporter.Output.WriteLine("Precompiling MVC views completed successfully");
                }

                return exitCode;
            });

            return app.Execute(args);
        }
    }
}
