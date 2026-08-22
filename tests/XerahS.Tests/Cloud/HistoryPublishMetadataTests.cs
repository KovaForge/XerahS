#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.History;

namespace XerahS.Tests.Cloud;

[TestFixture]
public sealed class HistoryPublishMetadataTests
{
    [TestCase("", "Image", "capture.png", false)]
    [TestCase("https://cdn.example/capture.txt", "Text", "capture.txt", false)]
    [TestCase("https://cdn.example/capture", "Image", "capture.bin", true)]
    [TestCase("https://cdn.example/capture", "", "capture.mp4", true)]
    [TestCase("https://cdn.example/capture", "Screencast", "capture.bin", true)]
    public void CanPublish_UsesUrlAndMediaKind(string url, string type, string fileName, bool expected)
    {
        var item = new HistoryItem { URL = url, Type = type, FileName = fileName };

        Assert.That(HistoryPublishMetadata.CanPublish(item), Is.EqualTo(expected));
    }

    [Test]
    public void StableClientIdAndOwnerBinding_SurviveUnpublish()
    {
        var item = new HistoryItem
        {
            URL = "https://cdn.example/capture.png",
            Type = "Image",
            FileName = "capture.png"
        };

        string firstClientId = HistoryPublishMetadata.EnsureClientId(item);
        string retryClientId = HistoryPublishMetadata.EnsureClientId(item);
        HistoryPublishMetadata.MarkPublished(item, "gallery-1", "owner-a", DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(retryClientId, Is.EqualTo(firstClientId));
            Assert.That(Guid.TryParse(firstClientId, out _), Is.True);
            Assert.That(HistoryPublishMetadata.CanUnpublish(item, "owner-a"), Is.True);
            Assert.That(HistoryPublishMetadata.CanUnpublish(item, "owner-b"), Is.False);
        });

        HistoryPublishMetadata.MarkUnpublished(item);

        Assert.Multiple(() =>
        {
            Assert.That(HistoryPublishMetadata.IsPublished(item), Is.False);
            Assert.That(HistoryPublishMetadata.EnsureClientId(item), Is.EqualTo(firstClientId));
            Assert.That(HistoryPublishMetadata.GetOwnerSubject(item), Is.EqualTo("owner-a"));
        });
    }

    [Test]
    public void CreateTitle_RemovesOnlyFinalExtension()
    {
        var item = new HistoryItem
        {
            FileName = "screenshot-2026-08-22.final.png",
            URL = "https://cdn.example/fallback.png"
        };

        Assert.That(HistoryPublishMetadata.CreateTitle(item), Is.EqualTo("screenshot-2026-08-22.final"));
    }
}
