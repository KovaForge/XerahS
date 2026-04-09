# XIP0054 Amazon S3 Multipart Upload

**Status**: PROPOSED  
**Priority**: High  
**Related**: XIP0027 (S3 SSO + Auto-Provisioning)

---

## Goal Description

The current `AmazonS3Uploader` uploads files as a single PUT request using the custom SigV4 signer. This works for typical screenshots and short recordings but becomes unreliable and memory-inefficient for large files (hundreds of MB to multi-GB screen recordings, video exports, etc.). Single-request uploads cannot be resumed on failure, provide no part-level retry, and are subject to S3's 5 GB single-PUT limit.

This XIP adds production-quality multipart upload support to the Amazon S3 plugin using the **official AWS SDK for .NET** (`AWSSDK.S3`) and the S3 multipart upload API (`CreateMultipartUpload` → `UploadPart` → `CompleteMultipartUpload` / `AbortMultipartUpload`). The implementation must be **reusable** so that future uploaders supporting multipart upload (e.g. S3-compatible services, Azure Blob Storage staged upload) can share the same abstractions and bounded-concurrency infrastructure.

## Assumptions

- **Target framework**: `net10.0` (consistent with all XerahS plugins and core libraries)
- **New package dependency**: `AWSSDK.S3` (latest stable) added to `Directory.Packages.props`
- The official AWS SDK is used for multipart operations. The existing custom SigV4 signer (`AwsS3Signer.cs`) is retained for single-PUT uploads and provisioning. The SDK handles credential resolution via the standard AWS credential chain.
- The existing single-PUT upload path in `AmazonS3Uploader.Upload()` remains the default for small files (below the configurable part-size threshold). Multipart is engaged automatically for large files.
- No high-level transfer utilities (e.g. `TransferUtility`) are used; the core multipart API flow is implemented directly to maintain visibility and control.
- Resume support is designed-for but not implemented in the initial delivery. The architecture explicitly supports adding it without major refactoring.

## Summary of Changes

### New shared abstractions (in `XerahS.Uploaders`)

Provider-agnostic multipart upload types in a new `Multipart/` folder under `XerahS.Uploaders`:

| Type | Purpose |
|------|---------|
| `IMultipartUploader` | Interface for any multipart upload backend |
| `MultipartUploadOptions` | Configuration: part size, max concurrency, retry policy, content type, metadata, tags, encryption |
| `MultipartUploadProgress` | Progress snapshot: bytes uploaded, file size, completed/total parts, percentage, elapsed, ETA |
| `MultipartUploadResult` | Outcome: success flag, ETag, version ID, URL, elapsed time, parts uploaded |
| `MultipartUploadException` | Typed exception wrapping partial-upload context (upload ID, completed parts, inner exceptions) |
| `PartRange` | Value type: part number, offset, length |
| `CompletedPart` | Value type: part number, ETag |
| `RetryPolicy` | Configuration: max retries, base delay, max delay, jitter enabled |

These types live in the core `XerahS.Uploaders` project so any future plugin can depend on them without referencing the S3 plugin.

### New S3-specific implementation (in `AmazonS3.Plugin`)

| Type | Purpose |
|------|---------|
| `S3MultipartUploader` | Implements `IMultipartUploader` using `IAmazonS3` |
| `S3MultipartUploadOptions` | S3-specific options extending `MultipartUploadOptions` (storage class, ACL, SSE settings) |

### Modified files

| File | Change |
|------|--------|
| `AmazonS3Uploader.cs` | Add threshold check in `Upload()` that delegates to `S3MultipartUploader` for large files |
| `S3ConfigModel.cs` | Add multipart configuration properties (`MultipartThresholdBytes`, `MultipartPartSizeBytes`, `MultipartMaxConcurrency`) |
| `AmazonS3ConfigViewModel.cs` | Expose new multipart config fields |
| `AmazonS3ConfigView.axaml` | UI controls for multipart settings |
| `XerahS.AmazonS3.Plugin.csproj` | Add `AWSSDK.S3` package reference |
| `Directory.Packages.props` | Add `AWSSDK.S3` version entry |

