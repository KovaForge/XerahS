# XIP0069 AfterCapture OCR Integration

## Summary

Implemented the `DoOCR` AfterCapture task flag defined in `TaskEnums.cs` (bit 17) by wiring it through the capture workflow.

## What was implemented

### Stage 1 [XIP-impl]

**Files changed:**

1. **`src/platform/XerahS.Platform.Abstractions/IUIService.cs`**
   - Added `ShowOcrWindowAsync(SKBitmap image)` method to the interface

2. **`src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs`**
   - Implemented `ShowOcrWindowAsync` — creates an `OcrViewModel` with the captured image, wires `SelectRegionRequested` callback (same delay/capture logic as `OcrToolService`), then shows `OcrWindow`

3. **`src/desktop/app/XerahS.UI/ViewModels/TaskSettingsViewModel.AfterCapture.cs`**
   - Added `DoOCR`, `BeautifyImage`, `ScanQRCode`, `PinToScreen`, `CopyFilePathToClipboard`, `ShowInExplorer`, `AnalyzeImage` properties
   - Each property reads/writes the corresponding `AfterCaptureTasks` flag via `HasFlag` / `UpdateAfterCaptureTask`

4. **`src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs`**
   - Added `PerformOCRAsync(TaskInfo info)` method called when `AfterCaptureTasks.DoOCR` flag is set
   - Calls `IOcrService.RecognizeAsync(image, OcrOptions)` with default options (en, scale 2x)
   - Stores result in `info.Metadata.OcrText`
   - Shows `OcrWindow` via `PlatformServices.UI.ShowOcrWindowAsync(image)` so user can review/adjust/copy

5. **`src/desktop/app/XerahS.UI/Views/TaskSettingsPanel.axaml`**
   - Added OCR checkbox and other AfterCapture checkboxes to the UI

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

## Branches

- `stage1/xip-0069-workflow-ocr-integration` — Core implementation
- `stage2/xip-0069-workflow-ocr-refactor` — OcrOptions fix
- `stage3/xip-0069-workflow-ocr-test` — Tests

## Notes

- OCR on Linux returns `Success=false` with Tesseract planned message. Windows OCR uses native `Windows.Media.Ocr`. macOS uses `VNRecognizeTextRequest`.
- `OcrWindow` is reused from the existing `OcrToolService` workflow path
- `BeautifyImage`, `ScanQRCode`, `PinToScreen`, `CopyFilePathToClipboard`, `ShowInExplorer`, `AnalyzeImage` are wired in UI and ViewModel but not yet implemented in `CaptureJobProcessor`
