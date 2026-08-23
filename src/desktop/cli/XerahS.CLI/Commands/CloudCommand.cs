#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XerahS.Core.Cloud;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands;

public static class CloudCommand
{
    public const int DefaultSignInTimeoutSeconds = 600;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Command Create(IServiceProvider services)
    {
        var command = new Command("cloud", "XerahS Cloud OAuth and account operations");
        var jsonOption = new Option<bool>("--json") { Description = "Write machine-readable JSON." };

        var statusCommand = new Command("status", "Show Cloud OAuth configuration and session state");
        var verifyOption = new Option<bool>("--verify")
        {
            Description = "Call the Cloud API to verify the saved session."
        };
        statusCommand.Add(jsonOption);
        statusCommand.Add(verifyOption);
        statusCommand.SetAction(async parseResult =>
        {
            Environment.ExitCode = await StatusAsync(
                services,
                parseResult.GetValue(jsonOption),
                parseResult.GetValue(verifyOption));
        });

        var signInCommand = new Command("sign-in", "Open the system browser and complete Cloud OAuth");
        var timeoutOption = new Option<int>("--timeout")
        {
            Description = "Seconds to wait for the browser authorization callback.",
            DefaultValueFactory = _ => DefaultSignInTimeoutSeconds
        };
        signInCommand.Add(jsonOption);
        signInCommand.Add(timeoutOption);
        signInCommand.SetAction(async parseResult =>
        {
            int timeout = parseResult.GetValue(timeoutOption);
            if (timeout <= 0)
            {
                timeout = DefaultSignInTimeoutSeconds;
            }

            Environment.ExitCode = await SignInAsync(
                services,
                parseResult.GetValue(jsonOption),
                TimeSpan.FromSeconds(timeout));
        });

        var completeCommand = new Command("complete", "Forward a xerahs:// OAuth callback to a waiting sign-in");
        var callbackArgument = new Argument<string>("callback")
        {
            Description = "The xerahs://oauth/callback URI returned by the browser."
        };
        completeCommand.Add(callbackArgument);
        completeCommand.Add(jsonOption);
        completeCommand.SetAction(async parseResult =>
        {
            Environment.ExitCode = await CompleteAsync(
                parseResult.GetValue(callbackArgument),
                parseResult.GetValue(jsonOption));
        });

        var signOutCommand = new Command("sign-out", "Clear the Cloud session on this device");
        signOutCommand.Add(jsonOption);
        signOutCommand.SetAction(parseResult =>
        {
            Environment.ExitCode = SignOut(services, parseResult.GetValue(jsonOption));
        });

        command.Add(statusCommand);
        command.Add(signInCommand);
        command.Add(completeCommand);
        command.Add(signOutCommand);
        return command;
    }

    public static bool TryForwardCallback(string[] args, out int exitCode)
    {
        exitCode = 1;
        if (!CloudOAuthCallbackPipe.TryGetCallbackArgument(args, out string? callback) || callback == null)
        {
            return false;
        }

        exitCode = CompleteAsync(callback, json: false).GetAwaiter().GetResult();
        return true;
    }

    public static async Task<int> CompleteAsync(string? callbackArgument, bool json)
    {
        if (!CloudOAuthCallbackPipe.TryCreateCallbackUri(callbackArgument, out Uri? uri) || uri == null)
        {
            WriteResult(json, ok: false, "invalid_callback", "The OAuth callback URI is invalid.");
            return 1;
        }

        bool sent = await CloudOAuthCallbackPipe.TrySendAsync(uri.AbsoluteUri).ConfigureAwait(false);
        if (!sent)
        {
            WriteResult(
                json,
                ok: false,
                "no_waiter",
                "No cloud sign-in is waiting. Run 'xerahscli cloud sign-in' first.");
            return 1;
        }

        WriteResult(json, ok: true, "forwarded", "Forwarded the OAuth callback to the waiting sign-in process.");
        return 0;
    }

    public static async Task<int> SignInAsync(
        IXerahSCloudOAuthCoordinator coordinator,
        IXerahSCloudClient client,
        Func<string, bool> openUrl,
        Func<TimeSpan, CancellationToken, Task<Uri?>> waitForCallback,
        Func<IDisposable>? createProtocolBinding,
        bool json,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured)
        {
            WriteResult(json, ok: false, "not_configured", "XerahS Cloud OAuth is not configured for this build.");
            return 1;
        }

