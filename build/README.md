# XerahS Build System Documentation

This document describes the build system structure and how builds work for each operating system.

---

## Directory Structure

```
build/
â”œâ”€â”€ README.md                          # This file
â”œâ”€â”€ windows/                           # Windows build scripts
â”‚   â”œâ”€â”€ package-windows.ps1           # Main PowerShell build script
â”‚   â”œâ”€â”€ XerahS-setup.iss              # Inno Setup installer script
â”‚   â”œâ”€â”€ scoop/                        # Scoop manifests
â”‚   â”‚   â””â”€â”€ scoop.json                # Scoop manifest for XerahS
â”‚   â”œâ”€â”€ winget/                       # WinGet manifests
â”‚   â”‚   â””â”€â”€ generate-winget.ps1       # Script to generate WinGet manifests
â”‚   â””â”€â”€ chocolatey/                   # Chocolatey packages
â”‚       â”œâ”€â”€ Sync-ChocolateyPackage.ps1# Sync + pack Chocolatey package metadata
â”‚       â”œâ”€â”€ xerahs.nuspec             # Chocolatey package definition
â”‚       â””â”€â”€ tools/                    # Chocolatey install/uninstall scripts
â”œâ”€â”€ linux/                             # Linux build scripts
â”‚   â”œâ”€â”€ package-linux.ps1             # PowerShell wrapper for Linux build (Windows)
â”‚   â”œâ”€â”€ package-linux.sh              # Bash script for Linux build (Linux/macOS)
â”‚   â””â”€â”€ XerahS.Packaging/             # C# packaging tool
â”‚       â”œâ”€â”€ Program.cs                # Packaging logic (tar.gz, .deb, .rpm)
â”‚       â””â”€â”€ XerahS.Packaging.csproj   # Project file
â”œâ”€â”€ macos/                             # macOS build scripts
â”‚   â”œâ”€â”€ package-mac.ps1               # PowerShell script for macOS build (Windows)
â”‚   â””â”€â”€ package-mac.sh                # Bash script for macOS build (macOS)
â””â”€â”€ android/                           # Android/Mobile build scripts
    â”œâ”€â”€ build-android.sh              # Bash script for Android build (Linux)
    â”œâ”€â”€ build-android.ps1             # PowerShell script for Android build (Windows)
    â””â”€â”€ README.md                     # Detailed Android build documentation
```

---

## Windows Build

### Files
- **`package-windows.ps1`** - PowerShell build orchestrator
- **`XerahS-setup.iss`** - Inno Setup installer definition

### How It Works

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                        Windows Build Flow                               â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                         â”‚
â”‚  1. Detect version from Directory.Build.props                           â”‚
â”‚                              â†“                                          â”‚
â”‚  2. For each architecture (win-x64, win-arm64):                         â”‚
â”‚                              â†“                                          â”‚
â”‚     a. dotnet publish (main app) â†’ build/publish-temp-{arch}/           â”‚
â”‚                              â†“                                          â”‚
â”‚     b. Publish Plugins to Plugins/ subfolder                            â”‚
â”‚        â€¢ Reads plugin.json for pluginId                                 â”‚
â”‚        â€¢ Publishes each plugin to Plugins/{pluginId}/                   â”‚
â”‚                              â†“                                          â”‚
â”‚     c. Deduplicate plugin files                                         â”‚
â”‚        â€¢ Removes duplicate DLLs already in main app                     â”‚
â”‚        â€¢ Saves ~170 MB per architecture                                 â”‚
â”‚                              â†“                                          â”‚
â”‚     d. ISCC.exe (Inno Setup)                                            â”‚
â”‚        â€¢ /dMyAppReleaseDirectory={publish-temp}                         â”‚
â”‚        â€¢ /dOutputBaseFilename=XerahS-{version}-{arch}                   â”‚
â”‚        â€¢ /dOutputDir={dist}                                             â”‚
â”‚                              â†“                                          â”‚
â”‚  3. Output: dist/XerahS-{version}-{arch}.exe                            â”‚
â”‚                                                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Key Features
- **Dual architecture support**: Builds for both x64 and ARM64
- **Plugin bundling**: Includes 5 plugins (amazons3, auto, gist, imgur, paste2)
- **File deduplication**: Saves space by removing duplicate DLLs from plugins
- **Inno Setup integration**: Creates professional Windows installers

