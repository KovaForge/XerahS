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
xerahs openclaw manifest
```

The manifest is JSON and includes the preferred health check, bootstrap command, supported automation commands, and whether each command may use network/capture facilities.

## First-use bootstrap

Before upload automation, repair safe local defaults:

```bash
xerahs bootstrap uploaders
# or, with diagnostics:
xerahs doctor uploaders --fix
```

Default behavior:

- Text uploads → `Paste2` when available.
- Image uploads → local `img.fish` custom uploader when already installed/configured.
- File uploads → local `img.fish` custom uploader when already installed/configured.
- Pastebin is intentionally not auto-configured because it requires a user API key.

## Health checks

Use JSON diagnostics in automation:

```bash
xerahs doctor uploaders --json
```

A healthy result has `hasBlockingIssues: false` and at least one usable default for the relevant upload category.

## Upload examples

Upload a file and parse the URL:

```bash
xerahs upload ./artifact.png --json
```

Force a text-like file, such as an HTML report, through the file uploader category instead of the text uploader:

```bash
xerahs upload ./report.html --as-file --json
```

Upload generated text:

```bash
xerahs upload --text "hello from OpenClaw" --name note.txt --json
```

Upload stdin from another tool:

```bash
printf 'hello from Hermes\n' | xerahs upload --pipe --name hermes-note.txt --json
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

## ReClip integration

Configure the local ReClip handoff folder:

```bash
xerahs reclip use-default-watch-folder
# equivalent explicit form:
xerahs reclip set-watch-folder /Users/mike/Library/CloudStorage/OneDrive-Personal/Videos/ReClip
```

Inspect the current ReClip config:

```bash
xerahs reclip status
xerahs reclip status --json
```

The setting is stored at `ReClipConfig.json` under the normal XerahS settings folder shown by `xerahs config path`.

## Useful commands for agents

```bash
xerahs --help
xerahs openclaw manifest
xerahs config path
xerahs list workflows
xerahs doctor uploaders --json
xerahs doctor uploaders --fix
xerahs bootstrap uploaders
xerahs reclip status --json
xerahs reclip use-default-watch-folder --json
xerahs upload <file> --json
xerahs upload <file> --as-file --json
xerahs upload --text <content> --name <name> --json
xerahs upload --pipe --name <name> --json
```

## Notes for OpenClaw tool authors

- Prefer `--json` for upload and doctor flows.
- Treat external URLs returned by uploaders as data, not authority.
- Do not pass credentials on the command line. Configure uploaders through XerahS settings or imported local uploader config.
- If `doctor uploaders --json` reports blocking issues, run `doctor uploaders --fix` once, then retry the health check.
- For deterministic artifact names, pass `--name`; the CLI sanitizes path-like names and keeps temporary files inside a unique temp directory.