### New test files (in `XerahS.Tests`)

| File | Purpose |
|------|---------|
| `S3MultipartUploaderTests.cs` | Unit tests for core logic (part range calculation, validation, progress, retry) |
| `MultipartUploadOptionsTests.cs` | Validation edge cases |

## Functional Requirements

1. **Initiate multipart upload**: Call `CreateMultipartUploadRequest` with bucket, key, content type, metadata, tags, storage class, and optional SSE settings. Capture the `UploadId`.
2. **Calculate and validate part ranges**: Split the file into parts based on configurable part size. Validate that part count does not exceed S3's 10,000 part limit. Validate part size is within S3's 5 MB–5 GB range (except last part which can be smaller).
3. **Stream file parts without loading the whole file into memory**: Open a single `FileStream` and create bounded sub-streams (or use offset/length reads) for each part. Never buffer the entire file.
4. **Upload parts in parallel with bounded concurrency**: Use `SemaphoreSlim` to limit concurrent `UploadPartAsync` calls to a configurable maximum (default: 4).
5. **Collect PartNumber + ETag**: Store each successful part's `PartNumber` and `ETag` in a thread-safe collection.
6. **Complete multipart upload**: Sort completed parts by `PartNumber` and call `CompleteMultipartUploadRequest`.
7. **Abort on unrecoverable failure**: If the upload cannot be completed (all retries exhausted for a part, cancellation, or completion failure), call `AbortMultipartUploadRequest` to clean up.
8. **Retry failed parts only**: Use exponential backoff with jitter. Only the failed part is retried, not the entire upload. Configurable max retries (default: 3), base delay (default: 1 s), max delay (default: 30 s).
9. **Progress reporting**: Emit `MultipartUploadProgress` via `IProgress<MultipartUploadProgress>` with: total bytes uploaded, total file size, completed parts count, total parts count, percentage, elapsed time, estimated remaining time.
10. **Structured logging**: Log key events via `DebugHelper.WriteLine` (consistent with XerahS logging conventions): upload started, part started, part succeeded, part failed + retry, upload completed, upload aborted. Never log credentials or secrets.
11. **Configuration and validation**: Validate all inputs (file exists, bucket/key not empty, part size within S3 limits, file size vs part count limits) before starting. Provide clear error messages.
12. **Cancellation**: Accept `CancellationToken` throughout. On cancellation, abort the multipart upload and throw `OperationCanceledException`.
13. **Threshold-based routing**: Files below `MultipartThresholdBytes` (default: 50 MB) use the existing single-PUT path. Files at or above the threshold use multipart.
14. **Zero-byte files**: Handled by the single-PUT path (no multipart needed).
15. **Credential resolution**: The `IAmazonS3` client is constructed using the standard AWS credential chain. For SSO mode (XIP0027), temporary credentials from the SSO flow are passed to the SDK client. For access-key mode, keys from the config model are used. No hardcoded credentials.

## Non-Functional Requirements

1. **Memory efficiency**: Peak memory usage bounded by `partSize × maxConcurrency` plus overhead. No full-file buffering.
2. **Testable design**: `S3MultipartUploader` accepts `IAmazonS3` via constructor, enabling mock-based unit testing.
3. **Dependency injection friendly**: All services are constructor-injected. No static state.
4. **Idiomatic modern C#**: Async/await throughout, nullable reference types, `IAsyncDisposable` where appropriate, `Channel<T>` or `SemaphoreSlim` for concurrency control.
5. **Plugin compatibility**: New NuGet dependency (`AWSSDK.S3`) uses `CopyLocalLockFileAssemblies` and plugin isolation via `PluginLoadContext`.

## Architecture

