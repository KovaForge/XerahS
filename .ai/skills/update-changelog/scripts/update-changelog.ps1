param(
    [string]$Version,
    [string]$FromTag,
    [string]$ChangelogPath = "docs/CHANGELOG.md",
    [switch]$Apply,
    [switch]$IncludeMerges,
    [string]$OutputPath,
    [switch]$NoConsolidation
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

function Get-ConsolidationBucket {
    param(
        [string]$Subject,
        [string]$Category,
        [string]$Component
    )

    if ($Subject -match '(?i)ShareX\.ImageEditor') {
        return @{
            GroupKey = "$Category|$Component|__consolidate_sharex_imageeditor__"
            Summary  = "ShareX.ImageEditor submodule updates"
        }
    }

    if ($Category -eq 'Documentation') {
        if ($Subject -match '(?i)(Add|Update)\s+2026-\d{2}-\d{2}.*blog') {
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

function Build-ChangelogSection([string]$Version, [object[]]$CommitRows, [bool]$ConsolidateSimilar) {
    $grouped = @{}
    foreach ($row in $CommitRows) {
        if ($row.Subject -match '^\[v\d+\.\d+\.\d+\]\s+\[CI\]\s+Release\s+v\d+\.\d+\.\d+$') {
            continue
        }

        $parsed = Categorize-Commit $row.Subject
        $description = $parsed.Description
        $key = $null

        if ($ConsolidateSimilar) {
            $bucket = Get-ConsolidationBucket -Subject $row.Subject -Category $parsed.Category -Component $parsed.Component
            if ($null -ne $bucket) {
                $key = $bucket.GroupKey
                $description = $bucket.Summary
            }
        }

        if ($null -eq $key) {
            $key = "{0}|{1}|{2}" -f $parsed.Category, $parsed.Component, $parsed.Description
        }

        if (-not $grouped.ContainsKey($key)) {
            $grouped[$key] = [pscustomobject]@{
                Category = $parsed.Category
                Component = $parsed.Component
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
    $lines.Add("## v$Version")
    $lines.Add("")

    foreach ($category in $categoryOrder) {
        if (-not $byCategory.ContainsKey($category)) {
            continue
        }

        $entries = @($byCategory[$category] | Sort-Object Component, Description)
        if ($entries.Count -eq 0) {
            continue
        }

        $lines.Add("### $category")
        foreach ($entry in $entries) {
            $hashes = ($entry.Hashes | Sort-Object) -join ", "
            $lines.Add("- **$($entry.Component)**: $($entry.Description) `($hashes`)")
        }
        $lines.Add("")
    }

    if ($lines.Count -eq 2) {
        $lines.Add("### Changed")
        $lines.Add("- No user-facing commits were detected in this range.")
        $lines.Add("")
    }

    return ($lines -join "`n").TrimEnd() + "`n"
}

function Upsert-ChangelogSection([string]$Content, [string]$Version, [string]$Section) {
    $escapedVersion = [regex]::Escape($Version)
    $existingPattern = "(?ms)^## v$escapedVersion\s*$.*?(?=^## v\d+\.\d+\.\d+\s*$|\z)"
    if ([regex]::IsMatch($Content, $existingPattern)) {
        return [regex]::Replace($Content, $existingPattern, $Section.TrimEnd() + "`n")
    }

    $unreleasedMatch = [regex]::Match($Content, '(?m)^## Unreleased\s*$')
    if ($unreleasedMatch.Success) {
        $insertIndex = $unreleasedMatch.Index + $unreleasedMatch.Length
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
$section = Build-ChangelogSection -Version $resolvedVersion -CommitRows $commits -ConsolidateSimilar:$consolidate

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
    $outText = $section
    $outText = $outText -replace [char]0x00C2 + [char]0x00A7, [char]0x2014
    $outText = $outText -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7
    $outText = $outText -replace "\r?\n", "`n"
    $outText = $outText -replace "`n{3,}", "`n`n"
    $outText = $outText -replace "`n", "`r`n"
    [System.IO.File]::WriteAllText($resolvedOutput, $outText, [System.Text.UTF8Encoding]::new($false))
}

if ($Apply) {
    $resolvedChangelog = if ([System.IO.Path]::IsPathRooted($ChangelogPath)) { $ChangelogPath } else { Join-Path $repoRoot $ChangelogPath }
    if (-not (Test-Path $resolvedChangelog)) {
        throw "Changelog file not found: $resolvedChangelog"
    }

    $existing = Get-Content -Path $resolvedChangelog -Raw
    $updated = Upsert-ChangelogSection -Content $existing -Version $resolvedVersion -Section $section

    # Fix double-encoded em-dash (C2 A7 mojibake → U+2014) and section-sign artifacts
    $updated = $updated -replace [char]0x00C2 + [char]0x00A7, [char]0x2014
    $updated = $updated -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7

    # Collapse 3+ consecutive blank lines
    $updated = $updated -replace "\r?\n", "`n"
    $updated = $updated -replace "`n{3,}", "`n`n"
    $updated = $updated -replace "`n", "`r`n"

    [System.IO.File]::WriteAllText($resolvedChangelog, $updated, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Target version : v$resolvedVersion"
$fromTagLabel = if ([string]::IsNullOrWhiteSpace($resolvedFromTag)) { "(none)" } else { $resolvedFromTag }
Write-Host "From tag       : $fromTagLabel"
Write-Host "Commits parsed : $($commits.Count)"
Write-Host "Consolidation  : $(if ($consolidate) { 'on (similar commits merged)' } else { 'off (-NoConsolidation)' })"
if ($Apply) {
    Write-Host "Applied to     : $ChangelogPath"
}
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Host "Draft output   : $OutputPath"
}
Write-Host ""
Write-Output $section
