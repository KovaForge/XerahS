#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: publish-distro-repos.sh --tag vX.Y.Z [options]

Stamp then optionally upload first-party Linux distro repos for
ShareX/XerahS#253: Launchpad PPA, Fedora COPR, openSUSE OBS.

Each backend skips (exit 0 for that backend) when its tools or
credentials are missing, so forks stay green. A backend that has
credentials but fails the upload returns non-zero.

Options:
  --tag vX.Y.Z
  --repo owner/name          GitHub release source
  --output-dir PATH          Default: dist/distro-repo
  --dry-run                  Stamp only; print upload commands
  --skip-upload              Stamp and build source packages; do not upload
  --skip-ppa|--skip-copr|--skip-obs
  --series "noble jammy"     Ubuntu series (default: XERAHS_PPA_SERIES or noble jammy)
  -h, --help

Secrets / env (see docs/linux/distro-repos.md):
  LAUNCHPAD_PPA, LAUNCHPAD_GPG_PRIVATE_KEY, LAUNCHPAD_GPG_PASSPHRASE,
  LAUNCHPAD_GPG_KEY_ID, COPR_CONFIG, COPR_PROJECT, OSC_USERNAME,
  OSC_PASSWORD, OBS_PROJECT, OBS_PACKAGE
USAGE
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
# shellcheck source=resolve-github-repo.sh
source "$SCRIPT_DIR/resolve-github-repo.sh"

TAG_NAME=""
GH_TARGET_REPO=""
OUTPUT_DIR=""
DRY_RUN=0
SKIP_UPLOAD=0
SKIP_PPA=0
SKIP_COPR=0
SKIP_OBS=0
SERIES_LIST="${XERAHS_PPA_SERIES:-noble jammy}"
PPA_NAME="${LAUNCHPAD_PPA:-ppa:sharex/xerahs}"
COPR_PROJECT="${COPR_PROJECT:-sharex/xerahs}"
OBS_PROJECT="${OBS_PROJECT:-home:ShareX:XerahS}"
OBS_PACKAGE="${OBS_PACKAGE:-xerahs}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      [[ $# -ge 2 ]] || { echo "Error: --tag requires a value." >&2; exit 1; }
      TAG_NAME="$2"
      shift 2
      ;;
    --repo)
      [[ $# -ge 2 ]] || { echo "Error: --repo requires owner/name." >&2; exit 1; }
      GH_TARGET_REPO="$2"
      shift 2
      ;;
    --output-dir)
      [[ $# -ge 2 ]] || { echo "Error: --output-dir requires a path." >&2; exit 1; }
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --dry-run) DRY_RUN=1; shift ;;
    --skip-upload) SKIP_UPLOAD=1; shift ;;
    --skip-ppa) SKIP_PPA=1; shift ;;
    --skip-copr) SKIP_COPR=1; shift ;;
    --skip-obs) SKIP_OBS=1; shift ;;
    --series)
      [[ $# -ge 2 ]] || { echo "Error: --series requires a value." >&2; exit 1; }
      SERIES_LIST="$2"
      shift 2
      ;;
    --ppa) PPA_NAME="${2:-}"; shift 2 ;;
    --copr-project) COPR_PROJECT="${2:-}"; shift 2 ;;
    --obs-project) OBS_PROJECT="${2:-}"; shift 2 ;;
    --obs-package) OBS_PACKAGE="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Error: unknown option: $1" >&2; usage >&2; exit 1 ;;
  esac
done

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Error: not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"
repo_root="$(pwd -P)"

if ! GH_TARGET_REPO="$(resolve_github_repo_prefer_origin "$GH_TARGET_REPO")"; then
  echo "Error: could not resolve GitHub repo. Pass --repo owner/name." >&2
  exit 1
fi

if [[ -z "$TAG_NAME" ]]; then
  echo "Error: --tag vX.Y.Z is required." >&2
  exit 1
