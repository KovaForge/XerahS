param(
    [string]$Version,
    [string]$FromTag,
    [string]$ChangelogPath = "docs/CHANGELOG.md",
    [switch]$Apply,
    [switch]$IncludeMerges,
    [string]$OutputPath,
    [switch]$NoConsolidation,
    [switch]$IncludeHashes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-GitRepository {
    $repoRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
        throw "Not inside a git repository."
    }

    return $repoRoot.Trim()
}

function Resolve-Version([string]$RepoRoot, [string]$RequestedVersion) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        if ($RequestedVersion -notmatch '^\d+\.\d+\.\d+$') {
            throw "Version '$RequestedVersion' is invalid. Expected X.Y.Z."
        }

        return $RequestedVersion
    }

    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        throw "Directory.Build.props not found at repository root."
    }

    $match = Select-String -Path $propsPath -Pattern '<Version>\s*(?<v>\d+\.\d+\.\d+)\s*</Version>' | Select-Object -First 1
    if ($null -eq $match) {
        throw "Could not resolve <Version> from Directory.Build.props."
    }

    return $match.Matches[0].Groups["v"].Value
}

function Resolve-FromTag([string]$RequestedTag) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedTag)) {
        return $RequestedTag
    }

    $tag = git describe --tags --abbrev=0 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($tag)) {
        return $null
    }

    return $tag.Trim()
}

function Resolve-GitHubRepositoryUrl {
    $remote = git remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remote)) {
        return "https://github.com/ShareX/XerahS"
    }

    $remote = $remote.Trim()
    if ($remote -match '^git@github\.com:(?<repo>[^/]+/[^/]+?)(\.git)?$') {
        return "https://github.com/$($Matches["repo"])"
    }

    if ($remote -match '^https://github\.com/(?<repo>[^/]+/[^/]+?)(\.git)?$') {
        return "https://github.com/$($Matches["repo"])"
    }

    return "https://github.com/ShareX/XerahS"
}

function Resolve-TagUrl([string]$Version) {
    $repoUrl = Resolve-GitHubRepositoryUrl
    return "$repoUrl/releases/tag/v$Version"
}

function Test-ReleaseTagExists([string]$Version) {
    $tagName = "v$Version"

    git show-ref --verify --quiet "refs/tags/$tagName"
    if ($LASTEXITCODE -eq 0) {
        return $true
    }

    git ls-remote --exit-code --tags origin "refs/tags/$tagName" *> $null
    return $LASTEXITCODE -eq 0
}

function Resolve-VersionHeading([string]$Version) {
    if (Test-ReleaseTagExists -Version $Version) {
        $tagUrl = Resolve-TagUrl -Version $Version
        return "## [v$Version]($tagUrl)"
    }

    return "## v$Version"
}

function Normalize-ChangelogText([string]$Text) {
    $arrowBad = [string][char]0x00E2 + [char]0x2020 + [char]0x2019
    $emDashBad = [string][char]0x00E2 + [char]0x20AC + [char]0x201D
    $enDashBad = [string][char]0x00E2 + [char]0x20AC + [char]0x201C
    $sectionBad = [string][char]0x00C2 + [char]0x00A7

    $Text = $Text.Replace($arrowBad, [string][char]0x2192)
    $Text = $Text.Replace($emDashBad, [string][char]0x2014)
    $Text = $Text.Replace($enDashBad, [string][char]0x2013)
    $Text = $Text.Replace($sectionBad, [string][char]0x00A7)
    $Text = $Text -replace "\r?\n", "`n"
    $Text = $Text -replace "`n{3,}", "`n`n"
    return $Text -replace "`n", "`r`n"
}

function Normalize-Component([string]$RawComponent) {
    $component = $RawComponent.Trim()
    if ([string]::IsNullOrWhiteSpace($component)) {
        return "Core"
    }

    $component = $component -replace '[_-]+', ' '
    $component = $component -replace '\s+', ' '
    $words = $component.Split(' ')
    for ($i = 0; $i -lt $words.Length; $i++) {
        if ($words[$i].Length -gt 0) {
            $words[$i] = $words[$i].Substring(0, 1).ToUpperInvariant() + $words[$i].Substring(1)
        }
    }

    return ($words -join ' ')
}

