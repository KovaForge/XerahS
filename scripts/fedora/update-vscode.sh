#!/usr/bin/env bash
set -euo pipefail

package_name="code"
repo_id="code"
repo_baseurl="https://packages.microsoft.com/yumrepos/vscode"
assume_yes=1
dry_run=0

usage() {
  cat <<'EOF'
Usage: update-vscode.sh [--dry-run] [--no-assume-yes]

Updates Visual Studio Code from the same DNF repository it is installed from:
  package: code
  repo id: code
  source:  https://packages.microsoft.com/yumrepos/vscode

Options:
  --dry-run        Show the DNF transaction without applying it.
  --no-assume-yes  Let DNF ask before applying the update.
  -h, --help       Show this help.
EOF
}

die() {
  echo "error: $*" >&2
  exit 1
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "$1 is required but was not found."
}

run_as_root() {
  if [[ "${EUID}" -eq 0 ]]; then
    "$@"
  elif command -v sudo >/dev/null 2>&1; then
    sudo "$@"
  else
    die "root privileges are required, and sudo was not found."
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run)
      dry_run=1
      ;;
    --no-assume-yes)
      assume_yes=0
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "unknown option: $1"
      ;;
  esac
  shift
done

need_cmd dnf
need_cmd rpm

rpm -q "${package_name}" >/dev/null 2>&1 || die "Visual Studio Code package '${package_name}' is not installed by RPM."

installed_from_repo="$(dnf repoquery --installed --qf '%{from_repo}' "${package_name}" 2>/dev/null | head -n 1)"
if [[ "${installed_from_repo}" != "${repo_id}" ]]; then
  die "installed '${package_name}' came from repo '${installed_from_repo:-unknown}', expected '${repo_id}'."
fi

repo_file=""
while IFS= read -r candidate; do
  if grep -Eq "^[[:space:]]*\\[${repo_id}\\][[:space:]]*$" "${candidate}" \
    && grep -Eq "^[[:space:]]*baseurl=${repo_baseurl}[[:space:]]*$" "${candidate}"; then
    repo_file="${candidate}"
    break
  fi
done < <(find /etc/yum.repos.d -maxdepth 1 -type f -name '*.repo' -print 2>/dev/null)

if [[ -z "${repo_file}" ]]; then
  die "repo '${repo_id}' with baseurl '${repo_baseurl}' was not found under /etc/yum.repos.d."
fi

current_version="$(rpm -q "${package_name}")"
available_version="$(dnf repoquery \
  --refresh \
  --disablerepo='*' \
  --enablerepo="${repo_id}" \
  --arch="$(uname -m),noarch" \
  --latest-limit=1 \
  --qf '%{name}-%{version}-%{release}.%{arch}' \
  "${package_name}" 2>/dev/null | head -n 1)"

if [[ -z "${available_version}" ]]; then
  die "could not find '${package_name}' in repo '${repo_id}'."
fi

echo "Installed: ${current_version}"
echo "Available: ${available_version}"
echo "Repo:      ${repo_id} (${repo_file})"
echo "Source:    ${repo_baseurl}"

dnf_args=(
  --refresh
  --disablerepo='*'
  --enablerepo="${repo_id}"
  upgrade
  --best
)

if [[ "${dry_run}" -eq 1 ]]; then
  dnf_args+=(--assumeno)
elif [[ "${assume_yes}" -eq 1 ]]; then
  dnf_args+=(--assumeyes)
fi

dnf_args+=("${package_name}")

run_as_root dnf "${dnf_args[@]}"
