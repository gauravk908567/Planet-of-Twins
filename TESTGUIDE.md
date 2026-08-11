# USER TEST & SETUP GUIDE — P10–P19 acceptance (2026-07-08)

The consolidated runbook you asked for: everything **not machine-verified** that needs your
eyes, every setup step still owed, and every tool built this cycle with how/when to use it.
Work top to bottom — Section A first (some tests below depend on it). Tick items here as you
go; instruction.md §18's checklist mirrors the surviving A-items + B-P17.

Everything dev-only below is gated on `DevConfig.Trainer` and stripped from release builds.

---

## A. One-time setup still owed (do these first)

Completed rows are REMOVED as they finish (history lives in changelog.md). Gone so far:
A3 ranged arrow wiring (data-driven 2026-07-09) · A7 POI feed/buff cue ids (authored 2026-07-10)
· A9 prewarm-profile cleanup (enemy projectiles/bombs/chains auto-warm; your profile is empty)
· A4 pool prewarm (empty profile assigned on GameplayPoolRoot — fine as-is; only add rows if a
first-use hitch actually shows) · A8 L2 POIs — **dropped by design 2026-07-12** (user: L2 will not
contain POIs; no ecology/feed/SeekEnergy sites there — nothing owed) · A2 corruption cue — **DONE
2026-07-09** (`EnemyDarkEnergy._corruptionStateBook` = CommonCueBook on all 13 prefabs, id
`poi_corrupt`, authored + working — changelog 2026-07-09/10; user confirmed) · A6 timeline bindings —
**DONE** (verified 2026-07-12: the L1_Park `TimelineBindingResolver` has the 7 designed rows authored;
the Scene Health Dashboard's Timelines=FAIL is a false-positive — it audits the raw null bindings that
the resolver rebinds at runtime, see §note below).

| # | Setup | How | Done when |
|---|---|---|---|
| A1 | **Mood auras** (P11) | Open `Assets/Scripts/Manpu/Data/ManpuVocabulary.asset` (custom inspector lists every mood as a row). Drag a sustained aura ParticleSystem prefab into **Loop Prefab** on: **Enraged** (rage), **Panicked** (fear), **Aggressive** (energy-burst). No sprite needed on the row. | The three rows show a prefab; test B-P11 passes |
| A10 | **Spawn-lead retest** (fix 2026-07-09) — *user note: can't be checked until a real spawn point exists in a scene* | Enemies were invisible because the spawn lead drove a reveal-material float enemies don't have. Now it toggles renderers: TestLab → Ctrl+1..9 spawn → enemy must appear **1.2 s** after the spawn VFX starts (VFX runs 1.8 s). Also check a pool-reused enemy (kill + respawn same type). | Every archetype visibly appears at 1.2 s, every time |
| A5 | **Grade tuning** (P17) — *deferred by user until the grading model is understood (see §Grade primer)* | The 7 profiles in `Assets/Settings/Grading/` are §11 *starting* values. Eyeball each on a real scene and tune — especially `Grade_Shock`'s CA 0.4 and `Grade_LateChaos`' split-tone. | Each grade reads right to you in-game |

---

## B. Per-phase manual tests (what · how · expected)

All TestLab tests: open `Assets/Scenes/Sandbox/TestLab.unity` **as the only open scene** →
Play → **Ctrl+`** opens the debugger panel. (If twins fall: the Ground plane self-bakes NavMesh
at runtime; Persistent auto-loads — check the Console first.)

### P11 — Manpu mood auras (needs A1)
1. **Aura appears/ends with the mood.** How: spawn a melee (Ctrl+1 or button), select it, press mood **Enraged** → aura starts; press **Normal** → aura stops. Expected: aura lives exactly as long as the mood; no glyph sprite required.
2. **Pool reuse is clean.** How: with the aura running, **Kill** → spawn the same archetype again. Expected: fresh enemy has NO aura until you set a mood; no orphaned aura at the corpse.
3. **Severed grief-rage.** How: spawn the Severed pair, kill one. Expected: partner's rage aura starts with grief-rage and ends exactly when the rage ends.

### P12 — GameDebugger v2 (round-2 fixes)
1. **Toggle + drag.** How: Ctrl+` → drag the window by its title bar to a screen corner. Expected: never renders half-cut; re-clamps inside the Game view every frame.
2. **Ctrl+1..9 spawn hotkeys.** How: hold Ctrl, tap 1/2/3… Expected: the numbered spawnable ("1. …" on the button) spawns at the enemy pad — no clicking needed.
3. **FX stop buttons.** How: set a mood aura or fire a looping cue → **Stop FX on selected** / **STOP ALL FX**. Expected: the held effect ends immediately; the next mood transition restarts its aura cleanly.