function Categorize-Commit([string]$Subject) {
    $versionedMatch = [regex]::Match($Subject, '^\[v\d+\.\d+\.\d+\]\s+\[(?<type>[^\]]+)\]\s+(?<desc>.+)$')
    if ($versionedMatch.Success) {
        $type = $versionedMatch.Groups["type"].Value.Trim().ToLowerInvariant()
        $desc = $versionedMatch.Groups["desc"].Value.Trim()
        $component = "Core"

        $descPrefixMatch = [regex]::Match($desc, '^(?<component>[A-Za-z0-9\/ .&+\-]+):\s*(?<rest>.+)$')
        if ($descPrefixMatch.Success) {
            $component = Normalize-Component $descPrefixMatch.Groups["component"].Value
            $desc = $descPrefixMatch.Groups["rest"].Value.Trim()
        }

        return @{
            Category = switch ($type) {
                "feat" { "Features"; break }
                "feature" { "Features"; break }
                "fix" { "Fixes"; break }
                "refactor" { "Refactor"; break }
                "build" { "Build"; break }
                "ci" { "Build"; break }
                "chore" { "Build"; break }
                "infra" { "Build"; break }
                "infrastructure" { "Build"; break }
                "docs" { "Documentation"; break }
                "doc" { "Documentation"; break }
                "test" { "Testing"; break }
                "testing" { "Testing"; break }
                "perf" { "Performance"; break }
                "performance" { "Performance"; break }
                default { "Changed" }
            }
            Component = $component
            Description = $desc
        }
    }

    $conventionalMatch = [regex]::Match($Subject, '^(?<type>[a-zA-Z]+)(\((?<scope>[^)]+)\))?(!)?:\s*(?<desc>.+)$')
    if ($conventionalMatch.Success) {
        $type = $conventionalMatch.Groups["type"].Value.ToLowerInvariant()
        $scope = $conventionalMatch.Groups["scope"].Value
        $desc = $conventionalMatch.Groups["desc"].Value.Trim()

        return @{
            Category = switch ($type) {
                "feat" { "Features"; break }
                "feature" { "Features"; break }
                "fix" { "Fixes"; break }
                "refactor" { "Refactor"; break }
                "build" { "Build"; break }
                "ci" { "Build"; break }
                "chore" { "Build"; break }
                "docs" { "Documentation"; break }
                "doc" { "Documentation"; break }
                "test" { "Testing"; break }
                "tests" { "Testing"; break }
                "perf" { "Performance"; break }
                "performance" { "Performance"; break }
                default { "Changed" }
            }
            Component = if ([string]::IsNullOrWhiteSpace($scope)) { "Core" } else { Normalize-Component $scope }
            Description = $desc
        }
    }

    $prefixMatch = [regex]::Match($Subject, '^(?<component>[A-Za-z0-9\/ .&+\-]+):\s*(?<desc>.+)$')
    if ($prefixMatch.Success) {
        $component = Normalize-Component $prefixMatch.Groups["component"].Value
        $desc = $prefixMatch.Groups["desc"].Value.Trim()
        return @{
            Category = "Changed"
            Component = $component
            Description = $desc
        }
    }

    return @{
        Category = "Changed"
        Component = "Core"
        Description = $Subject.Trim()
    }
}

function Test-IsNoiseCommit([string]$Subject) {
    if ($Subject -match '^\[v\d+\.\d+\.\d+\]\s+\[CI\]\s+Release\s+v\d+\.\d+\.\d+$') {
        return $true
    }

    if ($Subject -match '(?i)^\[v[\d.]+\]\s+\[(Docs|Meta|CI)\]\s+(Bump version|Update hourly review|Record .+ in tracker|hourly|clawpatch|xerahs-review|sync tracker)') {
        return $true
    }

    if ($Subject -match '(?i)(hourly review (tracker|state)|clawpatch (ingest|report)|tracker:\s|xerahs-review:|update hourly_review_state|queue \d+ clawpatch|\[hourly\]|pre-commit:.*state JSON|XIP\d{4} state JSON)') {
        return $true
    }

    if ($Subject -match '(?i)^\[?v?[\d.]+\]?\s*(Bump version to|tracker:)') {
        return $true
    }

    return $false
}

