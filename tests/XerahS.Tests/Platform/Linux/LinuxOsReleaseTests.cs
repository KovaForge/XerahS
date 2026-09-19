// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

public class LinuxOsReleaseTests
{
    [Test]
    public void Load_OmarchyFile_ReturnsOmarchyId()
    {
        const string omarchy =
            "NAME=\"Omarchy\"\n" +
            "PRETTY_NAME=\"Omarchy\"\n" +
            "ID=omarchy\n" +
            "ID_LIKE=arch\n" +
            "BUILD_ID=\"4.0.4\"\n";

        var info = LinuxOsRelease.Load(_ => omarchy.Split('\n'));

        Assert.That(info.DistroId, Is.EqualTo("omarchy"));
        Assert.That(info.DistroIdLike, Is.EqualTo("arch"));
    }

    [Test]
    public void Load_FedoraFile_ReturnsFedoraId()
    {
        const string fedora =
            "NAME=\"Fedora Linux\"\n" +
            "ID=fedora\n" +
            "ID_LIKE=\"centos rhel fedora\"\n";

        var info = LinuxOsRelease.Load(_ => fedora.Split('\n'));

        Assert.That(info.DistroId, Is.EqualTo("fedora"));
        Assert.That(info.DistroIdLike, Is.EqualTo("centos rhel fedora"));
    }

    [Test]
    public void Load_MissingFile_ReturnsNulls()
    {
        var info = LinuxOsRelease.Load(_ => throw new FileNotFoundException());

        Assert.That(info.DistroId, Is.Null);
        Assert.That(info.DistroIdLike, Is.Null);
    }

    [Test]
    public void Load_EmptyFile_ReturnsNulls()
    {
        var info = LinuxOsRelease.Load(_ => Array.Empty<string>());

        Assert.That(info.DistroId, Is.Null);
        Assert.That(info.DistroIdLike, Is.Null);
    }

    [Test]
    public void Load_FileWithOnlyUnrelatedKeys_ReturnsNulls()
    {
        var info = LinuxOsRelease.Load(_ => new[] { "VERSION_ID=\"42\"", "PRETTY_NAME=\"Foo\"" });

        Assert.That(info.DistroId, Is.Null);
        Assert.That(info.DistroIdLike, Is.Null);
    }

    [Test]
    public void Load_QuotedValues_StripsQuotes()
    {
        var info = LinuxOsRelease.Load(_ => new[] { "ID=\"omarchy\"", "ID_LIKE=\"arch\"" });

        Assert.That(info.DistroId, Is.EqualTo("omarchy"));
        Assert.That(info.DistroIdLike, Is.EqualTo("arch"));
    }
}
