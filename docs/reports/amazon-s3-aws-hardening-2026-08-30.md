# Amazon S3 and AWS Hardening Report (2026-08-30)

## Snapshot

- Branch: `develop`
- Base reviewed: `140395a3`
- Implementation commit: `0b184c56`
- Package: `AWSSDK.S3` updated from `4.0.101.6` to `4.0.102.4`, the latest stable version found on 2026-08-30.
- XIP0054 remains Complete. Its multipart design was audited and retained.
- XIP0027 remains Open. Its SSO and provisioning source exists in the live tree; real-account verification remains outstanding.

## Live Surface

The desktop implementation is under `src/desktop/plugins/AmazonS3.Plugin/`:

- Provider and uploader: `AmazonS3Provider.cs`, `AmazonS3Uploader.cs`
- Configuration and credentials: `S3ConfigModel.cs`, `S3AuthMode.cs`, `S3CredentialSecrets.cs`
- SSO: `AwsSsoClient.cs`, `AwsSsoModels.cs`, `AwsSsoOidcClient.cs`
- Provisioning and signing: `S3Provisioner.cs`, `AwsS3Signer.cs`
- Multipart transport: `Multipart/S3MultipartUploader.cs`, `Multipart/S3MultipartUploadOptions.cs`
- UI: `ViewModels/AmazonS3ConfigViewModel.cs`, `Views/AmazonS3ConfigView.axaml`

The non-experimental mobile surfaces inspected were:

- Android upload: `src/mobile/android/core/data/src/main/java/com/getsharex/xerahs/mobile/core/data/upload/S3Uploader.kt`
- Android configuration: `src/mobile/android/feature/settings/src/main/java/com/getsharex/xerahs/mobile/feature/settings/S3ConfigScreen.kt` and `S3ConfigViewModel.kt`
- iOS configuration: `src/mobile/ios/XerahSMobile/Features/Settings/S3ConfigScreen.swift`
- iOS upload: `src/mobile/ios/XerahSMobile/Features/Upload/UploadScreen.swift`

`src/mobile-experimental/` was not inspected or changed.

## Current Flow Before the Fix

1. `S3ConfigModel` selected `AccessKeys` or `AwsSso` authentication.
2. Access-key secrets came from `S3CredentialSecrets`; SSO tokens and temporary role credentials came from `AwsSsoSecretStore`.
3. Small uploads used `AwsS3Signer` plus `HttpClient` directly in `AmazonS3Uploader.cs`.
4. Multipart uploads used `IAmazonS3` through `S3MultipartUploader`.
5. Both upload paths then used the same URL builder for custom-domain, path-style, and regional S3 URLs.
6. ACL and storage-class options were applied independently by the two transports.

That split was the dual-client smell: a configuration could work through the AWS SDK multipart path but fail through the raw signer single-PUT path. Explorer and provisioning also use `AwsS3Signer`, so deleting the signer wholesale would have been a larger and riskier change.

## Target and Smallest-Diff Decision

The target is one SDK-backed upload transport for both small and multipart files, while retaining the existing plugin abstractions, configuration UI, URL behavior, explorer, and provisioner.

The fix therefore removes `AwsS3Signer` only from the upload path. `AmazonS3Uploader` now creates one configured `IAmazonS3` client and uses `PutObjectAsync` for a small file or the existing `S3MultipartUploader` for a multipart file. Explorer and provisioning retain the custom signer until they can be migrated and verified as a separate change.

## Broken Paths at Base `140395a3`

Line ranges below identify the pre-fix source at the reviewed base commit.

| Path and range | Broken behavior | Resolution |
| --- | --- | --- |
| `AmazonS3Uploader.cs:125-188` | Single PUT used a separate raw signer and `HttpClient`, duplicating SDK endpoint, payload, ACL, storage-class, progress, cancellation, and error behavior. Scheme-bearing custom endpoints could diverge from multipart behavior. | Small PUT now uses `IAmazonS3.PutObjectAsync` with the same configured client as multipart. |
| `AmazonS3Uploader.cs:190-291` | Multipart progress used `Progress<T>`, which can capture a UI synchronization context while the caller synchronously waits. | An inline synchronous `IProgress<T>` reporter avoids context capture. |
| `AmazonS3Uploader.cs:93-123`, `AmazonS3Uploader.cs:399-402` | A configured threshold above the S3 5 GiB single-PUT limit could route an oversized object to single PUT. | Files over 5 GiB always select multipart. |
| `AmazonS3Provider.cs:594-608` | Explorer access-key resolution read only the base secret names and could bypass destination-alias isolation. | Resolution now uses `S3CredentialSecrets` for the configured destination alias. |
| `AmazonS3Provider.cs:611-658`, `AmazonS3Provider.cs:841-859`, `AwsSsoModels.cs:77-133` | Cached role credentials were accepted by expiry alone and were not bound to the selected AWS account and role. Switching roles could reuse credentials for the previous selection. Malformed cached JSON was not self-healing. | Cached credentials include account and role identity, selection checks evict stale/legacy values, and malformed records are removed. |
| `AmazonS3ConfigViewModel.cs:995-1017` | Successful SSO validation forced `SetPublicACL` back on after provisioning disabled ACLs for bucket-owner-enforced buckets. | Validation preserves the SSO public-policy model and keeps object ACLs off. |
| `Multipart/S3MultipartUploader.cs:185-228`, `Multipart/S3MultipartUploader.cs:307-358` | Retry and abort logs included object/file identifiers, upload IDs, and raw exception messages. | Logs contain operation, attempt, exception type, status, and AWS error code only. |

