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
using System.Net;
using System.Text;

namespace XerahS.Indexer
{
    public class IndexerHtml : Indexer
    {
        private const string DefaultCss = @":root {
    color-scheme: light;
    --bg: #f3f6fb;
    --surface: #ffffff;
    --surface-soft: #f8faff;
    --border: #d7deea;
    --text: #1b2430;
    --muted: #566277;
    --brand: #0c6cf2;
    --brand-soft: #dbe8ff;
    --shadow: 0 14px 34px -22px rgba(9, 29, 57, 0.45);
}

* {
    box-sizing: border-box;
}

html,
body {
    margin: 0;
    padding: 0;
}

body {
    min-height: 100vh;
    font-family: ""Segoe UI"", ""Helvetica Neue"", Arial, sans-serif;
    color: var(--text);
    background:
        radial-gradient(circle at 8% 0%, #e6f0ff 0, rgba(230, 240, 255, 0) 40%),
        radial-gradient(circle at 100% 18%, #edf5ff 0, rgba(237, 245, 255, 0) 36%),
        linear-gradient(180deg, #f8fbff 0%, var(--bg) 100%);
    line-height: 1.45;
}

a {
    color: var(--brand);
    text-decoration: none;
}

a:hover {
    text-decoration: underline;
}

.container {
    max-width: 1140px;
    margin: 0 auto;
    padding: clamp(20px, 4vw, 36px);
}

h1,
h2,
h3,
h4,
h5,
h6 {
    margin: 0;
    padding: 12px 14px;
    border: 1px solid var(--border);
    border-bottom: none;
    border-radius: 14px 14px 0 0;
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 10px;
    justify-content: space-between;
    background: linear-gradient(140deg, #ffffff 0%, #eef4ff 100%);
    font-weight: 650;
    letter-spacing: 0.01em;
    overflow-wrap: anywhere;
}

h1 {
    font-size: clamp(1.1rem, 1.7vw, 1.35rem);
}

h2 {
    font-size: 1.03rem;
}

h3 {
    font-size: 0.98rem;
}

h4,
h5,
h6 {
    font-size: 0.94rem;
}

.MainFolderBorder,
.FolderBorder {
    background: var(--surface);
    border: 1px solid var(--border);
    border-top: none;
    border-radius: 0 0 14px 14px;
    padding: 12px;
    box-shadow: var(--shadow);
}

.MainFolderBorder {
    margin: 0 0 18px 0;
}

.FolderBorder {
    margin: 12px 0 0 0;
    border-left: 3px solid var(--brand-soft);
}

.FileList,
ul {
    margin: 0;
    padding: 0;
    list-style: none;
}

li {
    margin: 0;
}

.FolderInfo {
    margin-left: auto;
    padding: 3px 10px;
    border-radius: 999px;
    background: #edf3ff;
    border: 1px solid #d6e4ff;
    color: var(--muted);
    font-size: 0.78rem;
    font-weight: 600;
    white-space: nowrap;
}

.FileRow,
ul > li {
    display: flex;
    align-items: center;
    gap: 12px;
    justify-content: space-between;
    padding: 7px 10px;
    border-radius: 10px;
    border: 1px solid transparent;
    transition: background-color 120ms ease, border-color 120ms ease;
}

.FileRow:nth-child(odd),
ul > li:nth-child(odd) {
    background: var(--surface-soft);
}

.FileRow:hover,
ul > li:hover {
    border-color: #d2def5;
    background: #f1f6ff;
}

.FileName {
    overflow-wrap: anywhere;
}

.FileSize {
    color: var(--muted);
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
}

.EmptyFolder {
    margin: 0;
    padding: 8px 10px;
    color: var(--muted);
    font-style: italic;
}

footer {
    margin-top: 22px;
    padding: 10px 12px;
    border-radius: 10px;
    color: var(--muted);
    background: rgba(255, 255, 255, 0.68);
    border: 1px solid var(--border);
    font-size: 12px;
}

@media (max-width: 860px) {
    .container {
        padding: 16px;
    }

    h1,
    h2,
    h3,
    h4,
    h5,
    h6 {
        align-items: flex-start;
        flex-direction: column;
    }

    .FolderInfo {
        margin-left: 0;
        white-space: normal;
    }

    .FileRow,
    ul > li {
        flex-direction: column;
        align-items: flex-start;
    }

    .FileSize {
        font-size: 0.82rem;
    }
}";
        protected StringBuilder sbContent = new StringBuilder();
        protected string rootFolderPath = string.Empty;
        private const int IndentSize = 2;
        private const int ContentBaseIndent = 3;

        public IndexerHtml(IndexerSettings indexerSettings) : base(indexerSettings)
        {
        }

        public override string Index(string folderPath)
        {
            sbContent.Clear();
            StringBuilder sbHtmlIndex = new StringBuilder();
            AppendHtmlLine(sbHtmlIndex, 0, "<!DOCTYPE html>");
            AppendHtmlLine(sbHtmlIndex, 0, HtmlHelper.StartTag("html", "", "lang=\"en\""));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.StartTag("head"));
            AppendHtmlLine(sbHtmlIndex, 2, "<meta charset=\"UTF-8\">");
            AppendHtmlLine(sbHtmlIndex, 2, "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
            AppendHtmlLine(sbHtmlIndex, 2, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.Tag("title", "Index for " + Path.GetFileName(folderPath)));
            AppendHtmlBlock(sbHtmlIndex, 2, GetCssStyle());
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.EndTag("head"));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.StartTag("body"));
            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.StartTag("div", "", "class=\"container\""));

            rootFolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));

