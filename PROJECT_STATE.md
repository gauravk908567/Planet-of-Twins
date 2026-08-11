# Planet of Twins — Project State

*A single-player, bond-driven action-adventure in a stylized East-Asian fantasy world.
One player controls both twins at once; they share one life, and the bond between them
is the entire game.*

**Snapshot date:** 2026-07-29 · **Engine:** Unity 6000.3.5f2 (Unity 6.3, URP) ·
**Stage:** playable prototype, two playtest rounds run · **Team:** solo developer, scaling with funding.

---

## 1. The one-line verdict

**Indie-AA scale. A systems-complete vertical slice entering the content phase.**

Roughly **560 C# scripts across 23+ systems**, all core mechanics built and cross-wired. The
architecture is the strong half — disciplined patterns, clean seams, data-driven tuning, one
unified effects path. The thin half is **content**: two greybox areas, placeholder art and audio,
encounter and boss scripting not yet authored. In short: *a strong skeleton with thin skin.* The
correct next investment is content and authoring tools, not structural rebuilds.

---

## 2. What the game is

One player moves **both twins simultaneously** — Kai (dark-energy / Vethara) and Lyra
(soul-light / Luminari). WASD moves the pair; a switch key changes which twin is *selected* for
abilities while the other mirrors the movement.

**The core mechanic is the bond.** The twins share a single health pool that drains with the
distance between them — closeness is survival, separation is death. When one twin falls, the
other's soul tears free from its body, crosses the planet's crack, and fights to bring them back
(the rescue event). The mechanics and the story grew from the same idea, so *playing the game is
the story*.

- **Genre:** single-player stylized East-Asian fantasy action-adventure — a character-action
  game with RPG-lite systems (nine skill trees, abilities, the bond as a shared resource),
  structured as linear-streamed levels. Shelf-mates: *Where Winds Meet*, *Ghost of Tsushima*.
- **Positioning:** *Brothers: A Tale of Two Sons* meets *It Takes Two*.
- **Tone:** bittersweet, never cute. The world is built warm, alive and beautiful precisely so its
  corruption reads as grief.
- **Expansion (playtest-guided):** two-player couch co-op (a twin each) and a roguelike run mode.

---

## 3. Systems built

Grouped by area. Status is honest: **Built** = implemented and cross-wired; **Built (greybox)** =
mechanic works, final content/art pending; **Scaffold** = framework in place, needs authoring or
rework.

### Player, bond & movement
| System | Status | Notes |
|---|---|---|
| Dual-twin input & movement | Built | One player drives both twins; selected/mirrored layer; all input flows through a single `IInputProvider` (New Input System) |
| Shared health & distance bonding | Built | Combined pool, per-twin health, distance-drain (full ≤6 m → 0 at >18 m), bond manager, distance zones |
| Damage pipeline | Built | All damage flows through one `DamageData` struct; linked-damage loop breaker for paired enemies |
| Rescue event (downed-twin save) | Built | Soul separates, crosses the crack, fights back to revive — the emotional centerpiece |

### Combat, abilities & time
| System | Status | Notes |
|---|---|---|
| Melee & ability combat | Built | Attack + ability dispatch to the selected twin |
| Nine ability skill trees | Built | Data-driven upgrade trees, point bank, runtime unlock state extracted to a clean runtime store |
| Accord State | Built | Both clan powers working together — the shared-power ability |
| Two distinct time systems | Built | Entity-level freeze (soul mode) vs. global **Setsuna** slow-motion + rewind — deliberately never conflated; all time-scale writes go through one arbiter service |

### Enemy AI & ecology
| System | Status | Notes |
|---|---|---|
| Hybrid GOAP + Behaviour-Tree + FSM engine | Built | Reusable framework; one GOAP brain per enemy archetype over a shared blackboard |
| Ecology layer | Built | Mood/Ikari, social bonds, faction energy, points-of-interest, perception memory, clan-war — an emergent layer beyond the original design |
| Enemy roster | Built (greybox) / Scaffold | Ten-plus archetypes, each attacking a *different facet of the bond* (drag the twins apart, link their health against them, hunt the rescuing soul). Most fully wired; three commander signature abilities and the boss death/level-complete are stubbed pending content |
| Enemy combo/pact system | Built (decision) / Stubbed (execution) | Enemies dynamically form pacts (dark-energy-gated) and correctly *select* the right combo from 25 authored combo powers driving the GOAP blackboard — but the execution layer that plays the combo attacks is still a placeholder. The single biggest "built-but-unfinished" item |
| Perception system | Built | Sensors with decay/memory, coordinated via a service locator |