### P13 — Input System migration (the human half; machine half already passed)
1. **Bootstrap tutorial progressive unlock.** How: Play from `Bootstrap`, full tutorial. Expected: each input category is dead until its tutorial step unlocks it — identical to before the migration.
2. **Direct-area fail-open.** How: Play directly in `L1_Park`. Expected: ALL input works instantly (no gate in the scene = everything allowed).
3. **Four entry paths.** Bootstrap full, Bootstrap dev-mode, direct L1, direct L2 — all playable.
4. **Feel pass.** WASD+arrows move (digital, matches old GetAxisRaw), Shift switch, E attack, Q ability, C teleport, F interact/convergence, R empower-hold, B overview, Tab skill tree, ESC pause (priority chain: overlay closes before pause opens), QTE mash on F, any-key intro skip, gamepad seats work.

### P14 — Scene Health Dashboard
1. How: Tools ▸ Planet of Twins ▸ **Scene Health Dashboard** → *Scan All*. Expected: opens every Build-Settings scene additively, closes without saving; L1_Park **Timelines = FAIL** (that's BUG-032 being caught, see A6); Enemy prefabs 9/12 (3 commanders lack ManpuSlot — known); everything else green or explainable. Clicking a cell lists findings with Select-to-ping.
2. Re-run after every authoring session on scenes — it's the regression net for R2/R9/R11.

### P15 — Upgrade-tier VFX (`_t[n]`)
1. How: pick one ability (e.g. Stun). In its cue book, duplicate the active id and name it `<id>_t1` with a visibly different colour. In TestLab: **grant skill points** (debugger), buy one Stun node (Tab → tree), stun an enemy. Expected: the `_t1` variant plays. Ids WITHOUT a `_t` variant keep playing their base at every tier (per-sub-id opt-in). At tier 3 with only `_t1` authored, `_t1` still plays (falls back DOWN).
2. The rule is printed in every cue book inspector and the Upgrade Data Editor.

### P16 — GameplayPool soak
1. **Chain reuse ×3.** How: spawn TetherBreaker, teleport a twin close (debugger), let it chain-throw three times. Expected: marker/beam/drag correct each time; no stuck chain, no vanished beam on the 2nd/3rd use.
2. **Ghost rescue twice.** How: trigger Siphon's rescue twice in a row. Expected: second ghost (a reused instance) behaves fresh — kill window works, colours reset.
3. **Coalesce linger.** How: Coalesce-aura an enemy, kill it mid-aura. Expected: aura detaches and lingers in place, never dragged into the pool with the corpse.
4. **Bombs.** Witness + Siphon bombs repeatedly. Expected: fuse ring resets every throw; no bomb that never explodes.

### P17 — Story grading (needs A5)
1. **Window crossfades.** How: in Play, ask Claude (MCP) to call `StoryGradeDirector.Instance.SetStoryProgress(0 / .2 / .4 / .7 / .9)`, or do it from a debug script. Expected: ~4 s smooth crossfade between grades, in story order.
2. **Shock.** `PlayGrade("shock")` → HARD cut (no fade); next `SetStoryProgress` fades back into the arc.
3. **Failure sting.** How: fail a tutorial encounter (or call `FailureResetSequencer.Instance.TriggerReset(...)`). Expected: desat+vignette+CA slam in together (~0.4 s), hold through the black + teleport, fade out ~0.3 s, world back to normal grade. Setsuna/pause must NOT slow any of this.

### P18 — Cue variants + shake
1. **Variants.** How: in any cue book, mark two consecutive elements **Variant** (header toggle), fire that id ~6× from the debugger's cue section. Expected: exactly ONE of the two per play, random mix across plays; elements after the group time off whichever was chosen.
2. **Shake falloff.** How: on a cue element's +Camera block set **Range** = 10; fire it near the camera, then far away. Expected: close = full kick, far = weak/none. Range 0 = the old everyone-shakes behaviour. `Custom` shape exposes your own curve.

### P19 — Package carve (regression only — nothing should LOOK different)
1. **Music still follows areas.** How: Bootstrap → walk L1→L2. Expected: music/ambience crossfade on the location change (now via `IFxSceneEvents`).
2. **Cue unload reclaim.** How: start a long/looping cue on an enemy near a boundary, walk away until the area unloads. Expected: no orphaned VFX, no console errors.
3. **Manpu unchanged.** Mood pulses/auras/ability glyphs all as before (now `PoT.Manpu`).
4. Watch the Console on boot: any "slot unwired" warning from FxManager/MusicManager means a Persistent slot got lost — tell Claude.

---

### Ecology — POI energy feed + SeekEnergy (built 2026-07-09; cue ids authored — ready to test)

**Setup (what a feed site IS):** a `PoiEnergyEmitter` is one extra component you put on any
existing POI object (a GameObject that already has `RitualSitePOI` / `SpawnPointPOI` /
`BarrierPOI`). It needs one thing assigned: a **PoiEnergyProfile** asset
(`AI/POI/Data/DefaultPoiEnergyProfile.asset` exists; duplicate it to make a hungrier/gentler
site) — amounts and cadence all live on the profile. Optional: the feed cue book + id
(defaults to `poi_feed`). It has no visual of its own; enemy layer auto-detects. To wire a
whole scene at once: open it, run the **Scene Health Dashboard**, and click the "Wire POI
ecology" Fix on its Wiring findings (idempotent — already run on L1_Park: 6 emitters verified
2026-07-10; all enemy prefabs carry SeekEnergy).

