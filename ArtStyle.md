# ArtStyle.md — Art Direction, Readability & Creation-Pipeline Reference

> **Provenance.** Same as Performance.md — reconstructed from the three supplied videos +
> corroborating primary sources (I can't scrape YouTube captions). §1–§5 = the art/readability
> body of *"How to Make Overwatch-Style Art"* (`qVz7MpbW8Mc`) and the OW art-style material;
> §6 = *"How Overwatch 2 Heroes Are Created"* (`RUVZzOsgw4w`); §7 = ability-VFX deep dive.
> Paste captions and I'll merge any verbatim specifics.
>
> Sources: [The Art of Overwatch: Evolving a Legacy (GDC Vault)](https://www.gdcvault.com/play/1024268/The-Art-of-Overwatch-Evolving) ·
> [Studying Overwatch Style (80.lv)](https://80.lv/articles/studying-overwatch-style) ·
> [Technical & Visual Analysis of Overwatch (80.lv)](https://80.lv/articles/overwatch-technical-overview) ·
> [Character Readability in TF2 & Overwatch (X. Coelho-Kostolny)](https://medium.com/@xavierck/character-readability-in-team-fortress-2-and-overwatch-68c41d454465) ·
> [How To Build An Overwatch Hero (Hotspawn)](https://www.hotspawn.com/overwatch/news/how-to-build-an-overwatch-hero) ·
> [NDC 2017: TAs streamline OW character dev — Ana (Inven)](https://www.invenglobal.com/articles/1701/ndc-2017-how-technical-artists-streamline-overwatch-character-development-in-the-case-of-ana)

---

## 1. Core pillars (the whole philosophy in four lines)

- **Readability beats realism.** Bright, colourful, comic-book/Pixar look chosen so dozens of
  things on screen stay legible mid-fight. Stylisation is in *service of clarity*.
- **Big readable shapes.** Large, interesting forms; **don't** drown the mesh in hyper-detail.
- **Silhouette-first.** Every character/weapon must be identifiable by silhouette alone.
- **Detail hierarchy.** Simple at distance, detail emerges up close and under moving light.

## 2. Shape language & silhouette

- **Shape language is the cast's alphabet** — decided in **concept art and *locked before* 3D
  blockout**, then re-tested with **silhouette studies throughout production.**
- New designs are checked against existing cast: *is this silhouette too close to someone we
  already have?* If yes, redesign. Distinctness is a hard requirement, not a nicety.
- Favour **large, interesting shapes** over noise; the read must survive being small/partially
  obscured/in motion.

## 3. Texture & material techniques

- **Lighten the base colour along convex edges** of shapes → edges "pop," gives a hand-painted
  read. (The single most recognisable OW texture trick.)
- **Specular-only detail.** Keep albedo simple/flat; push fine variation into the **specular/
  shiny** response so it shows up close + with light, and breaks tiling repetition — but the
  material stays readable and simple at distance.
- **Baked bevels** → soft, rounded finishes and natural rim-light **without geometry**.
- **Material layering** (puddle/dirt/sand/moss) over tiling base → large surfaces never look
  stale or obviously tiled.
- **Cubemap reflections** on windows, puddles, metal for depth.

## 4. Environment art (the third video's domain)

- **Modular kitbash** construction; **corner pieces** strategically hide seams and break the
  box-room read.
- **Intersection management** — allow minor clipping where surfaces share a material
  (stone-into-stone); the join is invisible and you skip bespoke geometry.
- **Trim sheets / tiling + layering** for surface variety at low texture cost.
- Compose for the **big silhouette read first**, then layer the detail hierarchy.
- Environment colour is **scripted to contrast with characters** so players never lose a hero
  against the backdrop (see readability, §5).

## 5. Readability rules (from the TF2/OW study — adopt the *wins*, avoid the *misses*)

**Wins to copy:**
- **Restricted palette** (skin + team colours + metal tones) forces shape/material to carry
  identity → consistency across cast and world.
- **Value/contrast hierarchy:** brightest, highest-contrast values placed at **chest/weapon**
  level — "bright lines pointing toward the dangerous part." Feet/lower body kept darker.
- **Saturation marks danger:** high saturation concentrated on weapons/threat zones to pull the
  eye; non-critical areas desaturated.
- **Team-colour landmarks** in the environment establish identity and contrast.

**Misses the article calls out in OW (so we *don't* repeat them):**
- OW characters lack strict light/dark value separation → guns don't always pop from the body.
- Saturation spread across non-critical areas dilutes the weapon read.
- No consistent team-colour system → it leans on nametags to tell allegiance.
- *Tracer works* because the **single yellow-tights gradient** is one clean focal splash.

## 6. How heroes & abilities are created (video 2 pipeline)

Headline: **150+ developers of many specialties per hero.** Order of operations:

1. **Ideation in "digital clay"** — rough forms, keyframes to transition between shapes. Goals:
   **focus** (team alignment) and **understanding the character's actions in gameplay.**
2. **Prototype 3D blockouts during ideation** — because scale reads differently 3rd- vs
   1st-person (Pharah's gun: rockets/angles scaled to feel satisfying in-hand).
3. **Heavy iteration before animation** — Wrecking Ball had **5+ serious iterations before
   rigging**; **abilities likewise go 5+ passes** tuning **visual intensity and effect size**
   off mock-ups and models.
4. **Test with an existing asset first** — Ana was prototyped on **Widowmaker's** rig; **rough
   art drives animation**, not finished art (animate early, polish late).
5. **Modeling → skinning (bones+skin) → controllers** placed so animators pose face→waist→knees.
6. **Physics joints** for ragdoll/secondary motion (what stays together).
7. **Polish pass** solidifies rig/skins/anims/victory poses → **hero cinematic → PTR test →
   live.**

**The reusable principle for us:** *iterate the ability's visual intensity and size on a cheap
blockout 5+ times before committing art.* Prove feel on greybox; the cue book is the final wire,
not the prototyping tool.

## 7. Why Overwatch ability VFX read so clean (the deep dive)

The "every effect stays readable even with dozens on screen" result comes from **deliberate
constraints**, not more particles:

- **Per-ability colour coding** — each ability owns a colour; the palette is a language. (We
  already do this: **Electro/violet = Kai, Stone/gold = Lyra.** Keep it strict.)
- **Distinct effect *shape*** — the effect has its own silhouette (ring vs cone vs bolt), so
  it's identified by form even before colour registers.
- **Restraint / negative space** — readable VFX is as much what you *leave out*; a clean core
  read + minimal noise. (VFX style guides exist precisely to enforce this — see VFX Apprentice.)
- **Glow via bloom, not clutter** — intensity comes from **HDR emissive into the bloom pass**
  (Performance.md §1.7), so "powerful" reads as brightness, not more sprites.
- **Gameplay-readable timing** — telegraph → active → recovery phases are visually separated so
  the *player can react* (this maps to our `startMode` Immediate/WithPrevious/AfterPrevious).

**Pipeline shape (concept → ship):** concept/intensity target → **blockout the effect size &
timing on greybox (5+ iterations)** → choose cheapest renderer that sells it (**flipbook/quad
first, mesh only if needed**) → unlit/per-particle shader → **optimisation pass** (atlas, pool,
consolidate emitters) → **readability review** against the colour/shape language. This is exactly
the lane our **CueBook → FxManager → VfxPool** implements; §4 of Performance.md is the perf gate.

## 8. How we apply this in Planet of Twins

- **Silhouette-distinct enemies are non-negotiable.** We have 10+ archetypes (Melee, Ranged,
  Grab, Severed, Penitent, Siphon, Witness, Summoner, TetherBreaker, 3 commanders). Run
  silhouette studies — each must be ID-able as a black shape. This is the worst-consumer test
  for our whole readability system.
- **Clan colour = our team-colour language.** Make the **selected twin** pop via value+saturation
  (and the mask→Sobel rim from Performance.md §5), not just a tint.
- **Convex-edge lightening + baked bevels + spec-only detail** are the recipe for the
  greybox→art transition without geometry cost.
- **Ability VFX:** strict per-ability colour + distinct shape + HDR-emissive glow + flipbook/quad
  budget. Iterate size/intensity on greybox before wiring the cue book.
- **Environment:** modular kitbash + corner pieces + trim layering; script environment colour to
  contrast with the twins so they never get lost against a busy area.

## 9. Cross-studio: how other top studios make ability VFX & characters

Overwatch isn't the only playbook. The two most useful *other* references converge on the **same
laws**, which is the real signal — readable stylized VFX is a solved problem and everyone agrees how.

### 9a. Riot / League of Legends — the VFX Style Guide (the most actionable public spec)
Written by Art Director Jin Ho Yang. Four goals delivered through **five areas: gameplay, value,
color, shape, timing.** Distilled rules:
1. **Emphasize focus** — split each effect into **primary** (instantly readable) + **secondary**
   (supporting, lower saturation/value). "Immediate clarity with minimal visual noise."
2. **Scale of importance** — a basic ability must **not** look as big as the ultimate. Visual
   impact = gameplay impact; players feel progression.
3. **Value range** — value = light/dark (0–100%). **Avoid extremes**, use mid-range; *wider* value
   range pulls focus and reads better.
4. **Illumination** — glow/illumination = contrast + clarity; conveys power, direction, duration.
   (= our HDR-emissive + bloom; Performance.md §1.7.)
5. **Saturation draws focus** — avoid 0%/100% (blends into env/UI); place saturation on what
   matters.
6. **Complementary colors** — one dominates, one supports; never two competing. **Conventions:**
   heal = green, frost = blue, gunpowder = orange-red. (We have Electro/violet=Kai, Stone/gold=Lyra.)
7. **Hand-drawn shapes/textures** — soft + hard combined; organic, no superfluous noisy detail.
8. **Add movement (motion blur)** — blurred motion reads more natural, communicates direction/power.
9. **Timing** — "**lead the brain with anticipation, overload it at the moment it's been waiting
   for, then give it time to process.**" Anticipation → climax → resolution.
10. **If it feels long, it's too long** — effects *support* the story, they don't tell it; length
    signals significance and prevents clutter when many cast at once.

### 9b. HoYoverse (Genshin / Honkai / Star Rail / ZZZ) — stylized NPR pipeline
- **Cel/toon shading with custom multi-light lighting**, controlled light↔shadow transition,
  **outline pass**, **SDF/ramp face-shadow** tweaking, **anisotropic hair**. Look authored via
  **ramps/LUTs**, not realistic BRDF.
- **Shared cross-game shader pipeline** (one toon system serves Genshin/HI/HSR/ZZZ) — supports
  real-time + baked lighting and dedicated **VFX shaders per character**.
- **Mobile-first performance:** bake what you can, **flipbook VFX** to keep cost low, per-particle
  shader behavior over heavy simulation (mirrors Performance.md §4).

### 9c. The convergence (treat this as law for our cues)
Overwatch + Riot + HoYo all agree: **readability via restraint; per-ability color coding; value &
contrast to mark the weapon/threat; distinct effect *shape*; short timing; glow = bloom on HDR
emissive; flipbook/quad budget over mesh+sim.** Every cue we author gets the **5-pillar check**
(gameplay-readable? right value? right color/convention? distinct shape? short timing?) and the
**"if it feels long, it's too long"** rule applied to its `duration`.

---

## 10. Faction palettes + art canon (2026-07-08 — **source of truth = `Planet_of_Twins_Colour_Bible_v1.docx`**)

> The Colour Bible (repo root) is the canonical colour document — full 5-step ramps,
> emission values, corruption recipes, environment palettes and the grading arc live there.
> This section is the working summary; **where the two conflict, the Colour Bible wins.**
> The bible's master system: **five signals** (3 living energies + 2 sicknesses),
> **HUE = allegiance · FINISH = health**.

**Source art (`potimg/`, canonical until replaced):** the Clan Symbol Codex
(`pot_clan_symbols_v2.png`) maps sub-clan sigils — Accord Keepers → Witness, Forge-Kin →
TetherBreaker, Bonded → Severed; the codex already draws **Vethara as blue-violet** and
**Luminari as antique gold**. `pot_soldiers_vethspawn_v1.png` + `pot_tahr_v2.png` ("the
Consuming King") set the Khal-Vor body language: black mass + **toxic teal** + thin gold
accents, the **chest eye-seal brand** on converted troops. The codex sigils are the source
art for the ground-symbol SDF pipeline (game.md §23.11/§23.14 material-float element).

**Rule for all three clans: near-white core + clan-color bloom** (the §7/§9 convergence —
hue lives in the bloom, value marks the threat). Hex values are the starting grade —
**user validates in Unity** (Volume + HDR intensity shift perceived hue).

The five signals (Colour Bible ramps — key values only; full 5-step ramps + emission in the bible):

| Signal | Core/specular | Light | Body | Deep | Read |
|---|---|---|---|---|---|
| **Luminari** (Lyra, soul-light) | `#FFF6D6` | `#FFCE52` | `#D99E2B` | `#3D2A0C` | warm gold — clean white-hot core |
| **Vethara** (Kai, dark-energy — royal violet) | `#EFE3FF` (pale lilac, never white) | `#A874F0` | `#7A3FD0` | `#201044` | royal violet — smooth deep body |
| **Pure Current** (the PLANET's own dark energy — cool teal) | `#D6FBFF` | `#35C9CF` | `#17909A` | `#032B31` | icy blue-leaning calm; rare + quiet, the world's pulse, never a faction |
| **Voreth** (internal rot — the hunger beneath) | none (black core `#0A0410`) | emissive `#5A1E7E` | `#34114F` | `#1C0A2E` | cold violet-black, cracked rotten edges, no clean specular |
| **Khal-Vor** (foreign invader — Tahr) | toxic `#24E89E` | `#22B386` | `#16916B` | `#04231C` (oil-veined `#0C5A42`) | sick green-teal, oil-slick sheen, never warm |

- **Pure Current vs Khal-Vor is a temperature read:** pure teal leans blue/icy/calm;
  the invader's teal leans green/warm/sick with an oily sheen. Same hue neighbourhood,
  opposite health. Canon (2026-07-08): the pure current is the planet's clash-born energy —
  **not Orveth's**; the Archon is merely depicted in this palette in her one scene.
- **Voreth vs Khal-Vor are the two DIFFERENT sicknesses, never states of each other:**
  Voreth = the planet's own current war-distorted (a corrupted local keeps their clan hue,
  curdled — violet-black bleeds in from the edges, ~70/30 clan/Voreth so the clan hue wins
  the read); Khal-Vor = foreign energy in foreign bodies, never gold, never violet.
  *(Supersedes the old "Voreth two-state / magenta-black" model — magenta `#C2187F` is retired.)*
- **The crack — Pure Current → Khal-Vor gradient (user canon 2026-07-08):** the crack
  renders in teal, NOT Voreth violet-black — violet-black sits too close to Vethara on
  screen (the bible's §7 layered recipe is superseded for the crack). **One story-driven
  gradient between the two teal poles:** early = bright icy blue-teal (Pure Current
  `#D6FBFF`/`#35C9CF` — the planet's own current, wounded but its own), late = dark oily
  green-teal (Khal-Vor `#0C5A42`→`#04231C` — the same current going sick as Tahr consumes
  it). The blue→green temperature shift is the bible's own healthy-vs-sick tell, and the
  late crack visually becomes *Tahr* — foreshadowing his end-state. One material float
  (0 = healthy, 1 = consumed) drives it (P17 StoryGradeDirector / P18 material-float
  element). Presence rule stands: bloom over saturation, half-speed motion, recede in lit
  frames. Voreth violet-black stays the corruption read on PEOPLE, never the crack.
- Khal-Vor premium upgrade: the **oily-iridescent sheen** (thin-film-style rim) on elite units.
- Twin map (fixed, everywhere): **Kai = RIGHT = Vethara violet · Lyra = LEFT = Luminari gold**.

## 11. Post-processing & the story grading arc (authoring spec — P17)

Base look (applies to the default/global profile; §17.1 game.md already sets HDR + ACES):
**ACES tonemap · contrast +10 · saturation −5…−10 · bloom threshold 1.1–1.3, scatter ≈0.7,
intensity ≈0.5 (soft/wide — glow game), per-area tint · vignette 0.25–0.35 · film grain
0.15–0.25 · chromatic aberration 0–0.08 ambient (cue-spiked via +Camera post-proc depth,
game.md §23.9) · DoF cutscenes only · motion blur OFF** (fast twin control + SMAA choice).

### 11.1 Volume architecture (the priorities are law — the Scene Health Dashboard lints them)

| Volume | Where | Priority | Content |
|---|---|---|---|
| `StoryGradeVolume` | Persistent, **exactly one**, global | 0 | the current story-grade profile (11.2) |
| Area identity volume | one per area scene, global-in-scene | 10 | Shadows-Midtones-Highlights hue/temp only — the area's *place* feel |
| `CrackDesatVolume` prefab | local box on **every crack** | 20 | saturation −20 + cold deep-teal shadow lift (Pure Current `#032B31` family, §10) |
| `FailureResetProfile` | `FailureResetSequencer._postProcessVolume` (slot already exists) | 30 | Valorant-style failure sting: desat ≈−80 + vignette pulse 0.45 + CA 0.25 — in 0.1 s, hold during reset, out 0.3 s |

No other global volumes, ever — an area that wants a mood ships a *profile suggestion* for
11.2, not its own global volume.

### 11.2 StoryGradeDirector (Persistent, P17 — **BUILT 2026-07-08**, profiles = starting values pending user tune) — 6 profiles over 10 story beats

`StoryGradeDirector` crossfades the `StoryGradeVolume` profile on `CheckPointManager`
progression flags (seconds-long blends; **Shock is a hard cut**). The user's 10 beats →
6 profiles (hero's-journey 25/50/25 with the **inverted return** — the game ends *losing*,
the sequel hook):

| Profile | Story window | Beats covered | Grade intent |
|---|---|---|---|
| `Grade_Act1_Warm` | 0–15% | happy · calm | warm gold lift, lowest vignette, sat −0 |
| `Grade_Shock` | the crack event (hard cut) | shock | crushed shadows, sat −30, CA spike, cold |
| `Grade_EarlyFear` | 15–35% | panic · instinct | cool shift, vignette 0.35, grain up |
| `Grade_MidPurpose` | 35–60% | fear/courage · worsening | neutral-cool, contrast +15 — resolve |
| `Grade_LateChaos` | 60–85% | purpose · chaos | split-tone (teal shadows / gold highlights), bloom up |
| `Grade_Ending_Losing` | 85–100% | LOSING end | coldest + most drained (sat −20, lifted blacks) — *they look like they're losing*; sequel hook |

Authoring = 6 VolumeProfile assets under `Settings/Grading/`; the director is the only
code. Beat→flag mapping lives with CheckPointManager data, not in the profiles.

---

*See Performance.md for the rendering pipeline and the perf budget these techniques live inside,
and WorldStorytelling.md for the JP/CN environmental-storytelling research. The render/URP
settings themselves live in game.md §17.1; the volume/grading spec above is enforced by the
Scene Health Dashboard (game.md §23.15.2).*