function Get-PlatformXipLabel([string]$Subject) {
    if ($Subject -notmatch '(?i)XIP(\d{4})') {
        return $null
    }

    $xipNum = $Matches[1]
    $xip = "XIP$xipNum"
    $priority = if ($Subject -match '(?i)\bP(\d+)\b') { " P$($Matches[1])" } else { '' }

    $platform = switch -Regex ($Subject) {
        '(?i)macOS|MacOS|Carbon|ScreenCaptureKit|Info\.plist|codesign|notarize|CGWindowList|sck_capture' { 'macOS'; break }
        '(?i)Linux|Wayland|wl-copy|xclip|portal|notify-send|rpm|\.deb' { 'Linux'; break }
        default {
            switch ($xipNum) {
                '0078' { 'macOS' }
                '0079' { 'Linux' }
                default { $null }
            }
        }
    }

    if ($null -eq $platform) {
        return $null
    }

    $topic = switch -Regex ($Subject) {
        '(?i)hotkey' { 'Hotkeys'; break }
        '(?i)notification|notify-send' { 'Notifications'; break }
        '(?i)clipboard|wl-copy|xclip' { 'Clipboard'; break }
        '(?i)monitor|mixed-DPI|DPI|normalizer' { 'Mixed-DPI'; break }
        '(?i)Info\.plist|bundle' { 'App bundle'; break }
        '(?i)permission|Screen Recording' { 'Permissions'; break }
        '(?i)window|CGWindowList|sck_capture' { 'Window capture'; break }
        '(?i)ScreenCaptureKit' { 'ScreenCaptureKit'; break }
        '(?i)codesign|notarize|DMG|package-mac' { 'Packaging'; break }
        '(?i)INSTALL|KNOWN_ISSUES|documentation|docs' { 'Documentation'; break }
        default { 'Platform' }
    }

    return "$platform — $topic ($xip$priority)"
}

function Get-ConsolidationBucket {
    param(
        [string]$Subject,
        [string]$Category,
        [string]$Component
    )

    if ($Subject -match '(?i)(pipe-drain|pipe-fill|stderr).*(deadlock|timeout)') {
        $platform = if ($Subject -match '(?i)Linux|Wayland|wl-|xclip|grim|slurp|gsettings|xdotool|xrandr|PulseAudio') { 'Linux' }
                    elseif ($Subject -match '(?i)macOS|MacOS|osascript|pbpaste|pbcopy') { 'macOS' }
                    else { $Component }
        return @{
            GroupKey = "$Category|$platform|__consolidate_pipe_drain__"
            Summary  = "$platform service helpers: drain stderr and bound subprocess waits to prevent pipe-fill deadlocks"
            ComponentOverride = $platform
        }
    }

    $xipLabel = Get-PlatformXipLabel -Subject $Subject
    if ($null -ne $xipLabel) {
        $cleanDesc = $Subject -replace '^\[v\d+\.\d+\.\d+\]\s+\[[^\]]+\]\s+', ''
        $cleanDesc = $cleanDesc -replace '(?i)^XIP\d{4}\s+P\d+:\s*', ''
        return @{
            GroupKey = "$Category|$xipLabel|__xip_platform__"
            Summary  = $cleanDesc.Trim()
            ComponentOverride = $xipLabel
        }
    }

    if ($Subject -match '(?i)ShareX\.ImageEditor') {
        return @{
            GroupKey = "$Category|$Component|__consolidate_sharex_imageeditor__"
            Summary  = "ShareX.ImageEditor submodule updates"
        }
    }

    if ($Category -eq 'Documentation') {
        if ($Subject -match '(?i)(Add|Update|Refresh)\s+2026-\d{2}-\d{2}.*blog') {
            return @{
                GroupKey = "Documentation|__blog_series__|__consolidate_blog_drafts__"
                Summary  = "Blog drafts (2026 series, add/update)"
            }
        }
        if ($Subject -match '(?i)\b(XIP\d+|IEIP\d+)') {
            return @{
                GroupKey = "Documentation|__xip_ieip__|__consolidate_xip_ieip_docs__"
                Summary  = "XIP/IEIP proposals and related documentation"
            }
        }
        if ($Component -eq 'Linux') {
            return @{
                GroupKey = "Documentation|Linux|__consolidate_linux_docs__"
                Summary  = "Linux install and capture documentation"
            }
        }
    }

    if ($Category -eq 'Changed' -and $Subject -match '(?i)(Create|Update)\s+(IEIP|XIP)\d+|(IEIP|XIP)\d+[^\n]*\.md\b') {
        return @{
            GroupKey = "Changed|__ieip_xip_md__|__consolidate_proposal_md__"
            Summary  = "IEIP/XIP proposal documents (create/update)"
        }
    }

    if (($Category -eq 'Changed' -or $Category -eq 'Features') -and $Subject -match '(?i)multipart(\s+upload)?|S3\s+multipart') {
        return @{
            GroupKey = "$Category|$Component|__consolidate_multipart__"
            Summary  = "Multipart upload support (S3, abstractions, coverage)"
        }
    }

    return $null
}

