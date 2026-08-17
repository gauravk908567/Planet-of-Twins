# COUCH MULTIPLAYER — Deep-Dive Conversion Plan (Planet of Twins)

> Saved 2026-08-16. This file holds BOTH analyses the user asked to keep:
> (1) the couch-only deep-dive conversion plan + staged build order (main body), and
> (2) the broader couch+online multiplayer-conversion **nuances** (Appendix).
> Working reference only — not yet folded into game.md §26 / instruction.md P20 (deferred to a later pass).

## Context
Convert PoT from "one player drives both twins" to **local 2-player couch co-op** (P1 + P2, one
twin each, PvE, one shared group camera). This is the **cheapest** MP path (single machine → all
shared-fate simulation is byte-for-byte unchanged) and the right first step. This plan is the
**exhaustive touch map**: every consumer found by grep that changes, everything that can break, and
the net-new work — scoped to couch ONLY. The broader couch+online mechanism analysis is preserved
verbatim in the **Appendix** at the bottom (the "MP conversion nuances" the user asked to keep).

**Verified by code sweep this session** (not assumed): grepped `SelectedTransform / OnTwinSelected /
ITwinSelector / ISelectionLock / ForceSelect / Lock/UnlockSelection`, `MirroredMovementModifier /
SetMovementModifier / GetSwitchDown`, and all **26** `IInputProvider` consumers; read the 3
dispatchers, `TwinSelector`, `EmpowerSystem`, `TeleportAbility`, `RescueEventController`,
`SetsunaSystem`, `SoulConvergenceSystem`, `AccordSpiritSystem`, `TwinAbilitySetup`, `SceneFlowManager`,
`SelectedPlayerUI`, `WorldSpacePickupPrompt`.

## Scope guardrails
- **Unchanged (single machine):** `SharedHealthPool`, `DistanceHealthSystem`, `TwinBondManager`,
  Setsuna time/rewind, all enemy AI + `EnemyPool` + streaming + QTE + checkpoint/soft-reset. These
  *are* the co-op design; both players now literally share them.
- **Camera unchanged** (user): single Persistent group cam, bond caps separation ~18 m. No
  split-screen EXCEPT the optional F13 ability-beat (which is its own item, not baseline).

---

## SECTION 1 — SELECTION SYSTEM REMOVAL (the spine)
`TwinSelector` (Persistent singleton: `ITwinSelector` + `ISelectionLock` + `ISelectionBroadcaster`)
dies and is replaced by a fixed **ownership map** `{ P1 → twinA, P2 → twinB }` set by character
select (§5). Every consumer below currently assumes ONE selected twin and must be reworked. **This is
the highest-risk part** because the coupling is wide:

| Consumer | File:line | What it does today | Couch change |
|---|---|---|---|
| Ability routing | `TwinAbilityDispatcher.cs:47,66,72` | `OnTwinSelected` → `_currentAbilityController` (selected twin) | Delete selection; each player's Q/C/X/R → their own twin's `AbilityController` |
| Rescue | `RescueEventController.cs:383,443` + Lock/Unlock ×6 | `ForceSelect(otherTwin)` on grab; locks selection during rescue | Remove all ForceSelect/Lock; grabbed player's twin freezes, **partner** does the mash (§4) |
| **Empower** | `EmpowerSystem.cs:306,307,422-424,263` | `ForceSelect(_empoweredTwin)` + `LockSelection`; resolves empowered twin via `SelectedTransform`; **Shift (`GetSwitchDown`) = dash** while empowered | **Redesign per D1 (RESOLVED): the CASTING player's twin anchors (locked), the PARTNER twin gets the speed/damage/dash buff** — a co-op teamwork move, closest to today. `_anchoringTwin` = caster; `_empoweredTwin` = partner; drop ForceSelect/Lock; rebind the dash off Shift to a per-player key. |
| Teleport (Gate) | `TeleportAbility.cs:223,378` | `LockSelection`/`Unlock` during soul travel | Make the lock a no-op (nothing to lock — no switching) |
| Ability setup | `TwinAbilitySetup.cs:57,155` | passes `ISelectionLock` into every `TeleportAbility` | drop the selection-lock injection |
| Active-location | `SceneFlowManager.cs:248-259` | picks active location by **selected twin** (music/active scene) | replace with a rule: prefer P1's twin, else first loaded (Open Decision D4) |
| Selection UI | `SelectedPlayerUI.cs` (whole file) | swaps `selectedMaterial`/`unselectedMaterial` on the selected twin | repurpose as **per-player identity** (P1/P2 tint/outline) or delete (D3) |
| Camera follow (dead) | `CameraFollowController.cs:10,15` | commented-out `OnTwinSelected` | ignore (already dead) |
| Tutorial (whole subsystem) | `TutorialDirector.cs:38,74`, `TutorialStepContext.cs:54,68`, `TutorialTimelineStepSO.cs:30,48`, `TutorialStepBase.cs:36`, `TutorialUnlockAllStepSO.cs:30` | every step Locks/Unlocks selection; teaches the switch mechanic | see **What Breaks #1** — biggest single job |

Deletions: `MirroredMovementModifier` (only `TwinSelector` used it); `NormalMovementModifier` becomes
the only modifier on every twin. `GetSwitchDown()` loses its primary consumer.

---

## SECTION 2 — INPUT LAYER: 1 provider → 2 providers
Today `TwinInputReader` is a **Persistent singleton** exposing ONE `IInputProvider`; all 26 consumers
read that one instance. Couch needs **two** device-bound providers. The critical work is
**reclassifying all 26 consumers** into two buckets — this is the subtle part most conversions get
wrong:

