#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion

namespace XerahS.Uploaders.PluginSystem;

/// <summary>Defines which file types an uploader instance handles within its category.</summary>
public class FileTypeScope
{
    /// <summary>Extensions matched by <see cref="TextFiles"/>: plain text, Markdown, data and source files.</summary>
    public static readonly IReadOnlyCollection<string> TextFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "text", "log", "nfo", "md", "markdown", "mdown", "mkd", "rst", "adoc", "org", "tex",
        "json", "jsonc", "json5", "yaml", "yml", "toml", "xml", "csv", "tsv", "ini", "cfg", "conf", "env", "properties",
        "html", "htm", "xhtml", "xht", "css", "scss", "less", "js", "mjs", "cjs", "ts", "tsx", "jsx",
        "c", "cpp", "cc", "cxx", "h", "hpp", "hxx", "cs", "vb", "fs", "java", "kt", "kts", "swift", "go", "gohtml",
        "rs", "py", "rb", "php", "pl", "lua", "dart", "r", "sql", "sh", "bash", "zsh", "fish", "ps1", "bat", "cmd",
        "diff", "patch", "srt", "vtt"
    };

    /// <summary>Catch-all for the category. Instances with specific types or <see cref="TextFiles"/> take precedence.</summary>
    public bool AllFileTypes { get; set; }

    /// <summary>Handles every extension in <see cref="TextFileExtensions"/>. Explicit extensions take precedence.</summary>
    public bool TextFiles { get; set; }

    public List<string> FileExtensions { get; set; } = new();

    public static bool IsTextFileExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && TextFileExtensions.Contains(extension.Trim().TrimStart('.'));

    public bool HandlesExtension(string extension)
    {
        if (AllFileTypes) return true;
        if (TextFiles && IsTextFileExtension(extension)) return true;
        return FileExtensions.Contains(extension.Trim().TrimStart('.'), StringComparer.OrdinalIgnoreCase);
    }
}
