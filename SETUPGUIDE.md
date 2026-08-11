# SETUP & USAGE GUIDE — World-Building Sprint (2026-07-17)

> User-requested runbook (explicitly authorized, like TESTGUIDE.md): how to USE everything
> built in the 2026-07-16/17 world-building sprint. CHECKLIST.md is the *what's left to do*
> board; this file is the *how it works / how to drive it* manual. Delete or fold into
> game.md when the sprint content is absorbed.

---

## 1. Story grading (StoryGradeDirector + 6 locked profiles)

**What exists:** `Settings/Grading/` — Act1_Warm (golden hour) · Shock (crack, hard cut) ·
EarlyFear (**blue dusk wake-up**) · MidPurpose · LateChaos · Ending_Losing. All six now have
ACES tonemapping + Motion Blur (0.2 warm → 0.5 shock). Values are LOCKED from the
Tsushima/Wukong/WWM synthesis ("two natures in one frame", game.md §1.1).

**How to drive it:**
- In play: select `StoryGradeVolume` owner (Persistent) → StoryGradeDirector →
  ContextMenu / GameDebuggerV2 → `SetStoryProgress(0..1)` scrubs act1 → ending.
  `PlayGrade("shock")` = the crack hard cut.
- Story wiring later calls the same two methods — no new mechanism needed.
- Failure sting is separate: `FailureResetSequencer` drives the prio-30 sting volume.

**Per-area identity:** `Settings/Grading/Areas/` — WhiteBalance + SMH only, one global
volume per area scene (prio 10). L1Park midtones are YOUR hand-tuned values — keep.

## 2. Sky states (SkyStateDriver + moon)

**What exists:** `SkyStateDriver` GO in Persistent driving `M_CoexistenceSkybox`;
states in `Settings/Sky/`: `day` (neutral/dev boot) · `golden_hour` (festival) · `dusk`
(post-cinematic wake-up — moon ON, sun 0).

**How to drive it:**
- Play mode → select SkyStateDriver → ContextMenu: "TEST: Apply golden_hour",
  "TEST: Blend to dusk (5s)", "TEST: Apply day". Blends are unscaled (run through pause).
- Story wiring later: `SkyStateDriver.Instance.BlendTo("dusk", seconds)` — same seam as
  the grade director. Boots into `day` (serialized `_bootStateId`).
- Tune by editing the three `SkyState_*` assets (colours/sun/moon per state).
**Moon & sun PLACEMENT (where the direction lives — it is NOT on the driver):** both the moon and
sun direction are authored **per state, INSIDE each `SkyState_*` asset** (Project → `Settings/Sky/` →
e.g. `SkyState_Dusk`) — *not* on the SkyStateDriver component (the driver just lists which state assets
exist, so you won't see any direction field there). Select the state asset to place them:
- **Moon** = a *fake disc painted in the skybox* (no light involved). On the state asset set **`moonDir`**
  — a direction vector = where the disc sits in the sky (e.g. `(-1, 0.2, 0.05)` = low, off to one side) —
  plus `moonIntensity` (0 = off), `moonSize`, `moonHalo`. Preview starting values: intensity 1.6, size
  0.14, dir (-1, 0.2, 0.05), halo 1.4. Changing `moonDir` moves ONLY the disc; it auto-dims with
  corruption and clouds pass in front automatically.
- **Sun** = the *real directional light* (the skybox sun disc reads `_MainLightPosition`, so it can't be
  a material value like the moon). On the state asset set **`sunDir`** (same kind of direction vector);
  the driver rotates the light so the **disc AND the shadows** move together. **One-time wiring:** drag
  the Persistent Directional Light into the SkyStateDriver's **`_sunLight`** slot (empty = sun left
  untouched — the safe default; `sunDir = (0,0,0)` on a state also = "don't move the sun here").
  **To find a good angle:** use the dev **`SunTestRotator`** (`Assets/Scripts/Debug/` — Azimuth/Elevation
  sliders, play-mode only) to scrub the sun live, read the light's resulting direction, bake it into the
  state's `sunDir`, then **remove/disable SunTestRotator before building** (it's dev-only, and both it and
  the driver rotating the light at once will fight).
- Both `moonDir` and `sunDir` are blended (Slerp) between states, so a `BlendTo` eases the moon/sun
  across the sky over the blend seconds.
- L2_Streets still uses the AllSkyFree HDRI — the driver can't reach it (checklist #48).

## 3. Terrain shader — now EIGHT layers with PoT features

**What changed (2026-07-17):** `Hidden/PoT/TerrainLit (Add Pass)` is new; `PoT/TerrainLit`'s
AddPassShader dependency points at it. Layers 5–8 now render with the SAME hex break-up,
parallax and corruption film as layers 1–4 (before: stock Unity add pass, no PoT features).

**The layer rules (performance contract):**
- Layers **1–4** = broad coverage (grass/soil/rock/path). One geometry pass.
- Layers **5–8** = ACCENTS only (small painted patches: moss ring, scorch, flowerbed).
  They cost a SECOND full geometry pass over the whole terrain — worth it only when used.
- **Hard cap 8** layers per terrain. 9+ = third pass = rejected.
- Distant/backdrop terrains stay ≤ 4 (one pass; nobody sees hex break-up at 400 m).
- L1 currently has 13–16 layers → still needs the trim to ≤ 8 (ideally ≤ 4 + accents).
- Height blend stays ≤ 4 layers only (Unity limitation, both shaders guard it).

**Known limits of the add pass (accepted):** the hidden add-pass material can't be tuned
per-terrain — hex/parallax dial values use the shader defaults (match `M_PoTTerrain` when
you retune it) and its keywords are driven GLOBALLY by TerrainQualityService.

## 4. Terrain details (grass/ferns) — PoT/DetailFoliage

- Detail prototypes: **"Vertex Lit" render mode + GPU instancing ON**, material =
  `M_PoTDetailGrass` (duplicate per texture — fern/bush/heather in `Art/Terrain/Details_Demo`).
