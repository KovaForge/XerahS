#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using System.IO.Pipes;
using System.Text;
using XerahS.Core.Cloud;

namespace XerahS.CLI.Commands;

/// <summary>
/// Forwards the <c>xerahs://oauth/callback</c> URI from a protocol-activated
/// secondary process to the waiting <c>cloud sign-in</c> process.
/// </summary>
public static class CloudOAuthCallbackPipe
{
    public const string DefaultName = "XerahS.CloudOAuth.Callback";

    public static async Task<Uri?> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string pipeName = DefaultName)
    {
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await server.WaitForConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            string? value = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            return TryCreateCallbackUri(value, out Uri? uri) ? uri : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
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

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            await client.ConnectAsync(3_000, cancellationToken).ConfigureAwait(false);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(uri.AbsoluteUri).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return false;
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
