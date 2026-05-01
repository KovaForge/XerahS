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

using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace XerahS.Mobile.Maui.ViewModels;

public sealed class MobileAboutViewModel
{
    public string AppName => AppInfo.Current.Name;
    public string Version => AppInfo.Current.VersionString;
    public string Build => AppInfo.Current.BuildString;
    public string VersionText => $"Version {Version}";
    public string PackageId => AppInfo.Current.PackageName;
    public string PlatformLabel => DeviceInfo.Current.Platform == DevicePlatform.iOS ? "iOS" : "Android";
    public string PlatformVersion => DeviceInfo.Current.VersionString;
    public ICommand OpenLinkCommand { get; }

    public ObservableCollection<AboutLink> Links { get; } =
    [
        new("Website", "https://xerahs.com"),
        new("GitHub Project", "https://github.com/ShareX/XerahS"),
        new("Issues", "https://github.com/ShareX/XerahS/issues/"),
        new("Contributors", "https://github.com/ShareX/XerahS/graphs/contributors"),
        new("Changelog", "https://xerahs.com/changelog.html"),
        new("Privacy Policy", "https://getsharex.com/privacy-policy")
    ];

    public ObservableCollection<AboutLink> SocialLinks { get; } =
    [
        new("X", "https://x.com/ShareX"),
        new("Discord", "https://discord.gg/ShareX"),
        new("Reddit", "https://www.reddit.com/r/sharex")
    ];

    public MobileAboutViewModel()
    {
        OpenLinkCommand = new AsyncRelayCommand<string>(OpenLinkAsync);
    }

    private static async Task OpenLinkAsync(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await Launcher.Default.OpenAsync(uri);
    }
}

public sealed record AboutLink(string Title, string Url);
