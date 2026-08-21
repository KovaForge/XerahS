Read `AGENTS.md`, `README.md`, `KNOWN_ISSUES.md`, `docs/WALKTHROUGH.md`, `docs/PROJECT_STATUS.md`, `developers/README.md`, and the specific files relevant to the bug you choose.

Treat this as an autoresearch-style bug-fix loop:

- choose one bug with a clear reproduction path and a clear validation path
- prefer small, high-confidence fixes over broad refactors
- verify the root cause before changing code
- keep only fixes that build, pass tests, and reduce real user-facing risk

Bug-selection priority:

1. A user-provided bug, if one is supplied alongside this mission.
2. A documented bug in `KNOWN_ISSUES.md` or `docs/PROJECT_STATUS.md`.
3. A concrete runtime or workflow bug indicated by a `TODO`, failing test, incorrect flag handling, or an obviously incomplete processor path.

Execution rules:

- Reproduce the bug first, or explain the strongest code-level evidence when direct reproduction is not practical.
- Implement the smallest defensible fix.
- Add or update focused regression tests where practical.
- Update docs only if behavior, limitations, or workflow expectations change.
- Avoid unrelated cleanup.

Constraints:

- Preserve platform abstraction boundaries.
- Do not add new packages unless absolutely necessary.
- Do not weaken warnings, nullability, or build settings.
- Keep the implementation simple and reviewable.

Validate with:

```powershell
dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1
```

If the attempted fix fails validation, discard it and try a simpler, better-scoped bug fix.
