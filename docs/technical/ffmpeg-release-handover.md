# FFmpeg release handover

**Date:** 2026-09-30
**Status:** Linux releases implemented (2026-09-30). Windows release process and ShareX unchanged.
**Audience:** Whoever next publishes ShareX FFmpeg builds, Windows or Linux.

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
| [ShareX/FFmpeg](https://github.com/ShareX/FFmpeg) | Release bucket. Windows tags `v8.1`, `v8.0`, `v7.1`, … are uploaded by hand. `.github/workflows/linux-release.yml` publishes `v<version>-linux` (section 5). | `ffmpeg-8.1-win-x64.zip`, `ffmpeg-8.1-win-arm64.zip`. Each zip contains one file, `ffmpeg.exe`, at the archive root. |
| [ShareX/FFmpeg-Builds](https://github.com/ShareX/FFmpeg-Builds) | Fork of [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds). `master` is the Windows fork, frozen at 2024-06-25 with its workflow disabled. Branch `linux` is current upstream plus `sharex/` Linux packaging (section 5). | `ffmpeg-latest-win64.zip`, `ffmpeg-7.0-win64.zip`, and the `win32` pair. Last auto-build 2024-06-28. |

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

### Linux releases (what XerahS Linux matches)

```text
tag v8.1-linux   (never marked latest)
ffmpeg-8.1-linux-x64.zip
ffmpeg-8.1-linux-arm64.zip
SHA256SUMS
SHA256SUMS.sig
```

The zip names follow the Windows pattern `ffmpeg-{version}-{rid}.zip`. They live on a separate tag because `v8.1` is an immutable release: no asset can be added to it after publishing. See section 5.

Before 0.32.3, XerahS Linux downloaded BtbN's `ffmpeg-n8.1-latest-linux64-gpl-8.1.tar.xz` from `BtbN/FFmpeg-Builds`. That path is removed.

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

Running `Add-manifest.bat` with the Downloads manifest would drop `PerMonitorV2`. Of the three helpers, only `Extract-manifest.bat` is still useful, as a check. Leave the toolchain manifest in place. Keep `Extract-manifest.bat` as a check that `PerMonitorV2` is still present.

`ShareX/FFmpeg-Builds` has a copy of the weaker manifest at the repo root (`ffmpeg.exe.manifest`, added 2022-09-10). No script references it. The ShareX build workflow does not run `mt.exe` either.

Linux has no PE manifest. The equivalent requirement is which devices are compiled in (`x11grab`, `pulse`, and later `pipewire`).

---

## 4. How XerahS downloads FFmpeg

`DownloadLatestAsync` in `src/desktop/core/XerahS.Common/FFmpegDownloader.cs`:

- **Windows and macOS** call `FFmpegUpdateChecker("ShareX", "FFmpeg")`, which reads `/releases/latest`, download the zip and extract `ffmpeg.exe` / `ffprobe.exe` (or `ffmpeg` / `ffprobe`). When `ffprobe.exe` is missing on Windows, `DownloadFFprobeFallbackAsync` fetches one from `System233/ffmpeg-msvc-prebuilt`.
- **Linux** calls `DownloadLinuxAsync`:
  1. Lists `repos/ShareX/FFmpeg/releases` and picks the newest published, non-prerelease release whose tag matches `v<major>.<minor>-linux[.<n>]` and that has `ffmpeg-<major>.<minor>-linux-x64.zip` (or `-linux-arm64.zip`), `SHA256SUMS` and `SHA256SUMS.sig` (`SelectLinuxRelease`).
  2. Downloads `SHA256SUMS` and `SHA256SUMS.sig` and checks the ECDSA P-256 signature against the key built into `FFmpegReleaseVerifier.TrustedKeys`.
  3. Downloads the zip and checks its SHA-256 against the signed line for that exact file name.
  4. Extracts `ffmpeg` and `ffprobe` and marks them executable.

  Any failed check stops the install. There is no unsigned fallback.

Install folder: `PathsManager.GetToolsArchitectureFolderName()`.

- Windows: `win-x64`, `win-arm64`, `win-x86`
- macOS: `macos64`
- Linux: `linux-x64` or `linux-arm64`, the same suffixes as the assets.

`GetToolPath` still finds an FFmpeg installed by 0.32.2 in `Tools/linux64`. Plugins keep `Plugins/linux64` (`GetArchitectureFolderName()`), so installed plugins don't move.

`FFmpegUpdateChecker` maps Linux to `linux-x64.zip` / `linux-arm64.zip` for consistency. Linux doesn't use it to download.

Tests: `tests/XerahS.Tests/Common/FFmpegLinuxDownloadTests.cs` covers release selection, signature and hash checks, and the pinned key. It also has an `[Explicit]` network test that installs the real release and requires `libx264`, `libvpx-vp9` and `ffprobe`.

---

## 5. Linux releases

### Why a separate tag

- **`v8.1` can't take more assets.** It is an immutable release (`isImmutable: true`), so Linux zips can't be added to it.
- **Windows is made by hand.** Jaex builds and uploads the Windows zips.
- **ShareX looks at two things only:**
  - its build workflow downloads `releases/download/v8.1/ffmpeg-8.1-win-<arch>.zip` by exact URL;
  - its in-app `FFmpegUpdateChecker` reads `/releases/latest`, with no pre-releases.

  Windows XerahS also reads `/releases/latest`.

The Linux build is therefore published as its own release, `v<version>-linux`, created with `--latest=false`. The `latest` release stays Jaex's Windows release. ShareX and Windows XerahS see no change. The publish job fails if `/releases/latest` changes during the run.

ShareX's `new Version(tag.Substring(1))` would throw on `8.1-linux` if it ever became `latest`. That is why the check exists.

### Build: `ShareX/FFmpeg-Builds`, branch `linux`

- **Base.** Upstream BtbN master, 374 commits newer than the fork's 2024 `master`. It includes the `8.1` and `9.0` addins.
- **Untouched.** `master`, which is Jaex's Windows fork and still has `--disable-ffprobe`, `zip -j ffmpeg.exe` and a disabled workflow.
- **Added:** `sharex/`.
  - `build-linux.sh <linux64|linuxarm64> <version> <out>` pulls BtbN's `ghcr.io/btbn/ffmpeg-builds/<target>-gpl-<version>` image and records its digest. It builds FFmpeg from `release/<version>` with ffprobe, then writes a flat zip holding `ffmpeg`, `ffprobe` and `LICENSE.txt`, plus `build-info-<rid>.txt`.
  - `verify-linux.sh <zip>` checks:
    - the zip layout and executable bits;
    - `libx264`, `libx265`, `libvpx-vp9`, `libaom-av1`;
    - `x11grab`, `pulse`, `drawtext`;
    - that the newest glibc symbol is at most `GLIBC_2.28`.
- **Removed:** the upstream workflows on this branch, so nothing there builds or pushes images by itself.
- **Updating from upstream:** `git fetch https://github.com/BtbN/FFmpeg-Builds.git master && git merge FETCH_HEAD`, keeping the workflow deletions.

The toolchain and dependency images are BtbN's. The FFmpeg source and packaging are ours. Each release's notes record the image digest and the `FFmpeg-Builds` commit.

### Publish: `ShareX/FFmpeg`, `.github/workflows/linux-release.yml`

The workflow is started manually (`workflow_dispatch`). Inputs:

- `version` (for example `8.1`);
- `builds_ref` (default `linux`);
- `tag` (default `v<version>-linux`; use `v<version>-linux.2` for a rebuild);
- `publish` (default off).

1. **build**: runs `build-linux.sh` for `linux64` and `linuxarm64` on `ubuntu-24.04`.
2. **verify**: runs `verify-linux.sh` on native runners, `ubuntu-24.04` for x64 and `ubuntu-24.04-arm` for arm64.
3. **publish** (only with `publish: true`, environment `release`, `master` only):
   - writes `SHA256SUMS`;
   - signs it with `LINUX_SIGNING_KEY` via `openssl dgst -sha256 -sign`;
   - checks the signature against `keys/linux-release-2026.pub.pem`;
   - adds GitHub build-provenance attestations for both zips;
   - runs `gh release create --latest=false`;
   - checks that `/releases/latest` is unchanged.

Run a build-only test first (`publish` off). When Jaex publishes a new Windows version (for example `v8.2`), run this workflow with `version: 8.2` to add `v8.2-linux`.

### Signing

- **What gets signed.** Authenticode can't sign ELF binaries (section 6). The Linux release signs `SHA256SUMS` with ECDSA P-256 instead, and XerahS checks that signature with the pinned public key before it installs anything.
- **Why a checksum alone isn't enough.** A checksum published on the same release proves nothing to someone who can replace the release files.
- **Where the key lives.** The public key is `ShareX/FFmpeg` `keys/linux-release-2026.pub.pem`, also in `FFmpegReleaseVerifier.TrustedKeys`. The private key is the `release` environment secret `LINUX_SIGNING_KEY`, with an offline copy kept by the maintainer. The rotation steps are in `keys/README.md`.
- **Checking a release by hand.** Run `openssl dgst -sha256 -verify linux-release-2026.pub.pem -signature SHA256SUMS.sig SHA256SUMS`, then `sha256sum -c SHA256SUMS` and `gh attestation verify <zip> -R ShareX/FFmpeg`.

### Not done

- **PipeWire.** BtbN doesn't build a PipeWire input device, so Wayland capture keeps using GStreamer or `wf-recorder` (see [FFMPEG.md](FFMPEG.md)).

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

This signer is Authenticode. It signs PE files. A Linux `ffmpeg` / `ffprobe` ELF cannot go through `azure/artifact-signing-action`. Linux releases sign `SHA256SUMS` instead (section 5).

---

## 7. Checklist

Windows zip, before upload:

- [ ] `ffmpeg.exe` contains `PerMonitorV2` (`Extract-manifest.bat`, or strings). The Downloads manifest was not reapplied.
- [ ] `signtool verify /pa /v ffmpeg.exe` succeeds, timestamped.
- [ ] `ffprobe.exe` is either absent (current contract) or signed in the same step.
- [ ] Asset name is `ffmpeg-<version>-win-x64.zip` or `ffmpeg-<version>-win-arm64.zip` on a `v*` tag of `ShareX/FFmpeg`.
- [ ] Zip root is the exe, not a nested `bin/` directory.

Linux release (the workflow checks all of this):

- [ ] The tag is `v<version>-linux` (or `.2`, `.3` for rebuilds) and is not marked latest. `/releases/latest` is still the Windows `v<version>`.
- [ ] The assets are `ffmpeg-<version>-linux-x64.zip`, `ffmpeg-<version>-linux-arm64.zip`, `SHA256SUMS` and `SHA256SUMS.sig`.
- [ ] The zip root holds `ffmpeg`, `ffprobe` and `LICENSE.txt`, and the binaries are executable after unzip.
- [ ] `ffmpeg -encoders` lists `libx264`, `libx265`, `libvpx-vp9` and `libaom-av1`. `-devices` lists `x11grab` and `pulse`. `-filters` lists `drawtext`.
- [ ] The newest glibc symbol is `GLIBC_2.28` or older.
- [ ] `SHA256SUMS.sig` verifies with `keys/linux-release-2026.pub.pem`.

Signing identity, once, before the first FFmpeg-repo workflow:

- [ ] `release` environment on the repo that signs, with `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SIGNING_ENDPOINT`, `SIGNING_ACCOUNT_NAME`, `CERTIFICATE_PROFILE_NAME`.
- [ ] Federated credential subject includes that repo and environment.
- [ ] A trial run logs `Successfully signed` for `ffmpeg.exe`.
