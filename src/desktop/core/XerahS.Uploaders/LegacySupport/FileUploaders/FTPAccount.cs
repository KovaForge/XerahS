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

using XerahS.Common;
using System.ComponentModel;

namespace XerahS.Uploaders.FileUploaders
{
    public class FTPAccount : ICloneable
    {
        [Category("FTP"), Description("Shown in the list as: Name - Server:Port")]
        public string Name { get; set; }

        [Category("Account"), Description("Connection protocol"), DefaultValue(FTPProtocol.FTP)]
        public FTPProtocol Protocol { get; set; }

        [Category("FTP"), Description("Host, e.g. example.com")]
        public string Host { get; set; }

        [Category("FTP"), Description("Port number"), DefaultValue(21)]
        public int Port { get; set; }

        [Category("FTP")]
        public string Username { get; set; }

        [Category("FTP"), JsonEncrypt]
        public string Password { get; set; }

        [Category("FTP"), Description("Set true for active or false for passive"), DefaultValue(false)]
        public bool IsActive { get; set; }

        [Category("FTP"), Description("FTP sub folder path, example: Screenshots. You can use name parsing: %y = year, %mo = month.")]
        public string SubFolderPath { get; set; }

        [Category("FTP"), Description("Choose an appropriate protocol to be accessed by the browser"), DefaultValue(BrowserProtocol.http)]
        public BrowserProtocol BrowserProtocol { get; set; }

        [Category("FTP"), Description("URL = HttpHomePath + SubFolderPath + FileName. If HttpHomePath is empty then URL = Host + SubFolderPath + FileName. %host = Host")]
        public string HttpHomePath { get; set; }

        [Category("FTP"), Description("Automatically add sub folder path to end of http home path"), DefaultValue(false)]
        public bool HttpHomePathAutoAddSubFolderPath { get; set; }

        [Category("FTP"), Description("Don't add file extension to URL"), DefaultValue(false)]
        public bool HttpHomePathNoExtension { get; set; }

        [Category("FTPS"), Description("Type of SSL to use. Explicit is TLS, Implicit is SSL."), DefaultValue(FTPSEncryption.Explicit)]
        public FTPSEncryption FTPSEncryption { get; set; }

        [Category("SFTP"), Description("Key location")]
        public string Keypath { get; set; }

        [Category("SFTP"), Description("OpenSSH key passphrase"), JsonEncrypt]
        public string Passphrase { get; set; }

