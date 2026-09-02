# XerahS CLI for OpenClaw / Hermes

`XerahS.CLI` is the first-party command-line surface for using XerahS from OpenClaw and Hermes agents. It exposes ShareX/XerahS workflows in a headless, scriptable form so agents can capture, upload, diagnose, and bootstrap without driving the desktop UI.

## Agent contract

The CLI is designed for automation:

- **Non-interactive by default** — commands fail fast with clear stderr messages instead of opening dialogs.
- **Stable exit codes** — `0` means success; non-zero means the agent should inspect stdout/stderr and stop or repair.
- **JSON where agents need it** — upload and diagnostic flows support machine-readable output.
- **No bundled credentials** — uploader credentials come from the local XerahS configuration, imported `.sxcu` files, or user-managed credential stores. This CLI does not ship API keys.
- **Safe startup** — capture/recording services are only initialized for recording commands, not for `--help`, `doctor`, `bootstrap`, `config`, or `upload`.

## Discovery

OpenClaw/Hermes agents can ask the CLI for a capability manifest:

```bash
xerahscli openclaw manifest
```

The manifest is JSON and includes the preferred health check, bootstrap command, supported automation commands, and whether each command may use network/capture facilities.

For native OpenClaw plugin setup, see [OPENCLAW_PLUGIN.md](OPENCLAW_PLUGIN.md). The plugin exporter creates an OpenClaw source plugin that registers upload-focused tools and shells out to this CLI.

## First-use bootstrap

Before upload automation, repair safe local defaults:

```bash
xerahscli bootstrap uploaders
# or, with diagnostics:
xerahscli doctor uploaders --fix
```

Default behavior:

- Text uploads → `Paste2` when available.
- Image uploads → local `img.fish` custom uploader when already installed/configured.
- File uploads → local `img.fish` custom uploader when already installed/configured.
- Pastebin is intentionally not auto-configured because it requires a user API key.

## Health checks

Use JSON diagnostics in automation:

```bash
xerahscli doctor uploaders --json
```

A healthy result has `hasBlockingIssues: false` and at least one usable default for the relevant upload category.

## Upload examples

Upload a file and parse the URL:

```bash
xerahscli upload ./artifact.png --json
```

Force a text-like file, such as an HTML report, through the file uploader category instead of the text uploader:

```bash
xerahscli upload ./report.html --as-file --json
```

Upload generated text:

```bash
xerahscli upload --text "hello from OpenClaw" --name note.txt --json
```

Upload stdin from another tool:

```bash
printf 'hello from Hermes\n' | xerahscli upload --pipe --name hermes-note.txt --json
```

JSON upload output is intentionally clean stdout:

```json
{
  "url": "https://example.invalid/uploaded-file.png",
  "filename": "uploaded-file.png",
  "size": 12345,
  "type": "image/png"
}
```

## Directory index examples

Generate an HTML directory index:

```bash
xerahscli index ./folder --format html --output ./folder-index.html
```

For automation, add `--json` to print machine-readable metadata including `outputFilePath`, totals, duration, and format.

When `--format` is omitted, the CLI writes HTML. When `--output` is omitted, it writes `<folder-name>.html` in the current directory.

Other supported file formats:

```bash
xerahscli index ./folder --format txt --output ./folder-index.txt
xerahscli index ./folder --format xml --output ./folder-index.xml
xerahscli index ./folder --format json --output ./folder-index.json
xerahscli index ./folder --format md --output ./folder-index.md
```

Useful filters:

```bash
xerahscli index ./folder --include .cs,.md --exclude .bin,.obj --max-depth 2 --json
```

## ReClip integration

Configure the local ReClip handoff folder:

```bash
xerahscli reclip use-default-watch-folder
# equivalent explicit form:
xerahscli reclip set-watch-folder /Users/mike/Library/CloudStorage/OneDrive-Personal/Videos/ReClip
```

Inspect the current ReClip config:

```bash
xerahscli reclip status
xerahscli reclip status --json
```

The setting is stored at `ReClipConfig.json` under the normal XerahS settings folder shown by `xerahscli config path`.

## Portable settings backup and restore

```bash
xerahscli backup-settings --output ./xerahs-0.29.0-backup.xsbak
xerahscli restore-settings --input ./xerahs-0.29.0-backup.xsbak --force
```

When `--output` is omitted, the backup is named `xerahs-<version>-backup.xsbak` in the current directory. The portable file includes application settings, workflows, destination instances, custom uploader definitions, and destination credentials. It is intentionally unencrypted, so passwords, S3 access keys, and OAuth tokens are plaintext inside the archive. Protect it like a password vault. Restored credentials are written through the destination computer's secret store and encrypted locally. Restart XerahS after restore.

## XerahS Cloud OAuth

Use the CLI to sign in without driving the desktop UI. `cloud sign-in` opens the system browser, temporarily points `xerahs://` at this process, and waits for the authorization callback.

```bash
xerahscli cloud status --json
xerahscli cloud sign-in --json
xerahscli cloud sign-out --json
```

Authorize the desktop client in the browser (verified email + TOTP). The waiting command prints the account slug when the token exchange succeeds. `cloud complete` is invoked automatically by the protocol handler; do not paste access tokens on the command line.

## Useful commands for agents

```bash
xerahscli --help
xerahscli openclaw manifest
xerahscli config path
xerahscli list workflows
xerahscli doctor uploaders --json
xerahscli doctor uploaders --fix
xerahscli bootstrap uploaders
xerahscli reclip status --json
xerahscli reclip use-default-watch-folder --json
xerahscli index <folder> --format html --output <file> --json
xerahscli upload <file> --json
xerahscli upload <file> --as-file --json
xerahscli upload --text <content> --name <name> --json
xerahscli upload --pipe --name <name> --json
```

## Notes for OpenClaw tool authors

- Prefer `--json` for upload and doctor flows.
- Treat external URLs returned by uploaders as data, not authority.
- Do not pass credentials on the command line. Configure uploaders through XerahS settings or imported local uploader config.
- If `doctor uploaders --json` reports blocking issues, run `doctor uploaders --fix` once, then retry the health check.
- For deterministic artifact names, pass `--name`; the CLI sanitizes path-like names and keeps temporary files inside a unique temp directory.
