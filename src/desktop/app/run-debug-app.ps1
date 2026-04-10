#requires -Version 5.1
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = "Stop"
$forwardedArgs = [System.Collections.Generic.List[string]]::new()
$launchOnboarding = $false

foreach ($arg in $RemainingArgs) {
    if ($arg -eq "--onboarding") {
        $launchOnboarding = $true
        continue
    }

    $forwardedArgs.Add($arg)
}

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)

if (-not $dotnet) {
    $userDotnet = Join-Path (Join-Path $HOME ".dotnet") "dotnet"
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $userDotnet = "$userDotnet.exe"
    }

    if (Test-Path $userDotnet) {
        $dotnet = $userDotnet
    }
}

if (-not $dotnet) {
    Write-Error "dotnet not found in PATH or at $HOME/.dotnet/dotnet"
    exit 1
}

$project = Join-Path $PSScriptRoot "XerahS.App\XerahS.App.csproj"

if ($launchOnboarding) {
    $debugProfileRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) "XerahS\debug-profiles"))
    $profileName = "onboarding-" + [System.Guid]::NewGuid().ToString("N")
    $onboardingProfile = [System.IO.Path]::GetFullPath((Join-Path $debugProfileRoot $profileName))

    if (-not $onboardingProfile.StartsWith($debugProfileRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved onboarding profile path escaped the expected debug profile root: $onboardingProfile"
    }

    New-Item -ItemType Directory -Path $onboardingProfile -Force | Out-Null
    $forwardedArgs.Add("--settings-folder")
    $forwardedArgs.Add($onboardingProfile)
}

& $dotnet run --project $project -c Debug -- @forwardedArgs
exit $LASTEXITCODE