### World, streaming & progression
| System | Status | Notes |
|---|---|---|
| Multi-scene architecture | Built | Persistent scene (never unloaded, owns all managers) + streamed area scenes; strict cross-scene reference rulebook |
| Occupancy-based area streaming | Built | Loads areas by where the twins *and the rescue soul* are, plus adjacency; pre-unload events keep spawners/QTE safe |
| Checkpoints & soft-reset | Built | Save positions/points/upgrades; checkpoint respawn **without** a scene reload |
| Spawn systems | Built | Zone-registry enemy spawner + pooled enemies; a separate categorized `GameplayPool` for projectiles/summons/hazards |

### Presentation
| System | Status | Notes |
|---|---|---|
| Unified FX/audio cue engine | Built | One call plays a particle, VFX-graph effect, sound, or a timed sequence of all three; pooled, version-stamped handles, Setsuna/pause-aware — no per-call-site handling |
| Manpu (enemy emotion glyphs) | Built | Comic-style mood/relation glyphs driven by the AI mood system; sole owner of enemy mood expression |
| Camera cues | Built | Per-cue FOV / impulse-shake / post-process depth without ever touching the camera transform |
| Story grading director | Built (values pending tune) | A/B post-process crossfade across a six-profile story arc, driven by checkpoint progression |
| Rendering / URP config | Built | Forward+, HDR, ACES, split-toning, bloom-forward "glow game" look; failure-sting and crack-desaturation volume architecture |

### Interface & support systems
| System | Status | Notes |
|---|---|---|
| Tutorial (teach-anywhere) | Built | Per-category input gate that fails open; overlay/hint UI lives in Persistent, area steps resolve it at runtime |
| QTE / Gate puzzle | Built | State-machine QTE with shared UI and per-scene anchors |
| Dialogue & localization | Built | Eight languages |
| HUD & world-space UI | Built (revamp in progress) | Screen-space HUD in Persistent; world-space canvases per area |

### Developer tooling
| Tool | Status | Notes |
|---|---|---|
| TestLab + in-game debugger | Built | A sandbox scene to spawn any enemy, force any behavior, fire any cue, grant points, and teleport the twins — every archetype testable without playing a level. Dev-gated, stripped from release |
| Scene Health Dashboard | Built | Lints each scene against must-have recipes (entrances, navmesh, spawn zones, wiring, Timeline bindings, volume priorities) |
| Cue Book / VFX authoring suite | Built | Cue editor, linter, id verifier, upgrade-tier variant system, new-area generator |
| Editor validation | Built | Required-reference attributes + runtime integrity checks |

---

## 4. Engineering approach (why the skeleton is strong)

- **One consistent reference rulebook** governs every cross-scene access — no ad-hoc lookups,
  no duplicated managers, fail-loud on unresolved dependencies.
- **Interface-typed dependency injection**: serialized fields cast to interfaces (`IPointBank`,
  `ITutorialGate`, `IInputProvider`…) so gameplay and UI stay decoupled and co-op-ready.
- **Data-driven tuning**: all balance lives in ScriptableObjects; new enemies and abilities are
  data + a brain, not new plumbing.
- **One effects path, one time-scale arbiter, pooled spawns** — the patterns that keep a
  content push from turning into a bug farm.
- A ten-phase hygiene program (input-system modernization, debugger/TestLab, scene-health
  linting, upgrade-tier VFX, gameplay pooling, story grading, package seams) has been completed,
  so the systems layer is clean before the content layer scales up.

---

## 5. What's playable today

