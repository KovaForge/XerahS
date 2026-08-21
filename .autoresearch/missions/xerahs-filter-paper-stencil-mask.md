Read `AGENTS.md` and:
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/FilterImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ProceduralEffectHelper.cs`
- Example of threshold-based procedural effects:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/PixelSortingImageEffect.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/VintagePrintDamageImageEffect.cs`

Mission:
Create a new image effect filter class called `PaperStencilMaskImageEffect` in:
`ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/PaperStencilMaskImageEffect.cs`

Requirements:
- Namespace: `ShareX.ImageEditor.Core.ImageEffects.Filters`.
- Inherit from `FilterImageEffect`.
- Provide public properties:
  - `float Threshold` default around `140f` (0..255)
  - `float FeatherRadius` default around `8f` (0..200 heuristic; clamp internally to something like 0..30)
  - `float EdgeStrength` default around `70f` (0..100)
  - `float BackgroundDim` default around `35f` (0..100)
  - `bool InvertMask` default `false`
  - `int Seed` default around `1337`
  - `SKColor StencilColor` default (e.g. black with alpha > 0)
- Override:
  - `Name` => `Paper stencil mask`
  - `HasParameters` => `true`
- Implement `Apply(SKBitmap source)`:
  - Convert source pixels to luminance.
  - Create a mask:
    - `mask = luminance >= Threshold` (or inverted if `InvertMask`)
    - Feather edges using `FeatherRadius` with a smooth ramp around threshold (deterministic; can use smoothstep).
  - Apply stencil:
    - Where mask is on, output `StencilColor` (scaled by `EdgeStrength` or mask alpha).
    - Where mask is off, dim the original by `BackgroundDim`.
  - Preserve alpha.
  - Clamp output.

Constraints:
- No new packages.
- Keep code deterministic (Seed can be used for small edge jitter to avoid harsh banding).
- Keep it preview-friendly.

Validate with:
`dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false`

