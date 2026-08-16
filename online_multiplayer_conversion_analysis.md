# ONLINE MULTIPLAYER — Deep-Dive Conversion Plan (host / listen-server)

> Saved 2026-08-16. Companion to `couch_multiplayer_conversion_analysis.md`. Working reference only —
> not yet folded into game.md §26 / instruction.md P21 (deferred until the MP work is greenlit).

## Context
Convert PoT to **online 2-player co-op** on a **client-side hosting** model: one player runs the
game AND is the authority (listen-server / host); the other joins as a client. This is the online
counterpart to the couch deep-dive (saved in `couch_multiplayer_conversion_analysis.md`) — same
ownership spine, but now every "free" single-machine assumption needs an authority owner +
replication. This plan is **file-anchored and mechanism-level**: for each system, *who owns it, what
replicates, what's an RPC, and the porting risk*. It stays **SDK-agnostic** (neutral terms:
*authority owner / replicated var / intent-RPC / broadcast / networked-object*) and includes a
**stack-selection analysis** so the SDK is chosen after review.

**Grounded by code sweep this session:** read `PlayerAttackController` (client-local `OverlapSphere`
hit detection), `DamageData` (single damage chokepoint → `IDamageable`), `AbilityController` +
`AbilityBase.Tick` (per-instance cooldowns), `Enemy.cs` (GOAP brain + `Blackboard` + `NavMeshAgent` +
`StatusEffectController`, `IsBrainPaused`), `EnemyPool`/`GameplayPool` (pooled reuse), `SetsunaSystem`
(global `timeScale` + recorded-path rewind + `ForceSetHealth`), `SceneFlowManager`
(`Dictionary<Player,WorldLocationSO>` occupancy + `NotifyTeleported`), `SharedHealthPool`,
`RescueEventController`, plus the couch selection/input maps.

## Locked scope (user, 2026-08-16)
- **Model:** host / listen-server ("client-side hosting"). 2 players, PvE, one twin each.
- **SDK:** **stack-agnostic plan** + a selection analysis (§3); concrete SDK chosen after.
- **Session v1:** **lobby-join, host-leaves-ends.** No drop-in mid-level, no host migration. Both
  (join-in-progress + authority shifting / host migration) are a **later milestone** (N9).
- **Setsuna:** **whole-session slow + host-driven rewind** (both players slow — accepted design);
  scheduled as the **last** online milestone (N8).
- **Prereq:** the **couch ownership pipeline** (character select → `PlayerRoster` → per-player
  routing; `TwinSelector` dead) is assumed done first — online reuses it wholesale.

---

## THE AUTHORITY MODEL (the spine)
Two authority tiers, one deliberate split:
- **Client-authoritative (owner writes):** the **two player twins' movement**. Each client simulates
  its own twin locally at zero latency (`PlayerMovementController` unchanged) and writes its transform
  to the network; host does NOT re-simulate. Rationale: friendly PvE, responsiveness > anti-cheat.
- **Host-authoritative (everything with gameplay consequence):** shared health/bond, all damage,
  enemies + AI, rescue, abilities' *effects*, cooldowns, streaming, QTE, checkpoints, Setsuna.
- **The one collision:** Setsuna rewind must temporarily **seize** each twin's transform authority
  from its owner, then return it (§1 Setsuna).

Three primitives used throughout (neutral names):
- **REPL** = replicated state var (host writes, clients read) — e.g. `CombinedHealth`, `RescueState`.
- **RPC→host** = client sends an *intent* (attack, ability, mash) the host validates + executes.
- **RPC→clients** = host broadcasts a result/event so every client plays cues locally.

---

## SECTION 1 — SYSTEM-BY-SYSTEM AUTHORITY MAP (file-anchored)

