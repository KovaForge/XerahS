Read `AGENTS.md` and:
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/FilterImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ProceduralEffectHelper.cs`
- Pixel procedural style:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/CRTImageEffect.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/ASCIIArtImageEffect.cs`

Mission:
Create a new image effect filter class called `RisoPrintImageEffect` in:
`ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/RisoPrintImageEffect.cs`

Requirements:
- Namespace: `ShareX.ImageEditor.Core.ImageEffects.Filters`.
- Inherit from `FilterImageEffect`.
- Provide public properties:
  - `float InkStrength` default around `70f` (0..100)
  - `float PaperFade` default around `25f` (0..100)
  - `float Offset` default around `3f` (Offset/Shift heuristic wants -200..200; clamp internally)
  - `float DotScale` default around `18f` (0..100; use as frequency for dot texture)
  - `float InkNoise` default around `35f` (0..100)
  - `int Seed` default around `2026`
  - `SKColor InkColorA` default non-transparent (e.g. red/orange)
  - `SKColor InkColorB` default non-transparent (e.g. cyan/teal)
- Override:
  - `Name` => `Riso print`
  - `HasParameters` => `true`
- Implement `Apply(SKBitmap source)`:
  - Return `source.Copy()` if `InkStrength <= 0`.
  - Create a halftone/dot texture based on pixel position and `DotScale` (use a seeded hash for dot randomness).
  - Compute luminance from source pixels.
  - Map luminance to two ink colors (ink A/B) with a slight seeded channel misregistration using `Offset`.
  - Add paper fade by mixing toward a near-white paper tone using `PaperFade`.
  - Add small ink noise using `InkNoise` and `Seed`.
  - Preserve alpha.

Constraints:
- No new packages.
- Deterministic for a given `Seed`.
- Keep it straightforward; avoid complex nested loops that are too slow for previews.

Validate with:
`dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false`