function Get-CommitRows([string]$FromTag, [bool]$IncludeMerges) {
    $range = if ([string]::IsNullOrWhiteSpace($FromTag)) { "HEAD" } else { "$FromTag..HEAD" }
    $logArgs = @("log", $range, "--pretty=format:%h%x1f%s%x1f%an")
    if (-not $IncludeMerges) {
        $logArgs += "--no-merges"
    }

    $raw = git @logArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read commits from git log."
    }

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    $rows = @()
    $lines = $raw -split "`r?`n"
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line.Split([char]0x1f)
        if ($parts.Length -lt 3) {
            continue
        }

        $rows += [pscustomobject]@{
            Hash = $parts[0].Trim()
            Subject = $parts[1].Trim()
            Author = $parts[2].Trim()
        }
    }

    return $rows
}

function Merge-EntriesByComponent {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IEnumerable]$Entries
    )

    $merged = @{}
    foreach ($entry in $Entries) {
        $component = $entry.Component
        if (-not $merged.ContainsKey($component)) {
            $merged[$component] = [pscustomobject]@{
                Category = $entry.Category
                Component = $component
                Descriptions = New-Object System.Collections.Generic.List[string]
                Hashes = New-Object System.Collections.Generic.List[string]
            }
        }

        $desc = $entry.Description.Trim()
        if (-not [string]::IsNullOrWhiteSpace($desc) -and -not $merged[$component].Descriptions.Contains($desc)) {
            $merged[$component].Descriptions.Add($desc)
        }

        foreach ($hash in $entry.Hashes) {
            if (-not $merged[$component].Hashes.Contains($hash)) {
                $merged[$component].Hashes.Add($hash)
            }
        }
    }

    $result = @()
    foreach ($item in $merged.Values) {
        $text = if ($item.Descriptions.Count -le 1) {
            $item.Descriptions[0]
        }
        elseif ($item.Descriptions.Count -le 3) {
            ($item.Descriptions | Sort-Object) -join '; '
        }
        else {
            (($item.Descriptions | Sort-Object | Select-Object -First 2) -join '; ') + '; and related changes'
        }

        $result += [pscustomobject]@{
            Category = $item.Category
            Component = $item.Component
            Description = $text
            Hashes = $item.Hashes
        }
    }

    return $result
}