### Requirements
- Inno Setup 6 (installed at `%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`)
- .NET SDK 10.0+

### Package Managers
The `build/windows` directory also contains resources for submitting XerahS to package managers.
- **Scoop**: `build/windows/scoop/scoop.json`
- **WinGet**: `build/windows/winget/generate-winget.ps1` (Generates manifests to `manifests/` subdir)
- **Chocolatey**: `build/windows/chocolatey/`
  - `Sync-ChocolateyPackage.ps1` updates nuspec metadata, installer checksums, and `VERIFICATION.txt` from an existing GitHub release.
  - `Test-ChocolateyPackage.ps1` smoke-tests the generated package with local `choco install` and `choco uninstall`.
  - `tools/chocolateyBeforeModify.ps1` stops running XerahS processes before upgrade or uninstall.
  - Add `-Pack` to generate `xerahs.<version>.nupkg`.
  - Tag releases automatically build, smoke-test, and attach the `.nupkg` to the GitHub release in `release-build-all-platforms.yml`.

---

## Linux Build

### Files
- **`package-linux.ps1`** - PowerShell wrapper (Windows hosts)
- **`package-linux.sh`** - Bash script (Linux/macOS hosts)
- **`XerahS.Packaging/`** - C# packaging tool

### How It Works

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                         Linux Build Flow                                â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                         â”‚
â”‚  1. Detect version from Directory.Build.props                           â”‚
â”‚                              â†“                                          â”‚
â”‚  2. dotnet publish (main app)                                           â”‚
â”‚     â€¢ Runtime: linux-x64                                                â”‚
â”‚     â€¢ Single file: true                                                 â”‚
â”‚     â€¢ Self-contained: true                                              â”‚
â”‚     â†’ src/desktop/app/XerahS.App/bin/Release/net10.0/linux-x64/publish/ â”‚
â”‚                              â†“                                          â”‚
â”‚  3. Publish Plugins to Plugins/ subfolder                               â”‚
â”‚     â€¢ Same process as Windows                                           â”‚
â”‚     â€¢ Deduplicates files against main app                               â”‚
â”‚                              â†“                                          â”‚
â”‚  4. XerahS.Packaging tool creates:                                      â”‚
â”‚                              â†“                                          â”‚
â”‚     â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”     â”‚
â”‚     â”‚   Tarball       â”‚ XerahS-{version}-linux-x64.tar.gz        â”‚     â”‚
â”‚     â”‚   (.tar.gz)     â”‚ Portable, extract and run                â”‚     â”‚
â”‚     â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤     â”‚
â”‚     â”‚   Debian        â”‚ XerahS-{version}-linux-x64.deb           â”‚     â”‚
â”‚     â”‚   Package       â”‚ Installs to /usr/lib/xerahs/             â”‚     â”‚
â”‚     â”‚   (.deb)        â”‚ Creates /usr/bin/xerahs wrapper          â”‚     â”‚
â”‚     â”‚                 â”‚ Desktop entry + icon included            â”‚     â”‚
â”‚     â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤     â”‚
â”‚     â”‚   RPM Package   â”‚ XerahS-{version}-linux-x64.rpm           â”‚     â”‚
â”‚     â”‚   (.rpm)        â”‚ For Fedora/RHEL/CentOS/SUSE              â”‚     â”‚
â”‚     â”‚                 â”‚ Requires rpmbuild tool                   â”‚     â”‚
â”‚     â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜     â”‚
â”‚                              â†“                                          â”‚
â”‚  5. Individual plugin .zip files also created                           â”‚
â”‚     â€¢ {pluginId}-{version}-linux-x64.zip                                â”‚
â”‚                                                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Package Details

