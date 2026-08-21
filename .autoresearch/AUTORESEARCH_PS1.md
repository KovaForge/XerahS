# PowerShell Port

`autoresearch.ps1` adapts the original `autoresearch` idea to software engineering work:

- the mutable surface is a target git repo such as `XerahS`
- the human writes a mission prompt in Markdown
- an external agent CLI applies changes inside an isolated git worktree
- validator commands decide whether the attempt is kept or discarded
- accepted attempts advance a dedicated `autoresearch/<tag>` branch
- every attempt is logged to `results.tsv`

## What the script does

For each attempt it:

1. Creates a temporary worktree from the current accepted branch head.
2. Runs your agent command in that worktree.
3. Runs one or more validation commands.
4. Auto-commits validated changes if the agent left them uncommitted.
5. Moves the run branch forward only if the attempt passed validation and produced a real diff.
6. Logs the result and removes the temporary worktree unless `-KeepWorktrees` is set.

This script does not bundle an LLM. You provide the agent entry command through `-AgentCommand`.

## Important parameters

- `-TargetRepo`: repo to improve
- `-MissionFile`: Markdown prompt copied into the run state
- `-AgentCommand`: command template for your agent CLI
- `-ValidationCommands`: one or more PowerShell commands run after the agent step
- `-RunTag`: used for branch naming and state directory naming
- `-RunBranch`: defaults to `autoresearch/<RunTag>`
- `-StateRoot`: defaults to `<TargetRepo>\.autoresearch\<RunTag>`
- `-MaxAttempts`: number of attempts unless `-LoopForever` is set
- `-Resume`: continue an existing run branch and append to its `results.tsv`

## Agent command placeholders

These tokens are expanded inside `-AgentCommand` before execution:

- `{repo}`: temporary worktree path for the current attempt
- `{mission}`: copied mission file inside the run state
- `{attempt}`: zero-padded attempt id like `001`
- `{run_branch}`: accepted branch name
- `{start_ref}`: current accepted commit before the attempt
- `{target_repo}`: original repo path
- `{state_root}`: run state root directory
- `{attempt_dir}`: per-attempt directory
- `{agent_log}`: per-attempt agent log path
- `{validation_log}`: per-attempt validation log path

Quote these placeholders in your command string when they represent paths.

## XerahS example

Run these examples from `C:\Users\liveu\source\repos\ShareX Team\XerahS\.autoresearch`.

Example mission files:

- [missions/xerahs-smart-post-upload.md](./missions/xerahs-smart-post-upload.md)
- [missions/xerahs-bugfix.md](./missions/xerahs-bugfix.md)

Example invocation:

```powershell
.\autoresearch.ps1 `
  -TargetRepo ".." `
  -MissionFile ".\missions\xerahs-smart-post-upload.md" `
  -AgentCommand 'codex exec --cwd "{repo}" --prompt-file "{mission}"' `
  -ValidationCommands @(
    'dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false',
    'dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1'
  ) `
  -RunTag "xerahs-post-upload" `
  -MaxAttempts 5
```

## Outputs

The run state directory contains:

- `mission.md`: copied mission prompt
- `run.json`: run metadata
- `results.tsv`: attempt log
- `attempts\<n>\agent.log`: agent stdout and stderr
- `attempts\<n>\validation.log`: validator stdout and stderr
- `worktrees\<n>`: temporary worktree if retained

## Practical notes

- The script uses committed refs only. Dirty files in your main target repo are left untouched.
- `results.tsv` is append-only and easy to inspect or post-process.
- If your agent CLI already commits, the script keeps those commits. If it does not, the script auto-commits on a successful attempt.
- `-DryRun` is useful to verify branch and worktree setup before running an agent.
