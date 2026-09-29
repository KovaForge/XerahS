#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using XerahS.Core.Cloud;

namespace XerahS.CLI.Commands;

/// <summary>
/// Forwards the <c>xerahs://oauth/callback</c> URI from a protocol-activated
/// secondary process to the waiting <c>cloud sign-in</c> process. A temp file
/// is used so an elevated waiter can still receive a medium-integrity browser
/// protocol launch.
/// </summary>
public static class CloudOAuthCallbackPipe
{
    public const string DefaultName = "XerahS.CloudOAuth.Callback";

    public static string GetCallbackPath(string pipeName = DefaultName) =>
        Path.Combine(Path.GetTempPath(), pipeName + ".callback");

    public static async Task<Uri?> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string pipeName = DefaultName)
    {
        string path = GetCallbackPath(pipeName);
        string readyPath = path + ".ready";
        TryDelete(path);
        File.WriteAllText(readyPath, "ready");
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    string value = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    TryDelete(path);
                    return TryCreateCallbackUri(value.Trim(), out Uri? uri) ? uri : null;
                }

                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            TryDelete(readyPath);
            TryDelete(path);
        }
    }

    public static async Task<bool> TrySendAsync(
        string callbackArgument,
        CancellationToken cancellationToken = default,
        string pipeName = DefaultName)
    {
        if (!TryCreateCallbackUri(callbackArgument, out Uri? uri) || uri == null)
        {
            return false;
        }

        string path = GetCallbackPath(pipeName);
        string readyPath = path + ".ready";
        if (!File.Exists(readyPath))
        {
            return false;
        }

        string staging = path + ".tmp";
        await File.WriteAllTextAsync(staging, uri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
        File.Move(staging, path, overwrite: true);
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    public static bool TryGetCallbackArgument(IReadOnlyList<string> args, out string? callbackArgument)
    {
        callbackArgument = null;
        if (args.Count == 0)
        {
            return false;
        }

        if (XerahSCloudOAuthCallbackParser.IsCallbackArgument(args[0]))
        {
            callbackArgument = args[0];
            return true;
        }

        if (args.Count >= 3 &&
            string.Equals(args[0], "cloud", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(args[1], "complete", StringComparison.OrdinalIgnoreCase) &&
            XerahSCloudOAuthCallbackParser.IsCallbackArgument(args[2]))
        {
            callbackArgument = args[2];
            return true;
        }

        return false;
    }

    public static bool TryCreateCallbackUri(string? value, out Uri? uri)
    {
        uri = null;
        if (!XerahSCloudOAuthCallbackParser.IsCallbackArgument(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) ||
            !XerahSCloudOAuthCallbackParser.TryParse(parsed, out _))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