| System | File(s) | Authority | Replication / RPC | Porting risk |
|---|---|---|---|---|
| **Player twin movement** | `PlayerMovementController.cs` | **client (owner)** | owner writes networked-transform; host + peer read | Low — code stays; add a networked-transform (owner-auth) + ownership from `PlayerRoster` |
| **Input** | `TwinInputReader`, dispatchers | client-local | gameplay intents that have effects → **RPC→host** (attack/ability/teleport/mash); pure move stays local | Low-med — dispatchers already per-player from couch; wrap effectful calls as RPCs |
| **Shared health / bond** | `SharedHealthPool.cs` (`CombinedHealth`, `CombinedSurvival01`, `ForceSetHealth`, `OnSharedPoolEmpty`) | **host** | `CombinedHealth` + `CombinedSurvival01` = **REPL**; `OnSharedPoolEmpty` fires host-side → RPC→clients (game over) | Med — singleton → host-only; bar reads REPL |
| **Distance drain** | `DistanceHealthSystem.cs` (pure fn) | **host** | host computes from both **reported** twin positions → folds into health REPL | Low — pure function, just run host-side on authoritative positions |
| **Combat / hit detection** | `PlayerAttackController.cs:105-129` (`Physics.OverlapSphere`, animation-event-driven) + `DamageData` + `IDamageable` | **host** | owner plays swing anim locally (predict) + **RPC→host** "attacked"; host runs the overlap + applies `DamageData`; damage multipliers (`DamageOutMultiplier` = SC×Empower) must be **host-known** (buff state REPL) | **High** — every attack path; the single biggest gameplay-correctness change |
| **Abilities & cooldowns** | `AbilityController.cs` (`ActivatePrimary`/`TryActivate`, `Tick`), `AbilityBase.Tick` (Time-based cooldowns), the per-ability systems | **host** (effects) | `ActivatePrimary/Teleport` → **RPC→host** validate (`PrimaryLocked`, cooldown, unlocked) + execute; cooldown = host state, owner **predicts UI**; lock counters host-side | High — many ability systems, each an intent path |
| **Enemy AI** | `Enemy.cs:20-31` (GOAP brain + `Blackboard` + `NavMeshAgent`, `IsBrainPaused`), `EnemyAttackController`, `PerceptionManager`, `ServiceLocator` | **host-only sim** | enemy transform + anim + status flags = **REPL**; brain/blackboard/navmesh **never run on clients**; enemy attacks resolve host-side | **High** — largest surface; clients become render-only for enemies |
| **Enemy pool** | `EnemyPool.cs` (`Get`/`Return`/`SpawnReady`), `EnemySpawner` | **host** | pooled enemies need **stable networked-object id across reuse** — register/deregister on Get/Return, not just `SetActive`; spawn = host, replicate to clients | High — pooling ↔ net-object lifetime is a classic footgun |
| **Gameplay pool (projectiles/bombs/chains)** | `GameplayPool` (`AddUser`/`RemoveUser`), `Enemy.RegisterPooledPrefab` | **host** | host-owned networked-objects; refcounted warmers stay host-side | Med — same net-id-across-reuse concern as enemies |
| **Status / possession / grab** | `StatusEffectController`, `Enemy` (`IStunnable/IPossessable/IGrabbable/IFear/ISlow`) | **host** | apply host-side; replicate as flags → clients play stun/fear/possess visuals | Med — coroutine-driven effects run host-side |
| **Rescue** | `RescueEventController.cs` (721-line singleton FSM) | **host-only** | `RescueState`/mash progress/TTK/cooldown = **REPL**; rescuing client's F-mash = **RPC→host**; soul deploy/return + trap grab/kill resolve host-side | **High** — big FSM, many event edges, couch already removed its selection coupling |
| **Setsuna** | `SetsunaSystem.cs` (global `timeScale=0.15`, recorded-path rewind, `ForceSetHealth`, invuln + `CharacterController` disable) | **host** | host applies session-wide slow (both clients mirror `TimeScaleService` REPL); host records+replays both paths, **seizes** each twin's transform authority for the ~1.5 s rewind, streams corrected transforms (client rubber-bands), returns authority; invuln + CC-disable REPL | **Highest** — do LAST (N8) |
| **Time systems** | `TimeScaleService`, `TimeFactorManager`/bootstrapper | **host** | current timescale value/owner = **REPL**; clients mirror; entity-freeze registry host-side | Med |
| **Streaming** | `SceneFlowManager.cs` (`_currentLocation` `Dictionary<Player,…>`, `NotifyTeleported`, `OnLocationWillUnload`; reads `TwinSelector.Instance:102,248`) | **host** | occupancy (both twins + soul) = host state; loaded-set decisions host-side + networked scene management; **`NotifyTeleported` fires host-side**; active-location rule uses `PlayerRoster` (couch D4, not selection) | High — network scene load/unload + occupancy sync |
| **QTE** | `QTEManager` (FSM), `QTESceneAnchor` | **host** | QTE state + mash = **REPL**; the involved client's mash = **RPC→host** | Med |
| **Checkpoint / soft-reset** | `CheckPointManager`, `SoftResetController` | **host** | save data host-side; respawn positions/health/points/upgrades → REPL; soft-reset (no scene reload) must re-sync BOTH clients | Med-high |
| **Camera / FX / Manpu / Audio** | `CameraManager`/`CameraCueDriver`, `FxManager`, `ManpuSlot`, `AudioManager` | **client-local** | driven by **replicated events**, never host-local calls; each client plays its own cues/camera-feel; Manpu mood = replicate the mood-change event, glyph plays locally | Med — must route cue triggers through networked events |
| **Ownership / char select** | `PlayerRoster`, `CharacterSelectController` (from couch) | host assigns | ownership = REPL (networked-object ownership); reused wholesale | Low — built in couch |

