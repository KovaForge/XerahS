#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using System.Reflection;

namespace XerahS.CLI.Commands;

/// <summary>
/// Temporarily points the current-user <c>xerahs://</c> protocol at this CLI
/// so browser authorization returns here instead of the desktop GUI.
/// </summary>
public static class CloudProtocolBinding
{
    public const string CommandKey = @"Software\Classes\xerahs\shell\open\command";

    public static string CreateCommandLine(string executablePath) =>
        $"\"{executablePath}\" cloud complete \"%1\"";

    public static string? GetCurrentExecutablePath() =>
        Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;

    public static IDisposable BindToCurrentProcess()
    {
        string? executablePath = GetCurrentExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath) || !OperatingSystem.IsWindows())
        {
            return EmptyBinding.Instance;
        }

        return Bind(CreateCommandLine(executablePath));
    }

    public static IDisposable Bind(string commandLine)
    {
        if (!OperatingSystem.IsWindows())
        {
            return EmptyBinding.Instance;
        }

        return WindowsBinding.Create(commandLine);
    }

    private sealed class EmptyBinding : IDisposable
    {
        public static readonly EmptyBinding Instance = new();
        public void Dispose()
        {
        }
    }

    private sealed class WindowsBinding : IDisposable
    {
        private readonly string? _previous;
        private readonly bool _hadKey;
        private bool _disposed;

        private WindowsBinding(string? previous, bool hadKey)
        {
            _previous = previous;
            _hadKey = hadKey;
        }

        public static WindowsBinding Create(string commandLine)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CommandKey, writable: true)
                ?? throw new InvalidOperationException("Unable to register the xerahs:// protocol for CLI sign-in.");
            bool hadKey = key.GetValue(null) != null;
            string? previous = key.GetValue(null) as string;
            key.SetValue(null, commandLine);
            return new WindowsBinding(previous, hadKey);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CommandKey, writable: true);
            if (key == null)
            {
                return;
            }

            if (_hadKey)
            {
                key.SetValue(null, _previous ?? string.Empty);
            }
            else if (_previous != null)
            {
                key.SetValue(null, _previous);
            }
        }
    }
}
