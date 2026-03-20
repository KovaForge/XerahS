# Non-Functional / Gate Checks

Total items: `8`

## Build & Dependency Gates (`3`)
- [ ] NF-BUILD-CLEAN net10.0-windows10.0.26100.0 builds with 0 errors
- [ ] NF-WARN-NO-NEW-WARNINGS no new warnings introduced (TreatWarningsAsErrors respected)
- [ ] NF-SKIA-2.88.9 SkiaSharp version remains `2.88.9`

## Runtime / Smoke Gates (`5`)
- [ ] NF-RUNTIME-SMOKE App starts; no immediate exceptions; core UI usable
- [ ] NF-WORKFLOWS-SMOKE At least 1 capture/upload workflow completes successfully
- [ ] NF-IMAGEEDITOR-SMOKE Image editor can open an image and complete crop/save
- [ ] NF-REGRESSION-10MIN 10-minute interactive regression: no freezes/crashes
- [ ] NF-LOGS-CLEAN No unexpected errors in logs during smoke/regression

