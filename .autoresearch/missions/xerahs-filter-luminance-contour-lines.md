Read `AGENTS.md` and:
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/ImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/FilterImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ProceduralEffectHelper.cs`
- Reference-style for pixel loops and clamping:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/SobelEdgeImageEffect.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/VintagePrintDamageImageEffect.cs`

Mission:
Create a new image effect filter class called `LuminanceContourLinesImageEffect` in:
`ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/LuminanceContourLinesImageEffect.cs`

Requirements:
- Namespace: `ShareX.ImageEditor.Core.ImageEffects.Filters`.
- Inherit from `FilterImageEffect`.
- Provide public properties (names matter for editor heuristics):
  - `int Levels` default around `12` (quantization levels)
  - `float LineWidth` default around `6f` (0..200; clamp internally)
  - `float LineStrength` default around `65f` (0..100)
  - `float BackgroundStrength` default around `20f` (0..100)
  - `float Threshold` default around `0f` (0..255 heuristic; clamp to 0..255, then map to 0..1 in code)
  - `bool Invert` default `false`
  - `SKColor LineColor` default around black-ish with full alpha (alpha > 0)
- Override:
  - `Name` => `Luminance contour lines`
  - `HasParameters` => `true`
- Implement `Apply(SKBitmap source)`:
  - Return `source.Copy()` if `LineStrength <= 0` or `Levels <= 0`.
  - Convert each pixel to luminance.
  - Quantize luminance into `Levels`.
  - Create contour lines by detecting boundaries between quantized steps (use a smooth ramp around step edges controlled by `LineWidth`).
  - Blend:
    - Lines use `LineColor` scaled by `LineStrength`.
    - Background is source darkened/brightened by `BackgroundStrength`.
  - Preserve alpha from source.
  - Clamp output channels.

Constraints:
- No new packages.
- Keep code deterministic (Seed not required for this one).
- Keep algorithm cheap enough for preview.

Validate with:
`dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false`

