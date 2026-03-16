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

using XerahS.Platform.Abstractions;

namespace XerahS.Platform.MacOS.Services;

/// <summary>
/// Finder integration requires app extensions/bundle registration and is not implemented yet.
/// This service reports unsupported features instead of silently accepting toggles.
/// </summary>
public sealed class MacOSShellIntegrationService : IShellIntegrationService
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
