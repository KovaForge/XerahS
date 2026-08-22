#region License Information (GPL v3)

<#
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
#>

#endregion License Information (GPL v3)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("FastCompile", "TargetedTests", "FullProductBuild", "FullVerification")]
    [string] $Lane,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string] $Project = "src/desktop/core/XerahS.Core/XerahS.Core.csproj",

    [string] $TestProject = "tests/XerahS.Tests/XerahS.Tests.csproj",

    [string] $TestFilter,

    [string] $ArtifactsPath,

    [switch] $NoRestore,

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$desktopSolution = "src/desktop/XerahS.sln"
$mainTestProject = "tests/XerahS.Tests/XerahS.Tests.csproj"
$buildTestProject = "tests/XerahS.Build.Tests/XerahS.Build.Tests.csproj"
$mcpTestProject = "src/tools/XerahS.McpServer.Tests/XerahS.McpServer.Tests.csproj"

function Resolve-RepositoryTarget
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $candidate = $Path
    if (![System.IO.Path]::IsPathRooted($candidate))
    {
        $candidate = Join-Path $repoRoot $candidate
    }

    $resolved = [System.IO.Path]::GetFullPath($candidate)
    $repoPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $insideRepository = $resolved.Equals($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolved.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)

    if (!$insideRepository)
    {
        throw "$Description must be inside the repository: $resolved"
    }

    if (!(Test-Path -LiteralPath $resolved -PathType Leaf))
    {
        throw "$Description does not exist: $resolved"
    }

    return $resolved
}

function Resolve-ArtifactOutputPath
{
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path))
    {
        return $null
    }

    $candidate = $Path
    if (![System.IO.Path]::IsPathRooted($candidate))
    {
        $candidate = Join-Path $repoRoot $candidate
    }

    return [System.IO.Path]::GetFullPath($candidate)
}

function Get-CommonArguments
{
    $arguments = @(
        "--configuration", $Configuration,
        "--nologo",
        "--verbosity", "minimal",
        "--disable-build-servers",
        "-m:1",
        "-p:nodeReuse=false",
        "-p:UseSharedCompilation=false",
        "-p:BuildInParallel=false"
    )

    if ($NoRestore)
    {
        $arguments += "--no-restore"
    }

    if ($script:resolvedArtifactsPath)
    {
        $arguments += @("--artifacts-path", $script:resolvedArtifactsPath)
    }

    return $arguments
}

function Invoke-DotNet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $displayArguments = $Arguments | ForEach-Object {
        if ($_ -match "\s")
        {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else
        {
            $_
        }
    }

    Write-Host ""
    Write-Host "[$Lane] $Description" -ForegroundColor Cyan
    Write-Host ("> dotnet " + ($displayArguments -join " "))

    if ($DryRun)
    {
        Write-Host "Dry run: command was not executed."
        return
    }

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

if (!$DryRun)
{
    Get-Command dotnet -ErrorAction Stop | Out-Null
}
$script:resolvedArtifactsPath = Resolve-ArtifactOutputPath $ArtifactsPath

if ($script:resolvedArtifactsPath)
{
    Write-Host "Artifacts path: $script:resolvedArtifactsPath"
    Write-Host "The lane does not delete or clean this directory."
}

Push-Location $repoRoot
try
{
    switch ($Lane)
    {
        "FastCompile"
        {
            $target = Resolve-RepositoryTarget $Project "Fast compile project"
            $arguments = @("build", $target, "-p:AssembleProduct=false", "-p:BuildWebUI=false") + (Get-CommonArguments)
            Invoke-DotNet $arguments "Compile the selected project and its required dependencies"
        }

        "TargetedTests"
        {
            if ([string]::IsNullOrWhiteSpace($TestFilter))
            {
                throw "TargetedTests requires -TestFilter so the lane remains bounded."
            }

            $target = Resolve-RepositoryTarget $TestProject "Targeted test project"
            $arguments = @("test", $target, "--filter", $TestFilter, "-p:AssembleProduct=false", "-p:BuildWebUI=false") + (Get-CommonArguments)
            Invoke-DotNet $arguments "Run the requested filtered tests"
        }

        "FullProductBuild"
        {
            $target = Resolve-RepositoryTarget $desktopSolution "Desktop solution"
            $arguments = @("build", $target, "-p:AssembleProduct=true", "-p:BuildWebUI=true") + (Get-CommonArguments)
            Invoke-DotNet $arguments "Build the complete desktop product solution"
        }

        "FullVerification"
        {
            $solution = Resolve-RepositoryTarget $desktopSolution "Desktop solution"
            $buildArguments = @("build", $solution, "-p:AssembleProduct=true", "-p:BuildWebUI=true") + (Get-CommonArguments)
            Invoke-DotNet $buildArguments "Build the complete desktop product solution"

            $tests = Resolve-RepositoryTarget $mainTestProject "Main test project"
            $testArguments = @("test", $tests, "--no-build", "--no-restore") + (Get-CommonArguments | Where-Object { $_ -ne "--no-restore" })
            Invoke-DotNet $testArguments "Run the main test suite from the product build outputs"

            $buildTests = Resolve-RepositoryTarget $buildTestProject "Build and packaging test project"
            $buildTestArguments = @("test", $buildTests, "--no-build", "--no-restore") + (Get-CommonArguments | Where-Object { $_ -ne "--no-restore" })
            Invoke-DotNet $buildTestArguments "Run isolated build and packaging tests"

            $mcpTests = Resolve-RepositoryTarget $mcpTestProject "MCP test project"
            $mcpArguments = @("test", $mcpTests, "-p:AssembleProduct=false", "-p:BuildWebUI=false") + (Get-CommonArguments)
            Invoke-DotNet $mcpArguments "Build and run the MCP server test suite"
        }
    }
}
finally
{
    Pop-Location
}

Write-Host ""
Write-Host "Lane '$Lane' completed successfully." -ForegroundColor Green
