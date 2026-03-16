[CmdletBinding()]
param(
    [string]$BlogRoot = "docs/blog",
    [string]$Date,
    [ValidateRange(-12, 14)]
    [int]$UtcOffsetHours = 8,
    [ValidateSet("Features", "Fixes", "Build and Tooling", "Commits Reviewed", "Notes")]
    [string]$Section,
    [string]$Bullet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-TargetDate {
    param(
        [string]$InputDate,
        [int]$OffsetHours
    )

    if ([string]::IsNullOrWhiteSpace($InputDate)) {
        return [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours($OffsetHours)).Date
    }

    $formats = @("yyyy-MM-dd", "yyyyMMdd")

    foreach ($format in $formats) {
        try {
            return [DateTime]::ParseExact(
                $InputDate,
                $format,
                [System.Globalization.CultureInfo]::InvariantCulture).Date
        }
        catch [System.FormatException] {
        }
    }

    throw "Date must use yyyy-MM-dd or yyyyMMdd."
}

function New-TemplateContent {
    param(
        [DateTime]$TargetDate,
        [int]$OffsetHours
    )

    $dateText = $TargetDate.ToString("yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
    $offsetText = if ($OffsetHours -ge 0) {
        "UTC+$OffsetHours"
    }
    else {
        "UTC$OffsetHours"
    }

    return @(
        "# XerahS Daily Blog Draft - $dateText",
        "",
        "Date: $dateText",
        "Time Zone: $offsetText",
        "Status: Draft",
        "",
        "## Summary",
        "",
        "TBD",
        "",
        "## Features",
        "",
        "- TBD",
        "",
        "## Fixes",
        "",
        "- TBD",
        "",
        "## Build and Tooling",
        "",
        "- TBD",
        "",
        "## Commits Reviewed",
        "",
        "- TBD",
        "",
        "## Notes",
        "",
        "- TBD",
        ""
    )
}

function Add-SectionBullet {
    param(
        [string]$Path,
        [string]$TargetSection,
        [string]$SectionBullet
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content -Path $Path) {
        [void]$lines.Add($line)
    }

    $heading = "## $TargetSection"
    $headingIndex = -1

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -eq $heading) {
            $headingIndex = $i
            break
        }
    }

    if ($headingIndex -lt 0) {
        throw "Section '$TargetSection' was not found in $Path."
    }

    $endIndex = $lines.Count
    for ($i = $headingIndex + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith("## ")) {
            $endIndex = $i
            break
        }
    }

    $normalizedBullet = "- $($SectionBullet.Trim())"

    for ($i = $headingIndex + 1; $i -lt $endIndex; $i++) {
        if ($lines[$i].Trim() -eq $normalizedBullet) {
            return
        }
    }

    for ($i = $endIndex - 1; $i -gt $headingIndex; $i--) {
        $trimmedLine = $lines[$i].Trim()
        if ($trimmedLine -eq "- TBD" -or $trimmedLine -eq "TBD") {
            $lines.RemoveAt($i)
            $endIndex--
        }
    }

    $insertIndex = $headingIndex + 1
    if ($insertIndex -ge $lines.Count -or $lines[$insertIndex] -ne "") {
        $lines.Insert($insertIndex, "")
        $insertIndex++
        $endIndex++
    }
    else {
        $insertIndex++
    }

    while ($insertIndex -lt $endIndex -and $lines[$insertIndex].StartsWith("- ")) {
        $insertIndex++
    }

    if ($insertIndex -lt $lines.Count -and $lines[$insertIndex] -ne "") {
        $lines.Insert($insertIndex, "")
        $endIndex++
    }

    $lines.Insert($insertIndex, $normalizedBullet)

    [System.IO.File]::WriteAllLines($Path, $lines)
}

$targetDate = Get-TargetDate -InputDate $Date -OffsetHours $UtcOffsetHours
$yearFolder = $targetDate.ToString("yyyy", [System.Globalization.CultureInfo]::InvariantCulture)
$monthFolder = $targetDate.ToString("yyyy-MM", [System.Globalization.CultureInfo]::InvariantCulture)
$fileName = "blog-$($targetDate.ToString("yyyyMMdd", [System.Globalization.CultureInfo]::InvariantCulture)).md"
$directoryPath = Join-Path -Path $BlogRoot -ChildPath (Join-Path -Path $yearFolder -ChildPath $monthFolder)
$filePath = Join-Path -Path $directoryPath -ChildPath $fileName

[System.IO.Directory]::CreateDirectory($directoryPath) | Out-Null

if (-not [System.IO.File]::Exists($filePath)) {
    [System.IO.File]::WriteAllLines(
        $filePath,
        (New-TemplateContent -TargetDate $targetDate -OffsetHours $UtcOffsetHours))
}

if ([string]::IsNullOrWhiteSpace($Section) -xor [string]::IsNullOrWhiteSpace($Bullet)) {
    throw "Provide both -Section and -Bullet together."
}

if (-not [string]::IsNullOrWhiteSpace($Section)) {
    Add-SectionBullet -Path $filePath -TargetSection $Section -SectionBullet $Bullet
}

Write-Output $filePath