| Package Type | Install Location | Usage |
|-------------|------------------|-------|
| `.tar.gz` | User choice | Extract and run `./XerahS` |
| `.deb` | `/usr/lib/xerahs/` | `sudo dpkg -i xerahs.deb` |
| `.rpm` | `/usr/lib/xerahs/` | `sudo rpm -i xerahs.rpm` |

### Requirements
- .NET SDK 10.0+
- For RPM: `rpmbuild` tool (optional)

---

## macOS Build

### Files
- **`package-mac.ps1`** - PowerShell script for cross-compilation from Windows
- **`package-mac.sh`** - Bash script for building on macOS

### How It Works

#### Option 1: Build from Windows (Cross-Compilation)

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                    macOS Build Flow (from Windows)                      â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                         â”‚
â”‚  1. Detect version from Directory.Build.props                           â”‚
â”‚                              â†“                                          â”‚
â”‚  2. Verify pre-compiled native library exists                           â”‚
â”‚     â€¢ native/macos/libscreencapturekit_bridge.dylib                     â”‚
â”‚     â€¢ (Compile on macOS first if update needed)                         â”‚
â”‚                              â†“                                          â”‚
â”‚  3. For each architecture (osx-arm64, osx-x64):                         â”‚
â”‚                              â†“                                          â”‚
â”‚     a. dotnet publish with -p:CrossCompile=true                         â”‚
â”‚        â€¢ Uses net10.0 (not net10.0-windows...)                          â”‚
â”‚        â€¢ References XerahS.Platform.MacOS (not Windows)                 â”‚
â”‚                              â†“                                          â”‚
â”‚     b. Create .app bundle structure                                     â”‚
â”‚        XerahS.app/Contents/MacOS/                                       â”‚
â”‚                              â†“                                          â”‚
â”‚     c. Publish Plugins to Plugins/ subfolder                            â”‚
â”‚        â€¢ Same process as other platforms                                â”‚
â”‚                              â†“                                          â”‚
â”‚     d. Package as .tar.gz                                               â”‚
â”‚                              â†“                                          â”‚
â”‚  4. Output: dist/XerahS-{version}-mac-{arch}.tar.gz                     â”‚
â”‚                                                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

#### Option 2: Build from macOS (Native)

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                      macOS Build Flow (from macOS)                      â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                         â”‚
â”‚  1. Detect version from Directory.Build.props                           â”‚
â”‚                              â†“                                          â”‚
â”‚  2. Build native ScreenCaptureKit library                               â”‚
â”‚     â€¢ cd native/macos && make                                           â”‚
â”‚     â€¢ Produces libscreencapturekit_bridge.dylib                         â”‚
â”‚                              â†“                                          â”‚
â”‚  3. dotnet publish (triggers CreateMacOSAppBundle target)               â”‚
â”‚                              â†“                                          â”‚
â”‚  4. Plugins, packaging same as cross-compile                            â”‚
â”‚                                                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Cross-Compilation (`CrossCompile` Property)

The `-p:CrossCompile=true` flag enables building macOS/Linux binaries from Windows:

| Setting | Normal (Windows) | Cross-Compile (macOS/Linux) |
|---------|------------------|----------------------------|
| TargetFramework | `net10.0-windows10.0.26100.0` | `net10.0` |
| Platform Reference | `XerahS.Platform.Windows` | `XerahS.Platform.MacOS/Linux` |
| Preprocessor | `WINDOWS` defined | `WINDOWS` NOT defined |
| App Bundle | N/A | Created for macOS |

### Native Library Management

| Script | Native Library Source | Action |
|--------|----------------------|--------|
| `package-mac.sh` (macOS) | Source code | Compiles with `make` |
| `package-mac.ps1` (Windows) | Pre-compiled binary | Copies existing `.dylib` |

**To update the native library:**
1. Run `package-mac.sh` on macOS (compiles latest)
2. Commit the updated `libscreencapturekit_bridge.dylib`
3. Windows builds will use the updated binary

### Requirements

**For `package-mac.ps1` (Windows):**
- .NET SDK 10.0+
- Pre-compiled `native/macos/libscreencapturekit_bridge.dylib`

**For `package-mac.sh` (macOS):**
- macOS with Xcode Command Line Tools
- .NET SDK 10.0+

