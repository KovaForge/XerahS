#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Xml.Linq;
using NUnit.Framework;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class TestProjectBuildPropertiesTests
{
    [Test]
    public void DirectoryBuildProps_UserImport_CannotOverrideReleaseGuardrails()
    {
        string propsPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../Directory.Build.props"));

        XDocument props = XDocument.Load(propsPath);
        XElement project = props.Root ?? throw new InvalidOperationException("Directory.Build.props has no root element.");
        XElement userImport = project.Elements("Import")
            .Single(element => string.Equals((string?)element.Attribute("Project"), "Directory.Build.props.user", StringComparison.OrdinalIgnoreCase));
        XElement releaseGuardrailGroup = project.Elements("PropertyGroup")
            .First(element => element.Element("Version") is not null && element.Element("TreatWarningsAsErrors") is not null);

        Assert.That(project.Elements().ToList().IndexOf(userImport), Is.LessThan(project.Elements().ToList().IndexOf(releaseGuardrailGroup)));
    }

    [Test]
    public void XerahSTests_AppAndCliProjectReferences_DisableAppDrivenPluginBuild()
    {
        string testProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../tests/XerahS.Tests/XerahS.Tests.csproj"));

        XDocument project = XDocument.Load(testProjectPath);

        AssertProjectReferenceDisablesAppDrivenPluginBuild(project, "XerahS.App.csproj");
        AssertProjectReferenceDisablesAppDrivenPluginBuild(project, "XerahS.CLI.csproj");
    }

    [Test]
    public void McpServerTests_XunitRunnerVisualStudio_IsPrivateAsset()
    {
        string testProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../src/tools/XerahS.McpServer.Tests/XerahS.McpServer.Tests.csproj"));

        XDocument project = XDocument.Load(testProjectPath);

        AssertPackageReferenceIsPrivateBuildAsset(project, "xunit.runner.visualstudio");
    }

    [Test]
    public void XerahSTests_DiscoveryPackages_ArePrivateBuildAssets()
    {
        string testProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../tests/XerahS.Tests/XerahS.Tests.csproj"));

        XDocument project = XDocument.Load(testProjectPath);

        AssertPackageReferenceIsPrivateBuildAsset(project, "Microsoft.NET.Test.Sdk");
        AssertPackageReferenceIsPrivateBuildAsset(project, "NUnit3TestAdapter");
        AssertPackageReferenceIsPrivateBuildAsset(project, "Avalonia.Headless.NUnit");
    }

    [Test]
    public void McpServerTests_DiscoveryAndCoveragePackages_ArePrivateBuildAssets()
    {
        string testProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../src/tools/XerahS.McpServer.Tests/XerahS.McpServer.Tests.csproj"));

        XDocument project = XDocument.Load(testProjectPath);

        AssertPackageReferenceIsPrivateBuildAsset(project, "Microsoft.NET.Test.Sdk");
        AssertPackageReferenceIsPrivateBuildAsset(project, "xunit.runner.visualstudio");
        AssertPackageReferenceIsPrivateBuildAsset(project, "coverlet.collector");
    }

    private static void AssertPackageReferenceIsPrivateBuildAsset(XDocument project, string packageName)
    {
        XElement packageReference = project.Descendants("PackageReference")
            .Single(element => string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That((string?)packageReference.Element("PrivateAssets"), Is.EqualTo("all"));
            Assert.That((string?)packageReference.Element("IncludeAssets"), Does.Contain("build"));
            Assert.That((string?)packageReference.Element("IncludeAssets"), Does.Contain("buildtransitive"));
        });
    }

    private static void AssertProjectReferenceDisablesAppDrivenPluginBuild(XDocument project, string projectFileName)
    {
        XElement projectReference = project.Descendants("ProjectReference")
            .Single(element => ((string?)element.Attribute("Include"))?.EndsWith(projectFileName, StringComparison.OrdinalIgnoreCase) == true);

        string? additionalProperties = (string?)projectReference.Attribute("AdditionalProperties");

        Assert.Multiple(() =>
        {
            Assert.That(additionalProperties, Does.Contain("EnableAppDrivenPluginBuild=false"));
            Assert.That(additionalProperties, Does.Contain("SkipBundlePlugins=true"));
        });
    }
}