        XerahSCloudOAuthAttempt attempt = coordinator.Begin();
        IDisposable? protocolBinding = null;
        try
        {
            protocolBinding = createProtocolBinding?.Invoke();
            if (!openUrl(attempt.AuthorizationUri.AbsoluteUri))
            {
                WriteResult(
                    json,
                    ok: false,
                    "browser_unavailable",
                    "The system browser could not be opened for Cloud sign-in.");
                return 1;
            }

            if (!json)
            {
                Console.WriteLine("Opened the system browser for XerahS Cloud authorization.");
                Console.WriteLine("Approve the desktop client after TOTP. Waiting for xerahs:// callback...");
            }

            Uri? callback = await waitForCallback(timeout, cancellationToken).ConfigureAwait(false);
            if (callback == null)
            {
                WriteResult(json, ok: false, "expired", "Timed out waiting for the browser authorization callback.");
                return 1;
            }

            XerahSCloudOAuthCompletion completion = await coordinator
                .CompleteAsync(callback, cancellationToken)
                .ConfigureAwait(false);
            if (completion != XerahSCloudOAuthCompletion.Accepted)
            {
                WriteResult(json, ok: false, completion.ToString(), DescribeCompletion(completion));
                return 1;
            }

            XerahSCloudAccountSummary account = await client.GetAccountAsync(cancellationToken).ConfigureAwait(false);
            WriteSignedIn(json, account);
            return 0;
        }
        finally
        {
            protocolBinding?.Dispose();
        }
    }

    internal static async Task<int> SignInAsync(
        IServiceProvider services,
        bool json,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        IXerahSCloudOAuthCoordinator coordinator = services.GetRequiredService<IXerahSCloudOAuthCoordinator>();
        IXerahSCloudClient client = services.GetRequiredService<IXerahSCloudClient>();
        return await SignInAsync(
            coordinator,
            client,
            url => PlatformServices.System.OpenUrl(url),
            (waitTimeout, token) => CloudOAuthCallbackPipe.WaitAsync(waitTimeout, token),
            CloudProtocolBinding.BindToCurrentProcess,
            json,
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<int> StatusAsync(IServiceProvider services, bool json, bool verify)
    {
        IXerahSCloudClient client = services.GetRequiredService<IXerahSCloudClient>();
        XerahSCloudOptions options = services.GetRequiredService<XerahSCloudOptions>();
        var payload = new Dictionary<string, object?>
        {
            ["configured"] = client.IsConfigured,
            ["signedIn"] = client.HasSessionCredential,
            ["ownerSubject"] = client.CurrentOwnerSubject,
            ["apiBase"] = options.ApiBaseAddress.AbsoluteUri,
            ["oauthAuthority"] = options.OAuthAuthority?.AbsoluteUri,
            ["redirectUri"] = options.OAuthRedirectUri.AbsoluteUri
        };

        if (verify && client.IsConfigured && client.HasSessionCredential)
        {
            try
            {
                XerahSCloudAccountSummary account = await client.GetAccountAsync().ConfigureAwait(false);
                payload["verified"] = true;
                payload["slug"] = account.Slug;
                payload["profileUrl"] = account.ProfileUrl.AbsoluteUri;
                payload["strongAuth"] = account.StrongAuth;
                payload["canPublish"] = account.CanPublish;
            }
            catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException)
            {
                payload["verified"] = false;
                payload["error"] = ex.Message;
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
                }
                else
                {
                    Console.Error.WriteLine(ex.Message);
                }

                return 1;
            }
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        Console.WriteLine("XerahS Cloud:");
        Console.WriteLine($"  Configured:     {client.IsConfigured}");
        Console.WriteLine($"  Signed in:      {client.HasSessionCredential}");
        Console.WriteLine($"  Owner:          {client.CurrentOwnerSubject ?? "(none)"}");
        Console.WriteLine($"  API:            {options.ApiBaseAddress}");
        Console.WriteLine($"  Authority:      {options.OAuthAuthority}");
        Console.WriteLine($"  Redirect URI:   {options.OAuthRedirectUri}");
        if (payload.TryGetValue("slug", out object? slug) && slug != null)
        {
            Console.WriteLine($"  Slug:           {slug}");
            Console.WriteLine($"  Profile:        {payload["profileUrl"]}");
            Console.WriteLine($"  Strong auth:    {payload["strongAuth"]}");
            Console.WriteLine($"  Can publish:    {payload["canPublish"]}");
        }

        return 0;
    }

    internal static int SignOut(IServiceProvider services, bool json)
    {
        IXerahSCloudClient client = services.GetRequiredService<IXerahSCloudClient>();
        client.SignOut();
        WriteResult(json, ok: true, "signed_out", "Signed out on this device.");
        return 0;
    }

    public static string DescribeCompletion(XerahSCloudOAuthCompletion completion) => completion switch
    {
        XerahSCloudOAuthCompletion.Denied => "Authorization was denied.",
        XerahSCloudOAuthCompletion.Expired => "The sign-in request expired.",
        XerahSCloudOAuthCompletion.TokenRejected =>
            "The returned session did not pass XerahS Cloud security checks. See the XerahS log for the validator reason.",
        XerahSCloudOAuthCompletion.InvalidCallback => "The sign-in callback was invalid.",
        XerahSCloudOAuthCompletion.UnknownOrReplayedState =>
            "The sign-in callback was unknown or had already been used.",
        _ => $"Sign-in did not complete ({completion})."
    };

    private static void WriteSignedIn(bool json, XerahSCloudAccountSummary account)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                result = "Accepted",
                slug = account.Slug,
                profileUrl = account.ProfileUrl.AbsoluteUri,
                strongAuth = account.StrongAuth,
                canPublish = account.CanPublish
            }, JsonOptions));
            return;
        }

        Console.WriteLine($"Signed in as {account.Slug}.");
        Console.WriteLine($"Profile: {account.ProfileUrl}");
        Console.WriteLine($"Strong auth: {account.StrongAuth}");
        Console.WriteLine($"Publishing: {(account.CanPublish ? "enabled" : "disabled")}");
    }

    private static void WriteResult(bool json, bool ok, string result, string message)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok, result, message }, JsonOptions));
            return;
        }

        if (ok)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Error.WriteLine(message);
        }
    }
}
