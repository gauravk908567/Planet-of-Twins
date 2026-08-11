# Performance.md — Rendering & Performance Reference

> **Provenance.** Reconstructed from the three videos you supplied plus corroborating
> primary sources. I cannot scrape YouTube captions (the transcript is JS-delivered), so this
> is built from the videos' *identified source material* and cross-checked against published
> Blizzard/GDC/dev breakdowns. If you paste the actual captions I'll fold in any
> video-specific specifics verbatim.
>
> **Videos:**
> 1. *Overwatch Rendering Analysis | GameArch* (`wvILHa_o4u8`) — a walkthrough of Alain
>    Galvan's frame analysis. **This doc's §1 is essentially that video's content.**
> 2. *How Overwatch 2 Heroes Are Created* (`RUVZzOsgw4w`) — pipeline (see ArtStyle.md §6).
> 3. *How to Make Overwatch-Style Art – 3D Environment Art Tips* (`qVz7MpbW8Mc`) — asset
>    perf notes in §3 here; art rules in ArtStyle.md.
>
> Sources: [Frame Analysis – Overwatch (alain.xyz)](https://alain.xyz/blog/frame-analysis-overwatch) ·
> [Technical & Visual Analysis of Overwatch (80.lv)](https://80.lv/articles/overwatch-technical-overview) ·
> [Dynamic Render Scale in OW2 (Greasy Guide)](https://www.greasyguide.com/social/dynamic-render-scale-overwatch-2/) ·
> [Forward Rendering Pipeline for Modern GPUs (GDC Vault)](https://gdcvault.com/play/1016435/Forward-Rendering-Pipeline-for-Modern) ·
> [Get the Most Out of VFX Graph (Unity)](https://unity.com/blog/unity-6-vfx-graph-ebook)

---

## 1. Overwatch frame anatomy (full pass order)

Design mandate: **run anywhere at a smooth 60 fps.** Every pass below is budgeted against that.
Overwatch uses a **Forward+ (tiled forward) renderer** — thousands of lights with forward's
cheap MSAA/transparency and none of deferred's bandwidth/G-buffer-blowout downsides.

The frame, in order:

1. **Shadow depth pre-passes.** Orthographic depth projections per shadow-casting light.
   - Directional (sun): **2048×2048** depth (medium settings).
   - One omni/point light: **cubemap, 512×512 per face.**
   - Shadow results later composited with noise samples (dithered penumbra).
2. **Prepass / G-buffer**, rendered at an **up-scaled resolution (~150%, extra Y-axis pixels)**
   for cheap supersample AA. Channels packed:
   - View-space **normals**, view-space **depth** (centimetre units), **albedo**,
     **metalness**, **roughness**, **emissive**. Encoded for reuse in later passes.
3. **Ambient occlusion.** View-space AO computed, then **composited with baked AO** (so static
   geo pays nothing at runtime; dynamic gets the cheap screen-space pass).
4. **Reflection pass.** Prepass to flag reflective surfaces → ray-cast from them → **horizontal
   blur (azimuth scaling)** → **grow** → **mip-level integration keyed by metalness/roughness**
   (rougher = blurrier mip). Cheap glossy reflections without full SSR cost everywhere.
5. **Diffuse lighting** — accumulated **per light** (the analysed scene had 20+).
6. **Specular lighting** — PBR specular + **image-based lighting from the skybox cubemap**.
7. **Bloom** — standard downscale → blur → composite. **This is the game's "glow."** HDR
   emissive values above 1.0 bleed here. (Directly relevant to our chain-glow ask.)
8. **Mask / outline pass (team silhouettes through walls):**
   - Render **ally mask**, then **enemy mask**.
   - **Grow** the mask (dilate), then apply a **Sobel/edge operator** → clean coloured outline.
   - This is how OW draws the friendly/enemy rim you see through geometry — a *separable,
     cheap* post step, not per-object shader work.
9. **UI pass** — vector art rendered at **multiple blur levels**, composited last.

**Takeaways that are technique, not trivia:** glow = HDR emissive + a bloom pass; "see through
walls" highlight = mask → grow → Sobel; reflections are faked with blur+grow+mip, not RT;
static lighting/AO is baked, only dynamic pays screen-space cost.

## 2. Resolution & upsampling (Overwatch 2)

- **Dynamic Render Scale** — the renderer continuously trades internal resolution for a stable
  frame time; the scene is rendered below native and **temporally upsampled** back up.
- **Checkerboard rendering** — render a checkerboard of pixels (≈**50% of pixels for a 2× scale**)
  and reconstruct the rest from history. Half the shading cost for near-native output.
- Lesson: **decouple internal render resolution from output resolution** and let a controller
  hold the frame-time budget.

## 3. Asset-level performance techniques (the "art that's cheap" tricks)

- **Baked bevels** — big rounded bevels baked into the normal map give soft rim-light reads
  **without geometry**. Silhouette stays simple, lighting does the work.
- **Material layering on tiling textures** — combine puddle/dirt/sand/moss layers over tiling
  base so large surfaces never look stale or obviously tiled, at tiling-texture memory cost.
- **Poly density where it's seen** — heavy in face/hands/weapon (read up close, first-person),
  sparse on legs/backs. Budget polygons by attention, not uniformly.
- **Rectangular textures** (e.g. **1024×2048**) for long UV islands instead of wasting a square.
- **Kitbash + allow minor clipping** where objects share a material (stone-into-stone) — saves
  modelling unique joins; intersection is invisible when materials match.
- **Corner pieces** to hide seams and break the "box room" read.
- **Specular-only detail** — keep albedo simple/flat (readable at distance); push fine detail
  into the spec/roughness so it only appears up close and under moving light.

## 4. VFX performance rules (for our cue/Fx system)

- **Billboard quads beat mesh particles.** Quad output is almost always faster; reserve **mesh
  particles for things that genuinely need 3D** (debris, tumbling sparks). *(This is the answer
  to "particle over the whole chain without a mesh" — billboard quads + a strip, see chat.)*
- **Flipbooks / texture atlases** — bake complex DCC sims into a sprite sheet, animate via
  flipbook UV. Stylized effects stay cheap this way; favoured in mobile/stylized action RPGs.
- **Unlit shaders are fastest.** Most stylized VFX don't need lighting; drive look per-particle
  via vertex/streams instead.
- **1 emitter × 1000 particles > 10 emitters × 100** (Epic/GDC). Consolidate.
- **Component pooling for frequently (de)activated effects** — never Instantiate/Destroy at
  runtime. *(We already do this in `VfxPool`/`FxManager`.)*

## 5. How we apply this in Planet of Twins (Unity 6.3 / URP 17.3)

| OW technique | Our move |
|---|---|
| Forward+ renderer | URP 17.3 ships **Forward+**; switch the URP Renderer to it for many-light scenes (our VFX-heavy fights). Same family as OW. |
| Bloom = glow | One **URP Volume** with Bloom; author cue/ability emissives in **HDR (>1.0)** so glow is free and consistent. The chain/abilities "glow" through this, not per-effect hacks. |
| Mask → grow → Sobel outline | Use a **URP Renderer Feature (full-screen blit)** for the *selected-twin* highlight and enemy telegraph rims — cheap, readable through geometry, and it generalises to every enemy archetype (worst-consumer rule). |
| Baked AO + baked lighting | Bake static area lighting/AO per streamed scene; only twins/enemies/VFX use dynamic. Keeps the 60 fps budget during the multi-scene streaming. |
| Reflection blur+grow+mip | Use URP **reflection probes** (baked) for puddles/metal; skip SSR. |
| Dynamic Render Scale + temporal upsample | Enable URP **dynamic resolution / render-scale** + **TAA/STP upscaling**; hold a frame-time target. Big headroom on low-end. |
| Baked bevels, spec-only detail, material layering | Bake bevels into normals for the greybox→art pass; keep albedo flat for readability; layer trims to kill tiling. |
| Quads over mesh particles, flipbooks, pooling | Cue elements default to **billboard quads + flipbooks**; mesh particles only for debris; everything pooled (done). |
| Shadow budget (1 dir + 1 omni) | Cap real-time shadow casters hard; one directional + a couple of important point shadows per area, rest baked. |

---

*See ArtStyle.md for the look side (silhouette, readability, environment art, character &
ability creation pipeline).*
