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
/// Host context passed into plugins. <see cref="Secrets"/> must stay declared on this
/// interface so plugins compiled against SDK 1.0 keep resolving get_Secrets.
/// </summary>
public interface IProviderContext
{
    ISecretStore Secrets { get; }

    HttpClient CreateHttpClient(bool allowAutoRedirect = true, bool infiniteTimeout = false)
        => throw new NotSupportedException("This host does not provide HttpClient.");

    void Log(string message)
    {
    }
}
