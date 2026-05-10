# XIP0074 AfterCapture OCR Integration

**Status**: Implemented
**Implemented**: 2026-05-10
**Related**: XIP0071 (XerahS Spotlight Assistant)

## Summary

Implemented the `DoOCR` AfterCapture task flag defined in `TaskEnums.cs` (bit 17) by wiring it through the capture workflow.

XIP0074 is the interactive after-capture OCR workflow. It remains compatible with XIP0071 by sharing the same local OCR service layer, while XIP0071 owns silent background indexing and searchable History.

## What was implemented

### Stage 1 [XIP-impl]

**Files changed:**

1. **`src/platform/XerahS.Platform.Abstractions/IUIService.cs`**
   - Added `ShowOcrWindowAsync(SKBitmap image)` method to the interface

2. **`src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`**
   - Implemented `ShowOcrWindowAsync` — creates an `OcrViewModel` with the captured image, wires `SelectRegionRequested` callback (same delay/capture logic as `OcrToolService`), then shows `OcrWindow`

3. **`src/desktop/app/XerahS.UI/ViewModels/TaskSettingsViewModel.AfterCapture.cs`**
   - Added `DoOCR` property
   - The property reads/writes the corresponding `AfterCaptureTasks` flag via `HasFlag` / `UpdateAfterCaptureTask`

4. **`src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`**
   - Added `PerformOCRAsync(TaskInfo info)` method called when `AfterCaptureTasks.DoOCR` flag is set
   - Calls `IOcrService.RecognizeAsync(image, OcrOptions)` with default options (en, scale 2x)
   - Stores result in `info.Metadata.OcrText`
   - Shows `OcrWindow` via `PlatformServices.UI.ShowOcrWindowAsync(image)` so user can review/adjust/copy

5. **`src/desktop/app/XerahS.UI/Views/TaskSettingsPanel.axaml`**
   - Added OCR checkbox to the UI

### Stage 2 [Refactor]

**Files changed:**

- **`src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`**
  - Fixed `PerformOCRAsync` to pass `OcrOptions` to `RecognizeAsync`
  - Fixed return type handling: `OcrResult` has `Success`/`ErrorMessage` fields, not raw string

### Stage 3 [Test]

**Files added:**

- **`tests/XerahS.Tests/Helpers/AfterCaptureTaskFlagsTests.cs`**
  - Verifies `DoOCR` is bit 17 (1 << 17)
  - Verifies all `AfterCaptureTasks` values are distinct powers of 2
  - Verifies flags can be composed without collision

### Stage 4 [XIP0071 Compatibility]

**Files changed:**

1. **`src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`**
   - Persists successful `DoOCR` recognized text to the shared OCR catalog after the history row is created
   - Queues silent OCR indexing only when **Make screenshots searchable** is enabled and no interactive OCR result is already available
   - Keeps `AfterCaptureTasks.DoOCR` interactive and workflow-specific; silent indexing does not toggle the flag or open `OcrWindow`

2. **`src/desktop/core/XerahS.Core/Services/OcrIndexingService.cs`**
   - Added shared local OCR indexing and persistence path used by capture, assistant, and MCP flows
   - Reuses `IOcrService` and normalized OCR options
   - Runs background indexing off the UI thread with bounded single-worker concurrency

3. **`src/desktop/core/XerahS.History/HistoryOcrIndexStore.cs`**
   - Added the local SQLite OCR catalog table `HistoryOcrIndex`
   - Stores normalized OCR text, engine, language, timestamp, and status per history item

4. **`src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`**
   - Added global **Make screenshots searchable** setting under History settings

5. **`src/desktop/app/XerahS.UI/Views/HistoryView.axaml`** and **`src/desktop/app/XerahS.UI/ViewModels/HistoryViewModel.cs`**
   - Added History search UI and OCR-backed filtering

6. **`src/desktop/app/XerahS.Assistant/Services/AssistantHistoryService.cs`** and **`src/tools/XerahS.McpServer/Runtime/XerahSMcpRuntime.cs`**
   - Read and write OCR catalog text so assistant and MCP history search use the same recognized-text source

**Tests added/updated:**

- `tests/XerahS.Tests/Assistant/HistoryOcrIndexStoreTests.cs`
- `tests/XerahS.Tests/Assistant/AssistantCommandRouterTests.cs`
- `tests/XerahS.Tests/Assistant/AssistantHistoryServiceTests.cs`
- `tests/XerahS.Tests/Assistant/AssistantServiceTests.cs`

**Verification:**

- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter FullyQualifiedName~HistoryOcrIndexStoreTests --no-restore -m:1`
- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter FullyQualifiedName~AssistantCommandRouterTests --no-restore -m:1`
- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter FullyQualifiedName~AssistantHistoryServiceTests --no-restore -m:1`
- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter FullyQualifiedName~AssistantServiceTests --no-restore -m:1`
- `dotnet build -m:1`

## How it works

1. User selects "OCR text recognition" in the After Capture tab of Task Settings
2. Flag is stored in `TaskSettings.AfterCaptureJob` as `AfterCaptureTasks.DoOCR`
3. After capture, `CaptureJobProcessor` checks `HasFlag(AfterCaptureTasks.DoOCR)` and calls `PerformOCRAsync`
4. OCR runs on the captured image via `IOcrService.RecognizeAsync`
5. `OcrWindow` is shown automatically so user can:
   - Change OCR language
   - Adjust scale factor
   - Copy recognized text
   - Re-scan a different region

## Relationship to XIP0071

XIP0071 adds a separate user-facing feature, **Make screenshots searchable**, for silent OCR indexing and History search. It builds on the OCR primitives established by XIP0074, but it does not reuse the interactive `DoOCR` behavior directly.

### Shared foundation

- Both features use `IOcrService.RecognizeAsync` and the platform OCR implementations behind it.
- Both features use normalized OCR options where practical: language, scale factor, and single-line behavior.
- Both features persist successful recognized text in a form that downstream History, assistant, and MCP search can read.

### Different user intent

| Feature | User intent | Behavior |
|---|---|---|
| `DoOCR` after-capture task | "After this capture, show me OCR text so I can inspect/copy/rescan it." | Runs OCR as part of the after-capture workflow and opens `OcrWindow`. |
| **Make screenshots searchable** | "Index my screenshots so I can find them later from History search." | Queues background OCR after capture, writes to the OCR catalog, and does not open `OcrWindow`. |
| Assistant `ocr.run` | "OCR this known screenshot now." | Runs bounded local OCR on a selected/latest history image and may write the result back to the OCR catalog. |

### Combined behavior

When both `DoOCR` and **Make screenshots searchable** are enabled for the same capture, XerahS avoids duplicate OCR work where practical:

1. Capture saves the image and creates/updates the history row.
2. The interactive `DoOCR` path may run OCR and show `OcrWindow`.
3. If `DoOCR` produced non-empty recognized text, `OcrIndexingService` accepts that result and writes it to the catalog instead of re-running OCR.
4. If `DoOCR` is cancelled, fails, or returns no text, the indexing service may queue its normal background OCR job if screenshot search indexing is enabled.
5. `OcrWindow` remains the review/copy/rescan UI for explicit OCR workflows.

The implemented shared boundary is `OcrIndexingService.PersistRecognizedTextAsync`, which accepts OCR results from the after-capture processor, assistant, MCP details lookup, or background indexing worker.

### Compatibility requirements

- Implemented: the **Make screenshots searchable** setting does not silently enable `AfterCaptureTasks.DoOCR`; doing that would open OCR windows for every screenshot and violate the intended background indexing UX.
- Implemented: the `DoOCR` checkbox in Task Settings remains workflow-specific and interactive.
- Implemented: the **Make screenshots searchable** setting is global/History-scoped and non-interactive.
- Cloud OCR must not run automatically for either automatic indexing or metadata-only History search.
- `OcrWindow` remains the review/rescan UI for explicit OCR workflows, not the indexing UI.
- Implemented: OCR text persistence ignores empty or whitespace-only results.
- Search indexing should prefer the same native OCR engines XIP0074 already uses on Windows and macOS, and should mark Linux rows as `not_supported` until a local Linux OCR engine is available.

### Remaining hardening

- Add FTS5 for larger OCR catalogs.
- Add durable OCR job table and restart recovery.
- Add explicit backfill UI for existing screenshots.
- Add richer History UI indicators/snippets for OCR-only matches.

## Branches

- `stage1/xip-0074-workflow-ocr-integration` — Core implementation
- `stage2/xip-0074-workflow-ocr-refactor` — OcrOptions fix
- `stage3/xip-0074-workflow-ocr-test` — Tests

## Notes

- OCR on Linux returns `Success=false` with Tesseract planned message. Windows OCR uses native `Windows.Media.Ocr`. macOS uses `VNRecognizeTextRequest`.
- `OcrWindow` is reused from the existing `OcrToolService` workflow path