**(a) PER-PLAYER gameplay input** — must read the *owning* player's provider:
`TwinMovementDispatcher`, `TwinAttackDispatcher`, `TwinAbilityDispatcher`, `EmpowerSystem`,
`SoulConvergenceSystem`, `SetsunaSystem`, `AccordStateSystem`, `AccordSpiritSystem`,
`RescueEventController` (the mash).

**(b) SHARED / GLOBAL UI input** — "any player" (either device) triggers; needs an aggregator or a
designated device: `PauseMenuController` (either player pauses), `SkillTreeUI` (per-player — each
opens their own tree, Open Decision D5), `OverviewCamController`, `IntroController` (any-key skip),
`QTEManager` + `QTEController` (the player whose twin is in the QTE), `ControlHintsVisibility`,
`WorldSpacePickupPrompt` (glyph only), `InputPromptView`.

Mechanics:
- Stop `TwinInputReader` being a singleton; use Input System **`PlayerInputManager`** to spawn two
  readers (`_inputP1`, `_inputP2`), each with a cloned action map so keyboard/gamepad don't
  cross-read. Both still implement `IInputProvider` (seam unchanged — there are just two).
- Provide an **"any-of" aggregator** `IInputProvider` for bucket (b) so pause/skip/overview fire from
  either device without duplicating consumer code.
- **Tutorial gate** (`TutorialInputGate.SetGate`) currently registers into the ONE reader → must
  register per-provider (or a shared gate both consult). Ties into What Breaks #1.
- `WorldSpacePickupPrompt.cs:73` reads `TwinInputReader.Instance` for the F5 key glyph — with two
  providers, pick the device family to display (or show both).

---

## SECTION 3 — PER-PLAYER DISPATCH REWRITES
| Dispatcher | Today | Couch |
|---|---|---|
| `TwinMovementDispatcher.cs:68-73` | ONE `MoveCommand` → **both** twins (+ soul when active) | `_inputP1` → twinA.Movement; `_inputP2` → twinB.Movement; soul routes to the caster's provider |
| `TwinAttackDispatcher.cs:74-75` | `GetAttackDown()` → **both** attack; special-cases Accord melee / soul / rescue-free-twin | P1 attack → twinA; P2 attack → twinB; rescue "only free twin attacks" becomes automatic; Accord melee routed via the joint path |
| `TwinAbilityDispatcher.cs:86-137` | Q/C/X → `_currentAbilityController` (selected); teleport preview + X-cancel on both controllers | delete `_currentAbilityController`/`OnTwinSelected`; each player's Q/C/X/R → their own twin's controller; per-owner teleport preview + cancel |

---

## SECTION 4 — RESCUE REWORK (mechanism)
Delete every `ForceSelect`/`Lock/UnlockSelection` in `RescueEventController`. New flow: twinA grabbed
→ twinA movement-frozen (existing `IMovementFreezable.SetFrozen`), **twinB's player runs the
soul-mash** — the mash reads `_inputP2.GetRescueMash()` instead of the single provider. Keep as-is:
"both actively trapped = instant fail," `SetEmergencyOverride(isLeft,…)` (already twin-indexed),
soul deploy/return, TTK. The Gate/teleport rescue soul is driven by the caster's provider.

---

## SECTION 5 — NEW SYSTEMS REQUIRED FOR COUCH
1. **Character select** (`CharacterSelectController` + UI) — each device picks **Kai / Lyra /
   Random**; conflict resolution ensures two distinct twins; Random auto-assigns both. Output = the
   ownership map §1 depends on. Front of the whole pipeline.
2. **Joint-ability grace gate** (`JointAbilityGate`) — for abilities that need BOTH players to trigger
   "together": P1 fires at t1, P2 at t2; if `|t1−t2| ≤ grace` (~0.5 s) → fire; else cancel + feedback.
   Consumers (confirmed by input reads): **Setsuna** (`GetConvergenceHeld`), **SoulConvergence**
   (`GetConvergenceHeld`), **AccordSpirit** (`GetEmpowerHeld`), **Accord activation**. Which of these
   are joint vs per-player = Open Decision D2.
3. **F13 ability close-up cam (optional, couch form)** — instruction.md:1819 spec: two per-twin
   vcams from ONE shared CamRig clip (Valorant-knife model) → priority 30 → **two viewport halves**
   for the beat → wipe in/out → release (existing `CameraManager.DemoteExternalCamera`). Guard:
   twin grabbed/stuck/dead → skip split, full-screen the free twin. Couch tax: split HUD, ONE
   AudioListener (R9), ~2× render for the beat. Phase-1 half (a common CLOSE ability vcam) can ship
   before the split.
4. **Co-op sync-puzzle gate** (`CoopSyncGate`) — player A triggers node A → timed window → player B
   must hit node B within T, else reset. Reuses the grace-gate mechanism. Net-new *content*.

---

