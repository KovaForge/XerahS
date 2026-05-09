# First-party CLI or plugin prompt (reusable)

Use this when asking an assistant to design and implement a first-party XerahS CLI integration or plugin for a project such as OpenClaw or Hermes. Repository policy and workflow still apply; see root `AGENTS.md` and `developers/guidelines/AGENT_WORKFLOW.md`.

---

## Copy-paste prompt

```text
Create a first-party XerahS [CLI/plugin] integration for [OpenClaw/Hermes/other target].

Treat this as production work for the XerahS repository, not as a prototype. Stay on the current branch unless explicitly told otherwise. Follow root AGENTS.md, including build, test, commit-message, and no-new-branch rules.

Target:
- Name: [OpenClaw/Hermes/other target]
- Upstream repo or docs: [URL or local path]
- Integration type: [CLI/plugin/both]
- Primary user workflow: [describe the action a XerahS user should be able to perform]
- Required commands or plugin capabilities: [list expected commands, actions, settings, or UI entry points]
- Authentication or configuration: [none/API key/OAuth/local path/env vars/etc.]
- Output expectations: [JSON/text/files/history item/uploader result/etc.]

Discovery:
1. Inspect existing XerahS CLI or plugin patterns before designing anything new.
2. Identify the closest first-party implementation to reuse as the reference style.
3. Read the target project's official docs or source. Prefer primary sources.
4. Identify runtime, packaging, licensing, platform, and security constraints.
5. List assumptions and unknowns before implementation.

Design:
1. Define the smallest useful v1 integration.
2. Specify command names or plugin IDs using XerahS naming conventions.
3. Define stable inputs and outputs, including error shape and exit codes for CLI work.
4. Define how credentials/configuration are stored or passed.
5. Define how this integrates with capture history, workflows, destinations, or app settings if relevant.
6. Call out what is intentionally deferred after v1.

Implementation:
1. Make focused, idiomatic changes that follow existing XerahS architecture.
2. Reuse existing abstractions, plugin SDKs, service boundaries, serializers, and UI patterns where possible.
3. Add tests for command parsing, configuration validation, API/client behavior, and failure cases.
4. Avoid embedding secrets, vendor-specific hacks, or brittle string parsing when structured APIs are available.
5. Update docs or developer guidance when users or future maintainers need it.

Verification:
1. Run the narrowest relevant tests first.
2. Run `dotnet build` before finishing unless AGENTS.md explicitly allows skipping it.
3. If the integration has a CLI surface, show example invocations and expected output.
4. If the integration has a UI/plugin surface, verify registration/discovery and the primary workflow.
5. Summarize residual risks and any manual setup that could not be verified locally.

Deliverables:
- Short design summary
- Files changed
- Tests/build commands run and results
- Example usage
- Any required setup/configuration
- Commit and push only if explicitly requested or if the active repo instructions for the task require it
```

Replace bracketed placeholders before sending the prompt.
