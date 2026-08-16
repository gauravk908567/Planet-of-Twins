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
- **D3 — `SelectedPlayerUI`** (pending): repurpose as P1/P2 identity tint, or delete.
- **D4 — Active-location rule** (pending) when no selection (prefer P1's twin? first loaded?).
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
