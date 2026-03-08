$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Write-Host $Description
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit $LASTEXITCODE): git $($Arguments -join ' ')"
    }
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (!(Test-Path $Path)) {
        throw $Message
    }
}

$repoRoot = Resolve-Path $PSScriptRoot
$gitModulesPath = Join-Path $repoRoot ".gitmodules"
$solutionPath = Join-Path $repoRoot "src\desktop\XerahS.sln"

Assert-PathExists -Path (Join-Path $repoRoot ".git") -Message "Run this script from the XerahS repository root."
Assert-PathExists -Path $gitModulesPath -Message "Missing .gitmodules at: $gitModulesPath"
Assert-PathExists -Path $solutionPath -Message "Missing solution file at: $solutionPath"

$null = Get-Command git -ErrorAction Stop

Push-Location $repoRoot
try {
    $submoduleConfigLines = @(& git config --file .gitmodules --get-regexp '^submodule\..*\.path$')
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read submodule paths from .gitmodules."
    }

    $submodulePaths = @()
    foreach ($line in $submoduleConfigLines) {
        if ($line -match '^submodule\..*\.path\s+(?<Path>.+)$') {
            $submodulePaths += $Matches["Path"]
        }
    }

    if ($submodulePaths.Count -eq 0) {
        throw "No submodule paths were found in .gitmodules."
    }

    Write-Host "Initializing submodules required by src\desktop\XerahS.sln..."
    foreach ($submodulePath in $submodulePaths) {
        Write-Host "  - $submodulePath"
    }

    Invoke-Git -Description "`nSynchronizing submodule URLs..." -Arguments @(
        "submodule", "sync", "--recursive"
    )

    Invoke-Git -Description "`nCloning and checking out the pinned submodule commits..." -Arguments @(
        "submodule", "update", "--init", "--recursive"
    )

    $requiredProjects = @(
        @{
            Name = "ShareX.ImageEditor"
            Path = Join-Path $repoRoot "ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj"
        },
        @{
            Name = "ShareX.VideoEditor"
            Path = Join-Path $repoRoot "ShareX.VideoEditor\ShareX.VideoEditor\ShareX.VideoEditor.csproj"
        }
    )

    foreach ($requiredProject in $requiredProjects) {
        Assert-PathExists -Path $requiredProject.Path -Message "Required project for XerahS.sln is missing after submodule initialization: $($requiredProject.Name)"
    }

    Write-Host "`nSubmodule status:"
    & git submodule status --recursive
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read submodule status."
    }

    Write-Host "`nSubmodules are ready for src\desktop\XerahS.sln."
    Write-Host "Build with: dotnet build src\desktop\XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false"
} finally {
    Pop-Location
}
