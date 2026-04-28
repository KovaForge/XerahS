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

        XElement runnerReference = project.Descendants("PackageReference")
            .Single(element => string.Equals((string?)element.Attribute("Include"), "xunit.runner.visualstudio", StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That((string?)runnerReference.Element("PrivateAssets"), Is.EqualTo("all"));
            Assert.That((string?)runnerReference.Element("IncludeAssets"), Does.Contain("build"));
            Assert.That((string?)runnerReference.Element("IncludeAssets"), Does.Contain("buildtransitive"));
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
