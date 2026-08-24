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
/// Interface for uploader destination providers (plugins).
/// Implement this and expose it via your plugin's entry point (see plugin.json).
/// </summary>
public interface IUploaderProvider
{
    string ProviderId { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }
    UploaderCategory[] SupportedCategories { get; }
    Type ConfigModelType { get; }

    /// <summary>Creates a configuration view (e.g. Avalonia UserControl). Return null for default property grid.</summary>
    object? CreateConfigView();

    /// <summary>Creates a configuration ViewModel. Return null if no custom VM.</summary>
    IUploaderConfigViewModel? CreateConfigViewModel();

    /// <summary>
    /// Creates an uploader instance from serialized JSON settings.
    /// Preferred: return a type that implements <see cref="IUploadHandler"/>.
    /// Legacy: return a GenericUploader; the host adapts it.
    /// </summary>
    object CreateInstance(string settingsJson);

    Dictionary<UploaderCategory, string[]> GetSupportedFileTypes();
    bool ValidateSettings(string settingsJson);
    string GetDefaultSettings(UploaderCategory category);
    event EventHandler? ConfigChanged;

    UploaderCapabilities Capabilities => UploaderCapabilities.None;

    UploaderConfigSchema? GetConfigSchema() => null;
}
