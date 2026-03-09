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

using Newtonsoft.Json;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Immich.Plugin;

public sealed class ImmichProvider : UploaderProviderBase
{
    public override string ProviderId => "immich";
    public override string Name => "Immich Uploader";
    public override string Description => "Upload files to Immich Uploader";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File };
    public override Type ConfigModelType => typeof(ImmichConfigModel);

    public override Uploader CreateInstance(string settingsJson)
    {
        ImmichConfigModel config = DeserializeConfig(settingsJson);
        string apiToken = ResolveSecret(config.SecretKey, "apiToken");
        return new ImmichUploader(config, apiToken);
    }

    public override bool ValidateSettings(string settingsJson)
    {
        ImmichConfigModel config = DeserializeConfig(settingsJson);
        return !string.IsNullOrWhiteSpace(config.BaseUrl) &&
               !string.IsNullOrWhiteSpace(ResolveSecret(config.SecretKey, "apiToken"));
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes()
    {
        string[] allTypes =
        {
            "png", "jpg", "jpeg", "gif", "bmp", "tiff", "webp", "svg",
            "mp4", "avi", "mov", "mkv", "flv", "wmv", "webm",
            "txt", "log", "json", "xml", "md", "html", "css", "js",
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
            "zip", "rar", "7z", "tar", "gz",
            "exe", "dll", "so", "dmg", "apk", "ipa"
        };

        return new Dictionary<UploaderCategory, string[]>
        {
            { UploaderCategory.Image, allTypes },
            { UploaderCategory.Text, allTypes },
            { UploaderCategory.File, allTypes }
        };
    }

    public override object? CreateConfigView()
    {
        return new Views.ImmichConfigView();
    }

    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.ImmichConfigViewModel();
    }

    private string ResolveSecret(string secretKey, string secretName)
    {
        if (Secrets == null || string.IsNullOrWhiteSpace(secretKey))
        {
            return string.Empty;
        }

        return Secrets.GetSecret(ProviderId, secretKey, secretName) ?? string.Empty;
    }

    private static ImmichConfigModel DeserializeConfig(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new ImmichConfigModel();
        }

        return JsonConvert.DeserializeObject<ImmichConfigModel>(settingsJson) ?? new ImmichConfigModel();
    }
}