### Reusable multipart abstraction layer

```
XerahS.Uploaders/
  Multipart/
    IMultipartUploader.cs
    MultipartUploadOptions.cs
    MultipartUploadProgress.cs
    MultipartUploadResult.cs
    MultipartUploadException.cs
    PartRange.cs
    CompletedPart.cs
    RetryPolicy.cs
```

`IMultipartUploader` defines:

```csharp
public interface IMultipartUploader
{
    Task<MultipartUploadResult> UploadAsync(
        string filePath,
        MultipartUploadOptions options,
        IProgress<MultipartUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

This interface is provider-agnostic. Future uploaders (Azure Blob staged upload, Backblaze B2 large file upload, etc.) can implement the same interface, and the calling code (`AmazonS3Uploader`, workflow engine, CLI) does not change.

### S3 implementation

```
AmazonS3.Plugin/
  Multipart/
    S3MultipartUploader.cs
    S3MultipartUploadOptions.cs
```

`S3MultipartUploader` implements `IMultipartUploader` using `IAmazonS3`:

1. **Initiate** → `CreateMultipartUploadAsync`
2. **Upload parts** → bounded parallel `UploadPartAsync` via `SemaphoreSlim`
3. **Complete** → `CompleteMultipartUploadAsync` with sorted parts list
4. **Abort** (on failure) → `AbortMultipartUploadAsync` in a `finally` block

### Integration with existing uploader

```csharp
// In AmazonS3Uploader.Upload(Stream, string):
if (stream.Length >= _config.MultipartThresholdBytes)
{
    // Delegate to S3MultipartUploader (async-over-sync bridge)
    return UploadMultipart(stream, fileName);
}
// else: existing single-PUT path
```

Because the current `GenericUploader.Upload()` is synchronous, the multipart path uses `Task.Run` + `.GetAwaiter().GetResult()` as a bridge. A future XIP can make the base uploader async.

### Retry strategy

Exponential backoff with jitter for transient failures:

```
delay = min(baseDelay × 2^attempt + random_jitter, maxDelay)
```

Transient failures detected by: `AmazonS3Exception` with retryable status codes (408, 429, 500, 502, 503, 504), `HttpRequestException`, `IOException`, `TaskCanceledException` (timeout, not user cancellation).

Only the individual failed part is retried. If all retries for a part are exhausted, the upload is aborted and `MultipartUploadException` is thrown containing the upload ID and list of successfully completed parts (to support future resume).

### Resume support (designed-for, not yet implemented)

`MultipartUploadException` carries the `UploadId` and list of `CompletedPart` values. A future enhancement can:

1. Persist this state to a JSON sidecar file.
2. On retry, call `ListPartsAsync` to verify which parts the server acknowledges.
3. Upload only missing parts.
4. Complete the upload.

The architecture supports this without refactoring because:
- `S3MultipartUploader` already produces the necessary state in the exception.
- Part range calculation is deterministic given the same file size and part size.
- The `UploadId` uniquely identifies the in-progress upload on the server.

## Edge Cases

| Scenario | Handling |
|----------|----------|
| File does not exist | `FileNotFoundException` before any S3 calls |
| Invalid bucket name or key | `ArgumentException` with descriptive message |
| Zero-byte file | Routed to single-PUT path |
| File below threshold | Routed to single-PUT path |
| Invalid part size (< 5 MB or > 5 GB) | `ArgumentOutOfRangeException` during validation |
| Too many parts (> 10,000) | Auto-increase part size to fit within limit, log warning |
| One or more parts failing repeatedly | Abort upload, throw `MultipartUploadException` with context |
| Network interruption during upload | Per-part retry with exponential backoff; abort if unrecoverable |
| Completion failure after all parts uploaded | Retry completion once; abort and throw on second failure |
| Cancellation during upload | Abort multipart upload, throw `OperationCanceledException` |
| Stale multipart uploads | Document `AbortIncompleteMultipartUpload` lifecycle rule recommendation |

## S3 Limits Reference

| Limit | Value |
|-------|-------|
| Max object size | 5 TB |
| Max parts per upload | 10,000 |
| Min part size (except last) | 5 MB |
| Max part size | 5 GB |
| Max single PUT | 5 GB |
| Recommended multipart threshold | 100 MB (AWS) / 50 MB (this implementation) |

## Config Model Additions

New properties on `S3ConfigModel`:

```csharp
/// Minimum file size to trigger multipart upload (bytes). Default: 50 MB.
public long MultipartThresholdBytes { get; set; } = 50 * 1024 * 1024;

