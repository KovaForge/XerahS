#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using ShareX.Nextcloud.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class NextcloudClientTests
{
    [TestCase("cloud.example.com", "https://cloud.example.com")]
    [TestCase("https://cloud.example.com/", "https://cloud.example.com")]
    [TestCase("https://cloud.example.com/nextcloud/", "https://cloud.example.com/nextcloud")]
    [TestCase("https://cloud.example.com/nextcloud/?preview=true#files", "https://cloud.example.com/nextcloud")]
    [TestCase("cloud.example.com/nextcloud/?preview=true#files", "https://cloud.example.com/nextcloud")]
    public void NormalizeServerUrl_RemovesTrailingSlashQueryAndFragment(string input, string expected)
    {
        string actual = NextcloudClient.NormalizeServerUrl(input);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CombineRelativePath_RemovesTraversalSegments()
    {
        string actual = NextcloudClient.CombineRelativePath("ShareX/../Screenshots", "./capture.png");

        Assert.That(actual, Is.EqualTo("ShareX/Screenshots/capture.png"));
    }
}