## SECTION 6 — HUD / UI
- **Second per-player ability strip** (P1/P2). Shared-health bar stays single (it's shared).
- `SelectedPlayerUI` → repurpose to per-player identity or delete (D3).
- `SkillTreeUI` → per-player open/close (D5).
- `InputPromptView` / `ControlHintsVisibility` → per-device glyphs (F5 already reads live bindings).
- Character-select screen (new).

---

## WHAT BREAKS — ranked (couch)
1. **Tutorial system (#1).** Deeply selection-coupled: `TutorialDirector` + `TutorialStepContext` +
   `TutorialTimelineStepSO` + `TutorialStepBase` + `TutorialUnlockAllStepSO` all Lock/Unlock
   selection, and the tutorial **teaches the switch mechanic** (now deleted). `TutorialInputGate`
   gates per-category into the ONE reader → must go per-provider; progressive unlock ("attack locked
   until step X") assumes ONE learner. `TutorialTimelineDirector` rebinds by type/singleton (R11) —
   with two owned twins, which does it bind? **Fix:** decide the co-op tutorial model FIRST (D6),
   rewrite the switch-teaching steps, make the gate per-provider, rebind timelines to the ownership map.
2. **Empower.** Force-selects + anchors one twin + Shift-dashes — its entire model is single-driver.
   Needs a co-op redesign (D1), not a mechanical port.
3. **Selection sweep completeness.** 14 consumers across abilities/rescue/tutorial/streaming/UI. Miss
   one → a null `SelectedTransform` or a stuck selection lock. Grep-verified list is in §1 — clear all.
4. **Input reclassification errors.** Wiring a shared-UI consumer to only P1's provider (P2 can't
   pause) or a gameplay consumer to the aggregator (both twins react to one player). §2 buckets both.
5. **`GetSwitchDown` orphan.** Shift freed from switching but still read by Empower's dash and passed
   through `TutorialInputGate` — rebind per player, don't leave it dangling.
6. **`SceneFlowManager` active-location** picks by selected twin (music/active scene) → needs the D4 rule.
7. **`SelectedPlayerUI` material swap** references a now-meaningless "selected" state.

## DESIGN DECISIONS
- **D1 — Empower co-op model — ✅ RESOLVED (2026-08-16): CASTER buffs PARTNER.** The casting player's
  twin anchors (locked); the partner twin gets the speed/damage/dash buff. Keeps the original
  "one anchors, one empowered" feel as an intentional co-op teamwork move.
- **D6 — Tutorial co-op model — ✅ RESOLVED (2026-08-16, revisitable): SHARED progression.** Both
  players complete the tutorial **together** (steps advance for both at once) — user: "this is the
  core part of it." One gate state. May refine to per-player later ("we can see this later too").
- **D2 — Which held abilities are JOINT vs per-player** (pending): Setsuna, SoulConvergence,
  AccordSpirit, Accord activation. Default proposal: all four = joint (both trigger within grace);
  Empower is per-player (caster-initiated, per D1).
- **D3 — `SelectedPlayerUI` — ✅ RESOLVED (2026-08-16): neutralize in M1, remove after M5.** No real use in
  couch — Kai/Lyra are already visually distinct and character-select tells each player who's who, so a body tint
  (or even a start-of-game "you are X" marker) is redundant. M1 makes it **inert** (cut the `TwinSelector`/
  `OnTwinSelected` coupling so nothing dangles; apply a plain material once); physical removal is a **post-M5**
  cleanup. No identity-marker visual to build.
- **D4 — Active-location rule — ✅ RESOLVED (2026-08-16): pin to HOST (P1 / TwinA).** With no selected twin,
  `ResolveActiveLocation` follows one designated twin — deterministic, no music-crossfade flicker, and the same
  rule online will want (host authority). Rationale (user): adjacency streaming + the shared-health bond make a
  "twins split across non-adjacent scenes" state effectively unreachable (they'd die first), and it only gets less
  reachable as levels grow. If a freak straddle ever surfaces, revisit then.
- **D5 — Skill tree** (pending): one shared party tree, or per-player trees.

## STAGED EXECUTION — build order (what we do first, and why)
Guiding rule: **each stage ends in a playable, testable build** (Working Method: verify on two entry
paths). No big-bang rewrite. Order is dependency-driven — each stage unblocks the next. Stages M0–M1
are the risky foundation and get a **walking-skeleton proof** before we invest in abilities/tutorial.

### M0 — Input split (NON-BREAKING; game stays single-player-playable) — *do first*
- **Goal:** two `IInputProvider`s exist; game still plays exactly as today on one device.
- **Work:** make `TwinInputReader` instantiable (not a singleton); add `PlayerInputManager` spawning
  P1 (P2 optional/idle); build the **"any-of" aggregator** for shared-UI input; reclassify all 26
  consumers into per-player vs shared buckets (§2) but route the per-player ones through a
  `ResolveOwnerProvider(twin)` helper that **returns P1 for BOTH twins for now**.
- **Why first:** it's the one big change that can land without breaking single-player — everything
  else depends on there being two providers to route.
- **Checkpoint:** all four entry paths boot; one gamepad plays identically to today; a second device
  is inert; console clean. Nothing regressed.

### M1 — Ownership + per-player control (WALKING SKELETON → then full) — *the spine*
- **Goal (skeleton first):** two devices each **move** one twin. Then extend to attack + abilities.
- **Work:** introduce `PlayerRoster` (ownership map); **hardcode** P1→twinA, P2→twinB for now (no
  select UI yet). Replace `TwinSelector` with the ownership binder; delete `MirroredMovementModifier`
  (all twins Normal). Sweep the **14 selection consumers** (§1). Rewire the 3 dispatchers to
  per-player (§3). Fix `SceneFlowManager` active-location (D4) + `SelectedPlayerUI` (D3).
- **Why here:** ownership is the spine everything else routes through; prove movement (skeleton)
  before touching abilities so the input+ownership foundation is de-risked.
- **Checkpoint:** two devices, two twins, independent move → attack → primary abilities; tether drain
  on the shared bar; no null `SelectedTransform` / stuck-lock regressions (grep clean). *Tutorial is
  expected-broken here — fixed in M3.* Playable 2P sandbox achieved.

### M2 — Character select (Kai / Lyra / Random) — *feeds the roster*
- **Goal:** a real pre-game screen assigns the roster M1 hardcoded.
- **Work:** `CharacterSelectController` + UI; two-device pick; conflict resolution (distinct twins);
  Random auto-assigns both → writes `PlayerRoster`.
- **Why here:** needs M1's roster to exist; small and self-contained once ownership is real.
- **Checkpoint:** both players pick; distinct twins guaranteed; Random works; ownership flows into
  gameplay.

### M3 — Rescue + joint abilities + Empower — *the ability-layer rework*
- **Goal:** co-op rescue + the joint/held abilities behave.
- **Work:** rescue partner-mash (§4, remove ForceSelect/Lock); `JointAbilityGate` (§5.2) wired to the
  joint set (D2: Setsuna/SoulConvergence/AccordSpirit/Accord); **Empower per D1** (caster anchors,
  partner buffed; rebind dash off the freed Shift); Teleport selection-lock → no-op.
- **Why here:** depends on M1 ownership; rescue/joint logic is where the deleted selection hurt most.
- **Checkpoint:** P1 grabbed → P2 mashes to rescue; joint ability fires only on both-within-grace and
  cancels otherwise; Empower buffs the partner; nothing selection-related throws.

### M4 — Tutorial co-op rewrite (SHARED progression, D6) — *the long pole*
- **Goal:** the tutorial teaches two players together, no switch-mechanic step.
- **Work:** rewrite the 5 selection-coupled tutorial files for **shared progression** (steps advance
  for both at once); delete switch-teaching steps; make `TutorialInputGate` per-provider but advance
  jointly; rebind `TutorialTimelineDirector` targets to the ownership map (R11).
- **Why here:** most coupled + highest-risk; do it once the control model underneath is stable so we
  rewrite against a fixed target. Schedule generously.
- **Checkpoint:** full Bootstrap tutorial run, two players, progressive unlock per category advances
  jointly; direct-area play (no gate) fine; all four entry paths.

### M5 — HUD / UI polish — *parallelizable, finalize after M1–M3*
- Second per-player ability strip; shared-health bar stays single; identity UI (D3); per-device
  prompt glyphs; pause from either device.

### M6 — Optional: F13 ability close-up cam
- Phase-1 = common CLOSE ability vcam (TargetGroup, separation-gated). Phase-2 = the split-screen
  beat (two vcams, one CamRig clip, viewport halves, wipe in/out). Ships after the game is fun in 2P.

### M7 — Optional: co-op sync puzzles (new CONTENT)
- `CoopSyncGate` (reuses the M3 grace mechanism) + authored puzzle instances. Additive; last.

**Critical path:** M0 → M1 → M3 → M4. (M2 can slot after M1; M5 parallels; M6/M7 are optional tail.)

### Sub-slice checklist (living — tick as each lands; commit ref in parens)
- **M0 — input split (non-breaking)**
  - [x] M0.1 input-ownership seam — `PlayerInputRouter` + `IPlayerInputRouter` (`6aa2c1b`)
  - [x] M0.2a route shared-UI consumers → `PlayerInputRouter.SharedInput` (Pause, SkillTree, Overview,
    Intro, QTE + QTEController, ControlHints, WorldSpacePickupPrompt, InputPromptView) — 9 sites, 0-err (`3745fbc`)
  - [x] M0.2b Persistent scene wiring — `PlayerInputRouter` GameObject added to Persistent.unity, slot wired to
    same-scene `TwinInputReader` on `PlayerManager` (verified `Shared` resolves)
  - [ ] M0.3 second-provider scaffolding (`PlayerInputManager` / 2nd reader) spawnable but idle
- **M1 — ownership + per-player control (walking skeleton → full)**
  - [x] M1.1 `PlayerRoster` ownership map (hardcode P1→twinA, P2→twinB) — code (`PlayerRoster`+`PlayerSlot`, 0-err,
    `4601626`) + Persistent GameObject wired (twinA=Lyra/twinB=Kai, `Twins` verified)
  - [x] M1.2 **movement** decoupled from `TwinSelector` (SelectLeft/Right no longer set modifiers; `MirroredMovementModifier`
    deleted — null modifier = normal, mirror gone). `TwinSelector` kept ALIVE for the ABILITY path (selection stays until
    M3); physical `TwinSelector.cs`/`NormalMovementModifier` deletion = post-M3 cleanup (Appendix A teardown)
  - [x] M1.3 registry (13 sites → `PlayerRoster`, `6169043`) **+ D4** (SceneFlow active-location → TwinA/BUG-099)
    **+ D3** (SelectedPlayerUI neutralized/BUG-100). The ability-dispatch selection site → M1.5/M3 (entangled)
  - [x] M1.4 per-player **movement** dispatch — `TwinMovementDispatcher` drives each twin via `PlayerInputRouter.For(twin)`
    (P1→TwinA, P2→TwinB; P2 falls back to P1 single-device); soul on shared input
  - [→M3] M1.5 per-player **attack + ability** dispatch — **DEFERRED**: `TwinAttackDispatcher`/`TwinAbilityDispatcher`
    entangled with Accord/rescue/soul/teleport-emergency (M3) + pending D2. Stays selection-based until M3
  - [~] M1.6 router device-aware — `For(twin)` routes by `PlayerRoster.SlotOf` to P1/P2 slot (P2 optional, falls back).
    DONE. Pending: wire a real P2 device provider (user's Input-System work) + any-of `Shared` aggregator (BUG-096)
  - [→M3] M1.7 free Shift — **DEFERRED**: Shift still selects which twin casts abilities until M1.5/M3 land (BUG-098)
- **M2 — character select (Kai/Lyra/Random)** — *now part of a fuller FRONT-END: Start Menu (New Game /
  Continue / Options / Exit) → save-slot select → character select. Save slots = a separate later milestone
  (no disk save system exists yet — moderate/low-risk; `allLocations[]` registry solves the SO-ref gotcha). Menu +
  slot UI STUBBED for now; character-select core built first (couch-critical, roster-only, testable). Mode-agnostic
  (couch local devices now, online lobby later).*
  - [x] M2.1 `CharacterSelectController` + `CharacterPick` — pure state machine, writes `PlayerRoster.Assign` ×2 on
    finalize; both default Random; Select/Back = `SetReady`; both-ready+distinct gate; `OnSelectionComplete`.
    0-err + self-test green (`8aa9ec2`).
  - [x] M2.3 conflict resolution + Random auto-assign — folded into M2.1 (`TryResolve`: distinct guarantee,
    same-explicit → `HasConflict`/won't-start, both-Random coin). Headless [MenuItem] self-test (10 cases).
  - [~] M2.2 select UI — greybox built + wired in Persistent (`15c0cab`): P1/P2 Cycle+Ready+label, status, Back.
    **Mouse-driven for now**; real per-device input via `PlayerInputRouter` (+ P2 device) still pending.
  - [~] M2.4 Start Menu shell (New Game / Continue[stub] / Options / Exit) built + wired (`15c0cab`) +
    `GameBootstrapper.useFrontEnd` gate in the DEV branch (`249fc5c`). **Play-test pending** (needs DevConfig
    dev mode). Intro-mode integration + menu-first-boot rewire = later.

#### FRONT-END STUBS & OUTSTANDING — *nothing-lost ledger* (M2, 2026-08-17)
The couch/online pre-game FRONT-END = **Start Menu → (New Game→save-slot) → Character Select → gameplay**.
Only the character-select *logic core* is built. Everything below is explicitly deferred/stubbed so nothing is lost:

| Item | State | Notes / what "done" means |
|---|---|---|
| `CharacterSelectController` + `CharacterPick` (M2.1/M2.3) | ✅ **DONE, verified** | 0-err + self-test green (10 cases). Writes `PlayerRoster.Assign` ×2; both-Random default; same-twin blocks start. |
| Two-device select **UI screen** (M2.2) | 🟡 **GREYBOX DONE (`15c0cab`)** | Built + wired in Persistent. **Mouse-driven** for the flow test; real per-device input via `PlayerInputRouter` (+ P2 device) still to wire. |
| **Start Menu** shell (M2.4) | ✅ **GREYBOX DONE (`15c0cab`)** | New Game / Continue[stub] / Options / Exit — greybox canvas in Persistent, wired. |
| `GameBootstrapper` gate | ✅ **DONE (`249fc5c`)** | `useFrontEnd` flag runs the front-end in the DEV branch after Persistent, before area. Fail-open. Full menu-first-boot rewire + intro-mode integration = later. |
| **Play-test the flow** | ⛔ **NEXT** | Set `DevConfig.skipTutorial=1` (dev mode), open Bootstrap, Play → menu → New Game → pick+ready → L1_Park loads. Watch: TMP text visibility (font). MCP can't click, so this is a manual run. |
| **Continue** menu action | 🚧 **STUBBED** | Button present but disabled — depends on save persistence (below). |
| Save-slot **select screen** | 🚧 **STUBBED** | Deferred with persistence. New Game → straight to Character Select. |
| **Save persistence** (JSON slots) | ⛔ **OUTSTANDING (own milestone)** | *No disk save system exists yet* — settings-only (`PlayerPrefs`). Moderate/low-risk: `GameSaveData` (id-keyed mirror of `CheckpointData`) + JSON to `persistentDataPath/slot_N.json` + slot manager. One gotcha (SO refs) already solved by `SceneFlowManager.allLocations[]` + unique asset names; skill snapshot → `{treeId,level}` pairs. ~½–1 day. |
| **Options** action | 🚧 **STUB** | Currently logs a TODO; wire to the existing `GraphicsSettingsController` / settings UI (already `PlayerPrefs`-backed). |
| **P2 device provider** (from M1.6) | ⛔ **OUTSTANDING** | Real 2nd-device reader into `PlayerInputRouter` P2 slot — required for a genuine two-device select. User's Input-System work; single-device falls back to P1 until then. |
| **Exit** action | ✅ **DONE** | `Application.Quit()` (+ editor-stop guard) wired in `MainMenuController`. |

#### Character-Select screen V2 — SPATIAL redesign (future / user-requested, recorded 2026-08-17)
*Flow works with the greybox (Cycle/Ready) — this is the intended visual/UX for the polish pass. Deferred.*
- **Spatial layout, not per-slot cycle buttons:** three zones — **LEFT = Kai**, **RIGHT = Lyra**, **CENTER-BOTTOM = Random** (the default). Kai/Lyra names shown large on their sides; Random in the middle-bottom.
- **Player markers:** P1 and P2 each shown as a **clan-colour outline/marker** (Kai=Vethara, Lyra=Luminari hues — see ArtStyle clan ramps). Both markers **start on Random** and the player moves their marker onto Kai or Lyra (or leaves it on Random).
- **Explicit Start button** (replaces the current auto-start-on-both-ready). Pressing Start validates before launching.
- **Distinct-twin rule at Start (unchanged logic):** if **both markers are on the SAME twin**, Start is refused — at least one player must be on **Random** (or move to the other twin). Random then resolves to the free twin; both-Random → coin-flip. (This is exactly `CharacterSelectController.TryResolve` — the V2 is a UI reskin over the same controller: map "marker in zone" → `SetPick`, Start button → check `CanStart`/`HasConflict`.)
- **Reuse:** the existing `CharacterSelectController` already backs this — V2 only replaces `CharacterSelectScreen`'s presentation (drag/move marker between 3 zones + Start button) and keeps `SetPick`/`SetReady`/`CanStart`/`HasConflict`/`OnSelectionComplete`.
- **M3 — rescue + joint abilities + Empower** — *decisions locked 2026-08-18:* **D1 = caster anchors, partner
  buffed** (faithful port); **D2 = ALL FOUR combined powers joint** (Accord entry, SoulConvergence, Setsuna,
  AccordSpirit), grace = a **tunable synchronized-start leniency window, default 0.5s**.
  - [ ] M3.1 rescue partner-mash (remove ForceSelect/Lock; grabbed frozen, partner mashes)
  - [x] M3.2 `JointHoldSync` + `JointAbilityGate` grace-window infrastructure (`couch-m1-ownership`; self-test
    15/15 green). Generic: per-ability `JointHoldSync` tracker fed both players' reads of its OWN key; gate holds
    the one tunable `LeniencyWindow`. Gate lives at each ability (not the input layer) because AccordSpirit +
    Empower share `GetEmpowerHeld` (solo outside Accord / joint inside). Input map — joint keys: **X-hold**
    (`GetCancelHeld`) = Accord entry; **F-hold** (`GetConvergenceHeld`) = SC & Setsuna (shared channel);
    **Empower-key hold** (`GetEmpowerHeld`, inside Accord) = AccordSpirit. Single-device (P2→P1) → degrades to solo.
  - [ ] M3.3 wire joint set through the gate (each ability reads P1/P2 via `PlayerInputRouter.For(TwinA/TwinB)`;
    add the `JointAbilityGate` GameObject to Persistent + resolve R4 in each consumer)
  - [ ] M3.4 Empower redesign **D1** (caster anchors, partner buffed; drop `ForceSelect`/`LockSelection`/
    `SelectedTransform`; rebind the Shift-dash to the buffed partner's own input) /BUG-097
  - [ ] M3.5 Teleport selection-lock → no-op
- **M4 — tutorial co-op rewrite (shared progression, D6 / BUG-094 — the #1 breakage)**
  - [ ] M4.1 per-provider tutorial gate
  - [ ] M4.2 shared progression (steps advance for both at once)
  - [ ] M4.3 remove switch-teaching steps
  - [ ] M4.4 rebind `TutorialTimelineDirector` targets to the ownership map (R11)
- **M5 — HUD / UI**
  - [ ] M5.1 second per-player ability strip · [ ] M5.2 SelectedPlayerUI → identity/delete (D3)
  - [ ] M5.3 skill tree per-player (D5) · [ ] M5.4 per-device prompt glyphs · [ ] M5.5 char-select screen polish
- **M6 — optional F13 ability close-up cam**
  - [ ] M6.1 common CLOSE ability vcam (phase 1) · [ ] M6.2 two per-twin vcams + shared CamRig clip
  - [ ] M6.3 split-screen halves + wipe in/out · [ ] M6.4 grabbed/stuck/dead guard (full-screen free twin)
- **M7 — optional co-op sync puzzles**
  - [ ] M7.1 `CoopSyncGate` (reuses M3 grace mechanism) · [ ] M7.2 authored puzzle instances

## APPENDIX A — M1 CONSUMER MAP (grep-verified 2026-08-16, plan-only)

**Key finding:** `TwinSelector` secretly does **two** jobs — (1) a twin **REGISTRY** (`LeftTwin`/`RightTwin`)
and (2) a **SELECTION** state machine (`SelectedTransform`/`ForceSelect`/`OnTwinSelected` + the Shift-toggle +
Normal/Mirrored modifiers + the switch-lock). Couch kills job 2 and keeps job 1 — **job 1 becomes
`PlayerRoster`.** Most of the sweep is therefore a mechanical registry swap, not a redesign.

**API surface** (`TwinSelector.cs`): registry `LeftTwin`/`RightTwin`; selection `SelectedTransform` (get),
`OnTwinSelected` (event), `ForceSelect(Player)`; lock `LockSelection`/`UnlockSelection`/`IsSelectionLocked`;
interfaces `ITwinSelector`/`ISelectionBroadcaster`/`ISelectionLock`; applies `Normal`/`Mirrored` modifiers on
select; reads `GetSwitchDown()` (Shift) in `Update` to toggle.

### ROLE 1 — Registry (`LeftTwin`/`RightTwin`) → `PlayerRoster`, behaviour-identical (selection-agnostic)
These do NOT care about selection — they just want "the two twins." Mechanical swap to `PlayerRoster.TwinA/TwinB`.

| Consumer (file:line) | How it uses it | M1 action |
|---|---|---|
| `CheckPointTrigger.cs:30-31` | both twins → checkpoint position save | → roster |
| `SoftResetController.cs:136-137,181-182,208` | fallback both twins → reset positions + occupancy seed | → roster |
| `IntroTimelinePositioner.cs:90-91` | left/right → deterministic timeline positioning | → roster |
| `TutorialCheckpoint.cs:70-71` | both twins | → roster |
| `TutorialTrap.cs:97-98` | both twins | → roster |
| `TutorialZoneTrigger.cs:53-54` | both twins → zone-entry test | → roster |
| `TutorialBoundary.cs:57` / `TutorialOuterBoundary.cs:34` | both twins → boundary containment | → roster |
| `IntroController.cs:149-151,219-222,234-238` | both twins → `NotifyTeleported`, movement-lock, spawn placement | → roster |
| `GameBootstrapper.cs:105-107,135-138,150-154` | both twins → `NotifyTeleported`, movement-lock, spawn placement | → roster |
| `SoulParticleAttractor.cs:82-90` | nearest of the two twins (soul VFX target) | → roster |
| `SceneFlowManager.cs:101-103` | editor-only: seed both twins into occupancy | → roster |
| `GameDebuggerV2.cs:122` | debug handle | → roster |
| `TimelineBindingResolver.cs:102` | comment reference only | → update comment |

### ROLE 2 — Selection state (`SelectedTransform`) → REDESIGN
| Consumer | How | Disposition |
|---|---|---|
| `SceneFlowManager.cs:248-253` (`ResolveActiveLocation`) | active area (skybox/ambient/navmesh) = the *selected* twin's location | **D4, M1** — no selected twin; drop the selected-preference block and fall through to the existing first-actor fallback at `:260+` (recommended), or host/P1-twin |
| `TwinAbilityDispatcher.cs:66-67` + event `47/53/72` | routes ability input to `SelectedTransform`'s `AbilityController` | **CORE M1** — per-player: one dispatch path per twin, resolving `AbilityController` from `PlayerRoster` + `PlayerInputRouter.For(twin)` |
| `EmpowerSystem.cs:422-424` (`GetCurrentTwin`) | which twin is the caster | **M3 / D1** |
| `RescueEventController.cs:380` | was the *selected* twin the one grabbed? | **M3** |

### ROLE 3 — `ForceSelect` callers → REDESIGN (all inside M3 systems)
`RescueEventController.cs:383` (grabbed → switch to other), `:443` (post-death → select survivor); `EmpowerSystem.cs:307` (anchor on empowered twin). All become no-op/redesign in **M3**.

### ROLE 4 — Selection LOCK (`Lock`/`Unlock`/`IsSelectionLocked`) → NO-OP
The lock's ONLY purpose is blocking the Shift-switch (`TwinSelector.Update:59`). No switch in couch ⇒ nothing to block. Call sites: `TeleportAbility.cs:223,378`; `EmpowerSystem.cs:306,347`; `RescueEventController.cs:139,256,279,386,409,425,665`; `TutorialTimelineStepSO.cs:30,48`; `TutorialStepBase.cs:36`; `TutorialUnlockAllStepSO.cs:30`; `TutorialDirector.cs:38,74`; `TwinAbilitySetup.cs:47,57`; `TutorialStepContext.cs:54,68,74-75`.
→ **M1 strategy:** `PlayerRoster` (or a tiny `NoOpSelectionLock`) implements `ISelectionLock` as a harmless no-op (`IsSelectionLocked`→`false`, `Lock`/`Unlock`→∅). All ~20 call sites keep compiling and behave correctly (nothing to block). **Remove the calls in a post-M3 cleanup slice** — no churn during M1.

### ROLE 5 — `OnTwinSelected` subscribers → REDESIGN
`TwinAbilityDispatcher` (Role 2). `SelectedPlayerUI.cs:31,39` → **D3** (repurpose as a per-player twin-identity indicator, or delete) — M1 stub / M5 finalize.

### ROLE 6 — `GetSwitchDown()` (Shift) → ORPHAN / REBIND
`TwinSelector.cs:60` (dies with the class). `EmpowerSystem.cs:263` — **dash reuses Shift** (BUG-098 / D1) → rebind in **M3**. Plumbing `TwinInputReader.cs:136` / `TutorialInputGate.cs:80` / `IInputProvider.cs:12` stays (harmless); Shift left unbound in M1.

**Movement modifiers:** `MirroredMovementModifier` is applied ONLY by `TwinSelector` → **dies with it**. `NormalMovementModifier` + `SetMovementModifier` + `IMovementModifier` stay (general); both twins run Normal.

### TEARDOWN SEQUENCING (resolves "delete `TwinSelector` in M1 vs Rescue/Empower still need it until M3")
1. **M1:** add `PlayerRoster` (registry + no-op `ISelectionLock`). Migrate the 13 Role-1 sites. Rewrite ability dispatch per-player (Role 2). Resolve D4 (SceneFlowManager) + D3 (SelectedPlayerUI).
2. **M1 leaves** `TwinSelector`'s selection members alive **only** for Rescue+Empower — *or* `PlayerRoster` exposes a defined-default `SelectedTransform` (= `TwinA`) so those two keep resolving until M3.
3. **M3:** redesign Rescue + Empower off selection (Roles 2/3/6 for those files).
4. **Post-M3 cleanup slice (isolated commit):** delete `TwinSelector.cs` + `MirroredMovementModifier.cs`, strip the no-op lock calls. `.meta` GUIDs drop with the deleted files.
→ **Refinement to sub-slice M1.2:** "kill `TwinSelector`" in M1 = strip its registry role + neutralize selection for M1-scoped consumers; the **physical file deletion is gated on M3** so Rescue/Empower are never orphaned.

### `PlayerRoster` design (M1.1)
Persistent **R3** singleton (dup-destroy `Awake` guard, null `Instance` on `OnDestroy`, no DDOL). Takes over
`TwinSelector`'s `leftTwin`/`rightTwin` serialized slots (same-scene R1). API: `Player TwinA`, `Player TwinB`,
`Player For(PlayerSlot)`, `Player Other(Player)`, `IEnumerable<Player> Twins`; implements `ISelectionLock` as
no-op; optional transitional default `SelectedTransform => TwinA`. M1 hardcodes P1→TwinA / P2→TwinB; **M2**
(char-select) writes ownership into it.

### Decisions M1 forces — ✅ RESOLVED (2026-08-16)
- **D3 SelectedPlayerUI → neutralize in M1, remove post-M5.** Make inert (cut `TwinSelector`/`OnTwinSelected`,
  apply a plain material once); no identity-marker visual to build (char-select conveys who's who).
- **D4 active-location → pin to HOST (P1/TwinA).** `ResolveActiveLocation` drops the `SelectedTransform` block and
  follows TwinA deterministically (no crossfade flicker; online-ready). Bond + adjacency make a non-adjacent
  split unreachable.
- **Shift:** unbound in M1; EmpowerSystem dash rebind is M3/D1.

## VERIFICATION (two entry paths, per Working Method)
Bootstrap full + direct area play: character select assigns two distinct twins (+ Random); two
devices each drive one twin; tether drain reads on the shared bar; trap-grab on P1 rescued by P2's
F-mash; a joint ability fires only when both press within grace and cancels otherwise; Empower behaves
per the chosen D1 model; tutorial unlocks correctly per D6; pause works from **either** device; F13
beat (if built) plays + wipes back. Confirm no null `SelectedTransform` / stuck selection-lock
regressions (grep clean).

## EXECUTION STEP (when we build it)
No code in the planning pass — decision doc. Write this couch plan as a new **instruction.md** phase
(P20 — Couch co-op) and cross-ref `game.md §26`; commit the Appendix nuances into `game.md §26`; log
the changelog doc-revision line; add a `project_mp_transition` memory. (Per no-doc-proliferation:
specs live in game.md/instruction.md, not new files — this standalone doc is a working reference kept
at the user's explicit request.)

---
---

# APPENDIX — MULTIPLAYER CONVERSION NUANCES (PRESERVE — couch+online reference, destined for game.md §26)

## Ownership pipeline (spine both modes share)
`Character Select (Kai/Lyra/Random) → ownership map {P1→twinA, P2→twinB} → per-player routing`.
`TwinSelector` dies; `MirroredMovementModifier` deleted; sweep `SelectedTransform/OnTwinSelected/
ISelectionLock` consumers first.

## Online — mechanism level (host/listen-server)
**Movement authority (the key question):** two models — (A) *client-authoritative*: "P2 tells the
server I moved north 2 m" (owner writes `NetworkTransform`, host doesn't re-simulate); (B)
*server-authoritative*: "P2 asks the server to move" (host simulates, client predicts+reconciles).
**Recommend (A) for the two player twins**, host-authoritative for everything with gameplay
consequence — friendly PvE, zero-latency feel matters, cheating doesn't, and (A) maps 1:1 onto the
existing client-local `PlayerMovementController`. Host reads reported positions for bond/health, owns
damage/enemies/rescue. **Caveat:** Setsuna rewind must temporarily **seize** P2's transform authority.

**Abilities player-wise (RPC flow):** P2 presses Q → `AbilityController.RequestPrimary()` **ServerRpc**
→ HOST validates (unlocked/cooldown/charged/state) + executes authoritatively (overlap/damage/spawn via
host-owned pool, heal pool) → sets cooldown NetworkVariable + fires cue event → BOTH clients play the
cue locally. Host owns cooldowns; client predicts UI. Client-local melee/AOE overlap → host-side.

**Joint abilities + grace (online):** P1/P2 each ServerRpc "joint-ready"; both within grace → host
fires + ClientRpc "play F13 beat"; else expire → "joint failed."

**F13 online form:** no split-screen — each client renders its OWN full-screen close-up of its OWN
twin (own vcam→prio 30, own CamRig clip, own AudioListener). F13's hardest costs are couch-only.

**Setsuna online (hardest):** host applies global `timeScale=0.15` (both slow — acceptable, flag as
design decision); rewind replays both paths host-side + `ForceSetHealth`, host **seizes** the client
transform for the window, streams corrected transforms (client rubber-bands), returns authority.
Invuln + `CharacterController` disable replicate. Local-bubble slow = redesign, not recommended.

**Rescue/AI/pool/streaming/QTE online:** Rescue FSM (721 lines, singleton) → host-only, state =
NetworkVariables, mash = ServerRpc. GOAP+BT+Blackboard → host-only sim, clients render. `EnemyPool`
reuse → stable NetworkObject id across reuse. `SceneFlowManager` occupancy → replicated,
`NotifyTeleported` host-side. QTE/checkpoint/soft-reset → host-authoritative + re-sync both clients.

## What breaks (online-specific additions to the couch list)
Setsuna vs client-auth movement (host seizes authority); camera feel/F13 (drive cues from replicated
events, F13 per-screen); enemy pool ↔ NetworkObject lifetime; `Time.time` cooldowns per-client →
host; FxManager/Manpu mood glyphs = replicated events; singletons across disconnect/host-migrate
(R3 + NetworkObject lifetime); soft-reset re-sync.

## Netcode stack — **Unity Netcode for GameObjects (NGO)**
Host/listen-server + Unity Relay + Lobby. First-party (Unity 6 longevity), casual-co-op-positioned,
NetworkVariable/RPC maps 1:1 onto PoT's singleton+event design, free NAT punchthrough, proven by the
closest comparable (**Lethal Company = NGO**). **FishNet** = free fallback (better prediction).
**Fusion 2** = only for managed relay + top-tier netcode (CCU cost); its Shared/client-auth mode is
neutralized by shared-fate state. **PUN2 = avoid** (REPO/Content Warning use the older PUN; even
REPO's devs say pick Fusion next). Decide the stack before writing any netcode.
Comparable-game data: Lethal Company=NGO; R.E.P.O.=Photon PUN; Content Warning=Photon; Schedule 1=
community-reported FishNet (unconfirmed).

## Online roadmap (after couch ships)
Ownership pipeline (shared) → couch → online vertical slice (NGO: two twins client-auth move + bond
replicates + one GOAP enemy host-side + host-auth damage) → online systems port (ability RPCs, rescue
FSM, pool net ids, streaming, QTE, checkpoint) → Setsuna networked (last) → tutorial co-op model
threaded throughout.
