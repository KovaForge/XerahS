# Scheduling the blog draft (once per day)

The **full** skill (git history → Summary, Features, Fixes, prose) can be run in Cursor by an agent, or **automated** with Cursor Automations (scheduled cloud agent). You can also schedule only the **draft-creation** step via OS-level cron or Task Scheduler.

## Cursor Automations (recommended for full skill)

Cursor has **Automations**: cloud agents that run on a **schedule** (including custom cron) or on events (GitHub, Slack, webhooks, etc.). You can run the full blog-drafting workflow once per day without opening the editor.

- **Docs**: [cursor.com/docs/cloud-agent/automations](https://cursor.com/docs/cloud-agent/automations)
- **Create**: [cursor.com/automations](https://cursor.com/automations) or start from a [marketplace template](https://cursor.com/marketplace/automations)

**Setup:**

1. Create a new automation → choose **Scheduled** trigger.
2. Set the schedule (e.g. daily at 00:10 UTC+8, or use a cron expression like `10 0 * * *` if your automation uses UTC+8).
3. Select the **XerahS** repository and the branch (e.g. `develop`).
4. In the prompt, instruct the agent to run the **draft-blog-post** skill for **today** (UTC+8). For example:

   ```text
   Run the draft-blog-post skill for today (UTC+8). Follow the skill at .ai/skills/draft-blog-post/SKILL.md:
   - Resolve today's date in UTC+8 and ensure docs/blog/YYYY/YYYY-MM/blog-YYYYMMDD.md exists (use the upsert script if needed).
   - Gather git log (author date in UTC+8) for today from this repo and the ShareX.ImageEditor submodule (and XerahS.Editor if present). Use --since="<today>T00:00:00+08:00" --until="<tomorrow>T00:00:00+08:00" so author dates fall on the current UTC+8 day.
   - Populate all sections (Summary, Features, Fixes, Build and Tooling, Commits Reviewed, Notes) from real commits only. No placeholders.
   - Commit only that day's blog file with message "[vX.Y.Z] [Docs] Add YYYY-MM-DD <short description> blog draft." Do not push unless the instructions say to.
   ```

5. Enable **Open pull request** (or the appropriate write capability) so the agent can commit. For schedule triggers, the automation uses the repo/branch you selected.
6. Save and enable the automation.

Automations are billed as cloud agent usage (Pro/Teams). Scheduled runs may start with a short delay but not before the scheduled time.

## What a script-only scheduled job does

If you use **Task Scheduler** or **cron** (below), the job only runs the **draft-creation** script:

- Runs once per day (e.g. start of UTC+8 day or early morning your time).
- Ensures `docs/blog/YYYY/YYYY-MM/blog-YYYYMMDD.md` exists for **today** (UTC+8). If it doesn’t, creates it with the standard TBD template.
- Does **not** fill in content or commit; run the full skill in Cursor (or use Cursor Automations above) for that.

## Windows (Task Scheduler)

1. Open **Task Scheduler** → Create Basic Task (or Create Task).
2. **Trigger**: Daily at a fixed time (e.g. 00:05 in your time zone, or 16:05 UTC if you want start of UTC+8 day).
3. **Action**: Start a program.
   - **Program**: `powershell.exe`
   - **Arguments**:  
     `-NoProfile -ExecutionPolicy Bypass -File "C:\Users\liveu\source\repos\ShareX Team\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1"`
   - Use the **actual** path to your XerahS repo if different.
4. **Start in**: Optional; `run-daily-draft.ps1` switches to the repo root itself.
5. Under **Settings**, allow the task to run when the user is logged off if you want it to run in the background.

One-time test from PowerShell (run from any directory):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\liveu\source\repos\ShareX Team\XerahS\.ai\skills\draft-blog-post\scripts\run-daily-draft.ps1"
```

## Linux / WSL / macOS (cron)

1. Choose when to run (e.g. 00:05 in UTC+8, or 08:00 local).
2. Edit crontab: `crontab -e`.
3. Add a line (adjust path and time; example: 00:05 UTC+8 = 16:05 UTC previous day, so for “start of UTC+8 day” you might use `5 0 * * *` in a crontab that’s set to UTC+8, or the equivalent in your TZ).

Example (run at 00:05 in a timezone that’s UTC+8, e.g. `TZ=Asia/Shanghai`):

```cron
5 0 * * * TZ=Asia/Shanghai powershell -NoProfile -ExecutionPolicy Bypass -File "/path/to/XerahS/.ai/skills/draft-blog-post/scripts/run-daily-draft.ps1"
```

If you use bash and the repo is in `$HOME/repos/XerahS`:

```cron
5 0 * * * cd "$HOME/repos/XerahS" && powershell -NoProfile -ExecutionPolicy Bypass -File ".ai/skills/draft-blog-post/scripts/run-daily-draft.ps1"
```

(On macOS/Linux you may need `pwsh` if you use PowerShell Core, and the path to the script must be absolute or relative to the `cd` directory.)

## Summary

| What                | When                         | How                                                                 |
|---------------------|------------------------------|---------------------------------------------------------------------|
| Full skill (draft + content + commit) | Once per day (scheduled)     | **Cursor Automations** → scheduled trigger + prompt (see above)    |
| Draft file only     | Once per day (scheduled)     | Task Scheduler / cron → `run-daily-draft.ps1`                       |
| Content + commit    | Manual                       | Run draft-blog-post skill in Cursor (or rely on Automations)    |

**Recommendation:** Use **Cursor Automations** with a daily schedule to run the full skill (draft, git-based content, single-file commit) without opening the editor. Use **Task Scheduler / cron** only if you want to ensure the draft file exists and you’ll run the skill yourself later.
