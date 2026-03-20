Read `AGENTS.md` and the base types:
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/ImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/FilterImageEffect.cs`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ProceduralEffectHelper.cs`
- A couple of existing procedural filters for style/parameter clamping:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/CRTImageEffect.cs`
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/HolographicFoilShimmerImageEffect.cs`

Mission:
Create a new image effect filter class called `NebulaStarfieldImageEffect` in:
`ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/NebulaStarfieldImageEffect.cs`

Requirements:
- The class must be in namespace `ShareX.ImageEditor.Core.ImageEffects.Filters`.
- Inherit from `FilterImageEffect`.
- Provide the following public properties (parameter names matter for the editor UI heuristics):
  - `float Intensity` default around `70f` (0..100)
  - `float Scale` default around `80f` (0..100; clamp internally)
  - `float HueShift` default around `-15f` (-180..180; clamp internally)
  - `float StarDensity` default around `55f` (0..100)
  - `float StarSize` default around `10f` (0..200; clamp internally)
  - `float Twinkle` default around `40f` (0..100)
  - `float VignetteStrength` default around `18f` (0..100)
  - `int Seed` default around `1337`
- Override:
  - `Name` (user-facing) like `Nebula starfield`
  - `HasParameters` => `true`
- Implement `Apply(SKBitmap source)`:
  - Return `source.Copy()` if `Intensity <= 0` or `StarDensity <= 0`.
  - Generate a nebula glow plus seeded star speckles using `ProceduralEffectHelper.Hash01` (or similar) and per-pixel math.
  - Keep alpha unchanged (`dst.Alpha = src.Alpha`).
  - Blend the effect into the source (the source must remain visible as intensity increases).
  - Ensure all parameter values are clamped before use.

Constraints:
- Do not add new NuGet packages.
- Keep the algorithm deterministic for a given `Seed`.
- Prefer clear, small helper methods over large refactors.

Validate with:
`dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false`

