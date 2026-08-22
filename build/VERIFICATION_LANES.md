# Verification lanes

`build/verify.ps1` is the PowerShell front door for local and agent verification. It makes the intended verification scope explicit and applies the repository's conservative MSBuild defaults consistently:

- single-node execution (`-m:1` and `BuildInParallel=false`);
- disabled MSBuild node reuse and shared compilation;
- warnings-as-errors remain enabled by repository policy;
- no fixed build timeout;
- no automatic cleanup of outputs or build servers beyond `--disable-build-servers` for the current invocation.

Run the script from any directory. Paths passed to `-Project` and `-TestProject` may be absolute repository paths or paths relative to the repository root.

If Windows PowerShell's local execution policy blocks direct `.ps1` invocation, use a process-scoped bypass rather than changing machine policy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\verify.ps1 FullProductBuild
```

## Lanes

| Lane | Purpose | Command shape |
|---|---|---|
| `FastCompile` | Compile one selected project and the dependencies needed by that project. Product staging is disabled. Defaults to `XerahS.Core`; pass the project that owns the change. | `dotnet build <project> -p:AssembleProduct=false` |
| `TargetedTests` | Build and run one test project with a mandatory test filter. The mandatory filter prevents this lane from silently becoming a full test run. | `dotnet test <project> --filter <filter>` |
| `FullProductBuild` | Build and assemble everything in the supported desktop solution, including the main application, platform projects, plugins, tools, and test projects. This is the repository's normal pre-push compile gate. | `dotnet build src/desktop/XerahS.sln -p:AssembleProduct=true` |
| `FullVerification` | Run `FullProductBuild`, execute the main NUnit suite and isolated build/packaging suite from those outputs, then build and execute the MCP xUnit suite. | product build plus all three test projects |

Examples:

```powershell
# Compile the project changed by the current task.
./build/verify.ps1 FastCompile -Project src/desktop/app/XerahS.UI/XerahS.UI.csproj

# Run a bounded NUnit slice.
./build/verify.ps1 TargetedTests -TestFilter "FullyQualifiedName~CoordinateTransformTests"

# Run the product compile gate.
./build/verify.ps1 FullProductBuild -Configuration Debug

# Run the complete local verification sequence.
./build/verify.ps1 FullVerification -Configuration Debug
```

`TargetedTests` defaults to `tests/XerahS.Tests/XerahS.Tests.csproj`. Use `-TestProject` for another test project:

```powershell
./build/verify.ps1 TargetedTests `
    -TestProject src/tools/XerahS.McpServer.Tests/XerahS.McpServer.Tests.csproj `
    -TestFilter "FullyQualifiedName~XerahSMcpServerTests"
```

Use `-NoRestore` only after the selected lane's projects have been restored. The full verification lane still passes `--no-build --no-restore` to the main test execution because the immediately preceding solution build produced those outputs.

Use `-DryRun` to inspect resolved paths and generated commands without invoking `dotnet`:

```powershell
./build/verify.ps1 FullVerification -DryRun
```

## Isolated artifacts

Pass `-ArtifactsPath` to opt into the .NET SDK artifacts layout. This is useful when several agents, sessions, or worktrees share a checkout host and should not contend for the default `bin` and `obj` paths.

```powershell
$laneArtifacts = Join-Path $env:TEMP "xerahs-artifacts/session-42"
./build/verify.ps1 FastCompile `
    -Project src/desktop/core/XerahS.Core/XerahS.Core.csproj `
    -ArtifactsPath $laneArtifacts
```

The value is resolved to an absolute path and forwarded as `dotnet --artifacts-path` for build and test commands. Use one stable path for every lane in the same session so `--no-build` test steps can find the preceding build. The script never deletes that path. Custom project targets that write explicit output locations may continue to use those locations; `-ArtifactsPath` isolates SDK-managed build outputs, not arbitrary packaging directories such as `dist/`.

## Lane boundaries

- `FastCompile` is compile evidence for a bounded project, not the repository's pre-push build gate.
- `TargetedTests` is behavior evidence for the selected filter, not evidence that unrelated tests pass.
- `FullProductBuild` proves compilation with warnings-as-errors but does not execute tests.
- `FullVerification` is the broad local gate. Platform packaging, signing, installers, Android deployment, and GUI smoke tests remain separate workflows.
- Packaging scripts under `build/windows`, `build/linux`, `build/macos`, and `build/android` retain their current responsibilities.

## Product assembly boundary

`AssembleProduct` separates compilation from staging. It defaults to `false` in `Directory.Build.props`, so product projects do not recursively build plugins or stage the watch-folder daemon unless a product lane opts in. Bounded lanes also pass `BuildWebUI=false` as a global MSBuild property because the VideoEditor submodule has its own build-property root. `FullProductBuild` and `FullVerification` pass both properties as `true` and therefore retain the complete product-output checks.

Publish targets remain responsible for publish-only staging and packaging scripts retain signing and installer responsibilities. New compile-time side effects must be placed behind the same explicit property so bounded lanes remain bounded.