## XIP0054 Gap Audit

XIP0054's completed multipart behavior was not reimplemented.

| Concern | Existing coverage and result |
| --- | --- |
| Abort | Existing uploader aborts after failure and cancellation; covered by `S3MultipartUploaderTests.cs:104-167`. |
| Part retry | Existing bounded retry with backoff retained; covered by `S3MultipartUploaderTests.cs:104-134`. |
| Memory | Existing part streams read bounded file ranges rather than buffering the complete file; retained. |
| Progress | Existing aggregated multipart progress retained; UI context capture at the integration boundary was fixed. |
| Cancel | Existing cancellation propagation and abort retained; small PUT now uses the same cancellation source. |
| Secret in log | No access key or secret key was logged. Multipart object/upload identifiers and raw exception messages were additionally removed. |

## XIP0027 and Secret-Store Findings

The live tree already contains device authorization, SSO account/role discovery, temporary role credentials, provisioning, session-token signing, and Avalonia UI. The proposal's active paths were stale and are corrected separately in the same documentation increment.

This change closes two actual credential gaps:

- Temporary role credentials are usable only for the account and role that produced them.
- Malformed SSO secret records are deleted instead of repeatedly failing deserialization.

No secret values were added to source, tests, logs, or documentation.

## Mobile Parity

Static inspection confirmed Android configuration loads in `S3ConfigViewModel.kt:82-96` and saves at `S3ConfigViewModel.kt:110-143`; iOS configuration loads and saves in `S3ConfigScreen.swift:146-185`. Android stores the secret key through its Keystore-backed settings repository, and iOS stores it through Keychain-backed settings.

The mobile implementation remains access-key-only and uses a fixed `uploads/` object prefix. It does not yet match desktop SSO, configurable object prefix, multipart, storage class, or public-policy behavior. Those are real parity gaps, but changing them would create a second scope and was intentionally excluded from this smallest-diff desktop hardening change.

## Per-File Diff

- `Directory.Packages.props`: update `AWSSDK.S3` to `4.0.102.4`.
- `AmazonS3Uploader.cs`: unify upload transport on `IAmazonS3`, normalize cancellation/progress, enforce the 5 GiB boundary, sanitize failures, and add a test seam.
- `AwsSsoModels.cs`: bind cached credentials to account/role and self-heal malformed secret-store JSON.
- `AwsSsoClient.cs`: record the requested account and role with returned credentials.
- `AmazonS3Provider.cs`: use destination-aware access-key lookup and validate SSO cache identity.
- `ViewModels/AmazonS3ConfigViewModel.cs`: retain ACL-off behavior after SSO validation and validate the selected cached role.
- `Multipart/S3MultipartUploader.cs`: remove sensitive object/upload identifiers and raw messages from logs.
- `tests/XerahS.Tests/Uploaders/AmazonS3HardeningTests.cs`: add six regression tests for SDK single PUT, the 5 GiB boundary, ACL behavior, SSO cache binding, malformed cache recovery, and destination aliases.
- `developers/lessons-learnt/general.md`: record the test-double naming/nullability lesson found during verification.

## Automated Verification

All commands ran from the repository root after the implementation:

```text
dotnet build src/desktop/plugins/AmazonS3.Plugin/XerahS.AmazonS3.Plugin.csproj -m:1
Result: 0 warnings, 0 errors

dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --no-restore --filter FullyQualifiedName~AmazonS3HardeningTests
Result: 6 passed, 0 failed

dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~S3"
Result: 27 passed, 0 failed

dotnet build src/desktop/XerahS.sln -m:1
Result: 0 warnings, 0 errors

git diff --check
Result: clean
```

Existing multipart tests cover successful part upload and progress (`S3MultipartUploaderTests.cs:68-102`), retry exhaustion plus abort (`S3MultipartUploaderTests.cs:104-134`), and cancellation plus abort (`S3MultipartUploaderTests.cs:136-167`).

## Manual Verification Status and Checklist

These checks were not run because this workspace has no authorized AWS test account/bucket and no attached Android/iOS runtime. They remain required before closing XIP0027.

### Access Keys

1. Save access key ID and secret key under a named destination alias.
2. Upload one file below the multipart threshold and one above it.
3. Confirm both objects have the configured content type, storage class, ACL behavior, and returned custom-domain URL.
4. Repeat against any supported scheme-bearing S3-compatible endpoint.

### AWS SSO

1. Complete device authorization and select an account and role.
2. Validate, provision, and upload to a bucket-owner-enforced bucket with public policy enabled.
3. Confirm validation does not re-enable object ACLs.
4. Change role and confirm fresh role credentials are requested.
5. Confirm the returned URL uses the configured custom domain.

### Mobile Configuration Save and Load

1. On Android, save endpoint, bucket, region, access key, secret key, and custom domain; reopen settings and verify every value.
2. On iOS, perform the same save/reopen check and confirm the secret remains available through Keychain.
3. Upload one file on each platform and confirm the fixed `uploads/` key and returned URL match current mobile behavior.

## Residual Risk

- Explorer and provisioner still use `AwsS3Signer`; their removal needs separate behavior-preserving SDK migration and real AWS verification.
- Automated tests use fakes and do not prove AWS IAM policy, Identity Center, DNS/custom-domain, regional bucket creation, or a third-party S3-compatible service.
- The SDK package update is build- and unit-tested but not exercised against every supported S3-compatible endpoint.
- Mobile parity gaps listed above remain.
- XIP0027 must remain Open until its real-account verification plan passes.