        [Category("FTP"), Description("Protocol://Host:Port"), Browsable(false)]
        public string FTPAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Host))
                {
                    return string.Empty;
                }

                switch (Protocol)
                {
                    default:
                    case FTPProtocol.FTP:
                        break;
                    case FTPProtocol.FTPS:
                        break;
                    case FTPProtocol.SFTP:
                        break;
                }

                return $"{Name} - {EnumExtensions.GetDescription(Protocol)}";
            }
        }

        public FTPAccount()
        {
            Name = "New account";
            Protocol = FTPProtocol.FTP;
            Host = string.Empty;
            Port = 21;
            Username = string.Empty;
            Password = string.Empty;
            IsActive = false;
            SubFolderPath = string.Empty;
            BrowserProtocol = BrowserProtocol.http;
            HttpHomePath = string.Empty;
            HttpHomePathAutoAddSubFolderPath = true;
            HttpHomePathNoExtension = false;
            FTPSEncryption = FTPSEncryption.Explicit;
            Keypath = string.Empty;
            Passphrase = string.Empty;
        }

        public string GetSubFolderPath(string? fileName = null, NameParserType nameParserType = NameParserType.URL)
        {
            string path = NameParser.Parse(nameParserType, SubFolderPath.Replace("%host", Host, StringComparison.OrdinalIgnoreCase));
            return URLHelpers.CombineURL(path, fileName ?? string.Empty);
        }

        public string GetHttpHomePath()
        {
            return GetHttpHomePath(out _);
        }

        private string GetHttpHomePath(out bool autoAddSubFolderPath)
        {
            autoAddSubFolderPath = HttpHomePathAutoAddSubFolderPath;

            string homePath = HttpHomePath;
            if (!string.IsNullOrEmpty(homePath) && homePath.StartsWith("@", StringComparison.Ordinal))
            {
                autoAddSubFolderPath = false;
                homePath = homePath.Substring(1);
            }

            homePath = URLHelpers.RemovePrefixes(homePath).Replace("%host", Host, StringComparison.OrdinalIgnoreCase);

            ShareXCustomUploaderSyntaxParser parser = new ShareXCustomUploaderSyntaxParser
            {
                UseNameParser = true,
                NameParserType = NameParserType.URL
            };

            return parser.Parse(homePath);
        }

        public string GetUriPath(string fileName)
        {
            return GetUriPath(fileName, null);
        }

        public string GetUriPath(string fileName, string? subFolderPath)
        {
            if (string.IsNullOrEmpty(Host))
            {
                return string.Empty;
            }

            if (HttpHomePathNoExtension)
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            fileName = URLHelpers.URLEncode(fileName);

            if (subFolderPath == null)
            {
                subFolderPath = GetSubFolderPath();
            }

            UriBuilder httpHomeUri;

            string httpHomePath = GetHttpHomePath(out bool httpHomePathAutoAddSubFolderPath);
            string? ipv6HttpHomeHost = null;

            if (string.IsNullOrEmpty(httpHomePath))
            {
                string url = Host;

                if (url.StartsWith("ftp.", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(4);
                }

                if (httpHomePathAutoAddSubFolderPath)
                {
                    url = URLHelpers.CombineURL(url, subFolderPath);
                }

                url = URLHelpers.CombineURL(url, fileName);

                httpHomeUri = new UriBuilder(url)
                {
                    Port = -1
                };
            }
            else
            {
                int firstSlash = httpHomePath.IndexOf('/');
                int firstQuestion = httpHomePath.IndexOf('?');
                int firstEncodedQuestion = httpHomePath.IndexOf("%3F", StringComparison.OrdinalIgnoreCase);
                int firstQuery = firstQuestion >= 0 && firstEncodedQuestion >= 0
                    ? Math.Min(firstQuestion, firstEncodedQuestion)
                    : Math.Max(firstQuestion, firstEncodedQuestion);
                int firstPathOrQuery = firstSlash >= 0 && firstQuery >= 0
                    ? Math.Min(firstSlash, firstQuery)
                    : Math.Max(firstSlash, firstQuery);
                string httpHome = firstPathOrQuery >= 0 ? httpHomePath.Substring(0, firstPathOrQuery) : httpHomePath;

                string httpHomeHost = httpHome;
                int httpHomePort = -1;

                if (httpHome.StartsWith("[", StringComparison.Ordinal))
                {
                    int bracketEnd = httpHome.IndexOf(']');

                    if (bracketEnd >= 0)
                    {
                        httpHomeHost = httpHome.Substring(1, bracketEnd - 1);
                        ipv6HttpHomeHost = httpHomeHost;

                        if (httpHome.Length > bracketEnd + 2 && httpHome[bracketEnd + 1] == ':' &&
                            int.TryParse(httpHome.Substring(bracketEnd + 2), out int parsedPort))
                        {
                            httpHomePort = parsedPort;
                        }
                    }
                }
                else if (httpHome.StartsWith("%5B", StringComparison.OrdinalIgnoreCase))
                {
                    int bracketEnd = httpHome.IndexOf("%5D", StringComparison.OrdinalIgnoreCase);

                    if (bracketEnd >= 0)
                    {
                        httpHomeHost = httpHome.Substring(3, bracketEnd - 3);
                        ipv6HttpHomeHost = httpHomeHost;

                        if (httpHome.Length > bracketEnd + 4 && httpHome[bracketEnd + 3] == ':' &&
                            int.TryParse(httpHome.Substring(bracketEnd + 4), out int parsedPort))
                        {
                            httpHomePort = parsedPort;
                        }
                    }
                }
                else
                {
                    int portSpecifiedAt = httpHome.LastIndexOf(':');

                    if (portSpecifiedAt >= 0 && int.TryParse(httpHome.Substring(portSpecifiedAt + 1), out int parsedPort))
                    {
                        httpHomeHost = httpHome.Substring(0, portSpecifiedAt);
                        httpHomePort = parsedPort;
                    }
                }

                string httpHomePathAndQuery = firstPathOrQuery >= 0
                    ? httpHomePath.Substring(firstPathOrQuery + (httpHomePath[firstPathOrQuery] == '/' ? 1 : 0))
                    : string.Empty;
                int querySpecifiedAt = httpHomePathAndQuery.LastIndexOf('?');
                int encodedQuerySpecifiedAt = httpHomePathAndQuery.LastIndexOf("%3F", StringComparison.OrdinalIgnoreCase);
                int querySeparatorAt = querySpecifiedAt >= 0 && encodedQuerySpecifiedAt >= 0
                    ? Math.Max(querySpecifiedAt, encodedQuerySpecifiedAt)
                    : Math.Max(querySpecifiedAt, encodedQuerySpecifiedAt);
                int querySeparatorLength = querySeparatorAt == encodedQuerySpecifiedAt ? 3 : 1;
                string httpHomeDir = querySeparatorAt >= 0 ? httpHomePathAndQuery.Substring(0, querySeparatorAt) : httpHomePathAndQuery;
                string httpHomeQuery = querySeparatorAt >= 0 ? httpHomePathAndQuery.Substring(querySeparatorAt + querySeparatorLength) : string.Empty;

                httpHomeUri = new UriBuilder
                {
                    Host = httpHomeHost,
                    Path = httpHomeDir,
                    Query = httpHomeQuery
                };

                if (httpHomePort >= 0)
                {
                    httpHomeUri.Port = httpHomePort;
                }

                if (httpHomeUri.Query.EndsWith("=", StringComparison.Ordinal))
                {
                    string query = httpHomeUri.Query.TrimStart('?');
                    string queryValue = httpHomePathAutoAddSubFolderPath
                        ? URLHelpers.CombineURL(subFolderPath, fileName)
                        : fileName;
                    httpHomeUri.Query = query + queryValue;
                }
                else
                {
                    if (httpHomePathAutoAddSubFolderPath)
                    {
                        httpHomeUri.Path = URLHelpers.CombineURL(httpHomeUri.Path, subFolderPath);
                    }

                    httpHomeUri.Path = URLHelpers.CombineURL(httpHomeUri.Path, fileName);
                }
            }

            string scheme = EnumExtensions.GetDescription(BrowserProtocol);
            httpHomeUri.Scheme = scheme;

            if (ipv6HttpHomeHost != null)
            {
                string path = httpHomeUri.Path;

                if (!path.StartsWith("/", StringComparison.Ordinal))
                {
                    path = "/" + path;
                }

                string port = httpHomeUri.Port >= 0 ? ":" + httpHomeUri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                string schemePrefix = scheme.EndsWith("://", StringComparison.Ordinal) ? scheme : scheme + "://";
                return $"{schemePrefix}[{ipv6HttpHomeHost}]{port}{path}{httpHomeUri.Query}";
            }

            return httpHomeUri.Uri.OriginalString;
        }

        public string GetFtpPath(string fileName)
        {
            if (string.IsNullOrEmpty(FTPAddress))
            {
                return string.Empty;
            }

            return URLHelpers.CombineURL(FTPAddress, GetSubFolderPath(fileName, NameParserType.FilePath));
        }

        public override string ToString()
        {
            return $"{Name} ({Host}:{Port})";
        }

        object ICloneable.Clone()
        {
            return Clone()!;
        }

        public FTPAccount? Clone()
        {
            return MemberwiseClone() as FTPAccount;
        }
    }
}
