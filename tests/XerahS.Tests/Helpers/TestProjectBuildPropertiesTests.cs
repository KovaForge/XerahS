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
