# GNOME Wayland: XDG Portal, overlay region capture, and response codes

This note summarises a 2026-03 investigation into **Fedora GNOME / Wayland** failures when using the **XerahS overlay** for region capture (e.g. `RectangleTransparent`), and follow-up **online research** on XDG Desktop Portal behaviour.

## Symptoms (reported)

- After drawing a region on the overlay, post-selection capture failed.
- Logs showed:
  - XDG Screenshot portal: **`Response=2`**, often with **no usable `uri`** in results (fallback file lookup empty).
  - GNOME Shell D-Bus fallback: **`AccessDenied`** (e.g. full-screen screenshot not allowed for the app).
- **KDE / Endeavour** paths could work while **GNOME** did not, for similar workflows.

## Root cause (application): `UseTransparentOverlay` dropped before `CaptureRectAsync`

`CaptureStage` correctly sets `CaptureOptions.UseTransparentOverlay` for transparent rectangle workflows. The UI layer uses that for the overlay session.

However, `ScreenCaptureService.CaptureRegionAsync` built a **new** `CaptureOptions` instance for the post-overlay **`CaptureRectFromSelection` → `CaptureRectAsync`** path and **did not copy** `UseTransparentOverlay`. It therefore defaulted to `false`.

Downstream effects in `LinuxScreenCaptureService`:

1. **`ShouldForceInteractivePortalFullScreen`** (GNOME Wayland + transparent overlay) did not run → portal was called with **`interactive: false`** for the full-screen grab used to crop the selection.
2. **`ShouldUseDirectGnomeAreaCapture`** (GNOME + Wayland + transparent overlay → `ScreenshotArea` via Shell) did not run → no fast path; reliance on portal + D-Bus fallbacks remained.

**Fix:** copy `UseTransparentOverlay` from `effectiveOptions` into that `captureOptions` object in:

- `src/desktop/app/XerahS.UI/Services/ScreenCaptureService.cs`

So a **single** portal request can be **interactive** when the product intends it, without relying on a second attempt.

## Why `allowInteractiveFallback` stays `false`

`PortalScreenCapture.CaptureAsync` can, when enabled, retry after a non-interactive failure with **`interactive: true`**.

The project keeps **`allowInteractiveFallback: false`** when calling it from `LinuxScreenCaptureService` (`ILinuxCaptureRuntime.TryPortalCaptureAsync`) because:

- On **GNOME**, portal behaviour around permissions and **response codes** has been painful for many apps; a **silent failure followed by an automatic interactive retry** can mean **extra prompts**, confusing UX, or duplicate flows.
- The preferred approach is: **force interactive on the first request** when needed (overlay + GNOME Wayland path above), not **fail-then-retry**.

A short **inline comment** in `LinuxScreenCaptureService.cs` documents this for future maintainers.

## Guardrail: direct-area failure must restore the old full-screen fallback

GNOME Wayland now has two distinct stages in the overlay follow-up path:

1. Try the fast **GNOME Shell `ScreenshotArea`** path when the overlay workflow requests it.
2. If that stage fails, restore the **`v0.20.12` full-screen capture + crop fallback** instead of staying on the transparent-overlay fast path.

That fallback contract is intentional:

- Clear **`LinuxDisallowPortalAfterOverlaySelection`** so the portal provider can participate again.
- Clear **`UseTransparentOverlay`** so the follow-up capture behaves like the old full-screen crop path, not the newer GNOME-specific transparent-overlay path.
- Preserve crop metadata (`VirtualScreenBoundsForCrop`, `PhysicalVirtualScreenBoundsForCrop`, `PhysicalRectForCrop`) and workflow metadata so the returned bitmap can still be cropped correctly.

If a future change keeps `UseTransparentOverlay=true` after direct-area failure, the code is no longer reproducing the last known good fallback behavior from `v0.20.12`.

## Research: what `Response = 2` means in the spec

All screenshot (and other) portal calls complete via **`org.freedesktop.portal.Request::Response`**. The documented codes are ([Request documentation](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Request.html)):

| Code | Meaning (spec) |
|------|----------------|
| **0** | Success, the request is carried out |
| **1** | The user cancelled the interaction |
| **2** | The user interaction was ended **in some other way** |

So **by specification, 2 is not success**. Treating 2 as failure is spec-aligned; the phrase “some other way” is deliberately vague and backends may use it for different real situations.

The Screenshot portal returns a **`uri`** in the results vardict on success ([Screenshot documentation](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Screenshot.html)).

## Research: public issues (ecosystem, not a “false success” proof)

- **[flatpak/xdg-desktop-portal#950](https://github.com/flatpak/xdg-desktop-portal/issues/950)** — Screenshot permission / first-time **`interactive: true`**, active window, and **unclear or missing responses** on GNOME-related stacks; maintainers note behaviour vs documentation is still unclear.
- **[dynobo/normcap#555](https://github.com/dynobo/normcap/issues/555)** — Fedora / GNOME 45 / Wayland: **`interactive=False`** → **error code 2**, with **no `uri`** to parse (crash in client). This matches **real failure / denial**, not “success with code 2”.
- NormCap maintainer commentary: Wayland / portal **permission behaviour is fragile**; tools use **workarounds** until the protocol and implementations stabilise.

**Conclusion from research:** We did **not** find a credible upstream statement that **GNOME returns 2 together with a valid successful capture** (e.g. valid `uri`) as a systematic bug description. **2 with empty / missing results** is well reported. **Disabling automatic interactive fallback** remains a **UX and policy** choice that does not require proving “2 means success.”

## Related code (quick map)

| Area | Path |
|------|------|
| Post-overlay options / `CaptureRectAsync` | `XerahS.UI/Services/ScreenCaptureService.cs` |
| Portal call site, `allowInteractiveFallback`, GNOME interactive forcing | `XerahS.Platform.Linux/LinuxScreenCaptureService.cs` |
| Portal D-Bus + optional interactive retry | `XerahS.Platform.Linux/Capture/Portal/PortalScreenCapture.cs` |
| Transparent overlay flag for tasks | `XerahS.Core/Tasks/Pipeline/CaptureStage.cs` (`ShouldUseTransparentOverlay`) |
| GNOME Shell `ScreenshotArea` / full screen | `XerahS.Platform.Linux/Capture/Gnome/GnomeDbusScreenCapture.cs` |

## Maintainer checklist when touching this area

1. Any new **`CaptureOptions`** clone for **post-overlay** capture must preserve flags that Linux uses for **GNOME / Wayland** policy (`UseTransparentOverlay`, crop bounds, etc.).
   Exception: when GNOME direct-area capture has already failed, the fallback clone should intentionally reset `UseTransparentOverlay=false` and `LinuxDisallowPortalAfterOverlaySelection=false` to restore the `v0.20.12` full-screen crop path.
2. Re-enabling **`allowInteractiveFallback`** should be a **conscious product decision** (test on GNOME, Fedora, mixed-DPI), not only a “portal failed” convenience.
3. If **`Response=2` with a valid `uri`** is ever reproduced reliably, capture **portal + gnome-shell + xdg-desktop-portal-gnome versions** and consider filing upstream with a minimal repro.
