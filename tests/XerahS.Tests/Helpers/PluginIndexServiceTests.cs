#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Net;
using System.Net.Http;
using NUnit.Framework;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginIndexServiceTests
{
    [Test]
    public void ParseIndex_AcceptsValidCommunityPluginIndex()
    {
        string json = CreateIndexJson("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        var index = PluginIndexService.ParseIndex(json);

        Assert.Multiple(() =>
        {
            Assert.That(index.IndexVersion, Is.EqualTo("1.0"));
            Assert.That(index.Plugins, Has.Count.EqualTo(1));
            Assert.That(index.Plugins[0].PluginId, Is.EqualTo("pixelfox"));
            Assert.That(index.Plugins[0].DownloadUrl, Does.EndWith(".xsdp"));
        });
    }

    [Test]
    public void ParseIndex_RejectsNonHttpsDownloadUrl()
    {
        string json = CreateIndexJson(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            downloadUrl: "http://example.com/pixelfox.xsdp");

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("HTTPS is required"));
    }

    [Test]
    public void ParseIndex_RejectsMissingChecksum()
    {
        string json = CreateIndexJson(string.Empty);

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("sha256 checksum"));
    }

    [Test]
    public void ParseIndex_AcceptsAndFiltersDraftPluginsWithoutPackageMetadata()
    {
        string json = CreateIndexJson(string.Empty, downloadUrl: string.Empty, isDraft: true);

        var index = PluginIndexService.ParseIndex(json);

        Assert.That(index.Plugins, Is.Empty);
    }

    [Test]
    public async Task FetchIndexAsync_RequiresHttpsIndexUrl()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler("{}"));
        var service = new PluginIndexService(httpClient, "http://example.com/plugins-index.json");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchIndexAsync());

        Assert.That(ex!.Message, Does.Contain("HTTPS"));
        await Task.CompletedTask;
    }

    private static string CreateIndexJson(string checksum, string downloadUrl = "https://example.com/pixelfox.xsdp", bool isDraft = false)
    {
        return $$"""
        {
          "indexVersion": "1.0",
          "lastUpdated": "2026-04-26T00:00:00Z",
          "plugins": [
            {
              "pluginId": "pixelfox",
              "name": "Pixelfox",
              "version": "1.0.0",
              "author": "Pixelfox",
              "description": "Pixelfox uploader plugin.",
              "apiVersion": "1.0",
              "supportedCategories": ["Image"],
              "homepageUrl": "https://pixelfox.cc",
              "downloadUrl": "{{downloadUrl}}",
              "checksum": "{{checksum}}",
              "isDraft": {{isDraft.ToString().ToLowerInvariant()}},
              "minAppVersion": "1.0.0",
              "dependencies": []
            }
          ]
        }
        """;
    }

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