            FolderInfo folderInfo = GetFolderInfo(rootFolderPath);
            folderInfo.Update();

            IndexFolder(folderInfo);
            string index = sbContent.ToString().TrimEnd();
            AppendHtmlBlock(sbHtmlIndex, 0, index);
            if (settings.AddFooter)
            {
                AppendHtmlLine(sbHtmlIndex, ContentBaseIndent, HtmlHelper.StartTag("footer") + GetFooter() + HtmlHelper.EndTag("footer"));
            }

            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.EndTag("div"));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.EndTag("body"));
            AppendHtmlLine(sbHtmlIndex, 0, HtmlHelper.EndTag("html"));
            return sbHtmlIndex.ToString().Trim();
        }

        protected override void IndexFolder(FolderInfo dir, int level = 0)
        {
            int blockIndent = ContentBaseIndent + (level * 2);
            AppendHtmlLine(sbContent, blockIndent, GetFolderNameRow(dir, level));

            string divClass = level > 0 ? "FolderBorder" : "MainFolderBorder";
            AppendHtmlLine(sbContent, blockIndent, HtmlHelper.StartTag("div", "", $"class=\"{divClass}\""));

            if (dir.Files.Count > 0)
            {
                AppendHtmlLine(sbContent, blockIndent + 1, HtmlHelper.StartTag("ul", "", "class=\"FileList\""));

                foreach (FileInfo fi in dir.Files)
                {
                    AppendHtmlLine(sbContent, blockIndent + 2, GetFileNameRow(fi));
                }

                AppendHtmlLine(sbContent, blockIndent + 1, HtmlHelper.EndTag("ul"));
            }
            else if (dir.Folders.Count == 0)
            {
                AppendHtmlLine(sbContent, blockIndent + 1, HtmlHelper.Tag("p", "Empty folder", "", "class=\"EmptyFolder\""));
            }

            foreach (FolderInfo subdir in dir.Folders)
            {
                IndexFolder(subdir, level + 1);
            }

            AppendHtmlLine(sbContent, blockIndent, HtmlHelper.EndTag("div"));
        }

        private string GetFolderNameRow(FolderInfo dir, int level)
        {
            string folderSummary = GetFolderSummary(dir);
            string folderInfoRow = string.IsNullOrEmpty(folderSummary)
                ? string.Empty
                : " " + HtmlHelper.Tag("span", folderSummary, "", "class=\"FolderInfo\"");

            string pathTitle = GetDisplayPathTitle(dir);
            int heading = (level + 1).Clamp(1, 6);

            return HtmlHelper.StartTag("h" + heading) + WebUtility.HtmlEncode(pathTitle) + folderInfoRow + HtmlHelper.EndTag("h" + heading);
        }

        private string GetFileNameRow(FileInfo fi)
        {
            string fileNameRow = HtmlHelper.StartTag("li", "", "class=\"FileRow\"");
            fileNameRow += HtmlHelper.Tag("span", fi.Name, "", "class=\"FileName\"");

            if (settings.ShowSizeInfo)
            {
                fileNameRow += " " + HtmlHelper.Tag("span", fi.Length.ToSizeString(settings.BinaryUnits), "", "class=\"FileSize\"");
            }

            fileNameRow += HtmlHelper.EndTag("li");

            return fileNameRow;
        }

        private string GetFooter()
        {
            return $"Generated by <a href=\"{Links.XerahSWebsite}\">{AppResources.AppName} Directory Indexer</a> on {DateTime.UtcNow:yyyy-MM-dd 'at' HH:mm:ss 'UTC'}";
        }

        private string GetCssStyle()
        {
            string css = DefaultCss;

            if (settings.UseCustomCSSFile)
            {
                string? cssPath = ResolveCustomCssPath(settings.CustomCSSFilePath);
                if (!string.IsNullOrEmpty(cssPath) && File.Exists(cssPath))
                {
                    try
                    {
                        css = File.ReadAllText(cssPath, Encoding.UTF8);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            return $"<style type=\"text/css\">\r\n{css}\r\n</style>";
        }

        private string GetDisplayPathTitle(FolderInfo dir)
        {
            if (!settings.DisplayPath)
            {
                return GetSafeFolderName(dir);
            }

            if (!settings.DisplayPathLimited || string.IsNullOrEmpty(rootFolderPath))
            {
                return dir.FolderPath;
            }

            string relativePath = Path.GetRelativePath(rootFolderPath, dir.FolderPath);
            if (string.Equals(relativePath, ".", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(relativePath))
            {
                return GetSafeFolderName(dir);
            }

            return relativePath;
        }

        private string GetFolderSummary(FolderInfo dir)
        {
            if (dir.IsEmpty)
            {
                return string.Empty;
            }

            StringBuilder summaryBuilder = new StringBuilder();

            if (settings.ShowSizeInfo)
            {
                summaryBuilder.Append(dir.Size.ToSizeString(settings.BinaryUnits));
                summaryBuilder.Append(' ');
            }

            summaryBuilder.Append('(');

            if (dir.TotalFileCount > 0)
            {
                summaryBuilder.Append(dir.TotalFileCount.ToString("n0"));
                summaryBuilder.Append(" file");
                if (dir.TotalFileCount > 1)
                {
                    summaryBuilder.Append('s');
                }
            }

            if (dir.TotalFolderCount > 0)
            {
                if (dir.TotalFileCount > 0)
                {
                    summaryBuilder.Append(", ");
                }

                summaryBuilder.Append(dir.TotalFolderCount.ToString("n0"));
                summaryBuilder.Append(" folder");
                if (dir.TotalFolderCount > 1)
                {
                    summaryBuilder.Append('s');
                }
            }

            summaryBuilder.Append(')');
            return summaryBuilder.ToString();
        }

        private static string GetSafeFolderName(FolderInfo dir)
        {
            return !string.IsNullOrWhiteSpace(dir.FolderName) ? dir.FolderName : dir.FolderPath;
        }

        private static string? ResolveCustomCssPath(string? configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            string cssPath = configuredPath.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(cssPath))
            {
                return null;
            }

            if (cssPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(cssPath, UriKind.Absolute, out Uri? fileUri) &&
                fileUri.IsFile)
            {
                return fileUri.LocalPath;
            }

            cssPath = Environment.ExpandEnvironmentVariables(cssPath);

            if (cssPath.StartsWith("~", StringComparison.Ordinal))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(homeDirectory))
                {
                    cssPath = Path.Combine(homeDirectory, cssPath.TrimStart('~', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }
            }

            try
            {
                return Path.GetFullPath(cssPath);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        private static void AppendHtmlLine(StringBuilder builder, int indentLevel, string line)
        {
            builder.Append(new string(' ', indentLevel * IndentSize));
            builder.AppendLine(line);
        }

        private static void AppendHtmlBlock(StringBuilder builder, int indentLevel, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                AppendHtmlLine(builder, indentLevel, string.Empty);
                return;
            }

            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                AppendHtmlLine(builder, indentLevel, line);
            }
        }
    }
}