---

## Android/Mobile Build

### Files
- **`build-android.sh`** - Bash script for building Android apps (Linux)
- **`build-android.ps1`** - PowerShell script for building Android apps (Windows)
- **`README.md`** - Comprehensive Android build documentation

### How It Works

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                      Android Build Flow                                 â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                         â”‚
â”‚  1. Detect version from Directory.Build.props                           â”‚
â”‚                              â†“                                          â”‚
â”‚  2. Configure Java environment                                          â”‚
â”‚     â€¢ Set JAVA_HOME to JDK 21                                           â”‚
â”‚     â€¢ Verify Java version                                               â”‚
â”‚                              â†“                                          â”‚
â”‚  3. Build XerahS.Mobile.Ava (Avalonia Android)                          â”‚
â”‚     â€¢ dotnet build -c Release -f net10.0-android                        â”‚
â”‚     â†’ src/mobile-experimental/XerahS.Mobile.Ava/bin/.../net10.0-android/ â”‚
â”‚                              â†“                                          â”‚
â”‚  4. Build XerahS.Mobile.Maui (MAUI/Android)                             â”‚
â”‚     â€¢ dotnet build -c Release -f net10.0-android                        â”‚
â”‚     â†’ src/mobile-experimental/XerahS.Mobile.Maui/bin/.../net10.0-android/â”‚
â”‚                              â†“                                          â”‚
â”‚  6. Copy APKs to dist/android/                                          â”‚
â”‚     â€¢ XerahS-{version}-Android.apk                                      â”‚
â”‚     â€¢ XerahS-{version}-MAUI-Android.apk                                 â”‚
â”‚                                                                         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Platform-Specific Configuration

The MAUI project uses conditional targeting to support both platforms:

```xml
<!-- Build Android on all platforms, iOS only on macOS -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">
  net10.0-android;net10.0-ios
</TargetFrameworks>
<TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('osx'))">
  net10.0-android
</TargetFrameworks>
```

### Requirements

**For Android builds (Linux):**
- .NET SDK 10.0+ with Android workload
- OpenJDK 21 (not JDK 25!)
- Android SDK Platform API Level 36

**For Android builds (Windows):**
- .NET SDK 10.0+ with Android workload
- Microsoft JDK 21
- Android SDK Platform API Level 36

**Installation:**
See `build/android/README.md` for detailed setup instructions including:
- Android workload installation (requires custom temp directory on Linux)
- JDK 21 installation
- Android SDK dependencies installation

### iOS Builds

iOS projects (`XerahS.Mobile.iOS`, `XerahS.Mobile.iOS.ShareExtension`) **require macOS** and cannot be built on Linux or Windows. The MAUI project automatically excludes iOS targets on non-macOS platforms.

---

## Shared Plugin Build Process

All platforms use the same plugin discovery and build logic:

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                    Plugin Build Flow                            â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                 â”‚
â”‚  src/desktop/plugins/                                            â”‚
â”‚  â”œâ”€â”€ AmazonS3.Plugin/                                            â”‚
â”‚  â”‚   â”œâ”€â”€ XerahS.AmazonS3.Plugin.csproj                           â”‚
â”‚  â”‚   â””â”€â”€ plugin.json                                             â”‚
â”‚  â”œâ”€â”€ Auto.Plugin/                                                â”‚
â”‚  â”‚   â””â”€â”€ plugin.json                                             â”‚
â”‚  â””â”€â”€ ...                                                         â”‚
â”‚                                                                  â”‚
â”‚  Build script:                                                   â”‚
â”‚  1. Find all .csproj in src/desktop/plugins/                     â”‚
â”‚  2. Read plugin.json â†’ extract "pluginId"                       â”‚
â”‚  3. dotnet publish to Plugins/{pluginId}/                       â”‚
â”‚  4. Remove files that already exist in main app                 â”‚
â”‚                                                                 â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Current Plugins

