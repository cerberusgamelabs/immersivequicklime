using System;
using System.IO;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.DotNet.Restore;
using Cake.Core;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CakeBuild;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public const string ProjectName = "ImmersiveQuicklime";
    public const string PublishDirectory = "../Releases/_publish/mod";

    public string BuildConfiguration { get; }
    public string Version { get; }
    public string Name { get; }
    public bool SkipJsonValidation { get; }

    public BuildContext(ICakeContext context) : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SkipJsonValidation = context.Argument("skipJsonValidation", false);

        var modInfo = context.DeserializeJsonFromFile<ModInfo>($"../{ProjectName}/modinfo.json");
        Version = modInfo.Version;
        Name = modInfo.ModID;
    }
}

[TaskName("ValidateJson")]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.SkipJsonValidation)
        {
            return;
        }

        var jsonFiles = context.GetFiles($"../{BuildContext.ProjectName}/assets/**/*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                JToken.Parse(File.ReadAllText(file.FullPath));
            }
            catch (JsonException ex)
            {
                throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
            }
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var project = $"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj";

        context.DotNetRestore(project);
        context.DotNetClean(project, new DotNetCleanSettings
        {
            Configuration = context.BuildConfiguration
        });
        context.DotNetPublish(project, new DotNetPublishSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = BuildContext.PublishDirectory
        });
    }
}

[TaskName("Package")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists("../Releases");
        context.EnsureDirectoryExists($"../Releases/{context.Name}");
        context.CleanDirectory($"../Releases/{context.Name}");
        context.CopyFiles($"{BuildContext.PublishDirectory}/*", $"../Releases/{context.Name}");

        if (context.DirectoryExists($"../{BuildContext.ProjectName}/assets"))
        {
            context.CopyDirectory($"../{BuildContext.ProjectName}/assets", $"../Releases/{context.Name}/assets");
        }

        context.CopyFile($"../{BuildContext.ProjectName}/modinfo.json", $"../Releases/{context.Name}/modinfo.json");

        var zipPath = $"../Releases/{context.Name}_{context.Version}.zip";
        if (context.FileExists(zipPath))
        {
            context.DeleteFile(zipPath);
        }

        context.Zip($"../Releases/{context.Name}", zipPath);
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public sealed class DefaultTask : FrostingTask
{
}