---

## SECTION 2 — WHAT BREAKS — ranked (online-specific)
1. **Enemy AI authority (biggest surface).** GOAP + `Blackboard` + `NavMeshAgent` + perception must
   run **host-only**; clients render replicated transforms/anim/status. Every enemy archetype + the
   ecology layer (mood/faction/POI) is host-side. Risk: accidental client-side brain ticks →
   divergence.
2. **Combat correctness.** `PlayerAttackController` client-local `OverlapSphere` → host-authoritative
   overlap; double-damage / ghost-hits if any client still resolves damage. Buff multipliers
   (`DamageOutMultiplier`) must be host-known.
3. **Setsuna vs client-auth movement.** Rewind overrides the owner's transform → host must seize +
   return authority cleanly; a botched handoff = rubber-band/teleport bugs. (N8, last.)
4. **Enemy/gameplay pool ↔ net-object lifetime.** Reuse (`Get`/`Return`) must recycle stable
   networked-object ids, not just `SetActive`; stale ids desync spawns.
5. **`Time.time` / coroutine cooldowns per-client.** Ability cooldowns (`AbilityBase.Tick`) and
   status-effect coroutines desync if run per-client → resolve host-side; clients predict UI only.
6. **Streaming replication.** `SceneFlowManager` occupancy + network scene load/unload; both remote
   players drive the loaded set; `NotifyTeleported` host-side; `OnLocationWillUnload` ordering.
7. **Cue/camera/Manpu triggers.** `FxManager`/`CameraCueDriver`/`ManpuSlot` are effectively local —
   must be driven by replicated events so both clients see the same VFX/mood.
8. **Rescue FSM edges.** 721-line state machine → host-only + REPL; many transitions (soul died,
   both-trapped, TTK) must replicate exactly or enemies freeze/never attack (the existing
   `IsRescueActive` freeze contract).
9. **Singletons across connect/disconnect.** R3 (no DDOL) already fights Restart statics; add
   net-object lifetime + join/leave. (Host-migration deferred to N9, but disconnect cleanup is N1.)
10. **Soft-reset re-sync.** No-scene-reload respawn must re-sync both clients atomically.

---

## SECTION 3 — NETCODE SDK SELECTION ANALYSIS (choose after review)
Plan is SDK-agnostic; this is the decision aid. Criteria weighted for **2P host-authoritative PvE
co-op, Unity 6.3, GameObjects (not DOTS), free-preferred, single-machine sim that just needs
replicating (no competitive rollback)**.

| Criterion | NGO | FishNet | Mirror | Fusion 2 |
|---|---|---|---|---|
| Cost / licensing | Free | Free (Pro paid) | Free | CCU pricing |
| Host / listen-server fit | Native | Native | Native | Host Mode ✓ |
| Client-side prediction | Basic | **Strong** (built-in) | Manual | **Strongest** |
| Networked-object pooling | Supported | **Strong** | Supported | Supported |
| Unity 6 first-party / longevity | **Yes** | 3rd-party | 3rd-party | 3rd-party |
| Relay / NAT punchthrough | **Unity Relay+Lobby (free)** | via transports | via transports | Photon Cloud |
| Docs / learning curve | **Best (first-party)** | Good | Good | Good |
| Comparable co-op shipped | **Lethal Company** | Schedule 1 (reported) | Population:ONE | REPO/Content Warning (PUN) |
| Maps onto PoT singleton+event design | **1:1** | 1:1 | 1:1 | Needs more restructure |

**Recommendation: NGO** (first-party longevity, free Relay/Lobby, best fit for the manager-singleton
architecture, closest comparable). **FishNet** is the strong free alternative if built-in prediction
becomes a requirement. **Fusion 2** only if managed cloud relay + top-tier netcode justify CCU cost;
its client-auth Shared Mode is neutralized by PoT's shared-fate state. **PUN2 = avoid** (legacy).
**Decide the SDK before N1** — retrofitting is the expensive path.

