using System;
using System.IO;
using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class WaylandPortalSystemServiceTests
{
    [Test]
    public void CreateDirectoryUri_NormalizesRelativeDirectoryToAbsoluteFileUri()
    {
        string relativePath = Path.Combine("artifacts", "capture folder");
        string expected = new Uri(Path.GetFullPath(relativePath), UriKind.Absolute).AbsoluteUri;

        string actual = WaylandPortalSystemService.CreateDirectoryUri(relativePath);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CreateDirectoryUri_PreservesEscapingForWhitespaceAndSpecialCharacters()
    {
        string relativePath = Path.Combine("artifacts", "#draft shots");

        string actual = WaylandPortalSystemService.CreateDirectoryUri(relativePath);

        Assert.That(actual, Does.Contain("%23draft%20shots"));
    }
}
