[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRepo,

    [Parameter(Mandatory = $true)]
    [string]$MissionFile,

    [string]$RunTag = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [string]$BaseRef,
    [string]$RunBranch,
    [string]$StateRoot,
    [string]$AgentCommand,
    [string[]]$ValidationCommands = @(),
    [string]$CommitMessagePrefix = "autoresearch",
    [int]$MaxAttempts = 1,
    [switch]$LoopForever,
    [switch]$KeepWorktrees,
    [switch]$Resume,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] $Message"
}

function Resolve-ExistingPath {
    param([string]$PathValue)

    return (Resolve-Path -LiteralPath $PathValue).Path
}

function Resolve-FullPath {
    param(
        [string]$PathValue,
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function Write-Utf8File {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Append-Utf8Line {
    param(
        [string]$Path,
        [string]$Line
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::AppendAllText($Path, $Line + [Environment]::NewLine, $utf8)
}

function Quote-PowerShellLiteral {
    param([string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Quote-ProcessArgument {
    param([string]$Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return '""'
    }

    if ($Value -match '[\s"]') {
        return '"' + $Value.Replace('"', '\"') + '"'
    }

    return $Value
}

function Normalize-Cell {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ($Value -replace "`t", " " -replace "`r?`n", " ").Trim()
}

function Invoke-Git {
    param(
        [string]$RepoPath,
        [string[]]$Arguments
    )

    $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    $gitArguments = @("-C", $RepoPath) + $Arguments
    $gitArgumentString = ($gitArguments | ForEach-Object { Quote-ProcessArgument -Value $_ }) -join " "

    try {
        $process = Start-Process -FilePath "git" `
            -ArgumentList $gitArgumentString `
            -Wait `
            -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        $stdout = ""
        $stderr = ""

        if (Test-Path -LiteralPath $stdoutPath) {
            $stdout = [System.IO.File]::ReadAllText($stdoutPath)
        }

        if (Test-Path -LiteralPath $stderrPath) {
            $stderr = [System.IO.File]::ReadAllText($stderrPath)
        }

        $output = @($stdout.Trim(), $stderr.Trim()) | Where-Object { $_.Length -gt 0 }
        $output = $output -join [Environment]::NewLine

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output   = $output.Trim()
        }
    }
    finally {
        if (Test-Path -LiteralPath $stdoutPath) {
            Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        }

        if (Test-Path -LiteralPath $stderrPath) {
            Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-GitChecked {
    param(
        [string]$RepoPath,
        [string[]]$Arguments
    )

    $result = Invoke-Git -RepoPath $RepoPath -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        $argsText = ($Arguments -join " ")
        throw "git $argsText failed in $RepoPath`n$result"
    }

    return $result.Output
}

function Test-GitBranchExists {
    param(
        [string]$RepoPath,
        [string]$BranchName
    )

    $result = Invoke-Git -RepoPath $RepoPath -Arguments @("show-ref", "--verify", "--quiet", "refs/heads/$BranchName")
    return $result.ExitCode -eq 0
}

function Expand-Template {
    param(
        [string]$Template,
        [hashtable]$Tokens
    )

    $expanded = $Template
    foreach ($key in $Tokens.Keys) {
        $expanded = $expanded.Replace("{$key}", [string]$Tokens[$key])
    }

    return $expanded
}

function New-CommandSnippet {
    param([string]$CommandText)

    return @(
        '$__cmd = ' + (Quote-PowerShellLiteral $CommandText),
        'Write-Host ("Running: " + $__cmd)',
        'Invoke-Expression $__cmd',
        'if ($null -eq $LASTEXITCODE) { exit 0 }',
        'exit [int]$LASTEXITCODE'
    ) -join [Environment]::NewLine
}

function New-ValidationSnippet {
    param([string[]]$Commands)

    if ($Commands.Count -eq 0) {
        return @(
            'Write-Host "No validation commands configured."',
            'exit 0'
        ) -join [Environment]::NewLine
    }

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($command in $Commands) {
        $lines.Add('$__cmd = ' + (Quote-PowerShellLiteral $command))
        $lines.Add('Write-Host ("Validating: " + $__cmd)')
        $lines.Add('Invoke-Expression $__cmd')
        $lines.Add('if ($null -ne $LASTEXITCODE -and [int]$LASTEXITCODE -ne 0) { exit [int]$LASTEXITCODE }')
    }

    $lines.Add('exit 0')
    return $lines -join [Environment]::NewLine
}

function Invoke-LoggedSnippet {
    param(
        [string]$Snippet,
        [string]$WorkingDirectory,
        [string]$LogPath
    )

    $runnerPath = [System.IO.Path]::ChangeExtension($LogPath, ".runner.ps1")
    $runner = @(
        '$ErrorActionPreference = "Stop"',
        'Set-StrictMode -Version Latest',
        'Set-Location -LiteralPath ' + (Quote-PowerShellLiteral $WorkingDirectory),
        $Snippet
    ) -join [Environment]::NewLine

    Write-Utf8File -Path $runnerPath -Content $runner

    $powershellExe = Join-Path $PSHOME "powershell.exe"
    if (-not (Test-Path -LiteralPath $powershellExe)) {
        $powershellExe = (Get-Command powershell.exe).Source
    }

    & $powershellExe -NoProfile -ExecutionPolicy Bypass -File $runnerPath *> $LogPath
    return $LASTEXITCODE
}

function Ensure-ResultsFile {
    param([string]$ResultsPath)

    if (-not (Test-Path -LiteralPath $ResultsPath)) {
        Append-Utf8Line -Path $ResultsPath -Line "attempt`tstart_ref`tend_ref`tstatus`tagent_exit`tvalidation_exit`tchanged_files`tbranch`tnotes"
    }
}

function Append-Result {
    param(
        [string]$ResultsPath,
        [int]$Attempt,
        [string]$StartRef,
        [string]$EndRef,
        [string]$Status,
        [string]$AgentExit,
        [string]$ValidationExit,
        [string]$ChangedFiles,
        [string]$Branch,
        [string]$Notes
    )

    $line = @(
        $Attempt,
        (Normalize-Cell $StartRef),
        (Normalize-Cell $EndRef),
        (Normalize-Cell $Status),
        (Normalize-Cell $AgentExit),
        (Normalize-Cell $ValidationExit),
        (Normalize-Cell $ChangedFiles),
        (Normalize-Cell $Branch),
        (Normalize-Cell $Notes)
    ) -join "`t"

    Append-Utf8Line -Path $ResultsPath -Line $line
}

function Remove-AttemptResources {
    param(
        [string]$RepoPath,
        [string]$WorktreePath,
        [string]$BranchName,
        [switch]$Keep
    )

    if ($Keep) {
        return
    }

    if (Test-Path -LiteralPath $WorktreePath) {
        $null = Invoke-Git -RepoPath $RepoPath -Arguments @("worktree", "remove", "--force", $WorktreePath)
    }

    if (Test-GitBranchExists -RepoPath $RepoPath -BranchName $BranchName) {
        $null = Invoke-Git -RepoPath $RepoPath -Arguments @("branch", "-D", $BranchName)
    }
}

if (-not $LoopForever -and $MaxAttempts -lt 1) {
    throw "MaxAttempts must be at least 1 unless -LoopForever is used."
}

if (-not $DryRun -and [string]::IsNullOrWhiteSpace($AgentCommand)) {
    throw "AgentCommand is required unless -DryRun is used."
}

$TargetRepo = Resolve-ExistingPath -PathValue $TargetRepo
$MissionFile = Resolve-ExistingPath -PathValue $MissionFile

$gitDirCheck = Invoke-Git -RepoPath $TargetRepo -Arguments @("rev-parse", "--git-dir")
if ($gitDirCheck.ExitCode -ne 0) {
    throw "$TargetRepo is not a git repository."
}

if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    $BaseRef = Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
    if ($BaseRef -eq "HEAD") {
        $BaseRef = "HEAD"
    }
}

$baseCommit = Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("rev-parse", "$BaseRef^{commit}")

if ([string]::IsNullOrWhiteSpace($RunBranch)) {
    $RunBranch = "autoresearch/$RunTag"
}

if ([string]::IsNullOrWhiteSpace($StateRoot)) {
    $StateRoot = Join-Path $TargetRepo ".autoresearch\$RunTag"
}
else {
    $StateRoot = Resolve-FullPath -PathValue $StateRoot -BasePath $TargetRepo
}

$attemptsDir = Join-Path $StateRoot "attempts"
$worktreesDir = Join-Path $StateRoot "worktrees"
$resultsPath = Join-Path $StateRoot "results.tsv"
$missionCopyPath = Join-Path $StateRoot "mission.md"
$metadataPath = Join-Path $StateRoot "run.json"

[System.IO.Directory]::CreateDirectory($attemptsDir) | Out-Null
[System.IO.Directory]::CreateDirectory($worktreesDir) | Out-Null

Ensure-ResultsFile -ResultsPath $resultsPath

$branchExists = Test-GitBranchExists -RepoPath $TargetRepo -BranchName $RunBranch
if ($branchExists -and -not $Resume) {
    throw "Run branch '$RunBranch' already exists. Use a new RunTag or -Resume."
}

if (-not $branchExists) {
    Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("branch", $RunBranch, $baseCommit) | Out-Null
}

$currentAcceptedRef = Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("rev-parse", "$RunBranch^{commit}")
$dirtyStatus = Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("status", "--porcelain")
if (-not [string]::IsNullOrWhiteSpace($dirtyStatus)) {
    Write-Log "Warning: target repo has local changes. The run uses git worktrees from committed refs only."
}

Copy-Item -LiteralPath $MissionFile -Destination $missionCopyPath -Force

$metadata = [ordered]@{
    created_at           = (Get-Date).ToString("o")
    target_repo          = $TargetRepo
    mission_file_source  = $MissionFile
    mission_file_copy    = $missionCopyPath
    base_ref             = $BaseRef
    base_commit          = $baseCommit
    run_branch           = $RunBranch
    run_tag              = $RunTag
    state_root           = $StateRoot
    agent_command        = $AgentCommand
    validation_commands  = $ValidationCommands
    keep_worktrees       = [bool]$KeepWorktrees
    loop_forever         = [bool]$LoopForever
    max_attempts         = $MaxAttempts
    dry_run              = [bool]$DryRun
}
Write-Utf8File -Path $metadataPath -Content ($metadata | ConvertTo-Json -Depth 5)

$attempt = 1
if ($Resume) {
    $existingRows = @(Get-Content -LiteralPath $resultsPath | Select-Object -Skip 1 | Where-Object { $_.Trim().Length -gt 0 }).Count
    if ($existingRows -gt 0) {
        $attempt = $existingRows + 1
    }
}

Write-Log "State root: $StateRoot"
Write-Log "Run branch: $RunBranch"
Write-Log "Accepted head: $currentAcceptedRef"

while ($LoopForever -or $attempt -le $MaxAttempts) {
    $attemptId = "{0:D3}" -f $attempt
    $attemptBranch = "$RunBranch-attempt-$attemptId"
    $attemptDir = Join-Path $attemptsDir $attemptId
    $worktreePath = Join-Path $worktreesDir $attemptId
    $agentLog = Join-Path $attemptDir "agent.log"
    $validationLog = Join-Path $attemptDir "validation.log"
    $notesPath = Join-Path $attemptDir "notes.txt"
    $agentCommandPath = Join-Path $attemptDir "agent-command.txt"

    [System.IO.Directory]::CreateDirectory($attemptDir) | Out-Null

    $startRef = $currentAcceptedRef
    $status = "discard"
    $agentExit = ""
    $validationExit = ""
    $changedFiles = "0"
    $endRef = $startRef
    $notes = ""

    Write-Log "Starting attempt $attemptId from $startRef"

    try {
        if (Test-Path -LiteralPath $worktreePath) {
            throw "Worktree path already exists: $worktreePath"
        }

        if (Test-GitBranchExists -RepoPath $TargetRepo -BranchName $attemptBranch) {
            throw "Attempt branch already exists: $attemptBranch"
        }

        Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("worktree", "add", "-b", $attemptBranch, $worktreePath, $startRef) | Out-Null

        $tokens = @{
            repo           = $worktreePath
            mission        = $missionCopyPath
            attempt        = $attemptId
            run_branch     = $RunBranch
            start_ref      = $startRef
            target_repo    = $TargetRepo
            state_root     = $StateRoot
            attempt_dir    = $attemptDir
            agent_log      = $agentLog
            validation_log = $validationLog
        }

        if ($DryRun) {
            $status = "dryrun"
            $notes = "Prepared worktree only."
            Write-Log "Dry run complete for attempt $attemptId"
        }
        else {
            $expandedAgentCommand = Expand-Template -Template $AgentCommand -Tokens $tokens
            Write-Utf8File -Path $agentCommandPath -Content $expandedAgentCommand

            $agentExit = [string](Invoke-LoggedSnippet -Snippet (New-CommandSnippet -CommandText $expandedAgentCommand) -WorkingDirectory $worktreePath -LogPath $agentLog)
            if ($agentExit -ne "0") {
                $status = "agent-failed"
                $notes = "Agent command failed. See agent.log."
                Write-Log "Attempt $attemptId agent command failed with exit code $agentExit"
            }
            else {
                $diffNames = Invoke-GitChecked -RepoPath $worktreePath -Arguments @("diff", "--name-only", $startRef)
                $diffFiles = @($diffNames -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })
                $changedFiles = [string]$diffFiles.Count

                $validationExit = [string](Invoke-LoggedSnippet -Snippet (New-ValidationSnippet -Commands $ValidationCommands) -WorkingDirectory $worktreePath -LogPath $validationLog)
                if ($validationExit -ne "0") {
                    $status = "validation-failed"
                    $notes = "Validation failed. See validation.log."
                    Write-Log "Attempt $attemptId validation failed with exit code $validationExit"
                }
                else {
                    $worktreeStatus = Invoke-GitChecked -RepoPath $worktreePath -Arguments @("status", "--porcelain")
                    if (-not [string]::IsNullOrWhiteSpace($worktreeStatus)) {
                        Invoke-GitChecked -RepoPath $worktreePath -Arguments @("add", "-A") | Out-Null
                        $commitMessage = "${CommitMessagePrefix}: $RunTag attempt $attemptId"
                        $commitResult = Invoke-Git -RepoPath $worktreePath -Arguments @("commit", "--no-verify", "-m", $commitMessage)
                        if ($commitResult.ExitCode -ne 0) {
                            $status = "commit-failed"
                            $notes = "Commit failed after validation. See notes.txt."
                            Write-Utf8File -Path $notesPath -Content $commitResult.Output
                        }
                    }

                    if ($status -notin @("commit-failed")) {
                        $candidateRef = Invoke-GitChecked -RepoPath $worktreePath -Arguments @("rev-parse", "HEAD")
                        $endRef = $candidateRef

                        if ($candidateRef -eq $startRef) {
                            $status = "nochange"
                            $notes = "Agent produced no committed diff."
                            Write-Log "Attempt $attemptId produced no committed diff"
                        }
                        else {
                            Invoke-GitChecked -RepoPath $TargetRepo -Arguments @("branch", "-f", $RunBranch, $candidateRef) | Out-Null
                            $currentAcceptedRef = $candidateRef
                            $status = "keep"
                            $notes = "Accepted into $RunBranch"
                            Write-Log "Attempt $attemptId accepted at $candidateRef"
                        }
                    }
                }
            }
        }
    }
    catch {
        $status = "script-error"
        $notes = $_.Exception.Message
        Write-Log "Attempt $attemptId failed: $notes"
        Write-Utf8File -Path $notesPath -Content $notes
    }
    finally {
        Append-Result -ResultsPath $resultsPath `
            -Attempt $attempt `
            -StartRef $startRef `
            -EndRef $endRef `
            -Status $status `
            -AgentExit $agentExit `
            -ValidationExit $validationExit `
            -ChangedFiles $changedFiles `
            -Branch $RunBranch `
            -Notes $notes

        Remove-AttemptResources -RepoPath $TargetRepo -WorktreePath $worktreePath -BranchName $attemptBranch -Keep:$KeepWorktrees
        $null = Invoke-Git -RepoPath $TargetRepo -Arguments @("worktree", "prune")
    }

    if ($DryRun) {
        break
    }

    $attempt++
}

Write-Log "Run complete. Accepted head: $currentAcceptedRef"
Write-Log "Results: $resultsPath"