---

## SECTION 4 — STAGED BUILD ORDER (milestones)
Each milestone ends in a runnable host+client build. Order is dependency-driven and de-risks the two
hardest surfaces (AI authority, Setsuna) by isolating them.

- **N0 — Prereq: couch ownership pipeline done** (char select + `PlayerRoster` + per-player dispatch;
  `TwinSelector` dead). Online reuses it; do NOT start online on the selection-coupled codebase.
- **N1 — SDK spike + connection layer.** Pick the SDK (§3); stand up host/listen-server + lobby-join
  (host-leaves-ends); disconnect cleanup for the manager singletons (R3). Deliverable: two builds
  connect, spawn a networked-object each, clean disconnect.
- **N2 — Vertical slice: movement + shared health.** Two player twins with **client-auth** networked
  movement (owner writes) bound to `PlayerRoster`; `SharedHealthPool` host-only with `CombinedHealth`/
  `CombinedSurvival01` **REPL**; distance drain host-computed from both reported positions. Prove the
  tether syncs.
- **N3 — One enemy, host-authoritative.** One archetype's GOAP brain + navmesh + attack run **host-
  only**; transform/anim/status **REPL** to the client; enemy damage → shared pool host-side. Prove
  the client sees a correctly-behaving enemy it does not simulate.
- **N4 — Player abilities: intent-RPC + host cooldowns.** `ActivatePrimary`/melee → **RPC→host**;
  host validates + resolves overlap/damage (moves `PlayerAttackController` overlap host-side);
  cooldown host state, owner predicts UI; cues via replicated events (§1 FX row).
- **N5 — Rescue FSM host-only.** Port `RescueEventController` to host authority; `RescueState`/mash
  REPL; partner mash = RPC→host; verify the enemy-freeze `IsRescueActive` contract replicates.
- **N6 — Pools networked.** `EnemyPool`/`GameplayPool` reuse recycles stable networked-object ids;
  spawner host-authoritative; projectiles/bombs/chains host-owned.
- **N7 — Streaming + QTE + checkpoint.** `SceneFlowManager` occupancy REPL + networked scene
  load/unload + host-side `NotifyTeleported`; QTE host-authoritative; checkpoint/soft-reset re-sync
  both clients.
- **N8 — Setsuna networked (LAST).** Whole-session host slow (clients mirror `TimeScaleService`
  REPL) + host-driven rewind with transform-authority seize/return + invuln/CC-disable REPL.
- **N9 — Later milestone (post-v1): join-in-progress + host migration / authority shifting** — full
  live-state serialization on join + authority handoff when the host leaves. Explicitly out of v1.

**Critical path:** N0 → N1 → N2 → N3 → N4 → N5 → N7 → N8. (N6 supports N3/N4; N9 is post-v1.)

---

## SECTION 5 — DECISIONS
- **✅ Locked:** host/listen-server; client-auth twin movement + host-auth everything-else;
  SDK-agnostic plan (recommend NGO, decide before N1); session v1 = lobby-join/host-leaves-ends;
  Setsuna = whole-session slow + host rewind, scheduled N8; host migration + join-in-progress = N9.
- **Pending (surface at N1):** anti-cheat stance (assume relaxed for friendly PvE — client-auth
  movement trusts the peer); exact interpolation/reconciliation tuning; whether ability *windups* get
  client prediction or wait for the host round-trip (feel vs simplicity).

## SECTION 6 — VERIFICATION (host + client, two builds/editors)
Per milestone: run a host build + a client build (or two editors); assert — both twins replicate with
client-auth movement (no jitter/rubber-band in steady state); tether drain reads identically on both;
damage lands **host-side only** (no double-hit / ghost damage on the client); one enemy's GOAP runs
host-only and renders correctly on the client (kill it → dies on both); an ability intent round-trips
(host resolves, cue plays on both); rescue state + partner-mash replicate; disconnect leaves no leaked
manager singleton (R3) or orphaned networked-object; (N8) Setsuna slows both, rewinds both, returns
authority with no stuck-invuln/teleport. Run from Bootstrap + a direct area.

## EXECUTION STEP (when we build it)
No code in the planning pass — decision doc. When the MP work is greenlit, fold both this and the
couch analysis into `game.md §26` + instruction.md phases (couch = P20, online = P21) and add a
`project_mp_transition` memory. (Standalone docs kept at the user's explicit request; per
no-doc-proliferation the canonical home is game.md/instruction.md.)
