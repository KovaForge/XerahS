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
    color-scheme: dark;
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
    color: #e8f0ff;
    background:
        radial-gradient(circle at 10% -8%, #1a2b45 0, rgba(26, 43, 69, 0) 42%),
        radial-gradient(circle at 100% 16%, #132238 0, rgba(19, 34, 56, 0) 40%),
        linear-gradient(180deg, #0d1522 0%, #080e18 100%);
    line-height: 1.45;
}

body:has(#theme-toggle:checked) {
    color: #1b2430;
    background:
        radial-gradient(circle at 8% 0%, #e6f0ff 0, rgba(230, 240, 255, 0) 40%),
        radial-gradient(circle at 100% 18%, #edf5ff 0, rgba(237, 245, 255, 0) 36%),
        linear-gradient(180deg, #f8fbff 0%, #f3f6fb 100%);
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

.ThemeToggleInput {
    position: absolute;
    opacity: 0;
    pointer-events: none;
}

.ThemeToggleLabel {
    display: flex;
    align-items: center;
    gap: 10px;
    width: fit-content;
    margin: 0 0 14px auto;
    padding: 6px 10px;
    border-radius: 999px;
    border: 1px solid #304562;
    background: rgba(13, 24, 39, 0.78);
    color: #dbe8ff;
    font-size: 12px;
    font-weight: 650;
    letter-spacing: 0.01em;
    cursor: pointer;
    user-select: none;
    transition: border-color 120ms ease, background-color 120ms ease, color 120ms ease;
}

.ThemeToggleSwitch {
    position: relative;
    width: 38px;
    height: 20px;
    border-radius: 999px;
    border: 1px solid #3b5a81;
    background: #203551;
}

.ThemeToggleSwitch::after {
    content: """";
    position: absolute;
    top: 2px;
    left: 2px;
    width: 14px;
    height: 14px;
    border-radius: 50%;
    background: #cddfff;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.32);
    transition: transform 150ms ease, background-color 150ms ease;
}

.ThemeToggleText::before {
    content: ""Dark mode"";
}

.ThemeToggleInput:focus-visible + .ThemeToggleLabel {
    outline: 2px solid #7eb7ff;
    outline-offset: 2px;
}

.ThemeToggleInput:checked + .ThemeToggleLabel {
    border-color: #cedbf1;
    background: rgba(255, 255, 255, 0.9);
    color: #27384f;
}

.ThemeToggleInput:checked + .ThemeToggleLabel .ThemeToggleSwitch {
    border-color: #b8ceef;
    background: #d7e8ff;
}

.ThemeToggleInput:checked + .ThemeToggleLabel .ThemeToggleSwitch::after {
    transform: translateX(18px);
    background: #1d4d8f;
}

.ThemeToggleInput:checked + .ThemeToggleLabel .ThemeToggleText::before {
    content: ""Light mode"";
}

.IndexContent {
    --bg: #0a111c;
    --surface: #111b2a;
    --surface-soft: #162335;
    --border: #27384f;
    --text: #e8f0ff;
    --muted: #9db0ca;
    --brand: #70b1ff;
    --brand-soft: #21486f;
    --heading-top: #1a2a44;
    --heading-bottom: #122338;
    --badge-bg: #1a2b42;
    --badge-border: #294b73;
    --row-hover: #1c2c44;
    --footer-bg: rgba(16, 26, 40, 0.78);
    --shadow: 0 16px 40px -26px rgba(0, 0, 0, 0.72);
    color-scheme: dark;
    color: var(--text);
    border: 1px solid var(--border);
    border-radius: 18px;
    padding: clamp(14px, 2.4vw, 24px);
    background:
        radial-gradient(circle at 8% -2%, #172942 0, rgba(23, 41, 66, 0) 38%),
        radial-gradient(circle at 100% 14%, #142338 0, rgba(20, 35, 56, 0) 34%),
        linear-gradient(180deg, #0f1726 0%, var(--bg) 100%);
}

#theme-toggle:checked ~ .IndexContent {
    --bg: #f3f6fb;
    --surface: #ffffff;
    --surface-soft: #f8faff;
    --border: #d7deea;
    --text: #1b2430;
    --muted: #566277;
    --brand: #0c6cf2;
    --brand-soft: #dbe8ff;
    --heading-top: #ffffff;
    --heading-bottom: #eef4ff;
    --badge-bg: #edf3ff;
    --badge-border: #d6e4ff;
    --row-hover: #f1f6ff;
    --footer-bg: rgba(255, 255, 255, 0.72);
    --shadow: 0 14px 34px -22px rgba(9, 29, 57, 0.45);
    color-scheme: light;
}

.IndexContent h1,
.IndexContent h2,
.IndexContent h3,
.IndexContent h4,
.IndexContent h5,
.IndexContent h6 {
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
    background: linear-gradient(140deg, var(--heading-top) 0%, var(--heading-bottom) 100%);
    color: var(--text);
    font-weight: 650;
    letter-spacing: 0.01em;
    overflow-wrap: anywhere;
}

.IndexContent h1 {
    font-size: clamp(1.1rem, 1.7vw, 1.35rem);
}

.IndexContent h2 {
    font-size: 1.03rem;
}

.IndexContent h3 {
    font-size: 0.98rem;
}

.IndexContent h4,
.IndexContent h5,
.IndexContent h6 {
    font-size: 0.94rem;
}

.IndexContent .MainFolderBorder,
.IndexContent .FolderBorder {
    background: var(--surface);
    border: 1px solid var(--border);
    border-top: none;
    border-radius: 0 0 14px 14px;
    padding: 12px;
    box-shadow: var(--shadow);
}

.IndexContent .MainFolderBorder {
    margin: 0 0 18px 0;
}

.IndexContent .FolderBorder {
    margin: 12px 0 0 0;
    border-left: 3px solid var(--brand-soft);
}

.IndexContent .FileList,
.IndexContent ul {
    margin: 0;
    padding: 0;
    list-style: none;
}

.IndexContent li {
    margin: 0;
}

.IndexContent .FolderInfo {
    margin-left: auto;
    padding: 3px 10px;
    border-radius: 999px;
    background: var(--badge-bg);
    border: 1px solid var(--badge-border);
    color: var(--muted);
    font-size: 0.78rem;
    font-weight: 600;
    white-space: nowrap;
}

.IndexContent .FileRow,
.IndexContent ul > li {
    display: flex;
    align-items: center;
    gap: 12px;
    justify-content: space-between;
    padding: 7px 10px;
    border-radius: 10px;
    border: 1px solid transparent;
    transition: background-color 120ms ease, border-color 120ms ease;
}

.IndexContent .FileRow:nth-child(odd),
.IndexContent ul > li:nth-child(odd) {
    background: var(--surface-soft);
}

.IndexContent .FileRow:hover,
.IndexContent ul > li:hover {
    border-color: var(--border);
    background: var(--row-hover);
}

.IndexContent .FileName {
    overflow-wrap: anywhere;
}

.IndexContent .FileSize {
    color: var(--muted);
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
}

.IndexContent .EmptyFolder {
    margin: 0;
    padding: 8px 10px;
    color: var(--muted);
    font-style: italic;
}

.IndexContent footer {
    margin-top: 22px;
    padding: 10px 12px;
    border-radius: 10px;
    color: var(--muted);
    background: var(--footer-bg);
    border: 1px solid var(--border);
    font-size: 12px;
}

@media (max-width: 860px) {
    .container {
        padding: 16px;
    }

    .ThemeToggleLabel {
        margin-bottom: 12px;
    }

    .IndexContent h1,
    .IndexContent h2,
    .IndexContent h3,
    .IndexContent h4,
    .IndexContent h5,
    .IndexContent h6 {
        align-items: flex-start;
        flex-direction: column;
    }

    .IndexContent .FolderInfo {
        margin-left: 0;
        white-space: normal;
    }

    .IndexContent .FileRow,
    .IndexContent ul > li {
        flex-direction: column;
        align-items: flex-start;
    }

    .IndexContent .FileSize {
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
            AppendHtmlLine(sbHtmlIndex, 3, "<input type=\"checkbox\" id=\"theme-toggle\" class=\"ThemeToggleInput\" aria-label=\"Toggle color theme\">");
            AppendHtmlLine(sbHtmlIndex, 3, "<label for=\"theme-toggle\" class=\"ThemeToggleLabel\"><span class=\"ThemeToggleSwitch\" aria-hidden=\"true\"></span><span class=\"ThemeToggleText\"></span></label>");
            AppendHtmlLine(sbHtmlIndex, 3, HtmlHelper.StartTag("div", "", "class=\"IndexContent\""));

            rootFolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));

            FolderInfo folderInfo = GetFolderInfo(rootFolderPath);
            folderInfo.Update();

            IndexFolder(folderInfo);
            string index = sbContent.ToString().TrimEnd();
            AppendHtmlBlock(sbHtmlIndex, 1, index);
            if (settings.AddFooter)
            {
                AppendHtmlLine(sbHtmlIndex, ContentBaseIndent + 1, HtmlHelper.StartTag("footer") + GetFooter() + HtmlHelper.EndTag("footer"));
            }

            AppendHtmlLine(sbHtmlIndex, 3, HtmlHelper.EndTag("div"));
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
