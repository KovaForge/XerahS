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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Identifies one value in an <see cref="ISecretStore"/> without exposing the value itself.
/// </summary>
public readonly record struct InstanceSecretReference(string ProviderId, string SecretKey, string Name);

/// <summary>
/// Implemented by providers that can identify the secret-store values referenced by an
/// uploader instance's settings JSON.
/// </summary>
public interface IInstanceSecretBackupProvider
{
    /// <summary>
    /// Returns every secret-store reference needed to restore the configured instance.
    /// Invalid settings or settings without a secret key should return an empty list.
    /// </summary>
    IReadOnlyList<InstanceSecretReference> GetSecretReferences(string settingsJson);
}
