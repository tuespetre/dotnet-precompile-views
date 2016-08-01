using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using System;

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
                Description = "A tool for simple precompilation of Razor views within an MVC project"
            };
            app.HelpOption("-h|--help");

            var publishFolderOption = app.Option("-p|--publish-folder", "The path to the publish output folder", CommandOptionType.SingleValue);
            var frameworkOption = app.Option("-f|--framework <FRAMEWORK>", "Target framework of application being published", CommandOptionType.SingleValue);
            var configurationOption = app.Option("-c|--configuration <CONFIGURATION>", "Target configuration of application being published", CommandOptionType.SingleValue);
            var projectPath = app.Argument("<PROJECT>", "The path to the project (project folder or project.json) being published. If empty the current directory is used.");
            
            app.OnExecute(() =>
            {
                var publishFolder = publishFolderOption.Value();
                var framework = frameworkOption.Value();

                if (publishFolder == null || framework == null)
                {
                    app.ShowHelp();
                    return 2;
                }

                Reporter.Output.WriteLine($"Precompiling MVC views for the following project: '{publishFolder}'");

                var command = new PrecompileViewsCommand(
                    publishFolder,
                    framework,
                    configurationOption.Value(),
                    projectPath.Value);
                
                var exitCode = command.Run();

                Reporter.Output.WriteLine("Precompiling MVC views completed successfully");

                return exitCode;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                Reporter.Output.WriteLine(e.ToString().Yellow());
            }

            return 1;
        }
    }
}