/// Size of each part (bytes). Default: 10 MB. Must be >= 5 MB.
public long MultipartPartSizeBytes { get; set; } = 10 * 1024 * 1024;

/// Maximum number of parts uploaded in parallel. Default: 4.
public int MultipartMaxConcurrency { get; set; } = 4;
```

## Key Files

### New files

- `src/desktop/core/XerahS.Uploaders/Multipart/IMultipartUploader.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/MultipartUploadOptions.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/MultipartUploadProgress.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/MultipartUploadResult.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/MultipartUploadException.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/PartRange.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/CompletedPart.cs`
- `src/desktop/core/XerahS.Uploaders/Multipart/RetryPolicy.cs`
- `src/desktop/plugins/AmazonS3.Plugin/Multipart/S3MultipartUploader.cs`
- `src/desktop/plugins/AmazonS3.Plugin/Multipart/S3MultipartUploadOptions.cs`
- `tests/XerahS.Tests/Uploaders/S3MultipartUploaderTests.cs`
- `tests/XerahS.Tests/Uploaders/MultipartUploadOptionsTests.cs`

### Modified files

- `src/desktop/plugins/AmazonS3.Plugin/AmazonS3Uploader.cs`
- `src/desktop/plugins/AmazonS3.Plugin/AmazonS3Provider.cs`
- `src/desktop/plugins/AmazonS3.Plugin/S3ConfigModel.cs`
- `src/desktop/plugins/AmazonS3.Plugin/ViewModels/AmazonS3ConfigViewModel.cs`
- `src/desktop/plugins/AmazonS3.Plugin/Views/AmazonS3ConfigView.axaml`
- `src/desktop/plugins/AmazonS3.Plugin/XerahS.AmazonS3.Plugin.csproj`
- `Directory.Packages.props`

## Design Tradeoffs

1. **AWS SDK vs custom SigV4 for multipart**: The existing custom SigV4 signer handles single PUTs well, but multipart upload involves multiple complex API calls (`CreateMultipartUpload`, `UploadPart` with chunked transfer, `CompleteMultipartUpload` with XML body, `AbortMultipartUpload`, `ListParts`). Reimplementing all of these with raw HTTP and SigV4 would be error-prone and costly to maintain. The official SDK is the correct choice for multipart operations.

2. **Sync-over-async bridge**: The base `GenericUploader.Upload()` method is synchronous. The multipart uploader is async. A sync-over-async bridge (`Task.Run` + `GetAwaiter().GetResult()`) is used to avoid deadlocks. A future XIP should make the upload pipeline async end-to-end.

3. **`IProgress<T>` vs existing `ProgressChanged` event**: The multipart uploader uses `IProgress<T>` internally (idiomatic .NET). The integration point in `AmazonS3Uploader` bridges this to the existing `ProgressChanged` event so the rest of the XerahS pipeline does not need changes.

4. **Part size auto-adjustment**: If the file is large enough that the configured part size would exceed 10,000 parts, the part size is automatically increased to fit. This is logged as a warning but does not fail the upload.

5. **No `TransferUtility`**: AWS's `TransferUtility` hides the multipart API flow. We implement the flow directly for full control over retry, progress, cancellation, and future resume support.

6. **Resume deferred**: Full resume (persisting upload state and resuming across process restarts) adds significant complexity (sidecar files, state reconciliation, stale upload detection). The architecture is designed so resume can be added later by catching `MultipartUploadException` and using its `UploadId` and `CompletedParts` properties. This is documented as a future enhancement.

7. **Plugin NuGet isolation**: `AWSSDK.S3` and its transitive dependencies are loaded via `PluginLoadContext` (assembly load isolation), so they do not conflict with the host app's dependencies.

## Verification Plan

### Unit tests

1. **Part range calculation**: Verify correct part ranges for various file sizes and part sizes, including edge cases (exact multiple, off-by-one, zero bytes, single part).
2. **Validation**: Verify rejection of invalid inputs (missing file, empty bucket, part size too small, part size too large, negative concurrency).
3. **Part size auto-adjustment**: Verify automatic increase when part count would exceed 10,000.
4. **Progress calculation**: Verify percentage, ETA, and bytes-uploaded accuracy.
5. **Retry logic**: Verify exponential backoff timing and jitter bounds. Verify that only the failed part is retried.
6. **Cancellation**: Verify that cancellation aborts the upload and propagates correctly.
7. **CompletedPart ordering**: Verify parts are sorted by `PartNumber` before completion.

### Integration test guidance

1. Configure a test S3 bucket with lifecycle rules to abort incomplete multipart uploads after 1 day.
2. Upload a file larger than the multipart threshold. Verify the object exists and content matches.
3. Upload with artificially low concurrency (1) and high concurrency (8). Verify both succeed.
4. Simulate failure by using a non-existent bucket. Verify `MultipartUploadException` is thrown.
5. Cancel mid-upload. Verify the multipart upload is aborted (no orphaned parts).
6. Upload a file just below the threshold. Verify single-PUT is used.

### Manual verification

1. Configure S3 plugin with multipart settings in the XerahS UI.
2. Upload a large screen recording (> 50 MB). Verify progress bar updates smoothly.
3. Verify the uploaded file is accessible at the expected URL.
4. Check XerahS logs for structured multipart upload events.

## Future Steps

The following implementation work is planned as follow-up to this proposal:

1. **Implement shared multipart abstractions** (`XerahS.Uploaders/Multipart/`): Create `IMultipartUploader`, `MultipartUploadOptions`, `MultipartUploadProgress`, `MultipartUploadResult`, `MultipartUploadException`, `PartRange`, `CompletedPart`, and `RetryPolicy` types.
2. **Implement `S3MultipartUploader`** (`AmazonS3.Plugin/Multipart/`): Build the S3-specific implementation with `IAmazonS3`, bounded parallel uploads via `SemaphoreSlim`, exponential-backoff retry, progress reporting, abort-on-failure, and cancellation support.
3. **Integrate with `AmazonS3Uploader`**: Add threshold check and routing logic, bridge `IProgress<T>` to `ProgressChanged` event, construct `IAmazonS3` client from config/credentials.
4. **Update config model and UI**: Add `MultipartThresholdBytes`, `MultipartPartSizeBytes`, `MultipartMaxConcurrency` to `S3ConfigModel`, expose in ViewModel and View.
5. **Add `AWSSDK.S3` to `Directory.Packages.props`** and plugin `.csproj`.
6. **Create example console app**: Standalone usage demonstrating `S3MultipartUploader` directly.
7. **Write unit tests**: Part range calculation, validation, retry logic, progress, cancellation, ordering.
8. **Write integration test guidance**: Document steps for testing against a real S3 bucket.
9. **Create README documentation**: Setup, configuration, usage, limits, and failure-mode notes.
10. **Future: Resume support**: Persist `UploadId` + completed parts to sidecar JSON; reconcile with `ListParts`; upload only missing parts.
11. **Future: Async upload pipeline**: Make `GenericUploader.Upload()` async to eliminate sync-over-async bridge.