- `_WindAmount` ~0.08 for grass, 0 for pebbles. Sways with the same `_PoTWind` global as
  the prop/cloth wind (WindDriver). Corruption-stains with the world. Darkens when wet (rain).
- Rocks: NEVER as Grass-mode details (they'd sway). Pebbles = VertexLit details OK;
  anything the player can touch = prefab with collider.

## 5. Fog systems — the map, how to drive each, and how to build a new one

> **Read this first — there are FOUR separate fog/shaft systems. Never conflate them (this was
> the #1 source of churn).** Each is owned differently; "change the fog density" means a different
> place for each one.

| System | Shader / owner | Driven by | What it does | Where it lives |
|---|---|---|---|---|
| **Global god-ray fog** | CristianQiu Volumetric Light (`Assets/Shader/VolumetricLights/`) | a **Volume override** ("Custom → Volumetric Fog") | raymarched fog that carves real **sun/moon shafts** through the shadow map | `FogVolume` in **Persistent** + `VolumetricFogRendererFeature` on `PC_Renderer` |
| **Global distance fog** | `PoT/CoexistenceFog` | material / globals | flat atmospheric depth haze toward the horizon | scene lighting |
| **Local placed fog** | `PoT/LocalFogVolume` (§5B) | a **material on a placed Cube** | author-placed pockets (paths, hollows, crack mouths) that **drift with the wind** | any scene, per-cube |
| **Sun shafts (old)** | `M_SunShafts` fullscreen feature + `SunShaftsDriver` | driver/material | screen-space radial god-rays (pre-dates the CristianQiu fog) | `PC_Renderer` idx 6 (§18) |

**Retired this session (2026-07-24):** `PoT/VolumetricFog` (our own fullscreen shadowed-fog attempt)
+ its `GameCameraShadowFullScreenFeature` — removed from the renderer; the CristianQiu global fog
replaced it. Files remain as dead code pending a cleanup commit.
**Open decision (user):** the CristianQiu **global god-ray fog** and the old **`M_SunShafts`** feature
now do overlapping jobs (both are "god rays"). Pick one; retiring `M_SunShafts` also ends its
BUG-077 .mat-dirtying chore (§18). Not decided yet — both are currently live.

---

### 5A. Global god-ray fog (CristianQiu Volumetric Light) — VOLUME-driven, no material to edit

The material is **hidden** (`Shader.Find("Hidden/VolumetricFog")`, auto-created by the renderer
feature) — so you do **NOT** tune this fog on a material. You tune it on the **Volume override**.

**What must exist (verify all three):**
1. `VolumetricFogRendererFeature` added on **`PC_Renderer`** (the active renderer). It self-resolves
   its two hidden materials via `Shader.Find` — no material slots to assign.
2. **Depth Texture ON** on the active URP asset (`PC_RPAsset`) — the raymarch reconstructs world
   position from depth. No depth → no fog.
3. A **Volume** with the **"Volumetric Fog"** override (Add Override → Custom → Volumetric Fog),
   its `enabled` box ticked, and the whole Volume set to **Global** (Mode = Global). Ours is the
   **`FogVolume`** GameObject in **Persistent** (so it survives every scene load, R3).

**How to change density / distance / god-rays (all on the override, live):**
- `enabled` — master on/off for this fog.
- `density` (0–1, def 0.2) — **this is "make the fog thicker/thinner".**
- `distance` (0–512, def 64) — how far the fog raymarches toward the horizon.
- `enableMainLightContribution` — **the god-ray toggle.** OFF = flat fog; ON = the sun/moon carves
  visible shafts through shadows. (This is the whole point of adopting this fog.)
- `anisotropy` (−1..1, def 0.4) — forward-scatter glow toward the light (higher = stronger halo).
- `scattering` (0–1, def 0.15) — in-scatter strength / brightness of the beams.
- `tint` — fog colour.
- `baseHeight` / `maximumHeight` + `enableGround`/`groundHeight` — vertical extent.
- `maxSteps` (8–256, def 128) and `blurIterations` (1–4, def 2) — **quality vs cost** (drop these
  first if it's heavy).
- `renderPassEvent` — when it injects (leave default: before post-processing).

**Moonlight:** it will follow the moon automatically — it samples whatever the **main directional
light** is, so when `SkyStateDriver` swaps sun→moon (§2), the shafts follow the moon with no extra
wiring.

---

### 5B. Local placed fog — `PoT/LocalFogVolume` (raymarched box volume, drifts with the wind)

The Tsushima/WWM "smoke layer you walk through" — an author-placed pocket you scale to fit a region.
**Reworked 2026-07-24** from the old flat single-sample sheet into a **true 3D raymarched volume with
3D wind-driven noise** (genuine depth — thicker where you look through more of it — instead of a
panning 2D sheet).

**Setup (this is the whole recipe):**
1. `Create → 3D Object → Cube`.
2. **Delete/disable its BoxCollider** (fog must never block movement or shots).
3. Assign a material using **`PoT/LocalFogVolume`** (duplicate `ParkLocalFogVolume.mat` for a new one).
4. **Scale** the cube over the area (e.g. `20 × 3 × 12`), base at ground level.
5. Done. Wind is **automatic** — it drifts with the `WindDriver` `_PoTWind` global (the same one the
   grass and lanterns move to), and stains with `_WorldCorruption`. No renderer feature, no Volume.

> **Note the wind only moves it in PLAY mode** — `WindDriver` runs in Persistent; in the editor (edit
> mode) the fog renders but sits still. That's expected.

**Material dials (all live-tweakable in `Art/Materials`):**
- **Fog Body:** `_FogColor`, `_Density` (raymarch density — after the 2026-07-24 rework a placed
  value of ~0.3 is a thin haze; bump toward **0.5–1.0** for a fuller pocket).
- **Fill & Gradient:** `_GradientMode` (Uniform / **Bottom** / Top / Scattered), `_Fill` (fraction of
  box height the fog fills), `_HeightFade` (softness/feather of the fill edge).
- **Noise:** `_NoiseScale` (world m), `_NoiseAmount` (break-up 0 = smooth → 1 = fully broken).
- **Wind Drift:** `_DriftSpeed`, `_DriftSecondary` (second layer speed), `_DriftPeriod` (drift wrap
  period in seconds — leave ~120; it exists only to keep noise-coordinate precision safe, see §5C).
- **Soft Fades:** `_DepthFade` (soft where fog meets geometry), `_CameraFade` (soft as the camera
  enters), `_EdgeFade` (fraction of box — hides the cube walls so the box outline never reads).
- **Light Tint:** `_LightInfluence` (keep low ~0.15 — shafts are the global fog's job), `_Anisotropy`.
- **Corruption:** `_CorruptionColor`, `_CorrAmount`, `_CorruptionBias` (preview slider).
- **Quality:** `_Steps` (raymarch steps 8–48; drop to lighten cost).

**Where to place:** path to the temple mountain, crack mouths, under decks, cold hollows, L3 alley
floor. Layer 2–3 boxes of different heights + a `Fog.vfx` wisp for the premium spots.
**Perf:** one transparent raymarch pass over the box's screen area. Keep `_Steps` modest and avoid
stacking 5+ full-screen boxes in one view; 2–3 overlapping is fine.

---

### 5C. If you ever want to build a NEW fog shader from scratch (the box-volume recipe + footguns)

`PoT/LocalFogVolume` is the reference. To author another box-bounded volume shader:

1. **It's a MESH MATERIAL, not a renderer feature.** Put it on a default Cube (object space ±0.5).
   This avoids the entire MSAA / cameraColor-reassignment class of bugs that plagued the fullscreen
   attempt — a material pass writes into the normal transparent target and needs no custom pass.
2. **Render state:** `Blend One OneMinusSrcAlpha` (premultiplied — the raymarch accumulates
   coverage-weighted in-scatter), `ZWrite Off`, **`Cull Front`** (shade the BACK faces so a pixel's
   box is shaded exactly once, and it still works when the camera is inside), **`ZTest Always`** (do
   NOT let the box's own depth reject the pixel — occlusion is done per-ray in the shader instead).
3. **Ray-box intersection** in object space against the unit box `[-0.5, 0.5]` (slab test) — handles
   any position/rotation/scale for free. Convert the object-space entry/exit back to WORLD distances
   along the view ray, then march between them.
4. **Occlude against opaque geometry** by sampling `SampleSceneDepth` (needs Depth Texture ON),
   reconstructing the opaque world position, and clamping the far march distance to it — per-ray, so
   partial occlusion (fog in front of a wall AND behind it) works.
5. **Wind drift WITHOUT the dither bug — the one real footgun.** Scroll the noise domain along
   `_PoTWind.xz`, but the time offset **must be bounded**: `fmod(_Time.y * speed, _DriftPeriod)`.
   An unbounded `_Time.y * speed` offset grows the noise coordinate until float32 precision breaks
   down → the dither/stipple you saw on the old fullscreen fog. Two opposed octaves hide the wrap.
6. **Includes:** `Core.hlsl`, `Lighting.hlsl` (for `GetMainLight`), `DeclareDepthTexture.hlsl`.
   Wind/corruption globals (`_PoTWind`, `_PoTWindGust`, `_WorldCorruption`) go **outside** the
   `UnityPerMaterial` CBUFFER (they're `Shader.SetGlobal*`, same contract as `CoexistenceCommon.hlsl`).
7. **No shadow sampling.** Realtime-shadow light shafts are the CristianQiu global fog's job; a local
   pocket is a soft wind-blown body. (Binding the shadow map into a custom pass is exactly the pain
   we avoided by adopting their fog — don't reintroduce it here.)

---

### 5D. FOG VERIFICATION CHECKLIST — assign / make / verify (run to confirm "the full thing works")

**Global god-ray fog (CristianQiu):**
- [ ] `VolumetricFogRendererFeature` present on **`PC_Renderer`** (active renderer).
- [ ] **Depth Texture ON** on `PC_RPAsset`.
- [ ] `FogVolume` in **Persistent**: a **Global** Volume with the **Volumetric Fog** override,
      `enabled` ✓, `enableMainLightContribution` ✓ (for the shafts).
- [ ] Play a lit scene with shadow-casting geometry between camera and sun → **visible shafts**;
      raising `density` thickens the fog; `SkyState` sun→moon swap → shafts follow the moon.
- [ ] Old `PoT/VolumetricFog` / `GameCameraShadowFullScreenFeature` are **NOT** on `PC_Renderer`.

**Local placed fog (`PoT/LocalFogVolume`):**
- [ ] A Cube with **no collider** + a `PoT/LocalFogVolume` material, scaled over the target area,
      base at ground.
- [ ] In Play: the pocket renders as soft fog, **drifts** (wind), fades softly at its edges/top and
      where it meets the ground, and is **occluded** correctly by walls/props (no bleed through solids).
- [ ] `_Density` re-tuned post-rework if a pre-2026-07-24 placement now looks too thin (~0.3 → ~0.5+).
- [ ] `ParkLocalFogVolume.mat` still carries its tuned values (the in-place shader rework preserved
      every property name + its GUID — no re-authoring needed).
- [ ] No console errors; `_Steps` modest; ≤ 2–3 overlapping boxes per view.

**Sun-shaft decision:**
- [ ] Decide keep-or-retire `M_SunShafts` vs the CristianQiu god-rays (they overlap). If retiring
      `M_SunShafts`, remove its fullscreen feature from `PC_Renderer` — that also ends the BUG-077
      `git checkout M_SunShafts.mat` pre-commit chore (§18).

## 6. Rain system (toggle + one intensity dial)

**Shader side (DONE):** `_PoTWetness` global (0 dry → 1 soaked) is read by:
- `PoT/Coexistence` — albedo darken + light glint (custom-lit spec)
- `PoT/TerrainLit` + add pass — albedo darken + smoothness push (wet gloss)
- `PoT/DetailFoliage` — grass darkens with the ground
Exact no-op at 0. Textures for drops/ripples/drips/puddles extracted GUID-intact to
`Assets/Art/VFX/Rain/` (from the ShaderGraph weather sample, pre-deletion).

**Runtime side (BUILT + wired in Persistent):** `RainSystem` GO in Persistent —
`RainController` + `RainDrops` particle system (world-sim, camera-following box, world
COLLISION: drops die on any surface — so roofs shelter correctly) with two collision
sub-emitters (`RippleOnHit` expanding ring, `SplashOnHit` droplet spray) + `RainLoopAudio`
child (looping 2D AudioSource, **clip slot empty — assign your rain loop**).
- Drive it: `RainController.SetRain(bool, intensity)` — 0.15 ≈ drizzle, 1 = downpour —
  or the ContextMenu tests (Drizzle / Heavy / Stop) in play.
- Wetness LAGS (soak ~12 s, dry ~45 s) and writes the `_PoTWetness` global; zeroed on
  destroy so play never leaks a soaked look into the editor.
- `RainDripLine` = area-resident eave dripper (add under roof edges with a small
  ParticleSystem child): polls the controller, keeps dripping ~6 s after rain stops,
  fails open with no RainController (direct-area play).
- Materials/textures in `Assets/Art/VFX/Rain/` (M_RainDrop / M_RainRipple with the
  generated `ripple_ring.png` / M_RainSplash) — tint/alpha to taste.

## 7. Wind — one global system, everything listens

`WindDriver` (Persistent) owns `_PoTWind` (xyz dir, w strength) + `_PoTWindGust`.
Listeners: Coexistence wind-enabled materials (cloth/banners/ofuda — `_WindEnable` +
Hanging mode), `WindSwayPivot` (lantern hooks), PoT/DetailFoliage (grass), and now
PoT/LocalFogVolume (haze drift). One dial moves the whole world — that's the Tsushima trick.
`LocalWindZone`s at cracks push harder locally.

## 8. Time-of-day arc — how the pieces play together (game.md §1.1)

1. **Golden hour** (festival): grade `act1_warm` + sky `golden_hour`.
2. **Crack cinematic**: grade `shock` (hard cut) — sky untouched (cut hides it).
3. **Blue dusk wake-up**: grade `early_fear` + sky `dusk` (moon on, lanterns = key light).
4. Escalation: fog volumes denser, `RainController.SetRain(true, 0.2→0.6)`,
   `_WorldCorruption` climbs — all FROM dusk, night never fully falls.

**Firing a beat at runtime — `SkyboxMaterialChange` (the repurposed story-beat trigger,
`Assets/Scripts/Environment/`):** one activation-fired checkpoint object that drives any combination of
the three channels at a story point:
- **Sky** → `SkyStateDriver.BlendTo/ApplyState` (added 2026-07-24) · **Corruption** →
  `WorldAmbienceDriver.TransitionTo/SetProgress` · **Grade** → `StoryGradeDirector.PlayGrade`.

**Setup:** place one GO per beat. It fires on **Timeline Activation** (an Activation Track turning it on —
the intro Act 9 pattern; the Persistent `SkyboxChanger` GO is the first instance), plain scene activation
(Start), or code calling `Fire()`. Per-instance fields: `_targetProgress` + `_transitionSeconds`
(+ `_onlyIncrease` = never pull an already-fallen world back), `_gradeId` (empty = grade untouched),
`_skyStateId` + `_skyBlendSeconds` (empty = sky untouched; e.g. `dusk`, 4 s). One shot per activation
lifetime; resolves the Persistent singletons at fire time (R4), fails loud. **NOT persisted** across
Restart yet. Legacy class name — the rename to `StoryBeatTrigger` is a post-playtest GUID-preserving
commit (game.md §20.3).

**Play the channels without placing a trigger** — GameDebuggerV2 (`Ctrl+\``, Trainer-gated): the **World
& Sky** section = live corruption slider + sky-state buttons; **Story grading** = progress slider +
grade-id buttons; **Grayscale world test** = desaturation slider (checklist item 21). These benches are
on GameDebuggerV2 (TestLab-resident) — to drive them while walking a real area, the panel must be in that
scene (open decision: add GameDebuggerV2 to Persistent, dev-gated, so it's everywhere).

## 9. Ability VFX vs bushes/grass — the occlusion answer

- **No physical blocking**: terrain details have NO colliders — nothing about casting,
  raycasts or projectiles is affected. Prefab bushes' colliders (if any) only matter if
  the ability code raycasts against their layer — keep decorative prefabs on an
  ignore-raycast/environment-deco layer.
- **Visual overlap**: tall opaque grass CAN draw in front of a flat ground-circle quad
  (it writes depth — that's correct sorting, it just hides the ring). Fixes, in order:
  1. Ground rings/cast circles float ~0.05–0.1 m above ground (most already do).
  2. Keep gameplay-critical decals bright/emissive — they read THROUGH thin grass gaps.
  3. Don't paint max-density tall grass inside combat arenas (author rule — Tsushima
     clears vegetation around landmarks/fights for exactly this reason).
  4. Nuclear option for a specific cue: its material ZTest = Always (draws over
     everything — use only for player-owned target indicators, sparingly).

## 10. Scale & camera (locked findings)

- Twins = 2.0 m CharacterController — the world's measuring stick. Don't resize players.
- **FOV 40 → ~52** on gameplay vcams = the single biggest scale-feel fix (checklist #49).
- Metrics: doors 2.4–2.6 · storeys 3.4–4.0 · shrine 4.5–6 · torii 5–7 · lanterns 2.6–3.2
  · bushes 0.6–1.2 · trees 8–15 · banner drop 2.5–3.5.
- Judge scale ONLY through the gameplay camera in play — never in Scene view next to a capsule.

## 11. Temple mountain / vista landmark (the "weenie")

One bespoke luminous peak at the SAME azimuth in every scene; player always sees the goal.
Build order (see chat answer + checklist #53): greybox silhouette first (stacked primitive
masses, ~400–700 m out), emissive windows + fake light-beam cones + mist skirt
(LocalFogVolume boxes + Fog.vfx), then detail only the silhouette that reads at distance.
No concept art required to START — the greybox IS the concept; art can trace over a
screenshot later if we want a bespoke sculpt.

## 12. VFX draw-on-top (GroundVFX layer) — ground telegraphs over grass

For gameplay-critical ground VFX (cast circles, meteor ground-cracks, AOE rings) that must
never hide behind grass/props:
- **Authoring:** tick **Draw On Top** on the cue element (Transform Overrides block in the
  Cue Book editor). That's the whole workflow — FxManager moves the spawned instance onto
  the `GroundVFX` layer at play time and restores it on pool return (pool-safe, prefab
  untouched, same prefab usable both ways).
- **Under the hood:** `GroundVFX` layer (21) + `GroundVFXOnTop` RenderObjects feature on
  PC_Renderer (After Skybox, transparent queue, depth test Always, write off) — drawn
  after opaques so nothing occludes it, before transparents so fog/rain still veil it.
  The layer is excluded from the renderer's normal transparent pass (no double draw).
- Linter F8 flags the flag on Sound/Manpu elements (no visual — does nothing).
- Use it SPARINGLY — a telegraph that draws through walls is information, ten of them is
  visual soup.

## 13. Modular building kit (Opus pass)

`Assets/Art/Props/Kit/` — 51 baked prefabs (walls, plinths, decks, roofs, trims, pillars,
railings, doors/frames, windows, stairs, shrine pieces, modern accents), each with its own
mesh + bounds-matched BoxCollider, existing `M_Coex_*` materials preserved.
- **Pivot = bottom-centre**, box pieces snapped to 0.25 m where non-distorting.
- **KitPreview_Row** (inactive) in SampleScene at x ≥ 155 — activate to browse, delete when done.
- The six `*_Source_PB` houses + assembled house prefabs are untouched.
- Follow-up option: thin 1–2 m wall-panel variants for true tile-based assembly.

## 14. Metrics gym (scale reference)

`MetricsGym` in SampleScene at x −40…−28, z −20: capsule 2 m (twin) · door 2.5 m ·
storey 3.6 m · torii 6 m · lantern 3 m · bush Ø1 m · tree 12 m. Walk the twins next to it
(through the GAMEPLAY camera, FOV ~52 per checklist #49) and compare every prop you place.
Delete the gym when your scale pass is done.

## 15. Temple vista — status

`Assets/Art/Props/Vista/TempleMountVista.prefab` (mountain re-centred on its true peak,
420 m; 3 glow-band temple tiers + spire + 3 additive god-beams + LocalFogVolume mist skirt;
zero colliders) — staged in SampleScene at (80, −20, 620) + `SkyboxMountainsRing_Staged`
(the extracted horizon ring, off-palette snow material = greybox). Judge from ground level
in play; dial the five `M_Vista*` materials; place one instance per real scene at the SAME
azimuth (§11).

## 16. APV (Adaptive Probe Volumes) bake — this project's multi-scene recipe

Why APV over Light Probe Groups: auto-placement, per-pixel sampling, per-cell streaming —
the right fit for streamed additive scenes (game.md §17.1). One-time setup, then per-zone bakes.

1. **Enable once:** the active `PC_RPAsset` → Lighting → Light Probe System = **Adaptive Probe
   Volumes** (if it's already set, skip).
2. **Baking Set (the multi-scene part):** `Window > Rendering > Lighting > Probe Volumes` tab →
   create ONE Baking Set containing **Persistent + the area scene(s) being baked** (checklist
   #10 rule: Persistent is always in the set — the twins/managers live there and need valid
   probes). Scenes in the same set share streamed cells; SceneFlowManager's additive loading
   just works with it.
3. **Volume placement:** each area scene gets one **Probe Volume** GO sized over its playable
   space (Global mode is fine to start). Density defaults first; densify later only where light
   changes fast (interiors, crack mouths).
4. **Bake order rule (checklist #10):** bake AFTER buildings/rocks are placed — flora doesn't
   affect the bake. Re-bake per zone after big geometry moves, not after prop dressing.
5. **Verify:** `Window > Rendering > Rendering Debugger > Probe Volumes` → display probes; play
   both entry paths (Bootstrap + direct-area) and check nothing renders black — black objects =
   they sit outside every baked cell.
6. Gotcha: crack meshes must NOT have Contribute GI static flag (checklist #11) or they poison
   the bake with their emissive.

## 17. Occlusion culling bake — per-scene recipe

Baked independently per scene; Unity merges cull data for additively loaded scenes at runtime
(this is why each scene folder co-locates its own OcclusionCullingData — the folder-per-scene
convention).

1. Open the area scene (Persistent additive is fine — it has no static geometry and needs no bake).
2. Mark big blockers (buildings, walls, terrain props ≥ ~2 m) **Occluder Static**; everything
   that can be hidden **Occludee Static**. Small props/foliage = occludee only, never occluder.
3. `Window > Rendering > Occlusion Culling > Bake` — default cell sizes first.
4. Verify with the Occlusion window's Visualization tab + a camera fly-through: watch for objects
   popping in late (cells too coarse) — only then tune Smallest Occluder up/down.
5. Re-bake after any big geometry move in that scene. Terrain itself is not an occluder — the
   buildings on it are.

## 18. Post-process map — WHICH volume does WHAT (+ tonemapping)

The full architecture table is **ArtStyle.md §11** (priorities/authoring) and the base-look
numbers are **ArtStyle.md §11.2**; game.md §17.1 holds the render settings (HDR grading + ACES).
Quick orientation:

| Volume | Where | Prio | Owns |
|---|---|---|---|
| StoryGradeVolume (A/B pair) | Persistent, exactly 1 | 0 | the 6 story grades (act1_warm → ending_losing), ACES tonemap, contrast/sat, bloom, motion blur — the BASE look |
| Area identity volumes | 1 per area scene | 10 | hue/temp shift ONLY (area mood on top of the story grade) |
| CrackDesatVolume prefab | each crack | 20 | local desat −20 + magenta shadow lift |
| FailureResetSequencer sting | Persistent slot | 30 | failure flash (desat −80, vignette pulse, CA) — weight-driven, rests at 0 |

Rules of thumb: tonemapping (ACES) lives ONLY in the grade profiles — never add a second
Tonemapping override in area/crack volumes (highest-prio would silently win and double-grade).
Area volumes stay hue/temp-only so the story grade always reads through. Debug: GameDebuggerV2
(Ctrl+`) has grade buttons + a story-progress scrub slider.

**Sun shafts:** the god rays are a fullscreen renderer feature driven by `SunShaftsDriver` +
`M_SunShafts.mat`. There is no setup left to do — but the driver dirties the .mat on every Play
(BUG-077, WON'T FIX): run `git checkout -- Assets/Art/Materials/M_SunShafts.mat` before every
commit. If rays ever vanish: check the `_shaftsMaterial` slot on the driver in Persistent
(the known trap — BUGS.md 077).

**Crack gradient (still UNBUILT):** the colour-bible crack look (Pure Current blue → Khal-Vor
green depth gradient driven by one `_Corruption` float, UV.y depth ramp + slow pan) is designed
but NOT implemented — `Shader Graphs/CrackGlow` still has single `_colour`+`_EmissionColor`.
Canon: memory/Colour Bible §7; the current violet-magenta on the crack materials is the clash
this replaces.

## 19. Glass HUD panel (PoT/UIGlassPanel) — full usage

Live sample: **SampleScene → `GlassPanelSample`** (Screen Space Camera via `GlassSampleCam`) —
top = round-1 baseline (`M_UIGlassPanel`), bottom = approved direction
(`M_UIGlassPanel_FableV2`). Delete the whole `GlassPanelSample` + `GlassSampleCam` when done
judging; the materials/shader/script stay.

**To put a glass panel anywhere:**
1. Under any Canvas, create a UI **Image**; clear its Sprite.
2. Assign material `M_UIGlassPanel_FableV2` (or a duplicate per panel look).
3. Add **`UIGlassPanelView`** to the same GO — this is MANDATORY: it clones the material
   (Graphic.material writes to the shared .mat otherwise — the round-1 dirty-asset bug) and
   pushes `_PanelSize` on every rect resize so the caps/notch never stretch.
4. Size it with the **RectTransform width/height as usual — YES, you can adjust freely.**
   Width just grows the straight middle run (caps + notch stay fixed); height rescales the
   whole silhouette proportionally (every shader size is in panel-height units). Sleekness
   therefore = rect height + `_MidThickness` together.

**Dial map (on the material — all safe to drag live):**
| Group | Dial | What it does |
|---|---|---|
| Blade Silhouette | `_MidThickness` | body thickness (sleek ≈ 0.42–0.5; round-1 "squat" was 0.52 at 150px height) |
| | `_TipThickness` / `_CapLength` | how thin and how long the tapered ends run |
| | `_EdgeRound` / `_CentreOffset` | corner rounding / vertical bias |
| Centre Notch | `_NotchWidth/Height/CentreY/Round/Blend` | the emblem seat — keep fixed once the emblem is placed |
| Glass Body | `_GlassColor` (alpha = opacity), `_GlassTop`, `_SheenStrength/Angle/Width`, `_RimGlow/_RimFalloff` | the frosted look; darker body if it washes out on bright skies |
| Outline | `_EdgeColor` (HDR) `_EdgeWidth/_EdgeSoft` | the rim line the current rides on |
| Edge Current | `_CurrentColorA` (LEFT, gold/Lyra) · `_CurrentColorB` (RIGHT, violet/Kai) · `_CurrentStrength/Speed/Count/Sharpness/FadeStart/Width` | the two clan currents running tip→centre |

**Approved V2 values (2026-07-21, vs round-1):** MidThickness 0.46 · TipThickness 0.10 ·
CapLength 1.3 · EdgeRound 0.09 · RimGlow 0.75 · RimFalloff 0.14 · Sheen 0.35 · GlassTop 0.22 ·
GlassColor (0.045,0.05,0.085,0.5) · Edge (0.85,0.88,1.1,0.95) · CurrentStrength 2.2 ·
CurrentWidth 0.08 · CurrentSpeed 0.5 · currents HDR gold (1.6,1.25,0.46) / violet (0.78,0.48,1.6).
Sample panel rect: 980×118.

**Gotchas:** never assign through `Image.material` at runtime yourself (the view owns the clone) ·
a panel rendering as a PLAIN WHITE QUAD = its material reference died (round-1 footgun — re-assign
the .mat) · the shader is UI-masked/stencil-aware, safe under RectMask2D.

## 20. Ring timer (PoT/UIRingTimer) — the ONE timer/cooldown widget

Built fresh 2026-07-21 (the branch's `PoTUIRadial` is retired — never recover it). This is the
shared "ring language" from game.md §17.5: ability cooldowns, the QTE timer, any radial rundown
all use THIS. **Not hollow** — solid disc fill with an Overwatch-resurrect-style rim: faint full
backing circle + bright arc that grows with progress + glowing leading tip + soft inward glow.

Live sample: SampleScene → `GlassPanelSample` → three rings below the panels, editor-demo
animating without play mode: `Ring_Kai_Sweep` (violet, angular rundown) ·
`Ring_Lyra_CentreOut` (gold, the cooldown centre-out fill) · `Ring_Shared_Dual` (both clan
colours as halves — the COMMON-ability look).

**To use anywhere:** UI Image (no sprite) → assign one of `M_UIRingTimer_{Kai,Lyra,Shared}`
(or a duplicate) → add **`UIRingTimerView`** (mandatory — owns the material clone + the ready
flash). Consumer code: `SetProgress(0..1)` per frame; `PlayReadyFlash()` once on available
(top→bottom sweep, UNSCALED time so it plays through pause/Setsuna; dual-clan mats flash each
half in its own colour).

**Dials:** `_FillMode` Sweep/CentreOut/LeftRight/TopDown + `_InvertFill` · `_DualClan` +
`_FillColorA` (left/single) `_FillColorB` (right) · `_BackColor` unfilled · `_OuterRadius` /
`_InnerRadius` (0 = solid; raise for a band) · `_RimWidth`/`_RimColor` (the arc) · `_TipGlow`/
`_TipWidth` (leading tip, sweep mode) · `_FlashWidth`/`_FlashBoost` (ready flash). Editor
preview: tick `_editorDemo` on the view.

**QTE extras (2026-07-21 upgrade — modern readability, not the "old style" plain radial):**
- `_ClosingRing` — Sekiro-style ghost ring that CONTRACTS from `_ClosingMax`×outer onto the rim
  as time runs out; timing reads at a glance without tracking the arc. The rect must be ~2×
  the disc (QTE mat uses `_OuterRadius` 0.27 so the closing start fits inside the quad).
- `_Ticks`/`_TickStrength` — thin rim segment marks (Ragnarok-style clean segmentation).
- `_UrgencyThreshold`/`_UrgencyColor`/`_UrgencyPulse` — below the threshold the fill/closing
  ring heat toward the urgency colour and pulse ("running out" reads peripherally).
- `M_UIRingTimer_QTE` = closing ring ON + 12 ticks + band shape (inner 0.2/outer 0.27),
  warm-white fill, gold arc, hot gold tip.

**Colour rule for EVERY UI material under our post stack (ACES, ArtStyle §11.2):** ACES pushes
mid-bright HDR values toward white — so hue lives in SATURATION at moderate intensity
(fills ≈ 1.1–1.3 max channel), and only tips/flashes/currents go hot (≥2) where white-out IS
the read. If a UI colour looks washed in-game, lower its intensity before touching saturation.
Ring fills retuned to this rule (Kai 0.62,0.38,1.28 · Lyra 1.28,1.0,0.37).

## 21. ABILITY ICONS — full execution runbook (written 2026-07-21, NOT yet executed)

> The one remaining UI phase (CHECKLIST 66b/66d). Spec authority: game.md §17.5 (FINALISED
> block). This section is the complete build order for any future session — follow it verbatim,
> do not re-derive. Phases 1–2 (bars, emblem) are DONE (commit e903483). The ability HUD binder
> (`AccordHUDController` + `AccordIconSlot` + `AbilityIconUI`) EXISTS AND WORKS — §17.2 ruling:
> anyone who "finds it has no binder" is wrong; STOP and re-read §17.2 before writing any code.

### A. The shader — ✅ BUILT 2026-07-21 (`Assets/Art/Shaders/PoTUIAbilityIcon.shader`, imported 0 errors — UNTESTED visually; A/B it on the SampleScene board before wiring)
Part A below is now DONE as written; parts B–D (driver, materials, integration) remain. The
built shader adds one extra beyond this spec: a faint clan-coloured highlight line at the
colour-creep front, and a `_RevealMode` BottomUp fallback next to the locked CentreOut default.

#### Original part-A spec — make a NEW `PoT/UIAbilityIcon` (do NOT extend UIRingTimer)
Why new: the ring draws a DISC + arc; the icon effect must mask the SYMBOL SPRITE itself
(user spec: "colour creeps over the symbol"). Mixing both into one shader couples two
finished widgets. Copy the UI plumbing verbatim from `PoTUIRingTimer.shader` (stencil block,
clip-rect, alpha-clip, Blend SrcAlpha OneMinusSrcAlpha — lines 1–90 are reusable as-is).

Properties it must contain (each maps to one spec line in §17.5):
- `_MainTex` (the symbol sprite, [PerRendererData]) — ANY user-supplied symbol works; the
  shader only ever reads its alpha + rgb. No per-ability shader variants, ever.
- `[Toggle] _DualClan` + `[HDR] _ClanColorA` (gold 1.28,1.0,0.37) + `[HDR] _ClanColorB`
  (violet 0.62,0.38,1.28) — clan-specific = single colour, COMMON = both as half rings
  (split on uv.x, same convention as PoT/UIBar `_SplitPoint` 0.5). ACES rule (§20): fills
  ≤ ~1.3 max channel, only flashes ≥ 2.
- `_GreyColor` (≈0.42,0.43,0.47) + `_GreyStrength` — the UNAVAILABLE look. While cooling,
  the symbol renders greyscale-tinted (lerp by luminance), NOT hidden and NOT alpha-faded —
  the player must still read WHICH ability it is.
- `_Recharge01` + `[Enum] _RevealMode` (CentreOut/LeftRight/TopDown/Radial + `_InvertReveal`)
  — the reveal mask: where mask==1 the symbol shows in CLAN colour, where 0 it stays grey.
  CentreOut = `saturate(distance(uv,0.5)/0.7071) > _Recharge01 ? grey : clan` (soft edge
  `smoothstep(±_RevealSoft)`). This is the "colour creeping over the symbol from the centre".
- `_FlashT`/`_FlashWidth`/`_FlashBoost` — the top→bottom ready sweep, copy the block from
  UIRingTimer verbatim; dual-clan flash tints each half its own colour (also verbatim).
- `_DormantStrength` — multiplies the whole result toward `_GreyColor` and drops alpha to
  ~0.55: the Weaver's-Gate/shared-emblem "dormant until available" idle (66d). 0 = normal.

### B. The driver — `UIAbilityIconView.cs` (mirror `UIRingTimerView` structure exactly)
- `[RequireComponent(Image)]`, explicit material clone in Awake (Graphic.material does NOT
  auto-instance — the round-1 footgun), Destroy clone in OnDestroy, fail-loud when the
  material's shader isn't `PoT/UIAbilityIcon`.
- Public API (the ONLY writes): `SetRecharge01(float)` (1 = ready), `SetReady(bool)` (drives
  an internal edge: false→true fires the flash ONCE), `PlayReadyFlash()`, `SetDormant(float)`,
  `SetDualClan(bool)` — flash timer on UNSCALED time (R10: UI feedback plays through
  pause/Setsuna), everything else event/poll from the binder.
- Keep the `_editorDemo` preview pattern from UIRingTimerView so look review needs no play mode.

### C. Integration — INSIDE the existing binder, additive-slot pattern only
1. Read `AbilityIconUI` / `AccordIconSlot` first (Assets/Scripts/UI/Abilities/...). Find where
   each slot today writes its cooldown display (fillAmount / colour / text — whatever exists).
2. Add ONE optional serialized field to that class: `[SerializeField] UIAbilityIconView iconView;`
   — exactly the `HealthBarView.barView` pattern (§17.5 gotchas): field EMPTY ⇒ legacy path
   byte-identical; field SET ⇒ route the SAME cooldown value into `SetRecharge01` +
   ready-edge into the flash, and skip the legacy writes. NO other file changes. NO new
   binder, NO parallel HUD (§17.2 rule 1 — enforced review criterion).
3. Cooldown source of truth: whatever the binder already reads (AbilityController / ability
   instances). DO NOT invent a new cooldown query path — if the binder shows cooldown today,
   the number already flows; reroute it, don't re-derive it.
4. Per-icon authoring: on each icon GO add an Image w/ a per-ability material instance
   (duplicate `M_UIAbilityIcon_Base` → set symbol sprite + `_DualClan` per ability;
   clan map: Kai=violet, Lyra=gold; common accord abilities = dual). Keep the user's freedom:
   ANY sprite dropped in `_MainTex` just works.
5. Dormant/available (66d): shared emblem + Weaver's Gate icon get `SetDormant(1)` while the
   system reports unavailable and `SetDormant(0)`+flash on available. Sources: gate =
   TeleportAbility availability the binder already exposes; emblem = accord/charge state.
   Same rule: reroute the existing signal, never add a new poll.

### D. Verification gate (run ALL before calling it done — §10-style)
1. Compile 0 errors → enter play in TestLab AND Bootstrap path (two entry paths minimum).
2. One icon wired FIRST → screenshot → USER LOOK SIGN-OFF → only then propagate to the rest.
3. With `iconView` slot EMPTY on one control icon: confirm legacy display is byte-identical.
4. Cooldown cycle: fire ability → symbol greys instantly → colour creeps centre-out as it
   recharges → single top→bottom flash at ready → stays coloured. Confirm under pause AND
   Setsuna (flash must still play; recharge must freeze per R10 scaled time).
5. Dual-clan icon: halves tint separately, ready flash runs both halves L→R per §17.5.
6. Pooled/Restart canary: Restart → Bootstrap reload → icons still driven (no stale material,
   no duplicate flash), console clean both runs.

### E. Footguns (all previously hit — do not rediscover)
- `[Header(...)]` text: NO commas/parens/hyphens (hit twice).
- Component refs via MCP tools: GO instanceIDs do NOT auto-resolve into component fields —
  set with `{"instanceID": <componentID>}` and ALWAYS read the component back to verify.
- White-quad = dead material reference (§19 gotchas).
- Never edit `.playable`/scene YAML by hand; never a second Tonemapping override (§18).
- The old ability HUD stays ACTIVE until the new icons are user-approved — same
  keep-both-then-retire pattern as the shared emblem/slider.

### F. Panel expansion — ALREADY BUILT, just call it (verified 2026-07-21)
`UIGlassPanelView.SetSlotCount(int slots, bool instant=false)` is the whole feature: width =
`_baseWidth + _widthPerSlot × slots`, animated at `_growSpeed` on UNSCALED time (grows during
pause/Setsuna), notch + caps stay fixed (only the middle run stretches — shader design), and
`_PanelSize` is re-pushed on every rect change. Integration = ONE call site in the binder:
wherever an ability slot becomes unlocked/visible (the same place part C reroutes cooldown),
call `panelView.SetSlotCount(unlockedCount)`. Rules:
- Count = UNLOCKED base-ability slots only. Accord-state abilities REPLACE a base slot in
  place and must NEVER change the count (mid-combat resize is the bug this rule prevents).
- Resolve the panel via a serialized `UIGlassPanelView` slot on the binder (same-scene R1) —
  optional, null = panel keeps its hand-authored width (additive-slot pattern again).
- Tune `_baseWidth`/`_widthPerSlot` in the Inspector against the real icon size once part C
  places icons; the approved V2 material values live in §19.

### G. Retiring the OLD ability HUD (only AFTER user approves the new icons in-game)
Same keep-both-then-retire pattern as the shared slider. Order matters:
1. **Identify, don't guess**: the old HUD = the current on-screen ability indicators under the
   Persistent HUD canvas (the ones showing literal key letters), plus the old shared-health
   slider if the user retires it in the same pass. List the exact GameObjects to the user
   FIRST — the memory rule applies: never delete objects you did not create without sign-off.
2. **Deactivate, don't delete** (first session): `SetActive(false)` on the old HUD roots in
   Persistent.unity, save scene. Play all four entry paths (Bootstrap, dev-mode, direct
   L1/L2) — check the console for LogErrors from anything that resolved those objects
   (presenters/binders with serialized refs to them will fail loud per R4; if one does, the
   old HUD had a live consumer — REROUTE that consumer to the new view before deactivating).
3. **Delete only in a later isolated commit**, after at least one full playtest with the
   deactivated state; remove the GameObjects AND any now-orphaned presenter components, then
   grep for dangling `FormerlySerializedAs`/field refs. Never bundle the deletion with
   feature work (CLAUDE.md rule 7).
4. ControlHints note: `ControlHintsVisibility` captures rows inactive at init as "retired" —
   deactivating old HINT rows is already the supported retirement path there; no deletion needed.

---
*Still queued (next session): world-space UI restyle via the `UGUI_SG_Samples` kit
(Gradient-Bar-style fill materials on the Filled Images of health bars / rescue ring /
QTE circle — geometry fill keeps working under any material).*
