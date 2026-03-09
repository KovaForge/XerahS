Read `AGENTS.md`, `README.md`, `docs/WALKTHROUGH.md`, `docs/PROJECT_STATUS.md`, `docs/ROADMAP.md`, `src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs`, `src/desktop/plugins/Bitly.Plugin/*`, and `src/desktop/app/XerahS.UI/Services/QrCodeToolService.cs`.

Treat this as an autoresearch-style software improvement loop:

- make a small, coherent change
- run verification
- keep only changes that clearly improve the product
- prefer simple changes over broad refactors

Implement a user-facing feature called `Smart Post-Upload Actions`:

- Wire `AfterUploadTasks.UseURLShortener` in `UploadJobProcessor` using the existing URL shortener plugin system and `UrlShortenerDestinationInstanceId`.
- If shortening succeeds, use the shortened URL as the final URL for clipboard copy and silent open.
- Wire the silent `AfterUploadTasks.OpenURL` path.
- Wire `AfterUploadTasks.ShowQRCode` to display a QR code for the final URL.
- Add or update focused tests for the processor logic.
- Update the relevant docs.

Constraints:

- Preserve platform abstraction boundaries.
- Do not add new packages unless absolutely necessary.
- Keep the implementation simple.
- Avoid unrelated cleanup.

Validate with:

```powershell
dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1
```

If a change does not build, does not pass tests, or adds complexity without clear user value, discard it and try a simpler approach.