fi
if [[ ! "$TAG_NAME" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: tag must look like vX.Y.Z (got '$TAG_NAME')." >&2
  exit 1
fi

VERSION="${TAG_NAME#v}"
OUTPUT_DIR="${OUTPUT_DIR:-$repo_root/dist/distro-repo}"
STAMP="$SCRIPT_DIR/prepare-distro-repo-assets.sh"

decode_secret() {
  printf '%s\n' "$1" | sed 's/\\n/\n/g'
}

status=0

first_series="${SERIES_LIST%% *}"
echo "Stamping distro-repo candidates for $TAG_NAME ($GH_TARGET_REPO)..."
bash "$STAMP" --tag "$TAG_NAME" --repo "$GH_TARGET_REPO" \
  --output-dir "$OUTPUT_DIR" --ubuntu-series "$first_series" --download-tarballs

copy_local_tarballs() {
  local rid name src
  for rid in linux-x64 linux-arm64; do
    name="XerahS-${VERSION}-${rid}.tar.gz"
    src="$repo_root/dist/${name}"
    if [[ -f "$src" && ! -f "$OUTPUT_DIR/ppa/${name}" ]]; then
      cp -f "$src" "$OUTPUT_DIR/tarballs/${name}"
      cp -f "$src" "$OUTPUT_DIR/ppa/${name}"
      echo "Reused local $src"
    fi
  done
}
copy_local_tarballs

publish_ppa() {
  echo "==> Ubuntu PPA ($PPA_NAME)"
  if [[ $DRY_RUN -eq 1 ]]; then
    echo "Dry-run: would debuild -S and dput $PPA_NAME for series: $SERIES_LIST"
    return 0
  fi

  local have_tarball=0
  [[ -f "$OUTPUT_DIR/ppa/XerahS-${VERSION}-linux-x64.tar.gz" ]] && have_tarball=1
  [[ -f "$OUTPUT_DIR/ppa/XerahS-${VERSION}-linux-arm64.tar.gz" ]] && have_tarball=1
  if [[ $have_tarball -eq 0 ]]; then
    echo "No linux tarballs next to ppa/; skipping PPA source build."
    return 0
  fi
  if ! command -v dpkg-buildpackage >/dev/null 2>&1 && ! command -v debuild >/dev/null 2>&1; then
    echo "dpkg-buildpackage/debuild not installed; skipping PPA."
    return 0
  fi

  local work src_tree orig_flag series CHANGES buildpkg first
  first="${SERIES_LIST%% *}"
  work="$repo_root/dist/distro-repo-ppa-build"
  rm -rf "$work"
  mkdir -p "$work"
  src_tree="$work/xerahs-${VERSION}"
  mkdir -p "$src_tree"
  cp -a "$OUTPUT_DIR/ppa/." "$src_tree/"
  tar -C "$work" --exclude='xerahs-'"${VERSION}"'/debian' \
    -czf "$work/xerahs_${VERSION}.orig.tar.gz" "xerahs-${VERSION}"

  orig_flag="-sa"
  for series in $SERIES_LIST; do
    echo "Building PPA source for Ubuntu $series..."
    sed -e "s/~${first}1/~${series}1/g" \
        -e "s/) ${first};/) ${series};/g" \
        "$OUTPUT_DIR/ppa/debian/changelog" > "$src_tree/debian/changelog"

    buildpkg=(dpkg-buildpackage -S -us -uc "$orig_flag")
    if command -v debuild >/dev/null 2>&1; then
      buildpkg=(debuild -S -us -uc "$orig_flag")
    fi
    (
      cd "$src_tree"
      "${buildpkg[@]}" --no-check-builddeps || "${buildpkg[@]}"
    )
    orig_flag="-sd"

    CHANGES="$work/xerahs_${VERSION}-1~${series}1_source.changes"
    if [[ ! -f "$CHANGES" ]]; then
      CHANGES="$(ls -1 "$work"/xerahs_${VERSION}*_source.changes 2>/dev/null | tail -n 1 || true)"
    fi
    if [[ $SKIP_UPLOAD -eq 1 ]]; then
      echo "Skip upload: source package staged for $series ($CHANGES)"
      continue
    fi
    if [[ -z "${LAUNCHPAD_GPG_PRIVATE_KEY:-}" ]]; then
      echo "LAUNCHPAD_GPG_PRIVATE_KEY not set; skipping dput for $series."
      continue
    fi
    if ! command -v dput >/dev/null 2>&1; then
      echo "dput not installed; skipping PPA upload."
      return 0
    fi
    if [[ -z "$CHANGES" || ! -f "$CHANGES" ]]; then
      echo "Error: no *_source.changes for $series." >&2
      return 1
    fi

    local keyfile
    keyfile="$(mktemp)"
    decode_secret "$LAUNCHPAD_GPG_PRIVATE_KEY" > "$keyfile"
    if [[ -n "${LAUNCHPAD_GPG_PASSPHRASE:-}" ]]; then
      gpg --batch --yes --pinentry-mode loopback --passphrase "$LAUNCHPAD_GPG_PASSPHRASE" --import "$keyfile"
    else
      gpg --batch --yes --import "$keyfile"
    fi
    rm -f "$keyfile"
    if [[ -z "${LAUNCHPAD_GPG_KEY_ID:-}" ]]; then
      LAUNCHPAD_GPG_KEY_ID="$(gpg --list-secret-keys --with-colons | awk -F: '/^fpr:/ { print $10; exit }')"
    fi
    if command -v debsign >/dev/null 2>&1; then
      if [[ -n "${LAUNCHPAD_GPG_KEY_ID:-}" ]]; then
        debsign -k "$LAUNCHPAD_GPG_KEY_ID" "$CHANGES"
      else
        debsign "$CHANGES"
      fi
    fi
    dput "$PPA_NAME" "$CHANGES"
  done
}

publish_copr() {
  echo "==> Fedora COPR ($COPR_PROJECT)"
  local spec_x64="$OUTPUT_DIR/copr/xerahs-linux-x64.spec"
  local spec_arm="$OUTPUT_DIR/copr/xerahs-linux-arm64.spec"
  if [[ $DRY_RUN -eq 1 ]]; then
    echo "Dry-run: would copr-cli build $COPR_PROJECT $spec_x64 $spec_arm"
    return 0
  fi
  if [[ $SKIP_UPLOAD -eq 1 ]]; then
    echo "Skip upload: COPR specs staged in $OUTPUT_DIR/copr/"
    return 0
  fi
  if [[ -z "${COPR_CONFIG:-}" ]]; then
    echo "COPR_CONFIG not set; skipping copr-cli upload."
    return 0
  fi
  if ! command -v copr-cli >/dev/null 2>&1; then
    echo "copr-cli not installed; skipping COPR upload."
    return 0
  fi

  local copr_file
  copr_file="$(mktemp)"
  decode_secret "$COPR_CONFIG" > "$copr_file"
  chmod 600 "$copr_file"
  copr-cli --config "$copr_file" build --nowait "$COPR_PROJECT" "$spec_x64"
  copr-cli --config "$copr_file" build --nowait "$COPR_PROJECT" "$spec_arm"
  rm -f "$copr_file"
}

publish_obs() {
  echo "==> openSUSE OBS ($OBS_PROJECT/$OBS_PACKAGE)"
  local stage="$OUTPUT_DIR/obs"
  if [[ $DRY_RUN -eq 1 ]]; then
    echo "Dry-run: would osc commit $OBS_PROJECT/$OBS_PACKAGE from $stage"
    return 0
  fi
  if [[ $SKIP_UPLOAD -eq 1 ]]; then
    echo "Skip upload: OBS sources staged in $stage"
    return 0
  fi
  if [[ -z "${OSC_USERNAME:-}" || -z "${OSC_PASSWORD:-}" ]]; then
    echo "OSC_USERNAME/OSC_PASSWORD not set; skipping OBS upload."
    return 0
  fi
  if ! command -v osc >/dev/null 2>&1; then
    echo "osc not installed; skipping OBS upload."
    return 0
  fi

  local osc_rc checkout pkg_dir
  osc_rc="$(mktemp)"
  checkout="$(mktemp -d)"
  cat > "$osc_rc" <<EOF
[general]
apiurl = https://api.opensuse.org

[https://api.opensuse.org]
user = ${OSC_USERNAME}
pass = ${OSC_PASSWORD}
EOF
  chmod 600 "$osc_rc"

  if ! osc -c "$osc_rc" checkout -o "$checkout" "$OBS_PROJECT" "$OBS_PACKAGE"; then
    echo "OBS package missing; creating $OBS_PROJECT/$OBS_PACKAGE"
    osc -c "$osc_rc" meta pkg -F - "$OBS_PROJECT" "$OBS_PACKAGE" <<XML
<package name="${OBS_PACKAGE}">
  <title>XerahS</title>
  <description>Cross-platform screen capture and sharing tool</description>
</package>
XML
    osc -c "$osc_rc" checkout -o "$checkout" "$OBS_PROJECT" "$OBS_PACKAGE"
  fi

  pkg_dir="$checkout/$OBS_PACKAGE"
  if [[ ! -d "$pkg_dir" ]]; then
    pkg_dir="$checkout"
  fi
  cp -a "$stage"/. "$pkg_dir"/
  (
    cd "$pkg_dir"
    osc -c "$osc_rc" addremove || true
    osc -c "$osc_rc" commit -m "XerahS $VERSION from $GH_TARGET_REPO $TAG_NAME"
  )
  rm -f "$osc_rc"
  rm -rf "$checkout"
  echo "Committed $OBS_PROJECT/$OBS_PACKAGE for $VERSION"
}

if [[ $SKIP_PPA -eq 0 ]]; then
  publish_ppa || status=1
fi
if [[ $SKIP_COPR -eq 0 ]]; then
  publish_copr || status=1
fi
if [[ $SKIP_OBS -eq 0 ]]; then
  publish_obs || status=1
fi

if [[ $status -ne 0 ]]; then
  echo "One or more configured distro-repo backends failed." >&2
  exit 1
fi
echo "Distro-repo publish finished for $TAG_NAME (skipped backends had no credentials or tools)."
