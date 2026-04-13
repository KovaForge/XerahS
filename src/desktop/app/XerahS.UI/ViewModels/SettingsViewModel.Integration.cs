#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Security.Cryptography;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        // Integration Settings
        [ObservableProperty]
        private bool _isPluginExtensionRegistered;

        [ObservableProperty]
        private bool _supportsFileAssociations;

        [ObservableProperty]
        private bool _supportsContextMenuIntegration;

        [ObservableProperty]
        private bool _supportsSendToIntegration;

        public bool HasMcpApiKey => !string.IsNullOrWhiteSpace(SettingsManager.Settings.McpApiKey);

        public string McpApiKeyDisplay
        {
            get
            {
                var apiKey = SettingsManager.Settings.McpApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return "Not generated yet";
                }

                var suffixLength = Math.Min(4, apiKey.Length);
                return $"{new string('*', Math.Max(apiKey.Length - suffixLength, 0))}{apiKey[^suffixLength..]}";
            }
        }

        public string McpApiKeyStatusText => HasMcpApiKey
            ? "API key is configured. Use Copy to place the full token on the clipboard."
            : "API key has not been generated yet.";

        public string McpManifestUrl => "https://xerahs.com/.well-known/mcp/manifest.json";

        private string _assistantHotkeyStatusText = "Use the assistant shortcut to open the in-app command overlay.";

        public bool AssistantEnabled
        {
            get => SettingsManager.Settings.AssistantEnabled;
            set
            {
                if (SettingsManager.Settings.AssistantEnabled == value)
                {
                    return;
                }

                SettingsManager.Settings.AssistantEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool AssistantPromptHistoryEnabled
        {
            get => SettingsManager.Settings.AssistantPromptHistoryEnabled;
            set
            {
                if (SettingsManager.Settings.AssistantPromptHistoryEnabled == value)
                {
                    return;
                }

                SettingsManager.Settings.AssistantPromptHistoryEnabled = value;
                OnPropertyChanged();
            }
        }

        public string AssistantHotkeyText => SettingsManager.Settings.AssistantHotkey.GetDisplayString();

        public string AssistantHotkeyStatusText
        {
            get => _assistantHotkeyStatusText;
            private set => SetProperty(ref _assistantHotkeyStatusText, value);
        }

        [RelayCommand]
        private void TestAssistantShortcut()
        {
            AssistantHotkeyStatusText = PlatformServices.IsInitialized
                ? $"Current shortcut: {AssistantHotkeyText}"
                : "Platform services are not initialized yet.";
        }

        partial void OnIsPluginExtensionRegisteredChanged(bool value)
        {
            if (_isLoading) return; // Don't trigger during initial load

            try
            {
                PlatformServices.ShellIntegration.SetPluginExtensionRegistration(value);
            }
            catch (InvalidOperationException)
            {
                // Shell integration not available on this platform
            }
        }

        [RelayCommand]
        private async Task CopyMcpApiKeyAsync()
        {
            var apiKey = SettingsManager.Settings.McpApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                GenerateMcpApiKey();
                apiKey = SettingsManager.Settings.McpApiKey;
            }

            await PlatformServices.Clipboard.SetTextAsync(apiKey);
        }

        [RelayCommand]
        private async Task CopyMcpManifestUrlAsync()
        {
            await PlatformServices.Clipboard.SetTextAsync(McpManifestUrl);
        }

        [RelayCommand]
        private void GenerateMcpApiKey()
        {
            SettingsManager.Settings.McpApiKey = CreateMcpApiKey();
            SettingsManager.SaveApplicationConfig();
            NotifyMcpApiKeyChanged();
        }

        private void NotifyMcpApiKeyChanged()
        {
            OnPropertyChanged(nameof(HasMcpApiKey));
            OnPropertyChanged(nameof(McpApiKeyDisplay));
            OnPropertyChanged(nameof(McpApiKeyStatusText));
        }

        private static string CreateMcpApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(24);
            return Convert.ToBase64String(bytes)[..32];
        }

        private static bool ApplyContextMenuPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                IShellIntegrationService? shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
                if (shellIntegration == null || !shellIntegration.SupportsContextMenuIntegration)
                {
                    return !enable;
                }

                return shellIntegration.SetContextMenuIntegration(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: ContextMenu platform services not ready.");
                return false;
            }
        }

        private static bool ApplySendToPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                IShellIntegrationService? shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
                if (shellIntegration == null || !shellIntegration.SupportsSendToIntegration)
                {
                    return !enable;
                }

                return shellIntegration.SetSendToIntegration(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: SendTo platform services not ready.");
                return false;
            }
        }

        private static bool ApplyStartupPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                return PlatformServices.Startup.SetRunAtStartup(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: RunAtStartup platform services not ready.");
                return false;
            }
        }
    }
}
