# Scheduling the blog draft (once per day)

The full skill (git history to Summary, Features, Fixes, prose) can be run in Cursor by an agent, or automated with Cursor Automations (scheduled cloud agent). You can also schedule only the draft-creation step via GitHub Actions, Task Scheduler, or cron.

## GitHub Actions (committed in this repo)

This repo includes a scheduled workflow at `.github/workflows/draft-blog-post-daily.yml`.

- Runs every day at `16:00 UTC`.
- Checks out `develop`.
- Runs `.ai/skills/draft-blog-post/scripts/run-daily-draft.ps1 -IncludePreviousDay`.
- Ensures draft files exist for both the current UTC+8 day and the previous UTC+8 day.
- Commits and pushes `docs/blog/...` changes back to `develop` only when new draft files were created.

This workflow only creates missing draft files. It does not populate blog content from git history.

## Cursor Automations (recommended for the full skill)

Cursor has Automations: cloud agents that run on a schedule (including custom cron) or on events (GitHub, Slack, webhooks, etc.). You can run the full blog-drafting workflow once per day without opening the editor.

- Docs: [cursor.com/docs/cloud-agent/automations](https://cursor.com/docs/cloud-agent/automations)
- Create: [cursor.com/automations](https://cursor.com/automations) or start from a [marketplace template](https://cursor.com/marketplace/automations)

Setup:

1. Create a new automation and choose the Scheduled trigger.
2. Set the schedule.
3. Select the `XerahS` repository and the `develop` branch.
4. In the prompt, instruct the agent to run the `draft-blog-post` skill for today (UTC+8). For example:

   ```text
   Run the draft-blog-post skill for today (UTC+8). Follow the skill at .ai/skills/draft-blog-post/SKILL.md:
   - Resolve today's date in UTC+8 and ensure docs/blog/YYYY/YYYY-MM/blog-YYYYMMDD.md exists (use the upsert script if needed).
   - Gather git log (author date in UTC+8) for today from this repo and the ShareX.ImageEditor submodule (and XerahS.Editor if present). Use --since="<today>T00:00:00+08:00" --until="<tomorrow>T00:00:00+08:00" so author dates fall on the current UTC+8 day.
   - Populate all sections (Summary, Features, Fixes, Build and Tooling, Commits Reviewed, Notes) from real commits only. No placeholders.
   - Commit only that day's blog file with message "[vX.Y.Z] [Docs] Add YYYY-MM-DD <short description> blog draft." Do not push unless the instructions say to.
   ```

5. Enable the write capability you need so the agent can commit.
6. Save and enable the automation.

Automations are billed as cloud agent usage (Pro/Teams). Scheduled runs may start with a short delay but not before the scheduled time.

## What a script-only scheduled job does

If you use GitHub Actions, Task Scheduler, or cron, the job only runs the draft-creation script:

- Runs once per day.
- Ensures `docs/blog/YYYY/YYYY-MM/blog-YYYYMMDD.md` exists for the target UTC+8 day.
- With `-IncludePreviousDay`, it also ensures the previous UTC+8 day exists.
- Does not populate content from git history.

## Windows (Task Scheduler)

1. Open Task Scheduler and create a task.
2. Trigger it daily at a fixed time.
3. Use `powershell.exe` as the program.
4. Use these arguments:

   ```text
   -NoProfile -ExecutionPolicy Bypass -File "C:\Users\liveu\source\repos\ShareX Team\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1" -IncludePreviousDay
   ```

5. Optionally set Start in. `run-daily-draft.ps1` switches to the repo root itself.

One-time test from PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\liveu\source\repos\ShareX Team\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1" -IncludePreviousDay
```

## Linux / WSL / macOS (cron)

Example for 00:05 in a UTC+8 timezone:

```cron
5 0 * * * TZ=Asia/Shanghai powershell -NoProfile -ExecutionPolicy Bypass -File "/path/to/XerahS/.ai/skills/draft-blog-post/scripts/run-daily-draft.ps1" -IncludePreviousDay
```

If you use bash and the repo is in `$HOME/repos/XerahS`:

```cron
5 0 * * * cd "$HOME/repos/XerahS" && powershell -NoProfile -ExecutionPolicy Bypass -File ".ai/skills/draft-blog-post/scripts/run-daily-draft.ps1" -IncludePreviousDay
```

On macOS or Linux you may need `pwsh` instead of `powershell`.

## Summary

| What | When | How |
|---|---|---|
| Full skill (draft + content + commit) | Once per day (scheduled) | Cursor Automations with a scheduled trigger |
| Draft files only | Once per day (scheduled) | GitHub Actions, Task Scheduler, or cron running `run-daily-draft.ps1 -IncludePreviousDay` |
| Content + commit | Manual | Run the `draft-blog-post` skill in Cursor |

Recommendation: use Cursor Automations for the full skill, or the committed GitHub Actions workflow if you only want missing daily draft files created and committed on `develop`.