- **Feed test:** TestLab → add a `RitualSitePOI` + `PoiEnergyEmitter` (profile assigned) near
  the enemy pad → spawn a melee enemy → damage it below 50% → walk the twins away (no target).
  Expected: every ~12 s the enemy heals a tick (+6 HP default) and gains dark energy (watch
  the debugger HP readout); the `poi_feed` cue fires POI→enemy once authored.
- **Engagement pause:** stand a twin next to it (it chases) → feeding stops; stun/possess/grab
  it → feeding stops; leave it alone → resumes.
- **Threshold buff:** keep feeding (or spawn near a crack) until dark energy crosses 0.5.
  Expected once, exactly once: console `[DarkEnergy] … POI BUFF`, a Confident Manpu pulse,
  +10% outgoing damage from then on, and the held `poi_buff` aura (once authored) until death.
  Kill + respawn from pool → aura gone, buff reset (pool-safe).
- **SeekEnergy:** spawn an enemy, hurt it below half, make twins undetectable (perception
  section) → within ~seconds it should walk to the feed site and stand inside the radius.
  Bond-broken enemies (energy ≥ 0.8) do this constantly — that's the +25 score bonus.

### Final pass additions (2026-07-09 — the last build batch; test each)

| What changed | How to test | Expected |
|---|---|---|
| **Bomb/arrow cue books rewired** (your id move broke 6 compile errors — fixed via 3 new `EnemyVfxLibrary` slots: Arrow / WitnessBomb / SiphonBomb, books already assigned) | TestLab: spawn Witness → Force bomb; spawn Siphon → damage until panic bomb | Fuse + explode cues play from the NEW books; console clean. Cue Id Verifier ▸ Regenerate should produce zero diff |
| **Arrow flight/impact by id** (sub-emitters replaceable) | Author `arrow_Trail` / `arrow_Head` / `arrow_OnImpact` prefabs in ArrowCueBook, strip sub-emitters from the arrow mesh, spawn Ranged enemy, get shot | Trail + head glow ride the arrow (they follow its **Tip Anchor** if you assign one on the Arrow component), impact burst stays at the hit point after the arrow despawns; pooled re-fire identical |
| **Chain glow — LIVE wired** (`ChainGlowFx` added as child of ChainProjectile prefab + `ChainGlowDriver` auto-stretch) | TestLab: spawn TetherBreaker near a twin, let the chain GRAB and drag | Glow stream appears only while grabbed: source at the PLAYER end, flowing toward the TetherBreaker, its length breathing with the live chain span (max 10 = chainAttackRange). Gone on release/miss/despawn; reused chain starts clean. Tune look on `Assets/Shader/EnemyTetherBreaker/ChainGlowFx.prefab` (shape Z is driven at runtime — author at Z=1) |
| **Old coloured range circles removed** | Play normally (Trainer OFF): melee windups, enemy attacks | NO flat red/white circles anywhere (they still appear in Trainer/TestLab builds — debug aid) |
| **Teleport marker disc → cue** | Aim Weaver's Gate | The green/red disc is GONE; the `tele_castmark` cue follows the aim point instead (placement/obstacle LOGIC unchanged). No cue authored = invisible aiming but teleport still works |
| **Gate travel = helix, soul hidden** | Cast Weaver's Gate (rescue active), watch both directions | Soul body INVISIBLE during travel — only the `tele_casttravel` helix moves; soul pops in under the `tele_castin` burst at the destination; return trip same (helix back, soul visible again at the caster). Cancel mid-flight → still ends visible |
| **POI feed/buff via CommonFx** | Ecology tests above (ids AUTHORED 2026-07-10: `poi_feed`/`poi_buff`/`poi_corrupt` in CommonCueBook; feed capped 1.5 s — looping prefab leak fixed; auras set to Follow) | Stream fires POI→enemy and self-ends ≤1.5 s; buff aura FOLLOWS the enemy from 0.5 energy; `poi_corrupt` aura follows from bond-break (0.8) — code + 13 prefabs renamed to match your id |
| **Enemy death soul-release RESTORED** (`KillParticleBook` was deleted in the folder split — rebuilt to the locked 1.25 s spec + rewired into `KillParticleSpawner` in Persistent; played id fixed `death`→`kill_seq`; **helix fixed 2026-07-10 BUG-048** — it used to play at world ORIGIN and get reclaimed on frame 1, now spirals at the death spot, play-mode verified) | TestLab: spawn any enemy → Kill (combat kill) | Full 4-beat sequence at the death spot: helix orbs spiral (0.9 s) + body disintegrate, star burst ~0.45 s in, then soul-collect streams to the nearest twin (~1.3 s total). Pooled respawn+rekill plays it identically. If star/disintegrate visually overrun, set their VFX-graph lifetimes (SO duration only schedules VFX, doesn't stop them) |
| **Ranged damage on IMPACT (FIXED 2026-07-10, BUG-047)** | TestLab: spawn Siphon or Ranged at distance, watch health bar + the arrow | NO damage and NO hit spark at fire time; the sigil/arrow flies TIP-FIRST with trail riding it, damage + `arrow_OnImpact` land only when it reaches the twin. Move away mid-flight → arrow misses, zero damage. (Root causes fixed: melee hit-frame ran on ranged attacks; arrow prefabs had no Rigidbody/collider; pool spawn wiped the root's 180° facing fix) |
| **GameDebugger v2 rework (2026-07-10)** | TestLab, Ctrl+`: resize via the ◢ corner grip or the W/H footer fields; spawn a TetherBreaker → select → **Throw chain**; toggle **God mode**; **Down L twin** → soul spawns → Siphon **Force ghost** → tick **Pause bind timer**; drag the dark-energy slider on one enemy with "Freeze ALL others" on | Window resizes live; chain does the new Roadhog throw (readiness reach → fast out → decelerating landing, curved then straightening); god-mode twins take zero damage during grabs/drains; ghost bind holds while paused (mash still escapes); only the slider enemy escalates (poi_buff at 0.5, corrupt aura at 0.8) |
| **Chain throw feel (Roadhog model, 2026-07-10)** | Force-throw via the debugger (above) or let a TetherBreaker engage; tune `_travelCurve`/`_windupReach`/`_launchBow` on the ChainProjectile prefab | During the marker windup the chain hangs ~0.75 m toward you; the throw covers ~85% of the distance in the first 60% of the time, visibly slows into the landing spot, and leaves the hand curved, straightening at full stretch |
| **Tool consolidation (2026-07-10, batch 8)** | Tools ▸ PoT menu: only **Scene Health Dashboard**, **Area Tools**, **Upgrade Data Editor** (+ cue verifier/vocab) remain — Validate, New Area Scene, Area Setup, Wire POI Ecology, Create Grade Profiles are gone. Run Scan All in the Dashboard; open Upgrade Data Editor, link a book (e.g. Stun's), press **+ _t1** on an id | Dashboard shows the new References column + World graph/Code lint project rows; clicking a finding with a Fix shows the button (POI-emitter/grade-profile/sub-SO fixes run; scene fixes ask you to open the scene). Area Tools has both tabs working. + _t1 adds a deep-copied entry named `<id>_t1` to the book; pressing again does nothing |

**Scalable-emitter map:** canonical map (who scales what, how to stop one) lives in **game.md §23.12 item 1** — moved there 2026-07-10 so the tech spec owns it.

## B2. Playtest round-2 retest list (2026-07-11 fixes — all in TestLab unless noted)

| Fix | How to test | Expected |
|---|---|---|
| Summoner spawns + circle stops | Spawn Summoner, force TriggerSummon (debugger); kill minions; repeat past 3 summons | Minion appears at circle with NO generic spawn flash; circle stops when it lands; summons keep working after minion deaths |
| Witness ritual allies | Force StartRitual | Allies appear at ritual with no generic spawn flash; Witness itself shows no stray flash |
| Bomb FX | Force ThrowBomb (Witness + Siphon) | Fuse FX rides the rolling bomb; explosion plays at impact and ENDS (~1.2 s, nothing left looping in FxPoolRoot) |
| Weaver's Gate forward | Rescue-trigger a grab, cast gate, hold marker key LONG before releasing | Marker cue loops cleanly while held and clears; order reads OUT → helix travels (twin ribbons) → IN → soul appears standing ON the ground (no pop-up) |
| Gate cancel/return | Hold X in the cancel window; also let the timer lapse | OUT at the soul's position → ribbons fly back to the twin → IN at the twin → only then twins can move again |
| Death helix | Kill a small enemy and a big one (Witness) | Twin ribbons spiral AROUND the body, accelerate upward and taper; visibly larger on the big enemy |
| Emitter sizes — ⏳ needs checking. **NOTE (2026-07-13):** the `emitterScale` resize read wrong, so dedicated **per-radius pulse emitters** were authored in TestLab → `PULSE_EMITTERS` (empower R5 · soul R4 · coalesce R1.5 · accordShockwave R12; flat ground rings, materials duplicated in `Assets/Shader/Pulses/`). User to test → prefab → assign to the cue books, replacing the scaled `KnockBackParticle`/`AccordKnockBackParticle` | Activate Accord (shockwave), Coalesce with radius upgrades | Shockwave ring reads ~12 m (matches the actual knockback); aura footprint grows with upgraded radius |
| Setsuna trails (authoring) | — | Trail ART on Setsuna prefabs still owed (sky-ribbon language); no code seam missing |

## B3. Playtest round-3 retest list (2026-07-11 fixes)

| Fix | How to test | Expected |
|---|---|---|
| Bombs actually exist (BUG-053) — bomb **spawns ✅ (user)**; only the **fuse position** left to recheck | Force ThrowBomb on Witness AND Siphon | Bomb spawns, rolls, fuse rides the ROPE TIP (not the sphere pivot), explosion at impact; a broken slot now LogErrors instead of silence |
| HitVfx cleanup (BUG-054) | Melee-hit an enemy ~10×, watch FxPoolRoot | Each HltVfx returns to the pool ~0.6 s after the hit — no pile-up |
| Marker spam guard (BUG-055) | Cast gate, then spam the aim key while the soul is out | No second marker ever appears; nothing lingers after the gate ends |
| Circle linger | Force TriggerSummon / StartRitual | Circle stays ~0.75 s after the ally lands, then stops; interrupt/death still stops instantly |
| Travel easing — ✅ **user-confirmed done (2026-07-13)** | Cast gate over a long distance; kill an enemy | Soul/ribbons launch soft, snap to speed, plunge to a stop right at arrival (Kiriko feel); death helix ascent has the same profile |
| Emitter-scale bench | Debugger cue section → set the emitterScale slider ≠1, fire empower_pulse / pulse_fire / accord_Shockwave | Ring footprint visibly scales; at ×1 it's identical to authored (by design) |
| Stuck arrows (BUG-056) — ✅ **did NOT recur in play (2026-07-13, user)** | Stand a twin in the volley line, let arrows hit repeatedly, watch reused arrows | No arrow ever freezes at the muzzle or mid-air; damage lands ONCE per arrow (double-collider double-damage also fixed); console pairs every "[Arrow] hit" with a "returned to pool" |
| Slash direction (round-2 item E) | Melee swing both twins | Sparks/flash spray FORWARD (art was authored Y=148.9° sideways); if the Electro arc still reads backwards, that's the remaining Y=180 authoring — retune with your new assets |
| Ability ready cheat | Debugger Meta section → "Make abilities READY (cooldowns + SoulConv souls)" | Cooldowns cleared AND SoulConv counter jumps to cap/charged (F-hold usable); Gate still requires a twin in danger, SoulConv still needs its skill unlock |
| Unkillable pooled enemies (BUG-058) | Kill an enemy, respawn the SAME archetype (esp. Witness melee minions), attack it | Reused enemy takes damage and dies normally; no immune standing bodies |
| SoulConv shield visuals | Charge SoulConv (cheat button), F-hold | Only the per-twin cue shields show — no old purple sphere on top |
| RadiantSeeker orb | Accord State → seeker cast | Orb spawns again (Persistent slot was a dead prefab ref — re-pointed) |
| Enemy tint reset (BUG-057) | Stun/possess an enemy (or interrupt a Witness ritual), KILL it mid-state, respawn same archetype | Respawned enemy has its normal authored colour — no cyan/purple/ritual tint |

## C. The tool shelf — what to reach for, when

| Tool | Where | Use when |
|---|---|---|
| **GameDebugger v2 + TestLab** | `Scenes/Sandbox/TestLab.unity`, Ctrl+` in Play | Testing ANY enemy/cue/mood/skill behaviour without playing a level. Spawn (Ctrl+1..9), damage/kill/stun/possess, mood bench, force behaviors, perception toggles, fire-any-cue, **story-grading bench** (progress slider + per-grade buttons — THE way to preview/tune P17 grades), skill points, teleport twins, Stop-FX buttons |
| **Scene Health Dashboard** | Tools ▸ PoT ▸ Scene Health Dashboard | After ANY scene/prefab authoring; before a build. THE one completeness tool (2026-07-10 merge — the old Validate window is gone): must-haves, wiring (incl. POI emitters), counts, timelines (R11), volumes (§11.1 priorities + grade profiles), **References** (R2 cross-scene + null required), Build Settings, enemy prefab health (incl. SeekEnergy), **World graph**, **Code lint**. Fix buttons in the detail pane cover WorldLocationSO/NavMesh/AreaSpawnPoints/sub-SOs/POI-ecology wiring/grade profiles (scene fixes need the scene open) |
| **Cue Book editor** | Select any `CueBookData` asset | Authoring effects: per-element kind/audio/+Camera/variants/cuts; the `_t[n]` upgrade-tier rule is printed at the top |
| **Cue Book linter + Cue Id verifier** | Inline in the book inspector + Tools ▸ PoT (verifier window) | The linter flags timing/cut/variant mistakes (F3–F7) as you author; the verifier sweeps ids project-wide and regenerates `FxIds` constants |
| **Upgrade Data Editor** | Tools ▸ PoT ▸ Upgrade Data Editor | Tuning ability upgrade nodes in a table; shows each node's expected `_t{n}` cue ids. NEW (2026-07-10): link the ability's Cue Book on the panel below the table — it lists ids grouped by base with the tiers each has, and **+ _tN** deep-copies the highest variant as the next tier (Undo-able, never overwrites) |
| **Manpu Vocabulary editor** | Select `ManpuVocabulary.asset` | Authoring mood/perception/ability glyph rows + loop auras (A1) |
| **Area Tools window** | Tools ▸ PoT ▸ Area Tools | 2026-07-10 merge of New Area Scene + Area Setup. Tab 1 **New Area**: scaffold an area scene with the full kit (WorldLocationSO, zone configs, entrance, load trigger, QTE anchor, identity volume). Tab 2 **Setup Zone**: create/repair a SpawnZone's AreaZoneConfig + auto-populate left/right spawn points. Then run the Dashboard on it |
| **Prewarm profile** | Create ▸ PlanetOfTwins ▸ Spawn ▸ Prewarm Profile | A4 |

---

## D. Post-processing setup — why grading shows (and when it doesn't)

**The 2026-07-10 bug, fixed:** the story-grade slider WAS working — the effect rendered in the
Scene view but not the Game view because the **Main Camera's "Post Processing" checkbox was OFF**
(`UniversalAdditionalCameraData.renderPostProcessing`). The Scene view has its own post-process
toggle (the image-effects button in its toolbar), which is why the two views disagreed. Now ON and
saved in Persistent. If grading ever vanishes from the Game view again, check these IN ORDER:

1. **Camera ▸ Rendering ▸ Post Processing = ✔** on the Persistent Main Camera (the only camera, R9).
2. **Camera ▸ Environment ▸ Volume Mask** must include the layer the volumes sit on (ours: Default).
3. **The volume itself:** `weight > 0`, a Profile assigned, and for StoryGrade the DIRECTOR drives
   VolumeA/VolumeB weights — don't hand-set those two, use the debugger slider / `PlayGrade(id)`.
4. **Profile actually differs:** grading only shows what the profile OVERRIDES (each override row
   needs its checkbox ticked ON in the profile inspector, not just a value typed in).
5. **HDR grading note:** the active `PC_RPAsset` uses LDR color grading — strong filmic looks
   (bloom-into-grade, tonemapping punch) want Grading Mode = HDR (Project Settings ▸ Graphics ▸
   URP asset ▸ Post-processing ▸ Grading Mode). LDR still renders; it just clips earlier.

**Worked example — make `mid_purpose` visibly warm (do this once to sanity-check the chain):**
1. Open `Assets/Settings/Grading/Grade_MidPurpose.asset`.
2. Add/enable **Color Adjustments**: tick ✔ Post Exposure `+0.3`, ✔ Color Filter a light amber
   (`#FFE0B0`), ✔ Saturation `−5`. Tick the checkboxes — an unticked row is ignored (step 4 above).
3. Play in TestLab → GameDebugger (Ctrl+`) → story-progress slider to `0.35`.
4. Expected: the whole Game view crossfades warm over a few seconds (unscaled — works during
   pause/Setsuna). Slider to `0.85` → fades to the drained `ending_losing` look. If it shows in
   Scene view only, you've re-hit check 1.

## E. Standing notes / deferred (so nothing is lost)

- **Crack visuals (deferred, decided):** crack = ONE gradient, Pure Current icy blue-teal → Khal-Vor oily green-teal, driven by a `_Corruption` float; the CrackGlow shader needs `_ColourA/_ColourB` + the float + a depth gradient + slow pan (currently single violet-magenta emission). Flame VFX (`CrackFlame.vfx`): `FlameColor` `#35C9CF` HDR, same float later; scatter = EmitterBoxSize↑ + drag↓ + frequency↓ + LifetimeMax↑.
- **POI ecology pass — BUILT 2026-07-09 (test rows in §B "Ecology"; this note was stale):** `SeekEnergy` GOAP goal (frequent after bond break), `PoiEnergyEmitter` + per-POI `PoiEnergyProfile` (energy/health per feed, <50 % HP gate at rituals, 12 s per-enemy interval, threshold → mood buff via the Manpu loop), `poi_feed` stream cue — all live; the Scene Health Dashboard now checks the wiring (POI emitters + enemy SeekEnergy). Still deferred: per-particle nearest-only flame attraction (VFX-side).
- **P19 leftovers (your call, both fine to sit):** `namespace PoT.Fx` sweep + package.json layout + the literal empty-URP-project compile — all scheduled with the §20.4 folder restructure, which itself runs BETWEEN content milestones, one commit per stage.
- **Known content gaps:** 3 commander prefabs lack ManpuSlot (deferred roster); commanders' §24.8 cues are greppable `TODO(§24.8)` stubs; `SmartEnemyRanged` unarmed until A3.
- Load-bearing typos (`SceneLaoder/`, `Heath/`, `L4_MueseumStart`…) rename only as isolated GUID-safe commits (restructure stage 1).
