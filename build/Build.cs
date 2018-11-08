using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.InspectCode;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.Xunit;
using System;
using System.IO;
using System.Linq;
using static Nuke.Common.ControlFlow;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.OpenCover.OpenCoverTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Nuke.Common.Tools.Xunit.XunitTasks;

class Build : NukeBuild
{
    public static int Main ()
    {
        Execute<Build>(x => x.Test);
        Console.ReadKey();

        return 0;
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Solution("NukeAutomation.sln")] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.GetNormalizedAssemblyVersion())
                .SetFileVersion(GitVersion.GetNormalizedFileVersion())
                .SetInformationalVersion(GitVersion.InformationalVersion));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetVersion(GitVersion.NuGetVersionV2)
                .SetOutputDirectory(OutputDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableIncludeSymbols());
        });
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            //xunit.runner.console이 .net framework밖에 지원안해서 어쩔수없이 net461로 코드 테스트
            var framework = "net461";
            var xunitSettings = new Xunit2Settings()
                .SetFramework(framework)
                .AddTargetAssemblies(GlobFiles(Solution.Directory, $"*/bin/{Configuration}/{framework}/Nuke*Test.dll").NotEmpty())
                .AddResultReport(Xunit2ResultFormat.Xml, OutputDirectory / "tests.xml");

            if (IsWin)
            {
                OpenCover(s => s
                    .SetTargetSettings(xunitSettings)
                    .SetOutput(OutputDirectory / "coverage.xml")
                    .SetSearchDirectories(xunitSettings.TargetAssemblyWithConfigs.Select(x => Path.GetDirectoryName(x.Key)))
                    .SetRegistration(RegistrationType.User)
                    .SetTargetExitCodeOffset(targetExitCodeOffset: 0)
                    .SetFilters(
                        "+[*]*",
                        "-[xunit.*]*",
                        "-[FluentAssertions.*]*")
                    .SetExcludeByAttributes("System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"));

                ReportGenerator(s => s
                    .AddReports(OutputDirectory / "coverage.xml")
                    .AddReportTypes(ReportTypes.Html)
                    .SetTargetDirectory(OutputDirectory / "coverage"));
            }
            else
                Xunit2(s => xunitSettings);
        });
}
