<#
.SYNOPSIS
  Ensures the current blog draft exists, with an option to also upsert yesterday.
.DESCRIPTION
  Changes to the XerahS repo root (derived from this script's path), then runs
  upsert-blog-draft.ps1 for the current UTC+8 day. When -IncludePreviousDay is
  supplied, it also upserts the prior UTC+8 day. This script creates missing
  draft files only; it does not populate content from git history.
.EXAMPLE
  .\run-daily-draft.ps1
  # Or from anywhere: powershell -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1"
.EXAMPLE
  .\run-daily-draft.ps1 -IncludePreviousDay
#>
[CmdletBinding()]
param(
    [ValidateRange(-12, 14)]
    [int]$UtcOffsetHours = 8,
    [switch]$IncludePreviousDay
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..\..\..")).Path

Push-Location $RepoRoot
try {
    $UpsertScript = Join-Path $ScriptDir "upsert-blog-draft.ps1"
    $utcOffset = [TimeSpan]::FromHours($UtcOffsetHours)
    $nowInTargetZone = [DateTimeOffset]::UtcNow.ToOffset($utcOffset)
    $targetDates = [System.Collections.Generic.List[DateTime]]::new()

    if ($IncludePreviousDay) {
        [void]$targetDates.Add($nowInTargetZone.AddDays(-1).Date)
    }

    [void]$targetDates.Add($nowInTargetZone.Date)

    foreach ($targetDate in ($targetDates | Sort-Object -Unique)) {
        $dateText = $targetDate.ToString("yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $UpsertScript -Date $dateText -UtcOffsetHours $UtcOffsetHours
    }

    Write-Output "Daily draft upsert completed at $(Get-Date -Format 'o') for $($targetDates.Count) date(s)."
}
finally {
    Pop-Location
}
