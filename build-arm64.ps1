$ErrorActionPreference = "Stop"
$root = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$project = "$root\src\desktop\app\XerahS.App\XerahS.App.csproj"
$issScript = "$root\build\windows\XerahS-setup.iss"
$outputDir = "$root\dist"
$arch = "win-arm64"
$publishOutput = "$root\build\publish-temp-$arch"

# Check for ISCC
$programFilesX86 = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ProgramFilesX86)
$isccPath = "$programFilesX86\Inno Setup 6\ISCC.exe"
if (!(Test-Path $isccPath)) {
    Write-Error "Inno Setup Compiler not found at: $isccPath"
}

# Get version
$version = "0.28.2"
$propsFile = "$root\Directory.Build.props"
if (Test-Path $propsFile) {
    $xml = [xml](Get-Content $propsFile)
    $v = $xml.SelectSingleNode("//Version")
    if ($v -and $v.InnerText) { $version = $v.InnerText.Trim() }
}

# Kill lingering build processes
Get-Process | Where-Object { $_.Name -like "*VBCSCompiler*" -or $_.Name -like "*MSBuild*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
dotnet build-server shutdown | Out-Null

# Publish
Write-Host "Publishing $arch..."
if (Test-Path $publishOutput) { Remove-Item -Recurse -Force $publishOutput }
dotnet publish $project -c Release -p:OS=Windows_NT -r $arch -p:PublishSingleFile=false -p:SkipBundlePlugins=true -p:nodeReuse=false -p:UseSharedCompilation=false -p:BuildInParallel=false --disable-build-servers --self-contained true -o $publishOutput /m:1

# Publish plugins
Write-Host "Publishing Plugins..."
$pluginsDir = "$publishOutput\Plugins"
if (!(Test-Path $pluginsDir)) { New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null }
$pluginProjects = Get-ChildItem -Path "$root\src\desktop\plugins" -Filter "*.csproj" -Recurse
foreach ($plugin in $pluginProjects) {
    $pluginId = $plugin.BaseName
    $pluginJsonPath = Join-Path $plugin.Directory.FullName "plugin.json"
    if (Test-Path $pluginJsonPath) {
        try {
            $jsonContent = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
            if ($jsonContent.pluginId) { $pluginId = $jsonContent.pluginId }
        } catch {}
    }
    $pluginOutput = Join-Path $pluginsDir $pluginId
    dotnet publish $plugin.FullName -c Release -p:OS=Windows_NT -r $arch -p:nodeReuse=false -p:UseSharedCompilation=false -p:BuildInParallel=false --disable-build-servers --self-contained false -o $pluginOutput /m:1
}
dotnet build-server shutdown | Out-Null

# Deduplicate plugin files
Write-Host "Deduplicating plugins..."
foreach ($pluginDir in Get-ChildItem -Path $pluginsDir -Directory) {
    foreach ($file in Get-ChildItem -Path $pluginDir.FullName -File -ErrorAction SilentlyContinue) {
        $mainAppFile = Join-Path $publishOutput $file.Name
        if (Test-Path $mainAppFile) {
            try { Remove-Item -Path $file.FullName -Force -ErrorAction Stop } catch {}
        }
    }
}

# Compile Installer
Write-Host "Compiling Inno Setup installer for $arch..."
$setupBaseName = "XerahS-$version-$arch"
$setupExe = "$setupBaseName.exe"
$archLog = "$root\iscc_log_$arch.txt"
$arg1 = "/dMyAppReleaseDirectory=$publishOutput"
$arg2 = "/dOutputBaseFilename=$setupBaseName"
$arg3 = "/dOutputDir=$outputDir"

Write-Host "ISCC: $isccPath"
Write-Host "  $arg1"
Write-Host "  $arg2"
Write-Host "  $arg3"
Write-Host "  $issScript"

& $isccPath $arg1 $arg2 $arg3 $issScript 2>&1 | Out-File -FilePath $archLog -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
    Get-Content $archLog | Select-Object -Last 30
    Write-Error "ISCC failed with exit code $LASTEXITCODE"
}
$compiledSetup = Join-Path $outputDir $setupExe
if (Test-Path $compiledSetup) {
    $size = [math]::Round((Get-Item $compiledSetup).Length / 1MB, 2)
    Write-Host "Success! Generated $setupExe ($size MB) in dist"
} else {
    Write-Error "Installer not found at $compiledSetup"
}
