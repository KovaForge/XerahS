#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
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
    public void DirectoryBuildProps_ProductAssembly_IsExplicitOptIn()
    {
        XDocument props = LoadRepositoryXml("Directory.Build.props");
        XElement property = props.Descendants("AssembleProduct").Single();

        Assert.Multiple(() =>
        {
            Assert.That(property.Value, Is.EqualTo("false"));
            Assert.That((string?)property.Attribute("Condition"), Does.Contain("$(AssembleProduct)"));
        });
    }

    [TestCase("src/desktop/app/XerahS.App/XerahS.App.csproj")]
    [TestCase("src/desktop/cli/XerahS.CLI/XerahS.CLI.csproj")]
    public void DesktopProductProjects_DevStagingTargets_RequireProductAssembly(string relativeProjectPath)
    {
        XDocument project = LoadRepositoryXml(relativeProjectPath);
        string[] targetNames = ["BuildWatchFolderDaemonForDev", "CopyVideoEditorWebUiForDev", "BuildPlugins"];

        foreach (string targetName in targetNames)
        {
            XElement target = project.Descendants("Target")
                .Single(element => string.Equals((string?)element.Attribute("Name"), targetName, StringComparison.Ordinal));
            Assert.That((string?)target.Attribute("Condition"), Does.Contain("$(AssembleProduct)"), targetName);
        }
    }

    [Test]
    public void WindowsPlatformProject_DoesNotReferenceCore()
    {
        XDocument project = LoadRepositoryXml("src/platform/XerahS.Platform.Windows/XerahS.Platform.Windows.csproj");
        var references = project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(path => path is not null)
            .ToArray();

        Assert.That(
            references.Any(path => path!.Contains("XerahS.Core", StringComparison.OrdinalIgnoreCase)),
            Is.False);
    }

    [Test]
    public void PlatformCompatibilityRegistry_DoesNotOwnHostServiceProvider()
    {
        string sourcePath = ResolveRepositoryPath("src/platform/XerahS.Platform.Abstractions/PlatformServices.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("RootProvider"));
            Assert.That(source, Does.Not.Contain("SetRootProvider"));
        });
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
    public void XerahSBuildTests_TestInfrastructurePackages_ArePrivateBuildAssets()
    {
        string testProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../../tests/XerahS.Build.Tests/XerahS.Build.Tests.csproj"));

        XDocument project = XDocument.Load(testProjectPath);

        AssertPackageReferenceIsPrivateBuildAsset(project, "Microsoft.NET.Test.Sdk");
        AssertPackageReferenceIsPrivateBuildAsset(project, "NUnit3TestAdapter");
        AssertPackageReferenceIsPrivateBuildAsset(project, "NUnit.Analyzers");
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
            Assert.That(additionalProperties, Does.Contain("AssembleProduct=false"));
            Assert.That(additionalProperties, Does.Contain("EnableAppDrivenPluginBuild=false"));
            Assert.That(additionalProperties, Does.Contain("SkipBundlePlugins=true"));
        });
    }

    private static XDocument LoadRepositoryXml(string relativePath) =>
        XDocument.Load(ResolveRepositoryPath(relativePath));

    private static string ResolveRepositoryPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../../",
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