| Plugin ID | Name | Description |
|-----------|------|-------------|
| `amazons3` | Amazon S3 Uploader | Upload files to Amazon S3 buckets |
| `auto` | Auto Destination | Automatic upload destination selection |
| `gist` | GitHub Gist Text Uploader | Upload text/code to GitHub Gist |
| `imgur` | Imgur Uploader | Upload images to Imgur |
| `paste2` | Paste2 Text Uploader | Upload text to Paste2 service |

---

## Output Directory

All builds place their final artifacts in the `dist/` folder:

```
dist/
â”œâ”€â”€ Windows
â”‚   â”œâ”€â”€ XerahS-0.14.3-win-x64.exe
â”‚   â””â”€â”€ XerahS-0.14.3-win-arm64.exe
â”‚
â”œâ”€â”€ Linux
â”‚   â”œâ”€â”€ XerahS-0.14.3-linux-x64.tar.gz
â”‚   â”œâ”€â”€ XerahS-0.14.3-linux-x64.deb
â”‚   â”œâ”€â”€ XerahS-0.14.3-linux-x64.rpm
â”‚   â”œâ”€â”€ amazons3-0.14.3-linux-x64.zip
â”‚   â”œâ”€â”€ auto-0.14.3-linux-x64.zip
â”‚   â”œâ”€â”€ gist-0.14.3-linux-x64.zip
â”‚   â”œâ”€â”€ imgur-0.14.3-linux-x64.zip
â”‚   â””â”€â”€ paste2-0.14.3-linux-x64.zip
â”‚
â””â”€â”€ macOS
    â”œâ”€â”€ XerahS-0.14.3-mac-arm64.tar.gz  (Apple Silicon)
    â””â”€â”€ XerahS-0.14.3-mac-x64.tar.gz    (Intel Mac)
```

---

## Quick Reference

### Build Commands

| Platform | Command | Host OS | Native Library |
|----------|---------|---------|----------------|
| Windows | `.\build\windows\package-windows.ps1` | Windows | N/A |
| Linux | `.\build\linux\package-linux.ps1` | Windows | N/A |
| Linux | `./build/linux/package-linux.sh` | Linux/macOS | N/A |
| macOS | `.\build\macos\package-mac.ps1` | Windows | Pre-compiled |
| macOS | `./build/macos/package-mac.sh` | macOS | Compiled from source |

### Version Detection

All scripts read version from `Directory.Build.props`:
```xml
<Version>0.14.3</Version>
```

### Common Build Flags

| Flag | Purpose |
|------|---------|
| `-c Release` | Release configuration |
| `-p:OS={OS}` | Target OS (Windows_NT, Linux, macOS) |
| `-r {runtime}` | Runtime identifier (win-x64, linux-x64, osx-x64, etc.) |
| `-p:PublishSingleFile=true/false` | Single executable vs multiple files |
| `--self-contained true/false` | Include .NET runtime |
| `-p:CrossCompile=true` | Enable cross-compilation from Windows to macOS/Linux |
| `-p:SkipBundlePlugins=true` | Skip automatic plugin bundling |
| `-p:nodeReuse=false` | Disable MSBuild node reuse (prevents file locking) |

---

## Troubleshooting

### Windows
- **ISCC not found**: Install Inno Setup 6 at default location
- **File locked**: Script disables `nodeReuse` to prevent file locking

### Linux
- **rpmbuild not found**: RPM package will be skipped (others still built)
- **Permission errors**: Ensure `dotnet` is in PATH

### macOS (Cross-Compile from Windows)
- **Native library not found**: Run `package-mac.sh` on macOS first to compile `libscreencapturekit_bridge.dylib`, then commit it
- **Screen capture not working**: Native library is outdated - rebuild on macOS

### macOS (Build on macOS)
- **make: command not found**: Install Xcode Command Line Tools (`xcode-select --install`)
- **Codesign issues**: May need to disable SIP or sign with developer cert
- **Notarization**: Required for distribution outside App Store

---

## Related Documentation

- `../DEVELOPER_README.md` - General development setup
- `../.github/skills/git-workflow/SKILL.md` - Release procedures
- `../docs/architecture/PORTING_GUIDE.md` - Platform abstractions
- `../native/macos/README_NATIVE.md` - Native macOS library documentation
