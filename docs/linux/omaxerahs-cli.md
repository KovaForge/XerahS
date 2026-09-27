# omaxerahs: the XerahS command line

`omaxerahs` ships with native Linux packages (`/usr/bin/omaxerahs`) next to `xerahs`.
It is the scriptable surface of XerahS for shell scripts, the Omarchy bar plugin,
and AI coding agents.

## Contract

- Every command prints exactly one JSON object to stdout: `{"schemaVersion":1,"ok":true,...}`
  or `{"schemaVersion":1,"ok":false,"error":{"code":"...","message":"..."}}` with exit code 1.
- Human hints go to stderr; `--json` suppresses them.
- `omaxerahs capabilities` lists the capability ids this build supports.

## Commands

| Command | Purpose |
|---------|---------|
| `doctor` | Report whether an image upload destination is ready. |
| `upload <path>` | Upload a file through the configured image destination. |
| `workflow list` / `workflow show <workflow>` | Inspect workflows (id, job, after-capture tasks, image effects). |
| `workflow tasks <workflow> --add <task> --remove <task>` | Turn after-capture tasks on or off. |
| `workflow task-names` | List valid after-capture task names. |
| `effects import <file> --workflow <workflow> [--no-enable]` | Replace a workflow's image effects with a `.xsie` or ShareX `.sxie` preset. |
| `effects show / enable / disable / clear --workflow <workflow>` | Inspect or change a workflow's image effects. |
| `image resize / convert / watermark <files...>` | Batch image tools (same engine as the Image Resizer, Converter and Watermark windows). |
| `skill install / uninstall / path` | Manage the `xerahs` agent skill. |

`<workflow>` is a workflow id, a unique id prefix (6+ characters), or a workflow name.

Commands that change settings save `WorkflowsConfig` and send `--reload-workflows` to a running
XerahS over the single-instance channel, so the app picks up the change without raising its
window and does not overwrite it later. The response field `appNotified` reports whether a running
instance was reached; when XerahS is not running the change applies on its next start.

## Agent skill

`omaxerahs skill install` writes the bundled `xerahs` skill to
`$XDG_DATA_HOME/xerahs/agents/skills/xerahs` and symlinks it into `~/.agents/skills` and into the
skill directories of agents that are set up (`~/.claude`, `~/.codex`, `~/.hermes` and its profiles,
`~/.config/opencode`). This mirrors how Omarchy links its own skills. Existing `xerahs` skills that
were not created by `omaxerahs` are left untouched, and `skill uninstall` removes only its own links.

On Omarchy, XerahS runs `omaxerahs skill install` in the background at startup, so the agent launched
by `omarchy agent` always has a skill that matches the installed XerahS. Example request an agent can
then fulfil: "configure ~/Downloads/GoldBorder.sxie in XerahS" becomes
`omaxerahs effects import ~/Downloads/GoldBorder.sxie --workflow "Region capture"`.