Twin movement and shared health · the full rescue event · a roster of enemies with distinct AI ·
the ability and skill systems · checkpoints and a Gate QTE — all running in a prototype build with
two playtest rounds behind it.

**Not yet:** final art and audio (currently placeholder/greybox), authored encounters and boss
fights, and a discoverable automated-test suite. These are the content-phase investments, not
missing architecture.

---

## 6. Multiplayer potential

- **Couch co-op** is a natural fit: the selected-twin + mirrored-movement layer exists *only*
  because one player drives both. With two players that layer is deleted, not built — each player
  simply drives one twin. Weeks-scale.
- **Networked co-op** is a months-scale re-architecture (server-authoritative AI, replicated
  pooled enemies, the global Setsuna time-scale) — analyzed, deliberately deferred.
- **Roguelike run mode** is on the roadmap, direction to be guided by the next playtest.

The codebase is kept co-op-clean by rule: all input through one provider, no new player-scoped
statics, twin identity stays data-driven.

---

## 7. Clan colour palette

Colour is a **gameplay language**, not decoration: hue tells you *which* energy, value tells you
*how much threat*. The governing rule across every clan — **a near-white core with a clan-coloured
bloom** (the hue lives in the glow, brightness marks the danger). Hex values are the authored
starting grade; final perceived hue shifts slightly under HDR bloom in-engine.

**Twin map (fixed, everywhere): Kai = right = Vethara violet · Lyra = left = Luminari gold.**

| Signal | Meaning | Core / specular | Light | Body | Deep |
|---|---|---|---|---|---|
| **Luminari** | Lyra — soul-light (warm gold, clean white-hot core) | `#FFF6D6` | `#FFCE52` | `#D99E2B` | `#3D2A0C` |
| **Vethara** | Kai — dark-energy (royal violet, smooth deep body) | `#EFE3FF` | `#A874F0` | `#7A3FD0` | `#201044` |
| **Pure Current** | the planet's own energy — icy, calm, rare (never a faction) | `#D6FBFF` | `#35C9CF` | `#17909A` | `#032B31` |
| **Voreth** | internal rot — cold violet-black, no clean specular | `#0A0410` (black core) | `#5A1E7E` (emissive) | `#34114F` | `#1C0A2E` |
| **Khal-Vor** | the foreign invader (Tahr) — sick green-teal, oil-slick sheen | `#24E89E` | `#22B386` | `#16916B` | `#04231C` (oil `#0C5A42`) |

**Reading the two teals** — *Pure Current* leans blue/icy/calm (healthy); *Khal-Vor* leans
green/warm/sick with an oily sheen (corruption). Same hue neighbourhood, opposite health.
**Voreth vs Khal-Vor** are two *different* sicknesses: Voreth is the planet's own energy
war-distorted (a corrupted local keeps their clan hue, curdled with violet-black bleeding in);
Khal-Vor is foreign energy in foreign bodies, never gold, never violet.

**The crack** renders as a single story-driven gradient between the two teal poles — bright icy
blue-teal early (the planet wounded but still its own) shifting to dark oily green-teal late (the
current going sick as Tahr consumes it), driven by one material float.

---

## 8. Known gaps → next investment

| Gap | Plan |
|---|---|
| Enemy combo execution stubbed (pacts form and pick correctly, but the combo attacks don't yet play) | Author the execution layer — the top gameplay priority |
| Area authoring: one of three built area zones is populated with enemies; the other two are wired empty shells | Populate encounters as the roster firms up |
| Commander signature abilities + boss death/level-complete stubbed | Author the remaining archetype gameplay |
| Content: two greybox areas, no authored boss fights | Content authoring — the primary next phase |
| Final art & audio (placeholder today) | Art/audio pass on the built systems |
| Story grading & sky-state values carry starting values | Tune the authored profiles in-engine |
| No discoverable automated tests yet | Add a test assembly, move cue/save/streaming invariants into real tests |
| Hot-path debug logging | Gate behind dev config for release |

---

*This document is a plain-English snapshot of the project's current state. For the deep technical
reference see `game.md`; for the full colour canon see `ArtStyle.md`; for recent changes see
`changelog.md`.*
