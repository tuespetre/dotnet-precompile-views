# dotnet-compile-views

> A tool for precompiling ASP.NET Core MVC Razor views

## Installation

1. Add to `dependencies`:

    ```diff
      "dependencies": {
    +   "dotnet-precompile-views": "1.0.0-*"
      }
    ```

2. Add to `tools`:

    ```diff
      "tools": {
    +   "dotnet-precompile-views": "1.0.0-*"
      }
    ```
    
3. Add to `scripts`:

    ```diff
      "scripts": {
        "postpublish": [
    +     "dotnet precompile-views -p %publish:OutputPath% -f %publish:FullTargetFramework%",
          "dotnet publish-iis -p %publish:OutputPath% -f %publish:FullTargetFramework%"
        ]
    },
    ```
    
4. Add to `ConfigureServices`:

    ```diff
      services.AddMvc();
    + services.AddPrecompiledViews();
    ```
    
5. Run `dotnet publish` and enjoy a faster startup time!