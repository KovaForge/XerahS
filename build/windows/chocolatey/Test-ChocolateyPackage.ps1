[CmdletBinding()]
param(
    [string]$Version,
    [string]$PackageId = 'xerahs',
    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
} else {
    $scriptRoot = $PSScriptRoot
}

$packageRoot = (Resolve-Path $scriptRoot).Path
$repoRoot = (Resolve-Path (Join-Path $packageRoot '..\..\..')).Path
$propsPath = Join-Path $repoRoot 'Directory.Build.props'

function Get-RepositoryVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $props = [xml](Get-Content -Path $Path)
    $versionNode = $props.SelectSingleNode('//Version')
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Could not resolve <Version> from $Path."
    }

    return $versionNode.InnerText.Trim()
}

function Get-InstalledPackageLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    $output = & choco list --local-only --exact $Id --limit-output
    if ($LASTEXITCODE -ne 0) {
        throw "choco list failed with exit code $LASTEXITCODE."
    }

    $pattern = '^' + [Regex]::Escape($Id) + '\|'
    return $output | Where-Object { $_ -match $pattern } | Select-Object -First 1
}

if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    throw "Chocolatey CLI (choco) is required to smoke test the package."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-RepositoryVersion -Path $propsPath
}

if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repoRoot 'dist\chocolatey'
}

$resolvedSourceDirectory = (Resolve-Path $SourceDirectory).Path
$packagePath = Join-Path $resolvedSourceDirectory "$PackageId.$Version.nupkg"

if (-not (Test-Path $packagePath)) {
    throw "Chocolatey package was not found: $packagePath"
}

$packageWasInstalled = $false

try {
    $existingPackage = Get-InstalledPackageLine -Id $PackageId
    if (-not [string]::IsNullOrWhiteSpace($existingPackage)) {
        Write-Host "Removing existing local package registration for $PackageId before smoke test."
        & choco uninstall $PackageId -y --no-progress
        if ($LASTEXITCODE -ne 0) {
            throw "choco uninstall failed with exit code $LASTEXITCODE while cleaning existing package state."
        }
    }

    Write-Host "Installing $PackageId $Version from $resolvedSourceDirectory"
    & choco install $PackageId --version $Version --source $resolvedSourceDirectory -y --force --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "choco install failed with exit code $LASTEXITCODE."
    }

    $packageWasInstalled = $true

    $installedPackage = Get-InstalledPackageLine -Id $PackageId
    if ([string]::IsNullOrWhiteSpace($installedPackage)) {
        throw "Chocolatey did not report $PackageId as installed after the install step."
    }

    Write-Host "Uninstalling $PackageId"
    & choco uninstall $PackageId -y --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "choco uninstall failed with exit code $LASTEXITCODE."
    }

    $packageWasInstalled = $false

    $remainingPackage = Get-InstalledPackageLine -Id $PackageId
    if (-not [string]::IsNullOrWhiteSpace($remainingPackage)) {
        throw "Chocolatey still reports $PackageId as installed after uninstall."
    }

    Write-Host "Chocolatey smoke test passed for $PackageId $Version"
} finally {
    if ($packageWasInstalled) {
        & choco uninstall $PackageId -y --no-progress | Out-Null
    }
}