function Build-ChangelogSection([string]$Version, [object[]]$CommitRows, [bool]$ConsolidateSimilar, [bool]$EmitHashes) {
    $grouped = @{}
    foreach ($row in $CommitRows) {
        if (Test-IsNoiseCommit -Subject $row.Subject) {
            continue
        }

        $parsed = Categorize-Commit $row.Subject
        $description = $parsed.Description
        $component = $parsed.Component
        $key = $null

        if ($ConsolidateSimilar) {
            $bucket = Get-ConsolidationBucket -Subject $row.Subject -Category $parsed.Category -Component $parsed.Component
            if ($null -ne $bucket) {
                $key = $bucket.GroupKey
                $description = $bucket.Summary
                if ($bucket.ContainsKey('ComponentOverride') -and -not [string]::IsNullOrWhiteSpace($bucket.ComponentOverride)) {
                    $component = $bucket.ComponentOverride
                }
            }
        }

        if ($null -eq $key) {
            $key = "{0}|{1}|{2}" -f $parsed.Category, $component, $parsed.Description
        }

        if (-not $grouped.ContainsKey($key)) {
            $grouped[$key] = [pscustomobject]@{
                Category = $parsed.Category
                Component = $component
                Description = $description
                Hashes = New-Object System.Collections.Generic.List[string]
            }
        }

        if (-not $grouped[$key].Hashes.Contains($row.Hash)) {
            $grouped[$key].Hashes.Add($row.Hash)
        }
    }

    $categoryOrder = @("Features", "Fixes", "Refactor", "Build", "Documentation", "Testing", "Performance", "Changed")
    $byCategory = @{}
    foreach ($entry in $grouped.Values) {
        if (-not $byCategory.ContainsKey($entry.Category)) {
            $byCategory[$entry.Category] = New-Object System.Collections.Generic.List[object]
        }

        $byCategory[$entry.Category].Add($entry)
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add((Resolve-VersionHeading -Version $Version))
    $lines.Add("")

    foreach ($category in $categoryOrder) {
        if (-not $byCategory.ContainsKey($category)) {
            continue
        }

        $entries = @(Merge-EntriesByComponent -Entries $byCategory[$category] | Sort-Object Component, Description)
        if ($entries.Count -eq 0) {
            continue
        }

        $lines.Add("### $category")
        foreach ($entry in $entries) {
            if ($EmitHashes) {
                $hashes = ($entry.Hashes | Sort-Object) -join ", "
                $lines.Add("- **$($entry.Component)**: $($entry.Description) `($hashes`)")
            }
            else {
                $lines.Add("- **$($entry.Component)**: $($entry.Description)")
            }
        }
        $lines.Add("")
    }

    if ($lines.Count -eq 2) {
        $lines.Add("### Changed")
        $lines.Add("- No user-facing commits were detected in this range.")
        $lines.Add("")
    }

    $lines.Add("---")
    $lines.Add("")

    return ($lines -join "`n").TrimEnd() + "`n"
}

function Ensure-ChangelogPreamble([string]$Content) {
    if ($Content -match '(?ms)^#\s*Changelog\s*$') {
        return $Content
    }

    $preamble = @"
# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

---

"@

    return $preamble + $Content.TrimStart()
}

function Upsert-ChangelogSection([string]$Content, [string]$Version, [string]$Section) {
    $escapedVersion = [regex]::Escape($Version)
    $currentHeading = "##\s+(?:v$escapedVersion|\[v$escapedVersion\]\([^)]+\))"
    $anyVersionHeading = "##\s+(?:v\d+\.\d+\.\d+(?:\s+-[^\r\n]*)?|\[v\d+\.\d+\.\d+\]\([^)]+\)(?:\s+-[^\r\n]*)?)"
    $existingPattern = "(?ms)^$currentHeading\s*$.*?(?=^$anyVersionHeading\s*$|\z)"
    if ([regex]::IsMatch($Content, $existingPattern)) {
        return [regex]::Replace($Content, $existingPattern, $Section.TrimEnd() + "`n`n")
    }

    $unreleasedMatch = [regex]::Match($Content, '(?m)^## Unreleased\s*$')
    if ($unreleasedMatch.Success) {
        $insertIndex = $unreleasedMatch.Index + $unreleasedMatch.Length
        $insertion = "`n`n" + $Section.TrimEnd() + "`n"
        return $Content.Insert($insertIndex, $insertion)
    }

    $preambleBreak = [regex]::Match($Content, '(?m)^---\s*$')
    if ($preambleBreak.Success) {
        $insertIndex = $preambleBreak.Index + $preambleBreak.Length
        $insertion = "`n`n" + $Section.TrimEnd() + "`n"
        return $Content.Insert($insertIndex, $insertion)
    }

    return $Section.TrimEnd() + "`n`n" + $Content
}

$repoRoot = Require-GitRepository
Set-Location $repoRoot

$resolvedVersion = Resolve-Version -RepoRoot $repoRoot -RequestedVersion $Version
$resolvedFromTag = Resolve-FromTag -RequestedTag $FromTag
$commits = @(Get-CommitRows -FromTag $resolvedFromTag -IncludeMerges:$IncludeMerges)
$consolidate = -not $NoConsolidation
$section = Build-ChangelogSection -Version $resolvedVersion -CommitRows $commits -ConsolidateSimilar:$consolidate -EmitHashes:$IncludeHashes

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
    $outText = Normalize-ChangelogText -Text $section
    [System.IO.File]::WriteAllText($resolvedOutput, $outText, [System.Text.UTF8Encoding]::new($false))
}

if ($Apply) {
    $resolvedChangelog = if ([System.IO.Path]::IsPathRooted($ChangelogPath)) { $ChangelogPath } else { Join-Path $repoRoot $ChangelogPath }
    if (-not (Test-Path $resolvedChangelog)) {
        throw "Changelog file not found: $resolvedChangelog"
    }

    $existing = Get-Content -Path $resolvedChangelog -Raw
    $existing = Ensure-ChangelogPreamble -Content $existing
    $updated = Upsert-ChangelogSection -Content $existing -Version $resolvedVersion -Section $section

    $updated = Normalize-ChangelogText -Text $updated

    [System.IO.File]::WriteAllText($resolvedChangelog, $updated, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Target version : v$resolvedVersion"
$fromTagLabel = if ([string]::IsNullOrWhiteSpace($resolvedFromTag)) { "(none)" } else { $resolvedFromTag }
Write-Host "From tag       : $fromTagLabel"
Write-Host "Commits parsed : $($commits.Count)"
Write-Host "Consolidation  : $(if ($consolidate) { 'on (similar commits merged)' } else { 'off (-NoConsolidation)' })"
Write-Host "Hash output    : $(if ($IncludeHashes) { 'on (-IncludeHashes)' } else { 'off (default)' })"
if ($Apply) {
    Write-Host "Applied to     : $ChangelogPath"
}
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Host "Draft output   : $OutputPath"
}
Write-Host ""
Write-Output $section
Write-Host ""
Write-Host "Rewrite pass: compress categories, merge trivial patch versions, polish platform/XIP bullets before publishing." -ForegroundColor DarkYellow
