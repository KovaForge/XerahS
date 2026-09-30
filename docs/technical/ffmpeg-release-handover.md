# FFmpeg release handover

**Date:** 2026-09-30
**Status:** Findings only. No repo changes have been made from this investigation.
**Audience:** Whoever next publishes ShareX FFmpeg builds and wires XerahS Linux download to them.

User-facing recording behaviour stays in [FFMPEG.md](FFMPEG.md). This note is the release contract: which GitHub repos produce the binaries, what XerahS actually downloads, how the Windows DPI manifest fits in, and how ShareX Authenticode-signs `ffmpeg.exe`.

Checked trees:

- `ShareX/ShareX` at `/home/mike/Work/ShareX/ShareX` (HEAD `94838f6`)
- `KovaForge/XerahS` at `/home/mike/Work/KovaForge/xerahs`
- GitHub `ShareX/FFmpeg` release `v8.1` (2026-05-20)
- GitHub `ShareX/FFmpeg-Builds` release `latest` (2024-06-28)
- Local manifest helpers from `~/Downloads/Telegram Desktop/`: `Add-manifest.bat`, `Extract-manifest.bat`, `ffmpeg.exe.manifest`

---

## 1. Two repos, two jobs

| Repo | What it is | What it publishes today |
|---|---|---|
| [ShareX/FFmpeg](https://github.com/ShareX/FFmpeg) | Release bucket. Tree is `README.md` and `LICENSE.txt`. Tags are `v8.1`, `v8.0`, `v7.1`, … | `ffmpeg-8.1-win-x64.zip`, `ffmpeg-8.1-win-arm64.zip`. Each zip contains one file, `ffmpeg.exe`, at the archive root. |
| [ShareX/FFmpeg-Builds](https://github.com/ShareX/FFmpeg-Builds) | Frozen fork of [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds). Last commit 2024-06-25. Scripts can build Linux. CI does not. | `ffmpeg-latest-win64.zip`, `ffmpeg-7.0-win64.zip`, and the `win32` pair. Last auto-build 2024-06-28. |

XerahS Windows download points at the release bucket, not the build repo:

```55:56:src/desktop/core/XerahS.Common/FFmpegDownloader.cs
        public const string DefaultOwner = "ShareX";
        public const string DefaultRepo = "FFmpeg";
```

`v8.1` `ffmpeg.exe` is a static GPL build. Its configure line includes `libx264`, `libx265`, `libvpx`, `libaom`, `libsvtav1`, `gdigrab`, `dshow`, and `ddagrab`. It does not include `ffprobe`. On Windows, XerahS extracts `ffmpeg.exe` and, when `ffprobe.exe` is missing, downloads a probe from `System233/ffmpeg-msvc-prebuilt`.

---

## 2. Naming that already exists

Three naming schemes are in play. Only the first one matches the files XerahS downloads on Windows.

### Release bucket (what XerahS Windows matches)

```text
ffmpeg-8.1-win-x64.zip
ffmpeg-8.1-win-arm64.zip
```

`FFmpegUpdateChecker.GetExpectedAssetSuffix()` accepts a `v*` tag and an asset whose name ends with:

| Architecture | Suffix |
|---|---|
| `win64` | `win-x64.zip` |
| `winArm64` | `win-arm64.zip` |
| `win32` | `win-x86.zip` |
| `macos64` | `macos64.zip` |
| `linux64` | `linux64.zip` |

Source: `src/desktop/core/XerahS.Common/UpdateChecker/FFmpegUpdateChecker.cs`.

`linux64.zip` is declared and unused. Linux never reaches this checker. See section 4.

### Build repo (what the old ShareX checker matches)

`ShareX/FFmpeg-Builds` `build.sh` names artifacts `ffmpeg-${ADDINS_STR:-latest}-${TARGET}` and, for Windows, runs `zip -9 -j` on `bin/ffmpeg.exe` only. That produces `ffmpeg-latest-win64.zip` and `ffmpeg-7.0-win64.zip`.

ShareX desktop `ShareX.HelpersLib/UpdateChecker/FFmpegUpdateChecker.cs` still looks for `win64.zip`, `win32.zip`, and `macos64.zip`. `ffmpeg-8.1-win-x64.zip` does not end with `win64.zip`, so the current ShareX checker does not select the `v8.1` assets. `FFmpegGitHubDownloader` exists in that tree and has no caller in the Avalonia UI. The ShareX app workflow downloads the zip by the `win-x64` URL directly. See section 6.

### BtbN (what XerahS Linux matches today)

```text
ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz
ffmpeg-n8.1-latest-linuxarm64-gpl-8.1.tar.xz
```

Upstream BtbN zips the whole tree (`zip -9 -r` / `tar`) and keeps `ffprobe`. ShareX's fork disables `ffprobe` and flattens Windows to a single exe.

---

## 3. DPI manifest

`gdigrab` on a scaled Windows display records the wrong rectangle unless `ffmpeg.exe` is DPI-aware.

The three local helpers are a manual `mt.exe` step from the Windows SDK (`10.0.26100`):

- `Extract-manifest.bat` dumps resource 1 to `ffmpeg.exe.manifest`.
- `Add-manifest.bat` writes that file back with `mt.exe -manifest ffmpeg.exe.manifest -outputresource:ffmpeg.exe;1`.
- The manifest on disk only sets `dpiAware` to `true`.

`mt.exe -outputresource` replaces resource 1. It does not merge.

Both published binaries (`ffmpeg-8.1-win-x64.zip` and `ffmpeg-latest-win64.zip`) already contain FFmpeg's own manifest from `FFmpeg/FFmpeg` `fftools/fftools.manifest` (commit `f85e0673`, 2022-08-07):

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0" xmlns:asmv3="urn:schemas-microsoft-com:asm.v3">
  <asmv3:application>
    <asmv3:windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </asmv3:windowsSettings>
  </asmv3:application>
</assembly>
```

Running `Add-manifest.bat` with the Downloads manifest would drop `PerMonitorV2`. Leave the toolchain manifest in place. Keep `Extract-manifest.bat` as a check that `PerMonitorV2` is still present.

`ShareX/FFmpeg-Builds` has a copy of the weaker manifest at the repo root (`ffmpeg.exe.manifest`, added 2022-09-10). No script references it. The ShareX build workflow does not run `mt.exe` either.

Linux has no PE manifest. The equivalent requirement is which devices are compiled in (`x11grab`, `pulse`, and later `pipewire`).

---

## 4. How XerahS downloads FFmpeg

`DownloadLatestAsync` in `src/desktop/core/XerahS.Common/FFmpegDownloader.cs`:

- Windows and macOS call `FFmpegUpdateChecker("ShareX", "FFmpeg")`, download the zip, and extract entries named `ffmpeg.exe` / `ffprobe.exe` (or `ffmpeg` / `ffprobe`). `EnsureExecutable` sets the Unix mode on non-Windows.
- Linux returns immediately and calls `DownloadLinuxStaticAsync`.

The Linux path reads `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/tags/latest` and keeps the newest asset matching:

```text
^ffmpeg-n(?<major>\d+)\.(?<minor>\d+)-latest-(?<arch>linux64|linuxarm64)-gpl-\d+\.\d+\.tar\.xz$
```

It skips `master`, LGPL, and shared builds. Extraction is `tar -xJf` with wildcards `*/bin/ffmpeg` and `*/bin/ffprobe`. Both binaries are copied flat into the destination folder and marked executable.

`FFmpegLinuxDownloadTests` requires `libx264` and `libvpx-vp9` in that binary, plus a sibling `ffprobe`.

Install folder comes from `PathsManager.GetArchitectureFolderName()`:

- Windows: `win-x64`, `win-arm64`, `win-x86`
- macOS: `macos64`
- Linux: `linux64` for every CPU, including arm64

`FFmpegUpdateChecker.ResolveArchitecture()` has the same Linux bug: every Linux machine is `linux64`.

`DownloadFFprobeFallbackAsync` returns `null` unless the OS is Windows. A Linux zip that contains only `ffmpeg` breaks the video editor. `VideoEditorFfprobeResolver` looks for `ffprobe` beside `ffmpeg`, then calls the primary download, then the Windows-only fallback.

---

## 5. Linux assets to publish

Put these two files on the same `v8.1` (and later `v*`) release as the Windows zips:

| Asset | Zip root |
|---|---|
| `ffmpeg-8.1-linux-x64.zip` | `ffmpeg` and `ffprobe` |
| `ffmpeg-8.1-linux-arm64.zip` | `ffmpeg` and `ffprobe` |

That follows the Windows pattern: `ffmpeg-{version}-{rid}.zip`, flat zip, both tools.

`ffmpeg-8.1-linux-x64.zip` does not end with `linux64.zip`. The checker suffix has to change with the new files. Publishing BtbN names (`ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz`) on `ShareX/FFmpeg` would still miss, because the Linux code requests the tag named `latest` on `BtbN/FFmpeg-Builds`, not GitHub's "latest release" of `ShareX/FFmpeg`.

Build the GPL static variant. LGPL drops `libx264` and `libx265`. A shared build needs its `lib/` directory beside the binary, and the extractor copies only the two executables.

Each binary needs to show:

- encoders: `libx264`, `libx265`, `libvpx-vp9`, `libaom-av1`
- `drawtext` via libfreetype (watermarks)
- inputs: `x11grab`, `pulse`
- `ffprobe` next to `ffmpeg`

Keep BtbN's glibc 2.28 / Linux 4.18 floor so one zip runs on current Ubuntu, Debian, Fedora, RHEL 8, and Arch.

PipeWire is a follow-on. Current BtbN `scripts.d` builds PulseAudio and X11, not PipeWire, so `ffmpeg -devices` will not list `pipewire`. XerahS on Wayland then keeps using `wf-recorder` or GStreamer, which [FFMPEG.md](FFMPEG.md) already describes. Linking `libpipewire` dynamically is enough on a machine that can do portal capture. Do not block the zip on a fully static PipeWire link.

### Build repo work

The 2024 fork cannot produce an 8.1 Linux build. Current BtbN has addins through `9.0` and a matrix that includes `linux64` and `linuxarm64`. Merge that forward, then keep a thin ShareX publish step:

1. Build `linux64` and `linuxarm64`, variant `gpl`, addin matching the Windows release (`8.1` today).
2. Stop passing `--disable-ffprobe`. Windows can keep a zip of `ffmpeg.exe` only. Linux cannot.
3. Repack:

```bash
zip -9 -j "ffmpeg-8.1-linux-x64.zip" ffmpeg ffprobe
zip -9 -j "ffmpeg-8.1-linux-arm64.zip" ffmpeg ffprobe
```

4. Upload those zips onto the `ShareX/FFmpeg` tag `v8.1`, beside `ffmpeg-8.1-win-x64.zip`. XerahS resolves `repos/ShareX/FFmpeg/releases/latest` and only accepts tags that start with `v`. A floating `latest` tag on `FFmpeg-Builds` is not selected.
5. If a manifest copy stays in the build repo, replace it with the text of `fftools/fftools.manifest`. Do not run `Add-manifest.bat` on the built exe.

### XerahS work required before those zips are used

Publishing the files does nothing until Linux stops returning early.

- In `FFmpegDownloader.DownloadLatestAsync`, remove the `OperatingSystem.IsLinux()` branch that calls BtbN. The shared zip path already extracts `ffmpeg` and `ffprobe` and calls `EnsureExecutable`.
- In `FFmpegUpdateChecker`, map Linux x64 to a new `linux-x64` value and arm64 to `linux-arm64`. Suffixes: `linux-x64.zip` and `linux-arm64.zip`.
- In `PathsManager.GetArchitectureFolderName()`, use those same folder names so an arm64 download does not land in `Tools/linux64`.
- Update `tests/XerahS.Tests/Common/FFmpegLinuxDownloadTests.cs` to the new asset names. The network test should still find `libx264`, `libvpx-vp9`, and a sibling `ffprobe`.

After that, **Download FFmpeg** on Linux resolves `https://github.com/ShareX/FFmpeg/releases/download/v8.1/ffmpeg-8.1-linux-x64.zip`.

---

## 6. How ShareX signs `ffmpeg.exe`

Signing lives in `ShareX/ShareX` `.github/workflows/build.yml`, job `build`, `runs-on: windows-latest`, `environment: release`. The FFmpeg repos have no signing workflow.

Order inside that job:

1. Download `https://github.com/ShareX/FFmpeg/releases/download/v${FFMPEG_VERSION}/ffmpeg-${FFMPEG_VERSION}-win-${PLATFORM}.zip`. `FFMPEG_VERSION` is `8.1`. `PLATFORM` is `x64` or `arm64`.
2. Expand the zip and copy `ffmpeg.exe` to `ShareX\bin\<Configuration>\win-<rid>\ffmpeg.exe`.
3. `azure/login@v3` with secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. The job sets `permissions: id-token: write`, so this is GitHub OIDC. There is no PFX in the repo.
4. `azure/artifact-signing-action@v2` signs that folder.
5. `ShareX.Setup` packs the signed exe into the setup and the portable zip.
6. A second call to the same action signs `Output\ShareX-<version>-setup-<platform>.exe`.

The binary sign step:

```yaml
- name: Sign ShareX binaries
  uses: azure/artifact-signing-action@v2
  with:
    endpoint: ${{ vars.SIGNING_ENDPOINT }}
    signing-account-name: ${{ vars.SIGNING_ACCOUNT_NAME }}
    certificate-profile-name: ${{ vars.CERTIFICATE_PROFILE_NAME }}
    files-folder: ${{ github.workspace }}\ShareX\bin\${{ matrix.configuration }}\${{ env.RUNTIME_ID }}
    files-folder-filter: ShareX*.exe,ShareX*.dll,ffmpeg.exe
    file-digest: SHA256
    timestamp-rfc3161: http://timestamp.acs.microsoft.com
    timestamp-digest: SHA256
```

It runs when:

- the ref is `develop` and the configuration is `Release`, or
- the ref is a `v*` tag and the configuration is `Release`, `Steam`, or `MicrosoftStore`.

Debug and MicrosoftStoreDebug download `ffmpeg.exe` and skip signing. Avalonia DLL signing is a separate step on tags only, and its filter is `Avalonia*.dll`. The Steam launcher is signed from `ShareX.Steam\bin\Steam\ShareX_Launcher.exe` on Steam tags.

Verified on the public `develop` run [36726132024](https://github.com/ShareX/ShareX/actions/runs/36726132024), job `Build (Release, x64)` (`109923355740`), 2026-09-30:

- Filter matched 13 files, including `D:\a\ShareX\ShareX\ShareX\bin\Release\win-x64\ffmpeg.exe`.
- The log line is `Successfully signed: ...\win-x64\ffmpeg.exe`.
- `signtool` is the copy the action installs: `Microsoft.Windows.SDK.BuildTools.10.0.26100.4188\...\x64\signtool.exe`.
- Arguments: `sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256` plus the Artifact Signing dlib.
- The same log shows endpoint `https://neu.codesigning.azure.net/` and certificate profile `global-prod`. The account name stays in the `SIGNING_ACCOUNT_NAME` variable on the `release` environment.
- The setup exe is signed in a later step: `Successfully signed: ...\Output\ShareX-21.0.0.1625-setup-x64.exe`.

The published zip is unsigned. The certificate table in `ffmpeg-8.1-win-x64.zip`'s `ffmpeg.exe` has size 0. The signature exists only on the copy ShareX packs into its own installer. XerahS, and anyone else who downloads `ShareX/FFmpeg`, gets the unsigned exe.

### Signing the zip itself

Sign `ffmpeg.exe` and `ffprobe.exe` on a `windows-latest` job before `zip -9 -j`, with the same action and the same `release` environment. Suggested shape:

```yaml
jobs:
  sign-windows:
    runs-on: windows-latest
    environment: release
    permissions:
      contents: write
      id-token: write
    steps:
      - name: Azure login
        uses: azure/login@v3
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Sign FFmpeg
        uses: azure/artifact-signing-action@v2
        with:
          endpoint: ${{ vars.SIGNING_ENDPOINT }}
          signing-account-name: ${{ vars.SIGNING_ACCOUNT_NAME }}
          certificate-profile-name: ${{ vars.CERTIFICATE_PROFILE_NAME }}
          files-folder: ${{ github.workspace }}\sign
          files-folder-filter: ffmpeg.exe,ffprobe.exe
          file-digest: SHA256
          timestamp-rfc3161: http://timestamp.acs.microsoft.com
          timestamp-digest: SHA256
```

Then zip the signed files and upload them to the `v*` release.

The Entra federated credential behind `azure/login` is bound to the ShareX app repo (the working subject is `repo:ShareX/ShareX:environment:release`). A workflow in `ShareX/FFmpeg` or `ShareX/FFmpeg-Builds` fails login until that app registration has a federated credential for the repo that runs the job. The existing Artifact Signing "Certificate Profile Signer" role can stay. It is already what signs ShareX.

Sign after the manifest is in the binary. `mt.exe -outputresource` rewrites the PE and invalidates Authenticode. ShareX's workflow does not run `mt.exe`. Do not run `Add-manifest.bat` on an exe that is about to be signed, or on one that already is.

`ffprobe.exe` is not in ShareX's filter. Once the Windows zip contains it, add `ffprobe.exe` beside `ffmpeg.exe` in `files-folder-filter`, or ShareX will keep shipping an unsigned probe next to a signed `ffmpeg.exe`. Re-signing an already-signed file in the ShareX job is harmless: `signtool sign` replaces the signature, and it is the same certificate.

This signer is Authenticode. It signs PE files. A Linux `ffmpeg` / `ffprobe` ELF cannot go through `azure/artifact-signing-action`. Publish the Linux zip with a SHA256 checksum. The Windows executables are the files this workflow can sign.

---

## 7. Checklist

Windows zip, before upload:

- [ ] `ffmpeg.exe` contains `PerMonitorV2` (`Extract-manifest.bat`, or strings). The Downloads manifest was not reapplied.
- [ ] `signtool verify /pa /v ffmpeg.exe` succeeds, timestamped.
- [ ] `ffprobe.exe` is either absent (current contract) or signed in the same step.
- [ ] Asset name is `ffmpeg-<version>-win-x64.zip` or `ffmpeg-<version>-win-arm64.zip` on a `v*` tag of `ShareX/FFmpeg`.
- [ ] Zip root is the exe, not a nested `bin/` directory.

Linux zip, before upload:

- [ ] Names are `ffmpeg-<version>-linux-x64.zip` and `ffmpeg-<version>-linux-arm64.zip` on that same `v*` tag.
- [ ] Zip root contains `ffmpeg` and `ffprobe`, mode executable after extract.
- [ ] `ffmpeg -encoders` lists `libx264` and `libvpx-vp9`.
- [ ] `ffmpeg -devices` lists `x11grab` and `pulse`.
- [ ] SHA256 is published next to the asset.
- [ ] XerahS no longer calls `BtbN/FFmpeg-Builds`, and the checker suffix is `linux-x64.zip` / `linux-arm64.zip`.
- [ ] Tools folder is `linux-x64` or `linux-arm64`, matching the CPU.

Signing identity, once, before the first FFmpeg-repo workflow:

- [ ] `release` environment on the repo that signs, with `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SIGNING_ENDPOINT`, `SIGNING_ACCOUNT_NAME`, `CERTIFICATE_PROFILE_NAME`.
- [ ] Federated credential subject includes that repo and environment.
- [ ] A trial run logs `Successfully signed` for `ffmpeg.exe`.
