#
# Pre-commit hook to validate GPL v3 license headers in C#, Swift, and Kotlin files (PowerShell version)
# All require full GPL v3 license text. See developers/guidelines/CODING_STANDARDS.md
#
# To install: git config core.hooksPath .githooks
# To bypass: git commit --no-verify
#

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$MarkdownChecker = Join-Path (Split-Path -Parent $ScriptDir) "scripts/check-markdown-mojibake.py"

if (Get-Command python -ErrorAction SilentlyContinue) {
    $PythonCommand = "python"
} elseif (Get-Command py -ErrorAction SilentlyContinue) {
    $PythonCommand = "py"
} else {
    $PythonCommand = $null
}

# Expected header components
$CURRENT_YEAR = (Get-Date).Year
$EXPECTED_PROJECT = "XerahS - The Avalonia UI implementation of ShareX"
$EXPECTED_COPYRIGHT = "Copyright (c) 2007-$CURRENT_YEAR ShareX Team"
$EXPECTED_GPL_START = "This program is free software"

# Swift: copyright line may have trailing period; must include full GPL text
$EXPECTED_SWIFT_PROJECT = "XerahS Mobile (Swift)"

# Get list of staged C#, Swift, and Kotlin files
$STAGED_CS_FILES = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.cs$' }
$STAGED_SWIFT_FILES = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.swift$' }
$STAGED_KT_FILES = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.kt$' }
$STAGED_MD_FILES = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.md$' }

$TOTAL_VIOLATIONS = 0
$VIOLATION_FILES = @()

if ($STAGED_MD_FILES) {
    if (-not $PythonCommand) {
        Write-Host "FAIL: Python 3 is required to run the Markdown mojibake checker." -ForegroundColor Red
        exit 1
    }

    Write-Host "Checking Markdown files for mojibake and BOM issues..."
    if ($PythonCommand -eq "py") {
        & py -3 $MarkdownChecker --fix @STAGED_MD_FILES
    } else {
        & python $MarkdownChecker --fix @STAGED_MD_FILES
    }
    $markdownCheckExit = $LASTEXITCODE
    git add -- @STAGED_MD_FILES
    if ($markdownCheckExit -ne 0) {
        exit $markdownCheckExit
    }
}

# --- C# files ---
if ($STAGED_CS_FILES) {
    Write-Host "Checking license headers in staged C# files..."
    foreach ($FILE in $STAGED_CS_FILES) {
        if (-not (Test-Path $FILE)) { continue }
        $HEADER = (Get-Content $FILE -TotalCount 30) -join [Environment]::NewLine
        $MISSING = @()
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_PROJECT)) { $MISSING += "project name" }
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_COPYRIGHT)) { $MISSING += "copyright year $CURRENT_YEAR" }
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_GPL_START)) { $MISSING += "GPL v3 license text" }
        if ($HEADER -notmatch "#region License Information") { $MISSING += "#region License Information tag" }
        if ($MISSING.Count -gt 0) {
            $TOTAL_VIOLATIONS++
            $VIOLATION_FILES += $FILE
            Write-Host "FAIL: $FILE" -ForegroundColor Red
            Write-Host "  Missing: $($MISSING -join ', ')" -ForegroundColor Yellow
        }
    }
}

# --- Swift files (full GPL v3 text required) ---
if ($STAGED_SWIFT_FILES) {
    Write-Host "Checking license headers in staged Swift files..."
    foreach ($FILE in $STAGED_SWIFT_FILES) {
        if (-not (Test-Path $FILE)) { continue }
        $HEADER = (Get-Content $FILE -TotalCount 35) -join [Environment]::NewLine
        $MISSING = @()
        if ($HEADER -notmatch "Copyright \(c\) 2007-$CURRENT_YEAR ShareX Team\.?") { $MISSING += "copyright year $CURRENT_YEAR" }
        # Accept "XerahS Mobile (Swift)" or "XerahS Share Extension"
        if ($HEADER -notmatch "XerahS Mobile \(Swift\)" -and $HEADER -notmatch "XerahS Share Extension") { $MISSING += "XerahS Mobile (Swift) or XerahS Share Extension" }
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_GPL_START)) { $MISSING += "GPL v3 license text" }
        if ($MISSING.Count -gt 0) {
            $TOTAL_VIOLATIONS++
            $VIOLATION_FILES += $FILE
            Write-Host "FAIL: $FILE" -ForegroundColor Red
            Write-Host "  Missing: $($MISSING -join ', ')" -ForegroundColor Yellow
        }
    }
}

# --- Kotlin files (full GPL v3 text required) ---
if ($STAGED_KT_FILES) {
    Write-Host "Checking license headers in staged Kotlin files..."
    foreach ($FILE in $STAGED_KT_FILES) {
        if (-not (Test-Path $FILE)) { continue }
        $HEADER = (Get-Content $FILE -TotalCount 35) -join [Environment]::NewLine
        $MISSING = @()
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_PROJECT)) { $MISSING += "project name" }
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_COPYRIGHT)) { $MISSING += "copyright year $CURRENT_YEAR" }
        if ($HEADER -notmatch [regex]::Escape($EXPECTED_GPL_START)) { $MISSING += "GPL v3 license text" }
        if ($MISSING.Count -gt 0) {
            $TOTAL_VIOLATIONS++
            $VIOLATION_FILES += $FILE
            Write-Host "FAIL: $FILE" -ForegroundColor Red
            Write-Host "  Missing: $($MISSING -join ', ')" -ForegroundColor Yellow
        }
    }
}

if (-not $STAGED_MD_FILES -and -not $STAGED_CS_FILES -and -not $STAGED_SWIFT_FILES -and -not $STAGED_KT_FILES) {
    Write-Host "OK: No Markdown, C#, Swift, or Kotlin files to check" -ForegroundColor Green
    exit 0
}

if ($TOTAL_VIOLATIONS -gt 0) {
    Write-Host ""
    Write-Host "==============================================================" -ForegroundColor Red
    Write-Host "  LICENSE HEADER VALIDATION FAILED" -ForegroundColor Red
    Write-Host "==============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "$TOTAL_VIOLATIONS file(s) have incorrect or missing license headers:" -ForegroundColor Yellow
    foreach ($FILE in $VIOLATION_FILES) {
        Write-Host "  -> $FILE" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "C# / Swift / Kotlin: All require full GPL v3 license text. See developers/guidelines/CODING_STANDARDS.md"
    Write-Host ""
    Write-Host "To bypass this check (NOT RECOMMENDED):" -ForegroundColor Yellow
    Write-Host "  git commit --no-verify"
    Write-Host ""
    exit 1
}

$csCount = if ($STAGED_CS_FILES) { ($STAGED_CS_FILES | Measure-Object).Count } else { 0 }
$swiftCount = if ($STAGED_SWIFT_FILES) { ($STAGED_SWIFT_FILES | Measure-Object).Count } else { 0 }
$ktCount = if ($STAGED_KT_FILES) { ($STAGED_KT_FILES | Measure-Object).Count } else { 0 }
$mdCount = if ($STAGED_MD_FILES) { ($STAGED_MD_FILES | Measure-Object).Count } else { 0 }
Write-Host "OK: Staged file checks passed (Markdown: $mdCount, C#: $csCount, Swift: $swiftCount, Kotlin: $ktCount)" -ForegroundColor Green
exit 0
