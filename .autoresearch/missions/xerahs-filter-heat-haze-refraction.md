Read `AGENTS.md` and the base types:
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/ImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/FilterImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ProceduralEffectHelper.cs`
- A couple of existing procedural filters for style/parameter clamping:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/HologramScanImageEffect.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/LensBlurImageEffect.cs`

Mission:
Create a new image effect filter class called `HeatHazeRefractionImageEffect` in:
`ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/HeatHazeRefractionImageEffect.cs`

Requirements:
- Namespace: `ShareX.ImageEditor.Core.ImageEffects.Filters`.
- Inherit from `FilterImageEffect`.
- Provide these public properties (parameter names matter):
  - `float Strength` default around `45f` (0..100)
  - `float Frequency` default around `40f` (0..100)
  - `float BlurRadius` default around `10f` (0..200; clamp internally to something reasonable like 0..30)
  - `float Offset` default around `6f` (Offset/Shift heuristics expect -200..200; clamp internally to >=0 or signed based on chosen warp)
  - `float LuminanceInfluence` default around `55f` (0..100)
  - `int Seed` default around `2026`
- Override:
  - `Name` => `Heat haze refraction`
  - `HasParameters` => `true`
- Implement `Apply(SKBitmap source)`:
  - Return `source.Copy()` if `Strength <= 0`.
  - Do a UV-style refraction warp using procedural noise:
    - For each pixel, compute an offset vector from seeded hashes/noise, scaled by `Strength`, `Frequency`, `Offset`, and optionally modulated by source luminance by `LuminanceInfluence`.
    - Sample the source at the displaced coordinate using bilinear sampling (use `ProceduralEffectHelper.BilinearSample`).
  - Optionally apply a mild blur blend (using SKImageFilter blur like other filters) with blend weight derived from `Strength`.
  - Keep alpha unchanged (`dst.Alpha = src.Alpha`).
  - Clamp all parameters.

Constraints:
- Do not add new NuGet packages.
- Keep deterministic behavior for a given `Seed`.
- Keep implementation understandable and avoid huge allocations inside pixel loops.

Validate with:
`dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false`

