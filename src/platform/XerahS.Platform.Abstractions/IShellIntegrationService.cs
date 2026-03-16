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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Provides shell integration services such as file extension registration.
/// </summary>
public interface IShellIntegrationService
{
    /// <summary>
    /// True when plugin file association registration is supported on this platform.
    /// </summary>
    bool SupportsPluginExtensionRegistration { get; }

    /// <summary>
    /// True when context menu integration is supported on this platform.
    /// </summary>
    bool SupportsContextMenuIntegration { get; }

    /// <summary>
    /// True when Send To integration is supported on this platform.
    /// </summary>
    bool SupportsSendToIntegration { get; }

    /// <summary>
    /// Check if the plugin file extension (.xsdp) is registered with the system.
    /// </summary>
    bool IsPluginExtensionRegistered();

    /// <summary>
    /// Register or unregister the plugin file extension with the system.
    /// </summary>
    /// <param name="register">True to register, false to unregister.</param>
    void SetPluginExtensionRegistration(bool register);

    /// <summary>
    /// Check if the upload context menu integration entry is present.
    /// </summary>
    bool IsContextMenuIntegrationEnabled();

    /// <summary>
    /// Register or unregister upload context menu integration.
    /// </summary>
    /// <param name="enable">True to enable, false to disable.</param>
    /// <returns>True when requested state was applied.</returns>
    bool SetContextMenuIntegration(bool enable);

    /// <summary>
    /// Check if Send To integration is present.
    /// </summary>
    bool IsSendToIntegrationEnabled();

    /// <summary>
    /// Register or unregister Send To integration.
    /// </summary>
    /// <param name="enable">True to enable, false to disable.</param>
    /// <returns>True when requested state was applied.</returns>
    bool SetSendToIntegration(bool enable);
}

public sealed class UnsupportedShellIntegrationService : IShellIntegrationService
{
    public bool SupportsPluginExtensionRegistration => false;
    public bool SupportsContextMenuIntegration => false;
    public bool SupportsSendToIntegration => false;
    public bool IsPluginExtensionRegistered() => false;
    public void SetPluginExtensionRegistration(bool register) { }
    public bool IsContextMenuIntegrationEnabled() => false;
    public bool SetContextMenuIntegration(bool enable) => !enable;
    public bool IsSendToIntegrationEnabled() => false;
    public bool SetSendToIntegration(bool enable) => !enable;
}
