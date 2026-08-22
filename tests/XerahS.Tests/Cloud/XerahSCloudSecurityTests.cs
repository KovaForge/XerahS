#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using System.Net;
using System.Text;
using NUnit.Framework;
using XerahS.Core.Cloud;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Cloud;

[TestFixture]
public sealed class XerahSCloudSecurityTests
{
    [Test]
    public void SessionStore_PersistsRefreshCredentialButNeverAccessToken()
    {
        var secrets = new FakeSecretStore(isFallback: false);
        var store = new XerahSCloudSessionStore(secrets);

        store.Accept(new XerahSCloudSession("access-sensitive", "refresh-sensitive", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Multiple(() =>
        {
            Assert.That(secrets.Values.Values, Does.Contain("refresh-sensitive"));
            Assert.That(secrets.Values.Values, Does.Contain("owner-a"));
            Assert.That(secrets.Values.Values, Does.Not.Contain("access-sensitive"));
            Assert.That(store.Current?.AccessToken, Is.EqualTo("access-sensitive"));
        });
    }

    [Test]
    public void SessionStore_RejectsFallbackBackend()
    {
        var store = new XerahSCloudSessionStore(new FakeSecretStore(isFallback: true));

        Assert.Throws<XerahSCloudSecurityException>(() => store.Accept(
            new XerahSCloudSession("access", "refresh", "owner", DateTimeOffset.UtcNow.AddMinutes(5))));
    }

    [TestCase("xerahs://oauth/callback?code=abc&state=xyz", true)]
    [TestCase("xerahs://oauth/callback?code=abc&state=xyz&access_token=secret", false)]
    [TestCase("xerahs://hostile/callback?code=abc&state=xyz", false)]
    [TestCase("https://xerahs.com/oauth/callback?code=abc&state=xyz", false)]
    [TestCase("xerahs://oauth/callback?code=abc&code=again&state=xyz", false)]
    public void CallbackParser_EnforcesExactCodeAndStateOnly(string value, bool expected)
    {
        bool parsed = XerahSCloudOAuthCallbackParser.TryParse(new Uri(value), out XerahSCloudOAuthCallback? callback);

        Assert.That(parsed, Is.EqualTo(expected));
        Assert.That(callback != null, Is.EqualTo(expected));
    }

    [Test]
    public void ArgumentRedactor_RemovesCallbackSecrets()
    {
        string sensitive = "xerahs://oauth/callback?code=secret-code&state=secret-state";

        string[] result = XerahSCloudArgumentRedactor.Redact(["--safe", sensitive]);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.EqualTo("--safe"));
            Assert.That(result[1], Is.EqualTo(XerahSCloudArgumentRedactor.RedactedCallback));
            Assert.That(string.Join(' ', result), Does.Not.Contain("secret-code"));
            Assert.That(string.Join(' ', result), Does.Not.Contain("secret-state"));
        });
    }

    [Test]
    public async Task OAuthCoordinator_AcceptsOnceThenRejectsReplay()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sessions = new MemorySessionStore();
        var exchange = new FakeTokenExchange(clock);
        var coordinator = new XerahSCloudOAuthCoordinator(CreateOptions(), exchange, sessions, clock);
        XerahSCloudOAuthAttempt attempt = coordinator.Begin();
        var callback = new Uri($"xerahs://oauth/callback?code=one-time-code&state={Uri.EscapeDataString(attempt.State)}");

        XerahSCloudOAuthCompletion first = await coordinator.CompleteAsync(callback);
        XerahSCloudOAuthCompletion replay = await coordinator.CompleteAsync(callback);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(XerahSCloudOAuthCompletion.Accepted));
            Assert.That(replay, Is.EqualTo(XerahSCloudOAuthCompletion.UnknownOrReplayedState));
            Assert.That(exchange.LastVerifier, Is.EqualTo(attempt.CodeVerifier));
            Assert.That(exchange.LastNonce, Is.EqualTo(attempt.Nonce));
            Assert.That(sessions.Current?.OwnerSubject, Is.EqualTo("owner-a"));
            Assert.That(attempt.AuthorizationUri.Query, Does.Contain("code_challenge_method=S256"));
        });
    }

    [Test]
    public async Task ApiClient_UsesBearerAndStableIdempotencyKeyWithoutLiveNetwork()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"item\":{\"id\":\"gallery-1\",\"publishedAt\":\"2026-08-22T00:00:00Z\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var sessions = new MemorySessionStore
        {
            Current = new XerahSCloudSession("access", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5))
        };
        var client = new XerahSCloudApiClient(new HttpClient(handler), sessions, CreateOptions());
        var request = new XerahSCloudPublishRequest(
            "54bec9ab-e292-45bf-abba-b378e45e8463",
            "https://cdn.example/capture.png",
            null,
            "screenshot",
            "capture.png",
            DateTimeOffset.UtcNow,
            "Example",
            "image/png");

        XerahSCloudPublishResponse response = await client.PublishAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo("gallery-1"));
            Assert.That(captured?.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(captured?.Headers.Authorization?.Parameter, Is.EqualTo("access"));
            Assert.That(captured?.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo(request.ClientItemId));
        });
    }

    private static XerahSCloudOptions CreateOptions() => new()
    {
        FeatureEnabled = true,
        OAuthAuthority = new Uri("https://project.supabase.co/"),
        OAuthClientId = "desktop-public-client"
    };

    private sealed class FakeSecretStore(bool isFallback) : ISecretStore, ISecretStoreInfo
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public string BackendName => "test";
        public string BackendDetails => "test";
        public bool IsFallback { get; } = isFallback;

        public string? GetSecret(string providerId, string secretKey, string name) =>
            Values.GetValueOrDefault($"{providerId}:{secretKey}:{name}");

        public void SetSecret(string providerId, string secretKey, string name, string value) =>
            Values[$"{providerId}:{secretKey}:{name}"] = value;

        public void DeleteSecret(string providerId, string secretKey, string name) =>
            Values.Remove($"{providerId}:{secretKey}:{name}");

        public bool HasSecret(string providerId, string secretKey, string name) =>
            Values.ContainsKey($"{providerId}:{secretKey}:{name}");
    }

    private sealed class MemorySessionStore : IXerahSCloudSessionStore
    {
        public XerahSCloudSession? Current { get; set; }
        public void Accept(XerahSCloudSession session) => Current = session;
        public (string OwnerSubject, string RefreshToken)? ReadRefreshCredential() => null;
        public void Clear() => Current = null;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IXerahSCloudClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTokenExchange(FakeClock clock) : IXerahSCloudOAuthTokenExchange
    {
        public string? LastVerifier { get; private set; }
        public string? LastNonce { get; private set; }

        public Task<XerahSCloudSession> ExchangeAsync(
            string code,
            string codeVerifier,
            string expectedNonce,
            CancellationToken cancellationToken)
        {
            LastVerifier = codeVerifier;
            LastNonce = expectedNonce;
            return Task.FromResult(new XerahSCloudSession("access", "refresh", "owner-a", clock.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
