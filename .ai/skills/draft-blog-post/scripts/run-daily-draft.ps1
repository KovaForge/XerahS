<#
.SYNOPSIS
  Ensures today's blog draft exists. For use by cron / Task Scheduler.
.DESCRIPTION
  Changes to the XerahS repo root (derived from this script's path), then runs
  upsert-blog-draft.ps1 with no -Date so the draft for the current UTC+8 day
  is created if missing. Does not populate content; run the full skill in Cursor
  to fill Summary, Features, Fixes, etc. from git history.
.EXAMPLE
  .\run-daily-draft.ps1
  # Or from anywhere: powershell -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1"
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..\..\..")).Path

Push-Location $RepoRoot
try {
    $UpsertScript = Join-Path $ScriptDir "upsert-blog-draft.ps1"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $UpsertScript
    Write-Output "Daily draft upsert completed at $(Get-Date -Format 'o')"
}
finally {
    Pop-Location
}
