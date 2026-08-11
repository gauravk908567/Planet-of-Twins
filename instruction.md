# INSTRUCTION.md — Multi-Scene Architecture Correction Work Order

> **Audience:** Claude Code working in the Planet of Twins repo, branch `multiscenesetup`.
> **Status:** The game is currently broken after the Persistent-scene migration. This file is the
> authoritative work order to fix it. Read it fully before touching any file. `game.md` is the
> systems reference; `CLAUDE.md` is the working conventions; **this file is the active task list.**
>
> Work through the phases **in order**. Do not skip ahead. Each phase has a Definition of Done.
> Log every change in `changelog.md` as you go.

---

## 0. Root Cause — read this first

The macro-architecture you built is correct and stays:

```
Bootstrap.unity (index 0, only scene loaded at boot)
  → loads Persistent.unity additively (never unloaded during play)
  → loads Intro.unity OR jumps straight to the starting area (dev mode)
  → SceneFlowManager streams area scenes (L1_Park, L2_Streets, …)
     keeping {occupied area + declared adjacents} loaded.
```

What broke the game was **not** this structure. It was the migration of objects into
Persistent.unity **without a single canonical rule for how references work across scenes**.
Three distinct failures got conflated:

1. **Same-scene refs lost in transit.** When canvases/managers were cut from L1Park and pasted
   into Persistent, Unity nulled every serialized ref the moment source and target were in
   different scenes mid-move. Many of these pairs **ended up in the same scene** (both in
   Persistent) — those refs are not architecturally broken, they are just **unwired**. They need
   Inspector re-wiring, not code.

2. **Genuine cross-scene refs.** Scripts in area scenes (`FailureResetSequencer`,
   `FailureNotice` on TutorialManager) serialize-reference UI that now lives in Persistent.
   **Unity cannot serialize cross-scene references — ever.** These need a code-level access
   pattern, and the pattern must be one consistent rule, not per-script improvisation.

3. **No dev entry path.** Pressing Play directly in L1_Park loads no Persistent scene, so every
   manager is null and the game *looks* completely broken even where the code is fine. Until
   Phase 1 is done, half of what looks broken is actually this.

The previous "fix pattern" written in game.md §21 — *"add
`if (field == null) field = FindAnyObjectByType<T>()` in Awake()"* — is **revoked**. Do not
apply it anywhere new. Reasons: it cannot resolve interface-typed fields (interfaces are not
`UnityEngine.Object`), it silently grabs the wrong instance when duplicates exist, it hides
wiring mistakes instead of surfacing them, and `Awake()` ordering across additively-loaded
scenes is not guaranteed. The canonical replacement is Rulebook law **R4** below.

---

## 1. THE REFERENCE RULEBOOK — these are law from now on

Every future script must comply. Every fix in this work order applies one of these rules.
When reviewing your own diff, name the rule each reference obeys.

**R1 — Same-scene serialized refs are always allowed.** This includes Persistent→Persistent.
Prefer the project's existing DI pattern (`[SerializeField] MonoBehaviour` slot → cast to
interface in `Awake`). Inspector wiring stays the default inside a scene.

**R2 — Cross-scene serialized refs are forbidden.** Not discouraged — forbidden. If the
Inspector would show "Scene mismatch", the design is wrong. No exceptions.

**R3 — Persistence = residency in `Persistent.unity`. `DontDestroyOnLoad` is banned.**
The Persistent scene is never unloaded during play, so scene residency *is* persistence.
DDOL objects survive `LoadScene(Bootstrap, Single)` and therefore **duplicate every manager on
restart** — this is a live bug class. **Standing exemption (E1, §17):**
`CommonCore.MonoBehaviourSingleton<T>.OnAwake()` unparents + applies DDOL, and its `Instance`
getter fabricates a blank `GameObject` when none is found — the 1.4 correction of that base
class was attempted, **broke the enemy/perception stack, and is permanently cancelled**. R3
binds every *project-side* singleton (the hand-rolled `Instance` pattern); the AIFramework
base class is exempt as-is. The only other historical DDOL user (`CheckpointLoader`) is
obsolete.

**R4 — Area→Persistent access: serialized slot first, singleton resolve in `Start()` second.**
The canonical consumer shape:

```csharp
[SerializeField] private MonoBehaviour _pointBankMono;   // optional, for same-scene/test wiring
private IPointBank _pointBank;

private void Awake()
{
    _pointBank = _pointBankMono as IPointBank;           // self-wiring only — no lookups here
}

private void Start()
{
    _pointBank ??= SkillTreeManager.Instance;            // cross-scene resolve — Start(), not Awake()
    if (_pointBank == null)
    {
        Debug.LogError($"[{name}] IPointBank unresolved — is Persistent loaded?", this);
        enabled = false;                                  // degrade loudly, never NRE-spam
    }
}
```

Notes on R4: the field stays **interface-typed** (DIP preserved); the resolve line is the *only*
place a concrete singleton type may appear. Resolution happens in `Start()` because Persistent's
`Awake()`s are only guaranteed to have run by then under every entry path (Bootstrap and editor
direct-play). `FindAnyObjectByType` is reserved for genuinely scene-scoped, non-singleton lookups
and cold paths (e.g. `EnemyFreezeService` enumerating enemies) — never for manager acquisition.

**R5 — Persistent→Area access: registries only.** A persistent manager may never cache a
serialized or long-lived reference to an area-scene object. Area objects **self-register** in
`OnEnable` and **self-unregister** in `OnDisable`/`OnDestroy` (named handlers, both directions).
Managers iterate live registrations, tolerate zero entries, and defensively purge null entries
before iterating (scene unload can race lifecycle order).

```csharp
// Area-scene object (e.g. SpawnZone)
private void OnEnable()  => EnemySpawner.Instance?.RegisterZone(this);
private void OnDisable() => EnemySpawner.Instance?.UnregisterZone(this);
```

**R6 — Area↔Area references are forbidden.** Two different area scenes must never know about
each other directly. Communicate through a Persistent mediator or an SO event channel.

**R7 — ScriptableObjects are config, never runtime state.** Anything that mutates during play
lives in a plain runtime class owned by a Persistent manager. `AbilityUpgradeData.currentNodeIndex`
is the standing violation — Phase 4 removes it.

**R8 — Lifecycle law.** `Awake` = wire *yourself* (cast serialized slots, build internal state).
`Start` = resolve *others* (R4). `OnEnable/OnDisable` = subscribe/unsubscribe, always with named
handler methods (anonymous lambdas cannot be `-=`'d — this exact leak already bit `EnemySpawner`
once). `OnDestroy` = unregister from persistent registries.

**R9 — Scene content ownership.** `Persistent.unity` owns exactly one `EventSystem`, one
`AudioListener`, one `MainCamera`-tagged camera (with Cinemachine Brain), and all screen-space
HUD canvases. **Area scenes must contain none of those** — only geometry, spawn/QTE/tutorial
anchors, virtual cameras (priority-driven), per-scene NavMesh surfaces, lights, and world-space
canvases. World-space canvases in area scenes use `WorldSpaceCanvasCamera` to acquire the
Event Camera at runtime (this component already exists — it is the canonical pattern).

**R10 — Time law.** Setsuna sets `Time.timeScale = 0.15`; pause sets it to `0`. Any timer that
must keep real time during those windows uses `unscaledDeltaTime` / `Time.unscaledTime`, and the
choice (scaled vs unscaled) must be stated in a comment at the timer. `Time.timeScale = 1f` must
be restored on: game over, restart, scene-flow teardown, and Setsuna force-interrupt.
**Source review found SEVEN independent `Time.timeScale` writers** — `TutorialOverlayController`
(0↔1), `PauseMenuController` (0↔1), `GameOverController` (0, then 1 on restart),
`SetsunaSystem` (0.15↔1), `TeleportAbility` soul travel (0.85↔1), `SoftResetController` (=1),
and `SkillTreeUI` (0 on open ↔ 1 on close — verified). Any two of these
overlapping stomp each other (e.g. closing the skill tree mid-Setsuna restores 1, cancelling
the 0.15 slow). Once Phase 5.5's `TimeScaleService` lands, **direct `Time.timeScale` writes are banned**
— all writers go through `Request(owner, value)` / `Release(owner)`.

**R11 — Timeline law.** Three rules, born from the rescue-checkpoint bug and the lost camera
binding:
- **Bindings are scene-local.** A `PlayableDirector` may serialize track bindings only to
  objects in its own scene. Cross-scene targets (the Persistent Cinemachine Brain, the twins)
  are rebound at runtime by a `TimelineBindingResolver` component on the director GO, which
  calls `director.SetGenericBinding(track, target)` before `Play()` — Cinemachine Track → the
  Brain, twin tracks → `TwinSelector.Instance.LeftTwin/RightTwin`. **Single-instance tracks
  resolve by type; tracks needing disambiguation (Left vs Right twin) map to a role via an
  explicit Inspector `TrackAsset`→role reference — never a GameObject-name or track-name
  string** (name matching is fragile and was rejected by the user; a `TrackAsset` reference
  survives renames and is set once by dragging — see §16.1 for the exact component).
- **Cross-scene *actions* (fade, hide HUD, camera cue) use Signals, not bindings.** A
  `SignalEmitter` references a `SignalAsset` (a project asset — serializes across scenes),
  received by a **local** `SignalReceiver` on the director GO whose UnityEvent calls a **local
  relay** that forwards to the Persistent system at runtime (e.g. `FadeController.StartFromBlack`).
  A Signal Track's *own* binding is still scene-local, so the receiver/relay must be local —
  Signals do not cross scenes by themselves. Never hand-edit `.playable`/scene `m_SceneBindings`
  YAML. **Deleted targets cannot be auto-found** — those tracks are removed by hand. The live
  instance of all this is `TutorialTimelineDirector` (BUG-032 / §11): 11 of 42 bindings null
  after the single-scene→multiscene migration + level re-greybox.
- **Activation Tracks never control ancestors of gameplay-logic objects** (checkpoints,
  triggers, zones, spawn points, POIs). Visual-only hierarchies may be activation-driven;
  logic objects live outside them. Every Activation Track sets an **explicit** Post-playback
  state (Active or Inactive) — never Revert/Leave-As-Is for anything gameplay reads afterwards.
- **Completion detection:** poll `state != PlayState.Playing` or subscribe `director.stopped`
  *before* calling `Play()`, with Wrap Mode **None** (Hold keeps state at Playing forever).
  End-of-timeline Signal markers sit ≥ 0.1 s before the end — a load-hitch frame can jump the
  playhead past a marker on the final frame.

---

## 2. PHASE 0 — Entry paths (do this first; it unblocks all testing)

**0.1 — Editor auto-loader for Persistent.** Create `Editor-safe` runtime script
`PersistentSceneAutoLoader`:

```csharp
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

/// Loads Persistent.unity additively when entering Play Mode directly from an
/// area scene in the Editor, so all managers exist. No-op in builds and no-op
/// when Persistent is already loaded (Bootstrap path).
public static class PersistentSceneAutoLoader
{
    private const string PersistentScene = "Persistent";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsurePersistent()
    {
        if (SceneManager.GetSceneByName(PersistentScene).isLoaded) return;
        var active = SceneManager.GetActiveScene().name;
        if (active == "Bootstrap" || active == PersistentScene) return; // boot path handles it
        SceneManager.LoadScene(PersistentScene, LoadSceneMode.Additive);
        Debug.Log("[PersistentSceneAutoLoader] Loaded Persistent additively for editor play.");
    }
}
#endif
```

**0.2 — Idempotent boot.** `GameBootstrapper` must guard against double-loading Persistent
(check `isLoaded` before loading) and `SceneFlowManager` must tolerate the editor case where an
area scene is already open before it initializes (adopt it as the occupied area instead of
loading a duplicate).

**0.3 — Build settings.** Bootstrap = index 0. Persistent, Intro, L1_Park, L2_Streets all in
build list. `Restore.unity` / `Trees.unity` are temp and must **not** be in the build list.

**Definition of Done (P0):** Pressing Play in Bootstrap, in L1_Park, and in L2_Streets all reach
a playable state with zero `NullReferenceException`s in the console attributable to missing
Persistent managers (other known issues may remain).

---

## 3. PHASE 1 — Reference triage (fix every broken ref, by the correct rule)

For each row: verify which scene each end actually lives in *now*, then apply the listed fix.
Do not blanket-apply one fix style.

| # | Script (holder) | Broken field(s) | Holder scene | Target scene | Fix | Rule |
|---|------------------|-----------------|--------------|--------------|-----|------|
| 1 | `SkillTreeUI` (SkillTreeCanvas) | `_dataStoreMono`, `_purchaserMono`, `_pointBankMono` | Persistent | Persistent (`SkillTreeManager`) | **Re-wire in Inspector** (same scene). Add R4 `Start()` resolve as belt-and-braces. | R1+R4 |
| 2 | `AccordBarView` | `accordSystem`, `unlockStateMono` | Persistent | Persistent | Re-wire in Inspector; add R4 resolve. | R1+R4 |
| 3 | `SkillPointsHUDView` | `_pointBankMono` | Persistent | Persistent | Re-wire; add R4 resolve. | R1+R4 |
| 4 | `AbilitiesHUDController` | `accordSystem`, `empowerSystem`, `skillUnlockState` | Persistent | Persistent | Re-wire; add R4 resolve. | R1+R4 |
| 5 | `OverviewCamHUDView` | `overviewController` | Persistent | Persistent (`OverviewCamController`) | Re-wire; add R4 resolve. | R1+R4 |
| 6 | `KillParticleSpawnner` (ParticleSystemManager) | `deathNotifier` | **verify** | Persistent (`EnemyDeathNotifier`) | If holder is in an area scene: either move ParticleSystemManager into Persistent (preferred — it is a global system) and re-wire, **or** keep it per-area and R4-resolve. "Scene mismatch" means the two ends differ today — decide residency first, then wire. | R2→R1/R4 |
| 7 | `SharedHealthPresenter` | (has runtime fallbacks) | Persistent | Persistent | Replace its `FindAnyObjectByType` fallbacks with R4 singleton resolve for consistency; re-wire Inspector slots. | R4 |
| 8 | `TutorialStepContext` (TutorialManager, area scenes) | `overlay` ("Scene mismatch"), stale slots | Area | Persistent | **Clear the stale Inspector slots.** Verified `Resolve()`: overlay/hintDisplay already fall back to `Instance` ✓, but TwinSelector and the rescue provider fall back via `FindAnyObjectByType` — replace per R4: add a standard `public static TwinSelector Instance` and `public static RescueEventController Instance` (both are unique-by-design, Persistent-resident, currently **not** singletons — verified) and use `twinSelectorMono ??= TwinSelector.Instance`, `RescueProvider ??= RescueEventController.Instance`. Delete the `overlay` serialized field outright (it can only ever be cross-scene); LogError on any unresolved member. | R4 |
| 9 | `FailureResetSequencer` (TutorialManager, area) | `_postProcessVolume`, `_blackOverlay`, `_leftTwin`, `_rightTwin` | Area | Persistent | **Relocate the component itself to Persistent** (Phase 2) — all four deps become same-scene R1 serialized refs. Add `Instance`; context resolves it. | R1+R3→Phase 2 |
| 10 | `FailureNotice` (TutorialManager, area) | `_noticePanel`, `_noticeText` | Area | Persistent | **Relocate with its UI to Persistent** (Phase 2). Add `Instance`; context resolves it. Internals unchanged. | R1+R3→Phase 2 |
| 11 | `QTESceneAnchor` (ParkGateQTEAnchor) | Root Panel, Fill Bar, Timer Ring, labels | L1_Park | L1_Park (`QTEParkCanvasUI`) | Same scene — plain Inspector wiring, nothing clever. Confirm `WorldSpaceCanvasCamera` is on the canvas. | R1+R9 |
| 12 | `TutorialDirector` + `TutorialInputGate` | `inputGate` (input locks in `Awake` even when unwired); gate's reader lookups | Area | **Area — same scene** for gate↔director (verified: `TutorialInputGate` is *not* Persistent; it lives on TutorialManager and **push-registers** into the Persistent `TwinInputReader` via `SetGate(this)` / `SetGate(null)` — a correct R5 registration, keep it and document it as the canonical input example) | Director side: the gate ref is a plain **R1 serialized ref** — wire it; move the lock call from `Awake` to `Start`; if the slot is unwired, LogError and **do not lock** (fail open, never trap the player). Gate side (verified): it calls `FindAnyObjectByType<TwinInputReader>` **three times** (Awake fallback, OnEnable, OnDisable) — give `TwinInputReader` a standard `Instance` (unique, Persistent), resolve **once into a cached field in `Start`**, register there (guarded `_registered` flag; `Start` not `OnEnable` so editor direct-play ordering can't race Persistent), unregister in `OnDisable` via the cached ref; delete the dead `_realInputMono` cross-scene slot (the legacy `IInputProvider` passthrough keeps working off the cached ref). `TwinInputReader` treats a null gate as "all input allowed" (verified) — so the opening-cutscene lock only takes effect once the tutorial scene's gate has registered; sequence the intro accordingly. | R1+R4+R5+R8 |

**1.4 — Singleton base-class & DDOL audit.**

a) + b) **CANCELLED — Exemption E1 (§17).** The planned base-class correction (skip
   unparent+DDOL for Persistent residents; make `Instance` fabrication opt-in) was
   implemented once and **completely broke the enemy/perception system**; the user has locked
   the current `MonoBehaviourSingleton<T>` behaviour (unparent + DDOL + lazy fabrication) as
   deliberate. Do **not** reattempt — not in a refactor, not during the P19 restructure.
   Live with the documented consequences (CLAUDE.md footguns): watch for blank
   `Singleton<T>` ghosts in the Hierarchy; never rely on lazy fabrication for gameplay
   managers. One residue stays actionable because it is *project-side*, not the framework
   base: `LanguageManager.Awake`'s direct `DontDestroyOnLoad` call (verify removed — it
   lives in Persistent).

c) **`StandaloneSingleton<T>` + static state survive Restart in builds.** The editor clears
   `_Instance` on play-stop, but a build's `LoadScene(Bootstrap)` does **not** reload the
   domain — plain-C# singletons (and `ServiceLocator` registrations, `NameManager` interning,
   static events) carry stale references to destroyed objects across a Restart. Audit: find
   who calls the existing `OnBootstrapped()` hook (it exists on both bases — if nothing calls
   it, wire `GameBootstrapper` to invoke it / purge `ServiceLocator` on boot). Then test the
   restart loop: play → Game Over → Restart → play again, **twice in a row**; verify via the
   Hierarchy that no manager is duplicated and that a purchase fires UI updates exactly once.
   Canaries: `SkillTreeManager.OnPointsChanged` is an **instance** event (verified) — a
   double-fire there means a *duplicated manager*; `EnemyHealthComponent.OnAnyEnemyDied` is
   the **static**-event canary — a double-fire means a *stale static subscriber* survived the
   restart. Hand-rolled singletons must null their static `Instance` in `OnDestroy` — grep
   `public static .* Instance` for the full set; verified members so far: `SceneFlowManager`,
   `SoftResetController`, `QTEManager`, `POIManager`, `SpawnZoneRegistry`,
   `PoTWorldStateWriter`, `TutorialOverlayController`, `TimeFactorManager`, `TutorialContext`,
   `SkillPreviewModal`, `PauseMenuController`, `LanguageManager` (none DDOL except
   LanguageManager per (a) ✓, none null on destroy ✗). `TutorialHintDisplay` is worse: `Instance = this` with **no duplicate
   guard at all** — give it the standard pair.

**1.5 — Scene content sweep (R9).** Open L1_Park and L2_Streets; delete any `EventSystem`,
`AudioListener`, or `MainCamera`-tagged camera found there. Verify Persistent has exactly one of
each. Verify every world-space canvas in area scenes carries `WorldSpaceCanvasCamera`.

**Definition of Done (P1):** Both boot paths reach gameplay; skill tree opens and purchases;
accord bar fills; overview cam HUD updates; kill particles spawn; no "Scene mismatch" anywhere
in either area scene's Inspectors; restart loop run twice with no duplicates.

---

## 4. PHASE 2 — Tutorial failure UI: relocate, don't wrap (replaces both `TutorialHUDProvider` and the earlier facade plan)

The pending plan — a provider singleton handing raw UI references (`BlackOverlay`,
`NoticePanel`, `FailureText`) to area scripts — is rejected (it couples every consumer to
Persistent's canvas internals). An earlier draft of this document prescribed a `TutorialHUD`
behaviour facade instead; **source review supersedes that too.** `FailureResetSequencer` and
`FailureNotice` are already self-contained behaviour components: all timing is verified
`unscaledDeltaTime`/`WaitForSecondsRealtime` ✓, and *every* dependency they hold — the
post-process `Volume`, `BlackOverlay`, the notice panel/text, and the twins — is a Persistent
resident. A facade would also have left the sequencer's serialized `_leftTwin`/`_rightTwin`
refs cross-scene broken. The right move is residency, per R3's own principle:

1. **Move `FailureNotice`** (with its `NoticePanel`/`FailureText` children) onto
   `TutorialHUDCanvas` in Persistent. Add the standard `Instance` property
   (duplicate-destroy guard, null in `OnDestroy`).
2. **Move `FailureResetSequencer`** onto the same canvas (or a `TutorialFX` GO) in Persistent.
   Add `Instance`. Rewire all four serialized refs **same-scene (R1)**: `_postProcessVolume` →
   the Persistent global Volume, `_blackOverlay` → the canvas overlay Image, `_leftTwin`/
   `_rightTwin` → the Persistent twins. Internals stay untouched — they are already correct.
3. **`TutorialStepContext`:** delete the `resetSequencer` and `failureNotice` serialized slots
   (they can only ever be cross-scene now); `Resolve()` assigns
   `FailureResetSequencer.Instance` / `FailureNotice.Instance`, LogError on miss.
3b. **Re-point every caller — behavioural contract is UNCHANGED (verified against both
   boundary sources).** Exact changes:
   - `TutorialBoundary`: **delete** the `resetSequencer` and `failureNotice` serialized fields
     → resolve `FailureResetSequencer.Instance` / `FailureNotice.Instance` in `Start()`
     (LogError + disable on miss). **Delete** `leftTwin`/`rightTwin` fields → resolve
     `TwinSelector.Instance.LeftTwin/RightTwin` in `Start()` (these will be cross-scene the
     moment the duplicate L1_Park twins are deleted in the residency sweep). Its internal
     guard pattern is **correct** — `_resetting` cleared in the sequencer's `onComplete` —
     keep it; it is the model the outer boundary should copy.
   - `TutorialOuterBoundary`: same twin + `failureNotice` field deletions/resolves. Fix its
     sloppy guard: `DelegateResetNextFrame` clears `_resetting` immediately after *calling*
     `TriggerReset`, i.e. while the ~1.35 s sequence is still playing — either drop the outer
     guard entirely (the zone boundary's guard + the now re-entry-rejecting sequencer already
     cover it) or clear it in a completion callback. Its `WaitForSeconds(0.1f)` →
     `WaitForSecondsRealtime` (R10). Known cosmetic quirk to decide deliberately: the outer
     message ("Head to the next area") is overwritten 0.1 s later by the zone boundary's own
     message — either suppress the zone message on the delegated path or accept one message.
   - **Same method, same signature, same visuals, same teleport-to-reset-points behaviour** —
     only residency and resolution change. The out-of-bounds reset is part of the P2 DoD
     regression test below.
4. `TriggerReset` teleports twins *within* the current area, so no
   `SceneFlowManager.NotifyTeleported` call is needed; if a future reset point ever crosses an
   area boundary, the call becomes mandatory (3.7b).
5. **Optional, later:** the project now has three independent fade implementations
   (`SoftResetController.fadeImage`, the sequencer's overlay, `IntroController`'s fade). A
   single `ScreenFader` on the Persistent fade canvas could unify them — cleanup, not a
   blocker, and **do not** build it as part of this phase.

**Definition of Done (P2):** Boundary/checkpoint failure in L2_Streets shows notice + greyscale
+ fade + reset correctly; an overlay teaching step runs in L2_Streets; `TutorialManager`
Inspectors show no Missing refs; neither `TutorialHUDProvider` nor a `TutorialHUD` facade class
exists anywhere. (Teach-anywhere is the original product requirement — verify it explicitly by
triggering an overlay step and a failure reset in L2_Streets.)

---

## 5. PHASE 3 — Registry conversions & pooling residency (R5)

**3.1 — `EnemySpawner` zones (verified: registry conversion already done ✓).** `allZones[]`
is gone; zones self-register into a `SpawnZoneRegistry`, and the spawner subscribes
`OnZoneRegistered/Unregistered` with named handlers, symmetric in `OnEnable/OnDisable/OnDestroy`,
re-syncing already-registered zones on enable. Keep this. Two remaining items:

a) **Ordering hazard:** `EnemySpawner.OnEnable` starts with
   `if (SpawnZoneRegistry.Instance == null) return;` — a silent permanent opt-out. If the
   registry is a scene-placed component in Persistent, Awake/OnEnable order between two
   same-scene objects is **arbitrary**, so some boots will subscribe and some won't —
   intermittent "no enemies spawn" with zero errors. Fix by making the registry incapable of
   being null at that point: implement `SpawnZoneRegistry` as a **plain static class** (static
   events + static `List<SpawnZone>` — it owns no Unity state, so it shouldn't be a
   MonoBehaviour at all), or at minimum move the spawner's subscription to `Start` and
   LogError instead of silently returning. If it currently extends `MonoBehaviourSingleton`,
   the 1.4 fabrication fix applies to it too. (Source not yet reviewed — confirm against
   `SpawnZoneRegistry.cs`.) Note for the restart loop: a static registry must clear its list
   and events on Bootstrap entry (1.4c).
b) The barrier lookup is already lazy via `POIManager.GetNearest(POIType.Barrier)` — keep
   that pattern.

**3.2 — Pool residency.** `EnemyPool` and its spawned instances must be parented under a
Persistent-scene root. If pooled enemies are currently instantiated into the active/area scene,
unloading that scene **destroys pooled instances and corrupts the pool**. Verify
`Instantiate` parenting; fix to a `PoolRoot` transform in Persistent.

**3.3 — Despawn-on-unload (verified gap).** `HandleZoneUnregistered` unsubscribes the zone's
events and stops coroutines, but **does not despawn that zone's live enemies** — pooled
instances are parented under Persistent, so when their area unloads they survive, standing on
a deleted NavMesh (`"Failed to create agent because it is not close enough to the NavMesh"`
spam + ghost combat). `DespawnAll()` already exists and is thorough (death-notifier
unregister, time-factor unregister, bond clear, pool return via `_activePrefabMap`) — reuse
its body for a scoped version:
- Track origin per instance: add `Dictionary<GameObject, SpawnZone> _instanceZoneMap`
  alongside `_activePrefabMap` (populate in `SpawnEnemy`/`SummonerSpawn`, clear with the
  others in `DespawnAll`).
- Add `DespawnZone(SpawnZone zone)` — same per-instance cleanup as `DespawnAll`, filtered to
  that zone's instances, decrementing the side/type counters it touches.
- Primary signal: subscribe `SceneFlowManager.OnLocationWillUnload` (both Persistent, R1) and
  despawn every registered zone whose `gameObject.scene` matches the unloading location's
  scene, **before** the unload proceeds. `HandleZoneUnregistered` calls `DespawnZone` too as
  the belt-and-braces fallback.
Test: stand in L1, walk into L2 until L1 unloads, return — no console errors, enemies respawn
fresh, pool count stable.

**3.4 — POIManager residency.** `POIManager` is consumed from Persistent systems
(`AbilityController`, `EnemySpawner`) but POIs (`BarrierPOI`, `SpawnPointPOI`, `RitualSitePOI`)
are area objects. Confirm POIs self-register/unregister (R5) and that `POIManager` purges nulls;
`GetNearest` must return null-safe results when an area just unloaded — callers already treat
"no barrier" as a soft condition; verify they do.

**3.5 — Entrances & flow.** `LocationEntrance` already registers by name — confirm it follows
R5 (register `OnEnable`, unregister `OnDisable`) and that `SceneFlowManager`/`SoftResetController`
only query entrances **after** `SceneManager.sceneLoaded` has fired for the target scene plus
one frame (so area `Start()`s have run). If placement currently happens inside the load
callback synchronously, defer by one frame.

**3.6 — Cinemachine across scenes (verified).** `CameraManager` serializes the gameplay cams
**and the tutorial cam pair** — all of these are player-follow cams and must therefore live in
Persistent with the manager (R1); confirm the tutorial pair's residency during the Phase-1
sweep. QTE/cutscene cams stay area-side and are **never serialized by Persistent code**: the
`QTESceneAnchor` passes its cam as a parameter to `SwitchToCamera()` at runtime, which the
manager handles via the external-cam priority path (30) and demotes afterwards — this
parameter-passing pattern is correct and is the blessed alternative to a registry; no
`CameraRegistry` is needed. Timeline-driven cinematic cams bind through the
`TimelineBindingResolver` (R11), not through `CameraManager`.

**3.7 — `SceneFlowManager` required changes (verified against source).** The Graves-pattern
core (occupant counts → desired set = occupied ∪ adjacents, hysteresis, in-progress guards,
re-check after `unloadDelay`) is sound. Five concrete changes:

a) **Occupancy: int counts → one current location per actor (transition model).** Verified
   trigger semantics make this mandatory, not stylistic: `SceneLoadTrigger`s are strips placed
   *fully inside* the target area, so `OnTriggerExit` fires when a twin walks **deeper into**
   the area (leaving the strip) — under the count model that immediately zeroes the occupancy
   of the area the twin is standing in. The system currently survives only because
   `LoadStartLocation` hardcodes a count of `1` that can never be released (its own bug: the
   start area can never unload). Replace both with:
   `Dictionary<Player, WorldLocationSO> _currentLocation` — each tracked actor (both twins +
   `SoulPlayer` while travelling) is in exactly one location. `NotifyTwinEntered(location,
   actor)` **assigns** `_currentLocation[actor] = location` (a transition — the previous
   value is implicitly vacated). Desired set = all current locations ∪ their adjacents.
   **Exits are ignored entirely** — delete the `OnTriggerExit` notify in `SceneLoadTrigger`
   and `NotifyTwinExited` in the manager. This is idempotent, immune to missed exits, and a
   stale value can only ever keep an extra area loaded (safe), never unload occupied ground
   (unsafe). `SceneLoadTrigger.OnTriggerEnter` resolves the actor via
   `other.GetComponentInParent<Player>()` and passes it (keep the layer check as the cheap
   pre-filter; keep `comesFrom` — it drives `LocationEntrance` resolution, not occupancy).
   Delete `LoadStartLocation`; boot seeding goes through (b).
b) **Add `NotifyTeleported(Player actor, WorldLocationSO destination)`** — identical to a
   trigger transition: assign `_currentLocation[actor] = destination`, recalculate. Triggers
   never see teleports. Call sites: boot seeding (`GameBootstrapper` dev mode +
   `IntroController` / `IntroTimelinePositioner` for **both** twins), `SoftResetController`
   (Phase 7.5), Weaver's Gate soul travel, debug warps.
c) **Add `event Action<WorldLocationSO> OnLocationWillUnload`**, raised inside
   `UnloadLocationAsync` *after* the occupancy/desired-set re-checks pass and *before*
   `UnloadSceneAsync` — the deterministic despawn/cancel signal for 3.3 (`EnemySpawner`,
   `QTEManager`).
d) **Set the active scene.** The manager never calls `SceneManager.SetActiveScene` — only the
   bootstrapper does, once. After streaming, render settings (skybox/fog/ambient) stay bound
   to the boot area forever. On occupancy change, set the active scene to the location
   containing the **selected** twin (fallback: any occupied location whose scene is loaded);
   re-assert after a pending load of that scene completes.
e) **R10:** `UnloadLocationAsync`'s `WaitForSeconds(unloadDelay)` is scaled time — during
   Setsuna (0.15×) the 0.5 s grace becomes ~3.3 s and during pause it never elapses. Use
   `WaitForSecondsRealtime` and comment the decision.

**3.8 — `TimeFactorBootstrapper` unload purge (verified gap).** It scans on `sceneLoaded` and
registers scene-placed `ITimeAffected` from each new chunk — the correct streaming template —
but subscribes **no `sceneUnloaded`**, so destroyed scene-placed entries accumulate in
`TimeFactorManager.affectedEntities`; the next `TriggerEffect()` then calls `OnEffectStarted()`
on destroyed objects (`MissingReferenceException`). Pooled enemies are safe (the spawner's
despawn unregisters them — verified), so the fix is small: subscribe
`SceneManager.sceneUnloaded` and either unregister that scene's entries (track them per scene
at registration) or have `TimeFactorManager` null-purge its list before iterating (the R5
null-purge rule). Do both for defence in depth.

**Definition of Done (P3):** Walk L1↔L2 repeatedly: streaming loads/unloads with no NREs, no
pool errors, no orphaned enemies, QTE camera still frames correctly, rescue + abilities work on
both sides of a boundary crossing. Park twins on opposite sides of the boundary (inside bond
range): **both** scenes stay loaded. Checkpoint-teleport across the boundary: destination loads
via `NotifyTeleported` without either twin touching a trigger.

---

## 6. PHASE 4 — Skill-tree runtime state off the SO (R7)

`AbilityUpgradeData.currentNodeIndex` mutating the asset is a standing footgun (dirty assets in
editor, inconsistent reset semantics in builds, checkpoint coupling). Extract it:

1. New plain class `SkillTreeRuntimeState`: `int Points`, `Dictionary<AbilityUpgradeData,int> Levels`,
   `int GetLevel(AbilityUpgradeData)`, `void SetLevel(...)`, `Snapshot()/Restore(Snapshot)`.
   Owned by `SkillTreeManager` (Persistent). No `ScriptableObject` anywhere in it.
2. `SkillTreeManager` API stays identical to callers (`IPointBank`, `ISkillUnlockState`,
   `IAbilityDataStore`, `ISkillTreePurchaser` unchanged) — internally it reads/writes the
   runtime state instead of the SO field.
3. `AbilityUpgradeData` keeps **definitions only** (nodes, costs, labels, prerequisites). Mark
   `currentNodeIndex` `[Obsolete]`, route its getter to
   `SkillTreeManager.Instance.GetLevel(this)` during migration, then delete it once all call
   sites are migrated. Grep for every reader (`SkillNodeButton`, `SkillTreeUI`,
   `SkillPreviewModal`, abilities reading their upgrade data, `CheckPointManager` snapshot,
   `SoftResetController`) and migrate each explicitly — list them in the changelog entry.
4. Checkpoint/soft-reset snapshots serialize `SkillTreeRuntimeState.Snapshot()` instead of
   reading SO fields.
5. Delete the `ResetToBase()`-on-Awake band-aid once nothing mutates SOs.

**Definition of Done (P4):** Enter Play, buy nodes, exit Play — `git status` shows **no modified
.asset files**. Checkpoint save → soft reset restores points and levels exactly. Debug keys
L/O/P/I/K still function (they operate on the runtime state now).

---

## 7. PHASE 5 — Lifecycle & time audit (R8, R10)

**5.1 — Awake/Start sweep.** Grep all `Awake()` bodies under `Scripts/` for any of:
`.Instance`, `FindAnyObjectByType`, `FindObjectsByType`, `GetComponentInParent` reaching outside
the prefab, or cross-system event subscription to a not-yet-guaranteed target. Move each
violation to `Start()` per R4/R8. Known offender already triaged: `TutorialDirector` (row 12).

**5.2 — Subscription hygiene.** Grep for `+=` with lambda (`=> {`, `() =>`) on events whose
publisher outlives the subscriber (static events, Persistent manager events). Convert to named
handlers with matching `-=` in `OnDisable`/`OnDestroy`. The `SkillTreeUI.OnEnable`
`OnNodePurchased += _ => RefreshButtons();` line is a known instance of the pattern that
previously leaked in `EnemySpawner` — fix it and any siblings.

**5.3 — Unscaled-time audit (R10).** For each timer below, decide scaled vs unscaled, implement,
and leave a one-line comment stating the decision. Defaults given:

| Timer | During Setsuna (0.15×) | During Pause (0×) | Verdict |
|---|---|---|---|
| Rescue TTK & mash window | should slow with world (Setsuna is a power) | must halt | **scaled** ✔ (already) |
| Failure/notice/overlay visuals (sequencer, notice, overlay pop) | must run | overlay itself pauses time | **unscaled** ✔ (verified — all three already are) |
| QTE approach/mash timers (`QTEManager.Update`) | enemies frozen; player acts real-time | must halt | currently `Time.deltaTime` — **convert to unscaled**, gate on pause flag |
| `SoftResetController` sequence | must run | must run | **unscaled** ✔ (verified) |
| `TutorialDirector.RunSequence` 0.3 s lead-in | must run | must run | currently `WaitForSeconds` — **convert to Realtime** |
| Cooldown ticks & ability durations | slow with world | halt | **scaled** ✔ |
| `IntroController` / Timeline waits | n/a | n/a | Timeline `DirectorUpdateMode` = GameTime unless cutscene must ignore pause |
| `SceneFlowManager` load/unload logic | must run | must run | frame-driven/async — verify no `WaitForSeconds` (scaled) inside |

**5.4 — timeScale restoration.** Ensure `Time.timeScale = 1f` is forcibly set in:
`GameBootstrapper.Awake`, `GameOverController` before loading Bootstrap, `SetsunaSystem` on any
force-interrupt path (game over mid-Setsuna is the test case), and `PauseMenuController` resume.

**5.5 — `TimeScaleService` (mandated by R10's seven-writer finding).** A small Persistent
component ending the stomping wars:

```csharp
public class TimeScaleService : MonoBehaviour   // Persistent, Instance pattern
{
    // Min-value-wins: pause(0) beats overlay(0) beats Setsuna(0.15)
    // beats rescue-tutorial(0.25) beats soul-travel(0.85) beats default 1.
    public void Request(object owner, float scale);   // add/update owner's request
    public void Release(object owner);                // remove it; recompute
    // Applies: Time.timeScale = requests.Count == 0 ? 1f : requests.Values.Min();
}
```

Min-value-wins is correct for every verified pair: pausing during Setsuna holds 0 then returns
to 0.15, not 1; closing the overlay during the rescue-tutorial slow returns to 0.25, not 1;
ending soul travel during Setsuna returns to 0.15. Migrate the seven writers one at a time
(each is a two-line change: `Request` where they wrote a value, `Release` where they wrote
`1f`), identify the 0.25 rescue-tutorial writer during this pass, then add an editor-only
assert that nothing else writes `Time.timeScale` directly. `Release(owner)` for destroyed
owners must happen in their `OnDisable/OnDestroy` — and `TimeScaleService` null-purges dead
owners defensively.

Verified migration table (every call site source-confirmed except the 0.25):

| Writer | Today | Becomes |
|---|---|---|
| `PauseMenuController` | `OpenPause`→0, `Resume`/`ExitGame`→1 | `Request(this, 0)` / `Release(this)` |
| `TutorialOverlayController` | `Show`→0, `OnContinueClicked`→1 | `Request(this, 0)` / `Release(this)` |
| `GameOverController` | `TriggerGameOver`→0, `RestartScene`→1 | `Request(this, 0)` / `Release(this)` before Bootstrap load |
| `SetsunaSystem` | `Activate`→0.15, `BeginRewind`/`ForceEnd`→1 | `Request(this, 0.15)` / `Release(this)` |
| `TeleportAbility` | soul-arrival→0.85, `End`→1 | `Request(this, 0.85)` / `Release(this)` |
| `SoftResetController` | sequence start forces 1 | `ReleaseAll()` (reset clears every request — it's the one legitimate "force normal") |
| `SkillTreeUI` | open→0, close→1 (Tab/ESC toggle, verified) | `Request(this, 0)` / `Release(this)` |
| ~~rescue-tutorial 0.25~~ | **Resolved — not a writer.** `TutorialContext.rescueTimerScale` is *dead data*: grep across the codebase finds no consumer. The tutorial's forgiving rescue comes from `TutorialTrap.timeToKill = 35 s`, not slow-mo. Delete the field — or, if the designer wants it, implement it as a **local TTK multiplier on the trap**, never a global write |
| ~~`AccordStateSystem`~~ | **Resolved — verified clean.** Contains no `Time.timeScale` write; WIRING_GUIDE §9 conflated it with Setsuna |

**5.6 — Single ESC arbiter (verified — THREE independent consumers).**
`TutorialOverlayController.Update`, `SkillTreeUI.Update`, and `PauseMenuController.Update` all
read `Input.GetKeyDown(KeyCode.Escape)` in the **same frame**. `PauseMenuController`'s comment
claims "SkillTreeUI.Update runs before this and handles its own ESC" — that is execution-order
fiction: `GetKeyDown` is true for *every* reader in the frame regardless of order, so ESC on an
open skill tree closes it (`timeScale=1`) **and** opens pause (`timeScale=0`) simultaneously;
same for an open overlay. Fix — one owner of the chain, `PauseMenuController.Update`, in this
order, each step `return`ing:
1. `SkillPreviewModal.Instance.IsOpen` → modal closes itself (already in chain) ✓
2. `TutorialOverlayController.Instance.IsOpen` → overlay handles it (expose `IsOpen`)
3. `SkillTreeUI.IsOpen` → skill tree handles it (expose
   `public bool IsOpen => SkillTreePanel.activeSelf` and give the pause script a serialized
   same-scene ref — both live in Persistent, R1)
4. settings open → close settings; 5. pause open → resume; 6. else open pause.
`SkillTreeUI` and the overlay keep their own ESC handling but **only act when they are the
topmost open layer** (skill tree additionally checks the modal, as it already does).
While in `SkillTreeUI` (verified), fix its **subscribe-before-assign bug**: `OnEnable`
subscribes `_pointBank.OnPointsChanged`, but `_pointBank` is assigned in `Start` — the first
`OnEnable` runs before `Start`, so the subscription silently never happens and the points
label goes stale after purchases. Move the three interface casts from `Start` to `Awake`;
keep subscribe/unsubscribe in `OnEnable`/`OnDisable`. Long-term (backlog): with four ESC
consumers, a single `EscapeRouter` owning the key with an `IEscapeHandler` stack is now
justified — optional, the chain above is sufficient.

**Definition of Done (P5):** Trigger game over while Setsuna active → restart → time flows
normally. Open pause during a tutorial fade → fade completes after resume without desync.

---

## 8. PHASE 6 — Deletions & decommissioning

Remove in this order, compiling between steps:

1. `AreaManager` component instance still in **L1Park** scene (legacy streaming) — delete the
   GameObject usage; then delete `AreaManager.cs` + `AreaNode.cs` source (superseded by
   `SceneFlowManager`/`WorldLocationSO`). Grep for references first.
2. `CheckpointLoader.cs` — already `[Obsolete]`; once `SoftResetController` passes P3/P4 tests,
   delete the file and the `_pendingLoader` plumbing in `CheckPointManager`.
3. `Restore.unity` and `Trees.unity` — complete the drag-ins into L1_Park (verify static flags,
   occlusion data, and lightmap indices on the restored `CommonStatic*` hierarchies), then
   delete both scenes and their build-settings absence is confirmed.
4. Stale Inspector slots cleared in Phase 1 — confirm none were left half-wired.
5. `TutorialHUDProvider` — must not exist (Phase 2 supersedes it). If a stub was created, delete.
6. Leave broader dead-code removal (`TwinManager`, `OldFactionComponent`, `EnemyStateMachine`,
   misspelled-folder renames, debug keys) **out of scope** for this work order — they are
   tracked in game.md §20 and are not blockers. Do not bundle them into these diffs.

---

## 9. PHASE 7 — Finish the outstanding wiring (from WIRING_GUIDE.md)

1. Create the `WorldLocationSO` assets for Park and Streets (scene ref, adjacency:
   Park↔Streets per current design; entrance IDs matching `LocationEntrance` names placed in
   each scene). Wire into `SceneFlowManager`.
2. Wire `GameBootstrapper` (mode flag, scene refs) and `IntroController` Inspector refs.
3. Attach `IntroTimelinePositioner` to the TutorialTimelineDirector GO and wire its target
   transforms — this is the open "post-cinematic twin reposition" item. Verify the intro →
   gameplay handoff places both twins and the camera correctly and that input unlocks only when
   the tutorial's first step says so (row 12 fix).
4. `QTEManager` Persistent UI refs + `QTE_ParkGate` definition wired; ParkGate anchor per
   triage row 11; run the gate QTE end-to-end including cancel (hold X) and retry paths, and
   confirm `EnemyFreezeService` freezes enemies across **all loaded scenes**.
5. **SoftReset completion (verified against source).** `SoftResetController` already does the
   skeleton correctly: `timeScale = 1` first ✓, unscaled fade ✓, `EnemySpawner.DespawnAll()` ✓,
   `RescueEventController.ForceReset()` ✓ (verified — it unfreezes the grabbed twin, releases
   the emergency selection override, unlocks selection, cleans up). Close these gaps:
   - **Skill snapshot covers only 7 of the 9 trees — on BOTH sides (verified).**
     `SoftResetController.RestoreNodeLevels` *and* `CheckpointManager.CaptureNodeLevels`
     hand-list Stun, Possess, Gate, HealthRegen, AccordSpirits, Coalesce, SoulConv —
     **Empower and Accord State are never saved nor restored.** `SkillTreeManager` already
     has the authoritative ordered 9-tree enumeration as private `AllData()` — expose it
     (`public IReadOnlyList<AbilityUpgradeData> AllTrees`) and make both snapshot and restore
     iterate it. One source, identical order, future trees included automatically.
   - **Streaming-blind teleport.** `ApplyTwinState` sets raw positions; if the checkpoint is
     in an area that streamed out, the twins teleport into a void. Save side (verified):
     `CheckpointManager.SaveCheckpoint(leftPos, rightPos)` stores positions only — extend it
     to `SaveCheckpoint(leftPos, rightPos, WorldLocationSO location)`; each `CheckPointTrigger`
     serializes its area's `WorldLocationSO` (an asset ref — always scene-safe, R2 exempt) and
     passes it. `CheckpointData` gains `checkpointLocation`. Restore sequence becomes: ensure
     loaded (`SceneFlowManager` load + wait for `OnLocationLoaded` + one frame) → teleport →
     `NotifyTeleported` for **both** twins (3.7b). `CheckpointTrigger` itself (verified)
     resolves its manager and `TwinSelector` via `FindAnyObjectByType` in `Start` — replace
     per R4: add the standard `Instance` to `CheckpointManager` (it has none today) and use it
     plus `TwinSelector.Instance`; then add `[SerializeField] WorldLocationSO location` (asset
     ref, always scene-safe) and pass it through `SaveCheckpoint`.
   - **Force-end active power states** before teleporting — all four verified against source:
     `SetsunaSystem.ForceEnd()` ✓ exists and is correct. `SoulConvergenceSystem.ForceDeactivate()`
     ✓ exists and is clean (resets damage multipliers including
     `SharedHealthPool.IncomingDamageMultiplier`, destroys shields, hides UI). Note it
     deliberately does **not** clear `_soulCount`/`_charged` — collected souls persist through
     a checkpoint load; keep that unless design says otherwise, and record the decision.
     `AccordStateSystem.DeactivateAccord()` is **private** (line 419) — add
     `public void ForceDeactivate() { if (_isActive) DeactivateAccord(); }`.
     `EmpowerSystem.EndAbility()` is **private** (line 316; body verified clean — unlocks
     movement/abilities/selection, resets multipliers, stops the pulse coroutine, unsubscribes
     its named death handlers) — add `public void ForceEnd() => EndAbility();`. Never reach
     into private state from the reset code. Dying mid-Setsuna then loading a checkpoint is
     the test case.
   - **Trap reset (verified against `SkeletonTrap`).** The production trap is a state machine
     Dormant→Arming→Active→Dragging→Released/Killed with an existing `RearmRoutine`. Add
     `public void ForceReset()`: stop the rearm coroutine if running; if a player is grabbed,
     release them (unfreeze movement + `SetGrabbed(false)` — mirror `TutorialTrap.ResetState`,
     which is the verified model); then `TransitionTo(TrapState.Dormant)`. Subscribe
     `SoftResetController.OnSoftReset` with a named handler (R4 resolve in `Start`, R8 pair).
     **Streaming bug found while verifying:** `RescueEventController.Start()` registers traps
     via a one-time `FindObjectsByType<SkeletonTrap>` scan — **traps in streamed-in scenes
     never register.** Convert to self-registration: SkeletonTrap
     `OnEnable → RescueEventController.Instance?.RegisterTrap(this)`,
     `OnDisable → UnregisterTrap(this)` (`UnregisterTrap` exists — verified), and delete the
     scan. (`RescueEventController.Instance` comes from triage row 8.)
   - **Wire the Inspector refs and delete `AutoFindRefs()`.** Every dependency
     (`EnemySpawner`, `SharedHealthPool`, `SkillTreeManager`, `RescueEventController`) is
     Persistent-resident — same scene as this controller — so they are plain R1 serialized
     refs; the `FindAnyObjectByType` fallbacks violate the Law for no benefit.
   - Twin identification by proximity-to-checkpoint (`ApplyTwinState`) is fragile when both
     twins are far away — replace with direct serialized `leftTwin`/`rightTwin` refs (R1,
     twins are Persistent).
   - Its private fade can stay for now; the optional `ScreenFader` unification (Phase 2 item 5)
     would absorb it later — not a blocker.
   - **Abort any active QTE:** call `QTEManager.Instance.AbortQTE()` early in the sequence
     (the API exists and was designed for this — verified — but nothing calls it yet).
   - **Clear `SpawnZone` occupancy:** zones track players via trigger enter/exit
     (`_playersInZone` HashSet, verified) — a teleport never fires the exit, so the old zone
     stays "occupied" and spawning logic goes stale. Add `SpawnZone.ClearOccupants()` and have
     zones subscribe `SoftResetController.OnSoftReset` (or let the spawner broadcast it);
     colliders re-overlapping after the teleport re-fire enter naturally.
   Test: die mid-rescue with Accord active in a streamed-out checkpoint area → Load
   Checkpoint → clean state, both areas correct, Empower/Accord-State upgrades intact,
   console clean.

6. **Tutorial & Timeline robustness (fixes the intermittent "rescue checkpoint never
   activates" bug — root cause diagnosed).** `TutorialCheckpoint.Activate()` calls
   `SetActive(true)` on itself, which is a **no-op when any ancestor is inactive** — and the
   tutorial timeline drives a stack of Activation Tracks over scene groups (MainLvl,
   TimelineTutorial, the planegrow set, Lyra/Kai). Whether the checkpoint's ancestor chain is
   left active after the timeline depends on each track's Post-playback state and on exactly
   where evaluation stops (natural end vs skip vs a load-hitch frame jumping the playhead) —
   hence "sometimes." The existing `[Activate]` debug log printing `parent active=` confirms
   the suspicion was already on this; one repro run showing `parent active=False` is the
   definitive stamp. Fix set, in order:
   a) Move the checkpoints group (and every gameplay-logic GO) **out of any hierarchy an
      Activation Track touches**; set explicit Post-playback states on all Activation Tracks
      (R11). This alone ends the bug.
   b) Make `Activate()` fail loud: after `SetActive(true)`, if `!gameObject.activeInHierarchy`,
      walk up the transform chain, find the inactive ancestor, and `Debug.LogError` naming it.
      Silent no-op → pinpointing log, forever.
   c) **Step-SO subscription hygiene (R8):** `TutorialCheckpointStepSO` and
      `TutorialRescueWatchStepSO` subscribe lambdas to checkpoint/rescue events and never
      unsubscribe — re-running the tutorial (soft reset, restart) stacks dead handlers.
      Convert to named local handlers, unsubscribe in `finally` around the `WaitUntil`.
   d) `TutorialDirector.RunSequence`'s `WaitForSeconds(0.3f)` → `WaitForSecondsRealtime` (5.3).
   e) Add `TimelineBindingResolver` (R11) to TutorialTimelineDirector and the intro director —
      this restores the lost Cinemachine Brain binding and future-proofs the Lyra/Kai
      Activation bindings, which currently point at the **L1_Park duplicate twins** and will
      break the moment Phase-1 residency cleanup deletes those duplicates.
      **Full diagnosis (BUG-032), recovered by diffing the pre-multiscene `Assets/Scenes/L1Park.unity`
      (no underscore, still in git at HEAD) against the live `L1_Park/L1_Park.unity`:** the
      cutscene was authored single-scene before the split *and* before the re-greybox, so 11 of
      42 track bindings are null — **4 moved to Persistent** (Cinemachine→`CinemachineBrain`/Main
      Camera, Signal→`CameraManager` receiver, Activation 8 + Animation 7→`FadeCanvas`/`FadeController`,
      Activation 22→`HUD_Canvas`), **1 to Persistent UI** (Activation 20/21→`AbilityFeedbackDisplay`
      nameplate), **4 deleted by the re-greybox** (Activation 1/2→`GroupTransposeClose/Top`,
      Activation 10/11→`MainLvl (1)/(2)` — unrecoverable). The new scene already has
      `TimelineDollyCam/1/2` vcams. So the resolver handles the camera Brain by type;
      fade/HUD become Signals→local-relay (FadeController is built for it — its docstring says
      "call from a Timeline SignalReceiver"); the dead tracks are removed by hand. The director
      has `m_InitialState: 0` (no play-on-awake), so the resolver's `Start()`-time rebind lands
      before the tutorial flow calls `Play()`. The user edits the Timeline themselves — do not
      hand-edit the `.playable`/scene binding YAML.
   f) Wiring slip found in the screenshots: context checkpoint entry 12 "Rescue point B" is
      wired to `CheckpointsRescueL` — same as entry 11. Single-mode RescueCheckpoint only
      reads index 11 so it isn't today's bug, but fix it before anyone enables Dual mode.
   g) Small NRE guard: `TutorialOverlayController.OnContinueClicked` dereferences
      `_videoPlayer.prepareCompleted` without a null check one line after null-guarding it.
   h) **Deterministic wrong-twin reset (the simultaneous-entry bug, fixed structurally).**
      History: when both twins entered the wrong checkpoints in the same frame, both wrong-twin
      handlers ran and the twins landed on swapped/incorrect reset points. The current source
      already carries a *patch* — Dual mode uses fixed identity-based positions plus a
      `resetting` guard — but three holes remain, so the bug class is contained, not closed:
      `RunSingle` has **no guard at all** and still uses position-*swapping*
      (`GetSwappedResets`); `FailureResetSequencer.TriggerReset` **restarts** on re-entry
      (`StopCoroutine` + new coroutine — the first call's `onComplete` then never fires, so
      `FullReset()` never runs and the step can soft-lock with saturation stuck at −100); and
      the checkpoints stay **armed during the teleport**, so a twin materialising inside a
      trigger can fire events mid-sequence. Required changes — implement exactly this, no
      shortcuts:
      - Extract `WrongTwinResetHandler` (plain C# class, instantiated by the step coroutine;
        not a MonoBehaviour, not an SO field): ctor `(TutorialStepContext ctx,
        TutorialCheckpoint cpA, TutorialCheckpoint cpB /*null in Single*/, Func<string>
        failureMessage)`; one public method `HandleWrongTwin(TutorialCheckpoint source,
        Player wrong)`; **both modes use it** — delete the four duplicated lambda bodies and
        `GetSwappedResets` entirely.
      - **Positions resolve by twin identity, never by which checkpoint fired:** left twin →
        the step's designated left reset, right twin → the designated right reset (Single:
        both from the one checkpoint; Dual: cpA-left / cpB-right, as the current Dual patch
        already does). Identity comes from `TwinSelector.Instance.LeftTwin/RightTwin`, not
        from entry order.
      - `_resetting` guard lives in the handler and applies to **both** modes; set before any
        side effect.
      - Add `Suspend()` / `Resume()` to `TutorialCheckpoint` — toggles **only the trigger
        `Collider`** (not the GO, preserving marker/particle state). Handler suspends every
        checkpoint in the step before calling `TriggerReset`; the `onComplete` callback runs
        `FullReset()` on each (which resumes the collider) and only then clears `_resetting`.
      - `FailureResetSequencer.TriggerReset` becomes **re-entry-rejecting, not restarting**:
        `if (_activeReset != null) { Debug.LogWarning("[FailureResetSequencer] reset already
        running — ignored", this); return; }` — callers are guarded; this is defence in depth.
      - Subscription hygiene per (c) applies to the handler's subscriptions.
      Acceptance: Dual mode, warp both twins into the *wrong* checkpoints on the same frame
      (debug warp ×2) → exactly **one** reset sequence plays, Lyra lands on the left reset
      point, Kai on the right, both checkpoints re-arm, and the test repeats ×3 with no
      stacked-handler warnings. Then retire the physical-spacing workaround (the offset
      checkpoint can return to its designed position) and note it in the changelog.
   i) **`TutorialTrap` (verified):** `OnEnable` registers with the rescue trap registry but
      there is **no `OnDisable` unregister** — and the success path calls
      `gameObject.SetActive(false)`, so every rescued tutorial trap stays in
      `RescueEventController`'s registry as a disabled ghost. Add
      `OnDisable => _registry?.UnregisterTrap(this)` (`UnregisterTrap` exists on
      `RescueEventController` — verified; ensure `IRescueTrapRegistry` exposes it). Its serialized
      `leftTwin`/`rightTwin`/`rescueControllerMono` follow the same resolves as 3b
      (`TwinSelector.Instance`, `RescueEventController.Instance`). Its TTK tick is scaled —
      correct per 5.3 — but the tutorial re-arm `WaitForSeconds(resetDelay)` is scaled too:
      under the rescue-tutorial 0.25 slow, the 2 s re-arm becomes 8 s. Convert to
      `WaitForSecondsRealtime` (re-arm is pacing, not gameplay).
   j) **`SetsunaSystem` hardening (verified):** its `ForceEnd`/`OnDisable` teardown and
      restore-to-1 are already correct ✓ (5.4 satisfied). Three required fixes:
      - **Raw input bypass:** `HandleIdle`/`HandleCharging` read `Input.GetKey(KeyCode.F)`
        directly, bypassing `IInputProvider` and therefore the tutorial gate (DIP violation +
        gate hole) — and `SoulConvergenceSystem.HandleInput` has the identical bypass
        (verified, raw `Input.GetKey(_activateKey)`). Add **one** `GetConvergenceHeld()` to
        `IInputProvider`, implement in `TwinInputReader` (gated by `AbilityAllowed`) and the
        gate's passthrough, and use it in **both** systems — delete all three raw reads.
        `AccordStateSystem` also reads raw `Input.GetKey(KeyCode.X)` twice (lines 277/303) —
        X is deliberately ungated (cancel must always work) so it's not a gate hole, but route
        it through the existing `_input.GetCancelHeld()` anyway (DIP, and one place to change
        when input migrates).
      - **The rewind is not actually invulnerable:** the summary and flow comments promise
        invulnerability, but `SetInvulnerable()` only locks movement. An enemy hit during the
        1.5 s rewind can empty the shared pool and fire game-over mid-coroutine before the
        snapshot health is restored. Fix: also call `Health.SetInvincible(true/false)` on both
        twins inside `SetInvulnerable` (the API exists — `TeleportAbility` already uses it),
        and rename the method to match what it now truly does.
      - Delete the unused `EaseInOutCubic` (dead code). Its `Time.timeScale` writes migrate in
        5.5 like every other writer.

7. **Rewrite `WIRING_GUIDE.md` against current source — it is dangerously stale.** Verified
   wrong: §1 scene names/fields predate the rename and the `SceneReference` type; §2 + §7
   prescribe a Persistent screen-space QTE canvas wired to QTEManager fields **that no longer
   exist** (the code pulls world-space UI from each `QTESceneAnchor` — the anchor pattern is
   correct, the guide is not); §3's `IntroTimelinePositioner` fields (`leftTwinStart`/
   `rightTwinStart`) don't exist (it finds `AreaSpawnPoints` at runtime); §3's
   `LocationEntrance.location` field is actually `comesFrom` with different semantics; §8
   still claims `CheckpointLoader` persists via DDOL (obsolete). Regenerate the guide from the
   final state after P0–P7 so the next person wiring a scene isn't following fiction.

---

## 10. FULL VERIFICATION PROTOCOL (run after P7, log results in changelog)

Entry paths: (a) Bootstrap → Intro → gameplay; (b) Bootstrap dev-mode straight to L1_Park;
(c) editor Play directly in L1_Park; (d) editor Play directly in L2_Streets.

Smoke list, each on paths (a) and (c) minimum:
1. Move/switch/attack; Stun + Possess land; cooldown HUD ticks.
2. Skill orb pickup → Tab → purchase across all three tabs → close → buffs apply.
3. Accord: fill bar, activate, Q variants swap, Accord Melee, Spirits, deactivate restores Q.
4. Soul Convergence charge + fire; Setsuna inside Accord, including walking across a scene
   boundary **during** Setsuna (streaming must not stall on scaled time) and rewind landing.
5. Trap grab → Gate → mash rescue success; Siphon ghost bind/break; self-rescue mash.
6. Tutorial in L1: overlay step, hint, checkpoint, boundary reset (notice + greyscale + fade
   via the relocated Persistent `FailureResetSequencer`/`FailureNotice`).
7. Same tutorial elements exercised in L2_Streets (the "teach anywhere" requirement).
8. Gate QTE: lock-in, cancel, fail/retry, success → gate stays open after walking to L2 and back.
9. Streaming soak: walk L1↔L2 boundary 10×; watch for NREs, duplicate canvases, pool errors,
   NavMesh agent drops at the seam (add `NavMeshLink` at the boundary if agents need to cross).
10. Checkpoint → die → Load Checkpoint (soft reset): positions, HP, points, levels, sword state.
11. Game Over → Restart → full second playthrough of items 1–5 (duplicate-manager canary).
12. Pause/settings during: Setsuna, a fade, a QTE, a rescue — resume cleanly from each.

---

## 11. BUG-FORECAST APPENDIX — failure modes you must actively design against

These are predicted, specific, and each has a named mitigation above. Re-read before coding.

- **Duplicate managers after restart** — DDOL survivors + Bootstrap reload (→ R3, 1.4).
- **Static-event double-fire** — duplicated/relived subscribers on statics (`OnPointsChanged`,
  `OnAnyEnemyDied`) (→ 1.4, R8).
- **Awake-order nulls on direct-area play** — resolving managers in `Awake` (→ R4, P0).
- **Editor autoloader double-load** — autoloader + bootstrapper both loading Persistent (→ 0.2).
- **Wrong-instance grabs** — `FindAnyObjectByType` picking a duplicate/test object (→ R4 revokes it).
- **Pool corruption on unload** — pooled enemies parented in area scenes (→ 3.2, 3.3).
- **Registry iteration over destroyed entries** — unload racing unregister (→ R5 null-purge).
- **Entrance query before Start** — placing twins in the sceneLoaded callback frame (→ 3.5).
- **Two EventSystems / AudioListeners** — input raycasts misfire, audio doubles (→ R9, 1.5).
- **World-space canvas dead Event Camera** — area canvases after camera moved to Persistent
  (→ R9, `WorldSpaceCanvasCamera`).
- **Coroutine on deactivatable host** — fades dying mid-sequence (→ Phase 2 host rule).
- **Scaled-time stalls** — fades/QTE/reset frozen at timeScale 0/0.15 (→ R10, 5.3).
- **timeScale leak through game over** — restart at 0.15× speed (→ 5.4).
- **Cross-scene VCam serialization** — camera refs into area scenes from Persistent (→ 3.6).
- **Activation-Track ancestor kill** — `SetActive(true)` no-ops on gameplay objects whose
  parents a timeline left inactive; the state depends on where evaluation stopped, so it's
  intermittent by nature (→ R11, Phase 7.6 — the rescue-checkpoint bug).
- **timeScale stomping** — two of the seven writers overlapping, one's restore-to-1 cancelling
  the other's active scale (→ R10, Phase 5.5 `TimeScaleService`, min-value-wins).
- **Straddle unload** — one twin's exit unloading the area the other twin still stands in
  (→ 3.7 per-actor occupancy sets).
- **Teleport occupancy desync** — checkpoint/intro/soul teleports bypassing trigger exits, so
  the loaded set goes stale (→ 3.7 `NotifyTeleported` at every reposition call site).
- **Soft-reset ghosts** — orphaned rescue/TTK state, live Accord/Setsuna, or grabbed traps
  surviving a checkpoint load now that nothing reloads the scene (→ Phase 7 item 5).
- **SO state bleeding between sessions** — upgrade levels persisting in editor, dirty assets in
  VCS (→ Phase 4).
- **NavMesh seam drops** — agents at the L1/L2 boundary with separate surfaces (→ item 9,
  `NavMeshLink`).
- **Lighting pop on additive load** — area scenes carry their own lighting settings; verify
  `LightProbes`/skybox come from the intended scene (active scene controls render settings —
  keep the active scene = the area the player occupies, which `SceneFlowManager` should set via
  `SceneManager.SetActiveScene` after load; verify it does).

---

*End of work order. After all phases pass §10, update `game.md` "Known Open Issues" to reflect
the new reality and cut a changelog block. New features resume only after that.*

---

## 12. PRODUCTION GAP ANALYSIS — this codebase vs AA/AAA discipline, and SOLID enforcement

The macro-architecture after Phases 0–7 is sound for a game of this scope (persistent scene +
chunk-graph streaming, SO-driven data, hybrid GOAP/BT/FSM AI with blackboard decoupling — the
AI ecology layer in particular is *above* typical indie standard). What separates it from a
shippable AA codebase is not architecture but **discipline systems**: the machinery that stops
regressions from re-entering. That is Phase 8 — start it only after §10 passes.

| # | Area | AA/AAA practice | PoT today | Action |
|---|------|-----------------|-----------|--------|
| 1 | Automated testing | Smoke + contract tests run before merge | Test framework installed, **zero tests** — every contract in this document is hand-verified | **P8.1** |
| 2 | Scene/asset linting | Validators catch bad wiring before runtime | "Scene mismatch" hunted by eye in Inspectors | **P8.2** |
| 3 | Logging | Channeled, stripped from release, never per-frame | Raw `Debug.Log`, incl. **per-frame** (`TutorialRescueWatchStepSO` poll loop) and per-candidate loops (`GetSafestRitualSite`) | **P8.3** |
| 4 | Central time control | One arbiter | Seven writers | 5.5 ✔ scheduled |
| 5 | Save persistence | Versioned, serialized saves | Checkpoint is in-memory only — lost on app quit | **P8.4** |
| 6 | Profiling | `ProfilerMarker`s + frame budgets on hot paths | None | **P8.5** |
| 7 | Input | Rebindable action maps | Legacy `Input.GetKey` hardcoded | Known backlog — migrate whole, never half |
| 8 | Data architecture | Per-feature data types | `AbilityUpgradeData` is a **god-SO** (every asset carries Stun+Empower+Accord+Gate-pulse fields) | **P8.6** (backlog, design below) |
| 9 | Teardown/re-entry safety | Restart loops tested | 1.4 ✔ scheduled | — |
| 10 | VFX system | Pooled, managed, sequenceable | 23 scripts, 5 wiring patterns, ~25 raw `Instantiate` calls, `SpawnOneShot` copy-pasted 3× — no pooling, no sequencing, stale-child leak on pooled enemies | **P9.3** |
| 11 | Audio system | Pooled voices, managed snapshots, music/ambience runtime | Mixer + volume prefs exist; zero playback at runtime — **12+ systems silent** (melee, abilities, QTE, rescue, checkpoints, UI, game over, footsteps) | **P9.2 + P9.4** |
| 12 | Assembly definitions | Domain isolation, faster incremental builds | None — all scripts in one implicit assembly; cross-system dependencies invisible to compiler | P8.1 backlog (game.md §20.1) |

**Explicit non-goals (do NOT gold-plate):** Addressables, ECS/DOTS, netcode, streaming LOD/HLOD,
a custom build pipeline. At two-to-five chunks and one platform these are over-engineering;
adding them is itself a violation of this work order.

### P8.1 — Minimal test suite (PlayMode + EditMode)
Make the core contracts machine-checked. Exactly these, in `Assets/Tests/`:
- `Boot_PersistentLoadsBeforeAreas` (PlayMode) — load Bootstrap, assert Persistent is loaded
  and every `Instance` in the CLAUDE.md singleton table is non-null before any area Awake runs.
- `RestartLoop_NoDuplicatesNoStaleStatics` (PlayMode) — boot → simulate Restart → boot; assert
  one of each singleton and that one skill purchase fires `OnPointsChanged` exactly once.
- `TimeScaleService_MinWins` (EditMode) — request/release permutations of {0, 0.15, 0.25,
  0.85}; assert resulting scale and restore-to-1 on empty.
- `Checkpoint_RoundTrip_AllNineTrees` (EditMode) — snapshot → mutate → restore via
  `SkillTreeManager.AllTrees`; assert Empower and Accord State included (regression-pins the
  7-of-9 bug).
- `Occupancy_TransitionModel` (EditMode) — **extract the desired-set computation from
  `SceneFlowManager` into a plain class `OccupancyModel`** (input: actor→location map +
  adjacency; output: desired set). This extraction is itself a SOLID win (logic testable
  without scenes) and the test covers: straddle keeps both loaded, teleport transition,
  single-actor walkthrough never unloads occupied ground.

### P8.2 — Scene lint (editor tool) — ✅ DELIVERED 2026-06-16 (the "Planet of Twins Tools" suite)
Shipped as `Tools ▸ Planet of Twins ▸ Validate` (`Assets/Scripts/Editor/Validation/ValidatorWindow.cs`
+ `SceneScan` core; **not** the originally-sketched `Assets/Editor/SceneLintWindow.cs` path — editor
tools live under `Assets/Scripts/Editor/` so they compile into `Assembly-CSharp-Editor` and can see
gameplay types). Covers, with ping-able references: (a) cross-scene serialized refs (R2, structural);
(b) null `[RequiredReference]` fields (the new attribute, `Assets/Scripts/Validation/`); (d)
`Time.timeScale` writes outside `TimeScaleService`, plus `DontDestroyOnLoad`/raw `Input.*`/snapshot
`TransitionTo`/debug keys, allowlisted + per-file-aggregated (doubles as the Phase-9 punch-list); and
conditional scene-completeness + WorldLocationSO graph sanity. **(c) — Activation-Track ancestor scan
for gameplay-logic types (R11) is NOT yet implemented** (carry-over; pairs with the deferred Timeline
auditor). A dev/editor-only `SceneIntegrityChecker` re-runs (a)+(b)+duplicate-singleton at runtime on
scene load. Run the Validator as the last step of every phase's DoD. See changelog 2026-06-16.

### P8.3 — Logging discipline
`static class Log` with `[Conditional("UNITY_EDITOR")]` channel methods
(`Log.Tutorial(...)`, `Log.Streaming(...)`, `Log.AI(...)`). Sweep and convert; **delete** the
per-frame `Debug.Log` in `TutorialRescueWatchStepSO`'s poll loop and the per-candidate logs in
`SpawnZone.GetSafestRitualSite` — per-frame string interpolation allocates and stalls even in
builds where the log is invisible.

### P8.4 — Save-data contract (contract now, UI later)
`SaveData` (plain `[Serializable]`, `int version = 1`) mirroring `CheckpointData` + tree levels
+ points + location asset GUID; `SaveService` (Persistent) with `Write/Read` to
`Application.persistentDataPath` as JSON. Wire nothing to UI yet — the point is that the data
shape is versioned *before* content multiplies, because retrofitting versioning is the classic
AA save-system failure.

### P8.5 — Profiler markers
`ProfilerMarker` around: `EnemySpawner.SpawnLoop` tick, `PoTWorldStateWriter.Update`,
`GOAPBrainBase.TickBrain`, `SceneFlowManager` load/unload coroutines. Ten lines total; makes
the top-down-camera perf work measurable instead of anecdotal.

### P8.6 — Split the god-SO (backlog — only after multi-scene is stable)
`AbilityUpgradeData` violates ISP/SRP: every tree asset carries every system's base fields —
and the **node is equally god-shaped** (verified: `AbilityUpgradeNode` carries every family's
bonus fields in one class, plus a dead duplicate — `cooldownReduction` is unused;
`CurrentCooldown` reads `cooldownBonus`. Delete the dead field during migration). Design (so
nobody improvises): abstract `AbilityUpgradeData` keeps only the shared core (economy,
`currentNodeIndex`, `ResetToBase`, `CurrentUnlockedLevel`); derived `StunUpgradeData`,
`GateUpgradeData`, `EmpowerUpgradeData`, `AccordUpgradeData`, … carry their own base values
and computed properties. For nodes, pick **one** of two designs in review before coding — do
not mix: (a) base node keeps label/pointCost/previewClip/description, per-family node
subclasses carry bonuses, and the base SO holds `[SerializeReference] List<AbilityUpgradeNode>`
(needs a small custom editor to add the right node type); or (b) each derived SO declares its
own concretely-typed node list and the base exposes abstract `NodeCount`/`NextNodeCost`/
unlock accessors. `SkillTreeManager`'s typed accessors change return types; `AllTrees` stays
`IReadOnlyList<AbilityUpgradeData>`. Migration = create derived assets, copy values, repoint
serialized slots, delete old assets — one commit, all consumers verified by P8.1's round-trip
test.

### P8.7 — Backlog additions (2026-07-03 architecture review)
- **Test asmdef FIRST:** the EditMode tests in `Assets/Tests/EditMode/` are **undiscoverable**
  (no test asmdef; predefined-assembly tests aren't picked up by the Test Runner) — P8.1 is
  silently void until `PoT.Tests.asmdef` exists. This is the first P8 action.
- **Tick allocations:** `SoulPulseSystem.BurnTickLoop` allocates a fresh `List` every 0.1 s
  tick — reuse a buffer.
- **Find-scans → registry:** `AccordSpiritAgent.FindNearestUnclaimed` and `RadiantSeekerOrb`
  target-seek via `FindObjectsByType` per call — replace with an R5-style registry (the
  Accord portal's `OverlapSphere` damage is correct and stays).
- **Log gating** (extends P8.3): hot-path spawn/cue/AI logs behind `DevConfig`/conditional
  compilation.

### SOLID enforcement — named violations and their fixes
- **SRP:** `TutorialCheckpointStepSO` mixed flow + reset policy + UI calls → fixed by 7.6h's
  `WrongTwinResetHandler` extraction. `GameOverController`'s sibling-raycast manipulation is a
  modal concern, not game-over logic → extract `ModalPanel` helper (backlog).
- **OCP:** the step-SO pattern is a *positive* example — new step type = new class, zero edits
  elsewhere. Preserve it; never add mode-enums to existing steps when a new step class will do.
- **LSP/DIP:** `QTEManager.ReturnCamera` downcasts `_cameraController is CameraManager` to
  reach `CinemachineCloseCam` → add `ICameraController.ReturnToGameplay()` and delete the cast.
  `PoTWorldStateWriter`/`SoftResetController`/`GameOverController` Find-fallbacks → wire (R1)
  and delete, per triage.
- **ISP:** the god-SO (P8.6).
- **DRY:** tree hand-lists (fixed via `AllTrees`), duplicated wrong-twin lambdas (fixed via
  7.6h), three fade implementations (optional `ScreenFader`, Phase 2 item 5).
- The Rulebook §1 **is** the project's dependency-inversion policy — every rule maps to D in
  SOLID. Cite the rule number in the changelog for every new reference added.

### BANNED LAZY WORK — review-enforceable list
A change exhibiting any of these is rejected regardless of whether it "works":
1. `?.` or null-checks that **swallow** a required dependency instead of LogError + disable
   (R4). Optional deps must be commented as optional.
2. Any new `FindAnyObjectByType`/`FindObjectsByType`/`GameObject.Find`/string lookup outside
   the documented allowlist (`EnemyFreezeService`, `LocationEntrance.GetFor`,
   `IntroTimelinePositioner`, editor code).
3. Copy-pasted handler/coroutine bodies — extract before submitting.
4. TODO/stub inside a commit marked done; stubs require an `In progress` changelog entry.
5. New direct `Time.timeScale` write (post-5.5), any `DontDestroyOnLoad`, any event
   subscription without its paired unsubscribe, any lambda subscription to a longer-lived
   publisher.
6. A new serialized reference without naming its Rule (R1–R11) in the changelog entry.
7. Marking a phase done without executing its DoD in-editor and logging the result.
8. Changing player-facing behaviour without updating `game.md` in the same commit.
9. Raw `Input.*` reads outside `TwinInputReader` (they bypass `TutorialInputGate` — the
   Setsuna F-hold and the dual ESC consumers are the cautionary precedents). Extend
   `IInputProvider` instead; ESC goes through the pause controller's priority chain (5.6).

---

## 13. BUGS.md — living defect ledger (create BEFORE Phase 0 work begins)

Create `BUGS.md` in the repo root next to `CLAUDE.md`/`game.md`. It is the single source of
truth for defects across sessions — `game.md` §21 remains a snapshot of *player-facing* known
issues; `BUGS.md` tracks **every** defect through its full lifecycle.

Entry format — one block per bug; **never delete entries, close them**:

```
### BUG-014 — Rescue checkpoint never activates after timeline
Status: Open | In-Progress | Fixed | Verified | Watch
Severity: Blocker | Major | Minor
System: Tutorial / Timeline
Symptom: <player-visible behaviour, one or two lines>
Root cause: <verified cause — or "suspected: …" until confirmed in-editor>
Fix: instruction.md 7.6a–b / commit <hash or changelog block>
Verified by: <which §10 step or test ran, and the date>
Regressions: 0
```

Rules:
- **Seed it first:** before Phase 0 work starts, create an entry for every item in
  `game.md` §21 and one `Status: Watch` entry per failure class in §11 (the forecast). The
  seeding itself is the first changelog entry.
- **Log before fixing:** any defect discovered mid-phase gets its entry *before* the fix is
  written — no silent fixes (this pairs with Banned Lazy Work #4 and #7).
- `Fixed` requires the commit/changelog reference; `Verified` requires the relevant DoD or
  §10 step to have actually run in-editor — the two are never set in the same moment.
- A bug that comes back **reopens the same ID** and increments `Regressions:` — two or more
  regressions on one ID is the formal signal that the fix was a patch and a structural fix is
  required (escalate to a design note before patching again).
- Every phase ends with a BUGS.md status sweep committed alongside the changelog entry, and
  every new-feature session starts by reading the `Open`/`Watch` entries for the systems it
  touches.

---

## 14. PHASE 9 — FX & AUDIO ARCHITECTURE (the unified cue system)

> **⚠ SHIPPED IN A DIFFERENT FINAL FORM (2026-06-18) — this section is the original work-order, kept for
> history.** The cue system was redesigned and built per the user's spec: a `CueBookData` is a **container of
> NAMED effects (string `id`)** played by `FxManager.PlayBook(book, "id", ctx)` — **not** keyed by an `FxEvent`
> enum (removed), and the per-effect cue subtype SOs below (`ParticleCueData` / `VfxGraphCueData` /
> `CueSequenceData`) are **deleted**, folded into inline `CueElement`s with **per-element audio** (loop /
> one-shot / kill-with-visual) and a **Manpu** element kind. The live, canonical description is **game.md §23.6
> (+ §23.8 verifier)** and the changelog "Cue Book FINAL model" entry. Read those for the as-built system; the
> §14.1 / §14.1c / §14.3 cue-subtype details below describe the superseded intermediate design.
> **Identifier hardening:** the string ids are made compile-time safe via generated `FxIds.*` constants —
> see game.md §23.8 (the answer to "strings are typo-prone": the human never types the raw string at a call
> site; the Cue Id Verifier generates and checks the constants).

> Start only after §10 passes and Phase 8 is at least underway. This section is written to
> the same standard as §1–§13: the Rulebook applies to every line of it, every timer states
> scaled vs unscaled, and every migration row lands in the changelog with its rule number.

### 14.0 Why — current state (verified 2026-06-13)

**VFX today:** 23 scripts hand-roll effects with `[SerializeField] GameObject` prefab slots +
`Instantiate`/`Destroy`. Five wiring patterns coexist (A: serialized prefab refs, B:
scene-resident looping GOs toggled by `SetActive`, C: instantiate + timed destroy, D:
instantiate parented to owner, E: pooled — projectiles only). `SpawnOneShot()` is
**copy-pasted three times** (`MeleeAttackStrategy.cs:76`, `EnemyVfxController.cs:69`,
`AccordMeleeAbility.cs`) with diverging lifetime math — a standing Banned Lazy Work #3
violation. Nothing is pooled except enemy projectiles; ~25 `Instantiate` call sites allocate
and GC-churn per hit/cast. No effect can be sequenced with another except by hand-written
coroutine. Attached effects (Pattern D) leak onto **pooled enemies** — a despawned enemy
returns to the pool carrying live stun/aura children (§11 failure class: pooled reuse).

**Audio today:** `GameAudioMixer` (Master→Music/SFX, three exposed params) + volume
persistence in `SettingsMenuController` exist and are correct. Everything else is absent:
no audio manager, no music/ambience playback anywhere, no pooled voices, and **12+ gameplay
systems are silent** (melee, enemy death, every ability, QTE, rescue, checkpoints, UI, game
over, scene transitions, footsteps). `DialoguePlayer` and the L1 birds own private
`AudioSource`s. The AI `SoundEventSystem` is *perception only* — it tells enemies a sound
happened; it never plays one.

**Decisions (locked with the user, 2026-06-13):**
1. **Native Unity audio.** No FMOD/Wwise. A cue-SO layer over `AudioSource`/`AudioMixer`
   gives this project everything middleware would, stays Claude-editable, adds zero
   dependencies. Revisit only if a dedicated sound designer joins.
2. **Unified authoring, separate engines.** One composable `CueData` SO family authors
   *what plays when* (mixed VFX + SFX sequences in one asset); playback is delegated to two
   engines — `VfxManager`-side pooling inside `FxManager` (visuals) and `AudioManager`
   (voices, mixer). No god object: composites orchestrate, leaves delegate.
3. This section lives in instruction.md (not a separate doc), and its findings update §12.

### 14.1 Data layer — the cue SO family (`Assets/Scripts/Fx/Data/`) — R7: config ONLY

Every cue SO is immutable at runtime. Runtime state (handles, voices, sequence cursors)
lives in the managers — the Phase 4 `SkillTreeRuntimeState` precedent applies verbatim.

| SO class | Fields |
|---|---|
| `CueData` (abstract) | `TimeMode timeMode` — **`FromPrefab` (default)**, `Scaled`, or `Unscaled`. `FromPrefab` means *do not touch* `main.useUnscaledTime` — the artist already set it on the ParticleSystem; the cue only overrides when a designer deliberately picks Scaled/Unscaled. **The SO never re-stores what the prefab authored** (R7 + Banned Lazy Work #1). |
| `ParticleCueData` | `ParticleSystem prefab`; `AttachMode { World, Follow, FollowDetachOnTargetDeath, FromPrefab }` — **`FromPrefab` (default)** reads the root system's `main.simulationSpace` (Local→Follow, World→World) so local/world is **never re-authored**; pick an explicit mode only to *override*. `float explicitLifetime` — **default 0 = read it from the prefab** (`main.duration + main.startLifetime.constantMax`, recursed across sub-emitters via the helper below); set non-zero only for the rare looping system with no natural end. `Vector3 localOffset` defaults to `Vector3.zero` (no reposition — the prefab's own transform/animation owns position; offset is an *optional* nudge, not a required field). `int prewarmCount`. |
| `VfxGraphCueData` | `VisualEffect prefab`; same attach/offset; `float lingerAfterStop` (reclaim delay after `SendEvent("OnStop")`); `int prewarmCount`. |
| `SoundCueData` | `AudioClip[] clips` (one picked at random — variant rotation is the cheapest AA polish there is); `Vector2 volumeRange`, `Vector2 pitchRange` (randomized per play); `AudioMixerGroup outputGroup`; `bool spatial` + `float minDistance`/`maxDistance` (3D) — false = 2D; `bool loop`; `int priority` (voice stealing); `float cooldown` (anti-spam: re-trigger inside this window is dropped — scaled time); `int maxSimultaneous` (0 = unlimited). |
| `CameraShakeCueData` | Cinemachine Impulse: `float amplitude`, `float duration`. Build LAST — optional. |
| `CueSequenceData` | `List<CueStep>` where `CueStep { CueData cue; StartMode { AfterPrevious, WithPrevious }; float delay; bool waitForCompletion }`. Steps may nest other `CueSequenceData`; **editor `OnValidate` rejects self-reference, runtime caps nesting depth at 4 and LogErrors** (fail loud, R4 spirit). Sequence timers tick in the cue's own `timeMode` (R10 — comment at the field). |
| `MusicTrackData` | `AudioClip clip`; `float fadeInSeconds`, `fadeOutSeconds`; `bool loop`. |
| `AmbienceData` | `AudioClip bedLoop`; `SoundCueData[] randomOneShots`; `Vector2 oneShotIntervalRange` (unscaled — ambience keeps breathing during Setsuna). |

`WorldLocationSO` gains two optional fields: `MusicTrackData musicTrack`,
`AmbienceData ambience`. Config on a config SO — R7-clean. Null = silence, not an error
(documented-optional per Banned Lazy Work #1).

Asset naming: `Cue_<System>_<Event>` for leaves (`Cue_Melee_SlashVfx`, `Cue_Melee_HitSfx`),
`Seq_<System>_<Event>` for composites (`Seq_Melee_Hit`). Folder: `Assets/Data/Fx/`.

### 14.1b — THE PREFAB IS THE SOURCE OF TRUTH (non-negotiable — addresses re-authoring concerns)

A `CueData` asset is a thin **pointer + dispatch policy**, never a re-description of what the
artist authored on the ParticleSystem. The artist sets timing, sub-emitter durations,
simulation space, scaled/unscaled, start delay, dynamic position bursts — the cue system
**reads those, it does not duplicate them.** Every field defaults to "ask the prefab." A
designer types a value into a cue field *only* to deliberately override, and that override is
the rare exception that gets a comment. Same R7 principle as `SkillTreeRuntimeState`, applied
to VFX: data already living in the authored asset is never copied into a second asset that can
drift.

Against each stated worry:
- **"Don't make me find total sub-emitter time and match it in the SO."** You don't.
  `explicitLifetime = 0` (default) → `FxManager` computes it once at pool-prewarm, cached per
  prefab, recursing sub-emitters automatically:
  ```csharp
  // FxManager helper — a particle graph's natural lifetime, computed ONCE at prewarm.
  static float ComputeLifetime(ParticleSystem root)
  {
      float Max(ParticleSystem ps)
      {
          var m = ps.main;
          float self = m.duration + m.startDelay.constantMax + m.startLifetime.constantMax;
          float deepest = self;
          foreach (Transform c in ps.transform)              // sub-emitters = child systems
              if (c.TryGetComponent(out ParticleSystem child))
                  deepest = Mathf.Max(deepest, Max(child));   // recurse — no manual summing
          return deepest;
      }
      return Max(root) + 0.1f;   // small tail so the last particles finish
  }
  ```
  Only looping systems (no natural end) need a non-zero `explicitLifetime` — or they use
  `AttachMode.Follow` + an explicit `Stop(handle)` from gameplay (held-handle cue, tier B).
- **"Scaled/unscaled is already on the VFX — don't redo it."** Default `timeMode = FromPrefab`
  leaves `main.useUnscaledTime` exactly as authored. Override only on a deliberate Scaled/
  Unscaled pick (rare — the particle already knows; the one common case is a UI cue that must
  ignore pause `timeScale = 0`, an explicit Unscaled choice, commented per R10).
- **"Local→world particles break if you reparent / re-set position."** Default
  `AttachMode = FromPrefab` reads `main.simulationSpace`: **World** → spawned unparented at the
  pose and left alone (particles already live in world space — no follow, nothing to break);
  **Local** → parented to the follow target so particles ride the transform as authored. The
  system never flips simulationSpace and never forces a parent that fights the authored space.
- **"Some particles change position dynamically."** Those are World-space or self-animating;
  `FromPrefab` + zero offset leaves their motion untouched. If one must follow a moving target
  *and* was authored local-space, `AttachMode.Follow` parents it **once** (not a per-frame
  teleport), so authored motion composes with parent motion instead of being overwritten.

Rule for Claude Code: **a cue field that re-states authored particle data is a Banned Lazy
Work violation (#1/#6).** When unsure, read from the prefab and leave the field at its
`FromPrefab`/`0`/`zero` default.

### 14.2 Runtime layer — Persistent residents (R3: no DDOL, duplicate-destroy Awake guard, `Instance = null` in OnDestroy)

```
FxManager (Persistent)                 AudioManager (Persistent)
  ├─ Play(CueData, in CueContext)        ├─ Play(SoundCueData, pos/Transform) → voice
  │    → CueHandle                       ├─ PlayUI(SoundCueData)  ← 2D, unscaled,
  ├─ Stop(CueHandle)                     │     ignoreListenerPause = true
  ├─ StopAllOn(Transform)                ├─ SetPaused(owner) / ReleasePaused(owner)
  ├─ StopAll()                           │     → AudioListener.pause (sole writer)
  ├─ VfxPool (FxPoolRoot, mirrors        ├─ RequestSnapshot(owner, id, priority) /
  │   EnemyPool: per-prefab stacks)      │   ReleaseSnapshot(owner)  ← arbiter, see below
  └─ sequence runner (CueSequenceRunner, └─ 32 pooled AudioSource voices; stealing =
      plain C# class — EditMode-testable)     lowest priority, then oldest

                       MusicManager (Persistent)
                         A/B AudioSource crossfade — UNSCALED timers;
                         subscribes SceneFlowManager active-location change (named
                         handler, OnDestroy unsubscribe — R8);
                         no-op when the incoming track equals the playing one
```

- **`CueContext`** (readonly struct): `Vector3 position`, `Quaternion rotation`,
  `Transform followTarget` (null = world-space), `object owner`.
- **`CueHandle`** (readonly struct): `int id` + `int version`. The managers version-stamp
  pooled instances; a stale handle (instance since reclaimed and reissued) is **inert** on
  `Stop`/`IsPlaying` — this is the standard kill for the entire stale-pooled-reference bug
  class. Never hold a raw `ParticleSystem`/`AudioSource` reference across frames; hold the
  handle.
- **Leaf dispatch:** `ParticleCueData`/`VfxGraphCueData`/`CameraShakeCueData` play through
  `FxManager`; `SoundCueData` through `AudioManager`; `CueSequenceData` through the
  sequence runner which dispatches each step back through `FxManager.Play` (single entry
  point — gameplay code never talks to a leaf engine for one-shots).
- **Snapshot arbiter** (in `AudioManager`): `RequestSnapshot(owner, snapshotId, priority)` /
  `ReleaseSnapshot(owner)`; highest priority wins; empty set = `Default` snapshot;
  transitions via `AudioMixerSnapshot.TransitionTo` on **unscaled** time. This deliberately
  mirrors `TimeScaleService` — snapshot stomping is the *same* bug class as the seven
  timeScale writers, so it gets the same request/release cure on day one, not after the
  seventh writer appears.
- **Resolution:** area/gameplay scripts get the managers per R4 — optional serialized slot
  cast in `Awake`, `field ??= FxManager.Instance` in `Start()`, LogError + `enabled = false`
  if still null. Persistent→Persistent wiring is plain R1 serialized refs.

### 14.1c — VFX categories & the "too many SOs?" question

**On asset count — it's fine, and here's the bound.** One leaf cue per *distinct* effect, not
per spawn site. The migration table consolidates ~25 raw `Instantiate` calls + three copies of
`SpawnOneShot` into far fewer cues, because the same effect fired from three call sites becomes
**one** `Cue_*` asset referenced three times. A vertical slice lands around 30–50 leaf cues +
a dozen sequences — trivial for Unity (SOs are ~1 KB assets; projects ship thousands). What
actually causes "too many assets" pain is *duplication* (the same effect re-authored per
caller) and *drift* (data copied from the prefab) — both of which this system removes. If two
cues differ only by a number a designer tweaks, that's two assets by design; if they're
byte-identical, that's one asset wired twice. SOs also give you free diffability, no-code
reassignment, and per-effect prewarm tuning — net win over prefab slots scattered across 23
scripts.

**The four VFX categories and how each maps to the system** (this is the "what plays when /
what cues" answer):

| Category | Examples | How it fires | Attach / lifetime |
|---|---|---|---|
| **Player-ability** | Setsuna charge, Empower aura, Gate pulse, SC shield | the ability calls `FxManager.Play(cue, ctx)` at the existing fire point; **held**-handle cues (loops) keep the `CueHandle` and `Stop(handle)` on ability end | `Follow` the casting twin (Local-authored) or `World` at cast pose; loops = held handle, no auto-lifetime |
| **Pure environment** | ambient embers, barrier haze, ritual-site glow | scene-placed, self-running ParticleSystems that **never start/stop at runtime** | **not cues at all** — leave as scene objects (§14.4 / migration tier 4 explicitly excludes decor). Do not migrate these. |
| **Resultant / event** | ability *land*, melee *impact*, kill soul-wisp, projectile burst | the gameplay event that produces them calls `Play` (or fires a `Seq_*` mixing VFX+SFX) at the impact point/pose | usually `World` one-shot at the event position (auto-lifetime); the §14.3 `Seq_Melee_Hit` is exactly this |
| **Environment-reactive** | barrier flares when an enemy nears, spawn-point reacts to attack, ritual site reacts to Witness | the *reacting object* owns the trigger (proximity/POI/`PoTWorldStateWriter` signal it already gets) and calls `Play`/`Stop` on its own state change | `Follow` the reacting object if Local-authored, else `World`; held handle if it has an on/off state |

The dividing line is **"does runtime gameplay decide when it starts or stops?"** Yes → it's a
cue, it goes through `FxManager`. No (it just plays forever as scenery) → it stays a plain
scene ParticleSystem and the cue system never touches it. That single question prevents both
over-migration (turning decor into cues) and under-migration (leaving gameplay effects as raw
`Instantiate`).

**Environment-reactive is the only subtle one:** the reaction *trigger* is gameplay logic and
lives on the reacting component (e.g. `BarrierPOI` already tracks `NearbyEnemyCount`), but the
*effect* is a cue. So the barrier component calls `FxManager.Play(Cue_Barrier_Flare, ctx)` when
its count crosses zero and `Stop(handle)` when it returns — the trigger stays in the gameplay
system (R-correct ownership), only the visual is delegated. No new wiring pattern; it's the
held-handle player-ability pattern applied to an environment object.

### 14.1d — Who owns a per-target held effect: the ability, never a shared VFX engine

**Rule:** each ability/system owns its own cue book and plays/stops its own effects. A
*pre-existing shared VFX engine* (e.g. `StunVFXSystem`, which today renders the same per-enemy
aura for Stun **and** Possess **and** Coalesce) is **not** the home for a migrated cue — routing
a book's effect back through it re-couples the abilities the cue migration exists to separate.

Worked example (this resolves the live `OnStun_Hit` fork): `StunCueBook`'s `OnStun_Hit` is a
**held** per-enemy effect. It is owned by **`StunAbility`** — `StunAbility` plays it per stunned
enemy via `PlayBook(book, FxIds.StunCueBook.OnStun_Hit, CueContext.Follow(enemy))`, keeps the
`CueHandle` in its per-enemy map (`_hitHandles`), and `Stop(handle)` when *that enemy's* stun
ends. It is **not** owned by `StunVFXSystem`. Consequences to enforce:
- `StunVFXSystem` stops being Stun's VFX source. If Possess/Coalesce still use it, it is
  **renamed to what it actually is** (a shared immobilise-aura engine — e.g. `ImmobiliseAuraVFX`)
  because a class named for one ability that serves three is the Banned Lazy Work #14
  naming violation. If nothing else uses it after Stun migrates, it is **retired**. It is not
  left as `StunVFXSystem` quietly doing three abilities' work.
- Duration is **never baked in the asset** — `OnStun_Active`/`OnStun_Hit` loop; the ability's
  own timer drives `Stop`, so upgrade-scaled 3 s/6 s/4 s durations are correct for free (§14.1b).
  An enemy that wanders into range mid-window is cut at window-end like the rest; if frame-exact
  per-enemy timing is wanted, poll `IsStunned` per handle (optional precision, not required).
- **Follow targets the right actor.** `OnStun_Active` (caster channel) follows
  `_owner.transform` (the twin) — correct as authored. `OnStun_Hit` (the "I'm stunned" stars)
  must follow the **enemy** — set those elements to `Follow` with the enemy as the
  `CueContext` target, or make their prefab Local-space; never pass the caster as the target for
  an on-enemy effect.
- **Ids: code conforms to the asset, never the reverse.** The asset defines `OnStun_Active` /
  `OnStun_Hit`; `StunAbility`'s calls change to those ids (via generated `FxIds`, game.md §23.8),
  not the asset renamed to match stale code. Verify the asset's real ids before "fixing a typo" —
  do not introduce a rename to an already-correct id.

### 14.3 The worked example — this is the contract for "run things one after another"

The requirement (user, verbatim intent): *"if we want to run 3 things 1 after other for a
particular thing (like 3 effects) then we should be able to do it"* — including each visual
carrying its own sound. ONE asset expresses it:

```
Seq_Melee_Hit.asset (CueSequenceData)
  [0] Cue_Melee_SlashVfx      AfterPrevious  +0.00   ← t = 0
  [1] Cue_Melee_SlashSfx      WithPrevious           ← same instant as [0]
  [2] Cue_Melee_ImpactVfx     AfterPrevious  +0.10
  [3] Cue_Melee_ImpactSfx     WithPrevious
  [4] Cue_Melee_SoulWispVfx   AfterPrevious  +0.25
  [5] Cue_Melee_SoulChimeSfx  WithPrevious
```

Gameplay code, forever:

```csharp
_fx.Play(_meleeHitCue, new CueContext(hitPoint, target));   // one line, one slot
```

Re-timing the combo = editing numbers in the asset. Adding a fourth beat = adding a row.
No recompile, no coroutine, no per-call-site Setsuna/pause/unload handling — the runner
owns all of it. When the user says "play X, then Y with a sound, then Z", the work is:
create/edit one `Seq_*` asset + (if new) its leaf cues, wire one slot. Nothing else.

### 14.4 Failure-class contract (extends §11 — these are law for FX/audio)

| # | Failure class | Rule |
|---|---|---|
| F1 | **Scene unload** | `FxManager` subscribes `SceneFlowManager.OnLocationWillUnload` (named handler, R8) → stops + reclaims every live instance whose `followTarget` is in the unloading scene; world-space instances are pooled under Persistent and can't dangle — they finish naturally. |
| F2 | **Pooled enemy reuse** | The enemy despawn path (`EnemyPool` return) calls `FxManager.StopAllOn(enemyTransform)`. An enemy must re-enter the pool *visually and audibly naked*. This retires the existing stale-StunVfx-child bug class. |
| F3 | **Setsuna (timeScale 0.15)** | Cues default `Scaled` — slow-mo slows effects, which is the point. `SetsunaSystem` may `RequestSnapshot(this, Setsuna, …)` for low-pass/pitch color and MUST `ReleaseSnapshot(this)` in `ForceEnd` *and* natural end (both paths — §11 teleport-cancel precedent). |
| F4 | **Pause (timeScale 0)** | Gameplay audio halts via `AudioManager.SetPaused(owner)` (sole `AudioListener.pause` writer — same single-writer law as R10). UI sounds use `PlayUI` (unscaled + `ignoreListenerPause`) and stay audible. `PauseMenuController` requests/releases; it never touches `AudioListener` directly. |
| F5 | **Soft reset** | `SoftResetController` teardown adds `FxManager.StopAll()` + `AudioManager.StopAllSfx()` alongside its existing ForceEnd calls. Music/ambience continue (location unchanged). |
| F6 | **Restart → Bootstrap reload** | Managers are scene-resident singletons (R3). Pools rebuilt in `Awake`; the only static is `Instance`, nulled in `OnDestroy`. No `StandaloneSingleton`/static caches anywhere in the FX/audio layer — the §0 canary must stay dead. |
| F7 | **Editor direct-play** | Covered by `PersistentSceneAutoLoader` + R4 fail-loud. A cue slot left unwired LogErrors once at `Start` and disables the consumer — never silently skips (Banned Lazy Work #1). |
| F8 | **Voice exhaustion** | 33rd simultaneous sound steals the lowest-priority-then-oldest voice. Per-cue `cooldown`/`maxSimultaneous` stop one system (20 melee hits in a frame) from starving the rest. Stealing logs in editor only (P8.3 channel). |

### 14.5 Mixer extension

`GameAudioMixer` grows to: `Master → { Music, Ambience, SFX, UI, Voice }` with exposed
`AmbienceVolume`, `UIVolume`, `VoiceVolume` params added to `SettingsMenuController`'s
PlayerPrefs round-trip (same `Vol_*` key pattern, same dB conversion at line 202).
Snapshots: `Default`, `Paused` (duck Music −10 dB, low-pass SFX), `Setsuna` (low-pass +
slight pitch on SFX/Ambience), `GameOver` (duck everything but Music). Dialogue's existing
`AudioSource` is rerouted to the `Voice` group (migration row, not an exemption).

### 14.6 VFX migration table (tiered — each row = one changelog entry citing its rule)

Call-site change everywhere: `[SerializeField] GameObject _xPrefab` →
`[SerializeField] CueData _xCue` and `Instantiate(...)` → `_fx.Play(_xCue, ctx)`. Looping
patterns (B) become a held `CueHandle` stopped on the end event.

| Tier | Script (current pattern) | Notes |
|---|---|---|
| 1 — Critical | `SoulPulseSystem.cs:136` (C) | Pulse burst → one-shot cue |
| 1 | `StunVfxSystem.cs:93` (D, loop) | Held handle per enemy; F2 reclaim fixes the stale-child bug |
| 1 | `AccordStateSystem.cs:508,526` (C) | Knockback + strike point |
| 1 | `SoulConvergenceSystem.cs:250,273` (C) | Charge + shield — shield GO itself stays gameplay (collider), only its dressing becomes a cue |
| 1 | `AccordSpiritSystem.cs:171,211,227` (C) | Spirit/knockback/charge |
| 1 | `KillParticleSpawnner.cs:68` (C) | Keep `EnemyDeathNotifier` subscription; `SoulParticleAttractor` rides the pooled instance — reset its state on reclaim |
| 2 — High | `AccordMeleeAbility.cs:71,79`, `AccordSpiritAgent.cs:150,187`, `VoidStrikeAbility.cs:131`, `RadiantSeekerAbility.cs:88`, `CoalesceSystem.cs:111` + `CoalesceAura`, `EmpowerSystem.cs:383`, `WitnessAuraVfx.cs:41,52` | Deletes the `AccordMeleeAbility` copy of `SpawnOneShot` |
| 3 — Medium | `MeleeAttackStrategy.cs:44,71` (deletes the original `SpawnOneShot`), `BombProjectile.cs:175`, `EnemyVfxController.cs:48–74` (deletes the third copy), `IkariMarkVFX.cs`, `SpawnShieldRipples.cs:16` | Projectile *gameplay* objects stay in `EnemyPool` (E) — only their impact dressing migrates |
| 4 — Low | `HealthRegenVfx.cs`, `SoulFrozenVfx.cs` (B — held handles), scene-decor one-shots (SpawnPortal etc. — leave as scene objects; decor is not a cue) | |

**Out of scope:** Timeline-driven cutscene effects (R11 governs those — Signal → receiver
stays scene-local); UI tweens; decor particles that never start/stop at runtime.

### 14.7 Audio hook-up table (every row: trigger site → cue slot → rule)

| Trigger | Site | Wiring |
|---|---|---|
| Melee swing/hit | `MeleeAttackStrategy.ExecuteAttack` | Fold into `Seq_Melee_Hit` (14.3) — R1 slot |
| Enemy death | subscribe `EnemyDeathNotifier.OnEnemyCombatKill` | Per-archetype cue via `EnemyData` SO field (R7 config) |
| Each ability cast/end | existing events: `OnAccordActivated/Deactivated`, SC/Empower/Setsuna equivalents | R4 resolve of managers in each system's `Start()` |
| QTE | `QTEManager` state machine (approach/mash tick/success/fail) | R1 slots on `QTEDefinition` SO |
| Rescue | `RescueEventController` (start/struggle/success/fail) | R1 |
| Checkpoint save | `CheckPointTrigger.SaveCheckpoint` (line 48) | one confirmation cue, R4 |
| UI clicks/hover | central: `SkillNodeButton`, pause buttons → `AudioManager.PlayUI` | never per-button `AudioSource` |
| Footsteps | `PlayerMovementController` anim events → **dual-fire**: `AudioManager.Play` (audible) + `SoundEventSystem.Fire(Footstep)` (AI perception) — two systems, one call site, never merged | R4 |
| Game over | `GameOverController.TriggerGameOver` (line 72) | stinger + `RequestSnapshot(GameOver)` |
| Scene transition | `SceneFlowManager` location-changed | `MusicManager` handles it (14.2) — no extra hook |
| Music/ambience | `WorldLocationSO.musicTrack/.ambience` | R7 config, played by `MusicManager` |

### 14.8 Sub-phases & DoD

- **P9.1 — Data + runtime core.** `CueData` family, `FxManager` + `VfxPool` + `CueHandle`,
  `CueSequenceRunner` as a **plain C# class** (the `OccupancyModel` move — timing math
  testable without scenes). DoD: EditMode tests — sequence timing (AfterPrevious/
  WithPrevious/delay/waitForCompletion permutations), nesting-depth guard, handle staleness
  (reclaim → old handle inert); one demo `Seq_*` asset plays correctly from **both**
  Bootstrap and L1_Park direct-play; changelog + BUGS.md Watch entries for F1–F8.
- **P9.2 — Audio engines.** `AudioManager` (voices, stealing, cooldowns, `PlayUI`, pause
  owner, snapshot arbiter), `MusicManager` (A/B crossfade, location subscribe), mixer
  groups + snapshots + settings round-trip. DoD: pause → UI click audible, gameplay silent;
  Setsuna/GameOver snapshot transitions verified in play mode; crossfade on Park↔Streets
  walk; arbiter EditMode test (priority permutations, empty → Default).
- **P9.3 — VFX migration, tier by tier.** One tier per session minimum granularity; never
  half a tier. DoD per tier: migrated systems verified on both entry paths + the relevant
  §10 slice; the three `SpawnOneShot` copies deleted by end of Tier 3; per-row changelog.
- **P9.4 — Audio hooks.** Every 14.7 row checked off; placeholder clips are acceptable
  (assets are content, not architecture) but every slot must be wired and fail-loud.
  DoD: full table swept; §10 smoke 1/4/8/12 re-run (input, Setsuna, QTE, pause all now
  have audible behavior to verify).
- **P9.5 — Manpu arc & reaction extensions** (the Manpu system + cue integration already ship;
  these are two additive features — game.md §24.6 / §24.7). **(a) Ability closing *sequence*:**
  promote the single per-ability `Closing` glyph to a 1–4-beat `ClosingSequence` owned by
  `ManpuSlot` for its full duration, mood suppressed throughout, explicit current-mood handback
  on release; author Stun (sleep 0.75 → wake "!") and Possess (shock → "!") as the start-set.
  Write the three failure guards verbatim (pool-clear aborts the closing coroutine; a new
  ability claim cancels a running sequence; Setsuna E1 applies to the current beat). **(b)
  Event-driven reactions:** add optional `GameObject source` to `DamageData`, a thin
  `OnEnemyDamaged(victim, DamageData)` bus next to `EnemyDeathNotifier`, a `ManpuReactionListener`
  scene GO holding the rule, and `ManpuSlot.PlayReaction(book, id, ctx)` forwarding through
  `FxManager` (no raw spawns — §24.1). Wire 2–3 reactions only: `betrayed` (possessed-ally
  attacker), `ally_down` (ally died nearby), optional `startled`; reactions target the
  **victim's** slot and live in a `CueBook_Reactions`, never in `ManpuVocabulary`.
  DoD: stun a grunt → sleep-then-wake reads cleanly and mood resumes naturally after; possess
  → end shows shock→"!"; re-stun mid-wake cancels the wake and re-owns; kill a closing enemy →
  no leaked glyph on its pooled reuse; possess one grunt, have it hit an ally → the ally (not
  the attacker) flashes the betrayed glyph; Cue Id Verifier clean for the reaction ids.

### 14.9 Banned Lazy Work — additions (enforceable from P9.1 merge onward)

10. Any new `Instantiate` of a particle/VFX prefab outside `FxManager`, or new raw
    `AudioSource.Play/PlayOneShot` outside `AudioManager`/`MusicManager` (existing sites
    get migration rows — `DialoguePlayer` keeps its source but routes through the Voice
    group; the birds migrate in P9.4 or get a documented-optional exemption row).
11. Any `AudioListener.pause` write outside `AudioManager`; any
    `AudioMixerSnapshot.TransitionTo` call outside the snapshot arbiter.
12. A sequence/timed-set built as a coroutine in gameplay code when a single `CueBookData`
    effect (its ordered `CueElement` list with start modes + cuts, §23.6/§23.7) can express
    it — the cue book exists precisely so this never happens again.
13. A hand-typed string literal at a `PlayBook("id")` call site where a generated `FxIds.*`
    constant exists (game.md §23.8) — use the constant; raw strings are only for genuinely
    dynamic ids, which the verifier's literal-coverage pass guards.
14. Naming a cue type/field after one consumer (a `StunCueBook` *type*, a Stun-shaped API),
    perfecting the effects of one ability while the system is incomplete, or declaring the cue
    system done with one consumer wired. The deliverable is the generic mechanism proven on
    the hardest consumer, not the first (CLAUDE.md working-method 11). Per-*asset* names like
    `CueBook_Stun` are fine — that's an instance; a `StunCueBook` *class* is not.

---

## 16. CROSS-SCENE TIMELINE COOKBOOK (consolidated reference for R11 + BUG-032)

The doctrine is in R11; this section is the **implementation reference** so the pattern is
executed identically every time without reassembling it from R11 + BUG-032 + the changelog.
The governing fact: **a `.unity` scene serializes track bindings as fileIDs valid only inside
that one scene file — there is no fileID that reaches into another scene.** Every technique
below is a way to defer the cross-scene link to *runtime*, where singleton/type lookups cross
scenes freely. This is exactly what the paid "Advanced Multi-Scene: Cross-Scene References"
asset generalises (a GUID registry + custom drawer that re-resolves on load); we do not need
the general version — for a handful of Timeline targets, resolve-by-type is simpler, license-
free, and more robust than per-object GUID bookkeeping.

**Decision table — for each null/cross-scene track binding, pick the row by what the track
does:**

| Track type | What it does | Technique |
|---|---|---|
| Cinemachine Track | drives the Persistent Brain | leave empty → `TimelineBindingResolver` `SetGenericBinding` by **type** |
| Animation Track | animates a Persistent object (twin, camera rig) | leave empty → resolver `SetGenericBinding`, target from `TwinSelector.Instance` / type |
| Signal Track (fade, hide HUD, fire checkpoint, camera cue) | triggers a one-shot **action** on a Persistent system | local `SignalReceiver` on director GO → **local relay** → Persistent singleton at runtime |
| Activation Track over a gameplay-logic object | (should not exist) | move the logic object out of the activated hierarchy (R11); never bind it |
| Activation Track over a deleted object | dead | remove the track by hand in the Timeline window |

### 16.1 `TimelineBindingResolver` — the continuous-track rebinder (already exists; this is the spec it must meet)
On every `PlayableDirector` GO that has cross-scene continuous tracks. Resolves in `Start()`
(R4 — after Persistent Awake), then before the first `Play()`. **No name strings anywhere** —
single-instance tracks resolve by type; tracks that need disambiguation (Left vs Right twin)
are mapped to a role by an explicit Inspector reference to the `TrackAsset` itself:

```csharp
[RequireComponent(typeof(PlayableDirector))]
public class TimelineBindingResolver : MonoBehaviour
{
    public enum TwinRole { Left, Right }

    [System.Serializable]
    public struct TrackRoleBinding
    {
        [Tooltip("Drag the Animation/Track asset from this director's Timeline.")]
        public TrackAsset track;      // a direct asset reference — survives renames, no string
        public TwinRole   role;       // which twin this track drives
    }

    [Tooltip("One row per twin-specific track. The CinemachineTrack needs no row " +
             "(resolved by type). Drag each track in and pick its role.")]
    [SerializeField] private TrackRoleBinding[] _twinTrackBindings;

    private void Start()   // R4: Persistent singletons exist by Start
    {
        var director = GetComponent<PlayableDirector>();
        var brain    = FindAnyObjectByType<Unity.Cinemachine.CinemachineBrain>();   // by TYPE
        var sel      = TwinSelector.Instance;                                        // singleton

        // 1. Single-instance tracks → resolve by type, zero authoring.
        foreach (var output in director.playableAsset.outputs)
        {
            if (output.sourceObject is Unity.Cinemachine.CinemachineTrack ct && brain != null)
                director.SetGenericBinding(ct, brain);
        }

        // 2. Disambiguated tracks → resolve by the explicit Inspector role map.
        foreach (var b in _twinTrackBindings)
        {
            if (b.track == null) { Debug.LogError("[Resolver] empty track row", this); continue; }
            var twin = b.role == TwinRole.Left ? sel?.LeftTwin : sel?.RightTwin;
            var animator = twin ? twin.GetComponentInChildren<Animator>() : null;
            if (animator == null) { Debug.LogError($"[Resolver] no animator for {b.role}", this); continue; }
            director.SetGenericBinding(b.track, animator);
        }
    }
}
```

Why this beats a name match: the `TrackAsset` reference is a real serialized object link, so
renaming the track in the Timeline window keeps the binding intact, and a typo can't silently
break it. The designer wires each twin track's role **once** by dragging; the Cinemachine
track (and any other single-instance type) needs no row at all. If a target resolves null,
`Debug.LogError` naming the track/role and **do not** leave it silently unbound. Resolve once
in `Start` (or on `OnLocationLoaded` if the director can play before its Persistent targets
exist — for the tutorial it cannot). Pair with a tiny custom inspector that populates the
`track` dropdown from `director.playableAsset.outputs` so the designer picks from the actual
tracks rather than dragging blind.

### 16.2 `TimelineSignalRelay` — the cross-scene action bridge (THE fix for your Signal problem)
This is the piece that makes "the signal track needs an Activation Track on that object"
stop being true. The trap: a `SignalReceiver` on a Persistent object fails when an Activation
Track toggles that object — and a Signal Track bound to a cross-scene object can't bind at all.
Solution: **the receiver lives on the director's own always-active GO in the area scene, and a
local relay forwards to the Persistent singleton.**

```csharp
// On the SAME area-scene GameObject as the PlayableDirector + its SignalReceiver.
// The SignalReceiver's UnityEvent (wired in Inspector) calls these methods.
// Each forwards to a Persistent singleton at RUNTIME — which crosses scenes freely.
public class TimelineSignalRelay : MonoBehaviour
{
    public void FadeFromBlack()   => FadeController.Instance?.StartFromBlack();
    public void FadeToBlack()     => FadeController.Instance?.StartToBlack();
    public void HideHUD()         => HUDController.Instance?.SetVisible(false);
    public void ShowHUD()         => HUDController.Instance?.SetVisible(true);
    public void StartTutorial()   => GetComponent<TutorialDirector>()?.StartTutorial();
    public void FireCheckpoint(/* int index if needed */) { /* → TutorialDirector step */ }
    // one method per cross-scene action the timeline needs; all null-guarded
}
```

Editor wiring (the part the user does, once per signal):
1. On the Signal Track, the emitter references a `SignalAsset` (project asset — serializes fine).
2. Add a `SignalReceiver` component to the **director GO** (same GO, guaranteed active —
   nothing toggles the director while it's playing its own timeline).
3. In the `SignalReceiver`, map each `SignalAsset` → a `TimelineSignalRelay` method above.
4. The Signal Track's binding field points at **that local SignalReceiver** — a same-scene
   binding, so it serializes correctly.

Why this dodges the Activation-Track trap entirely: the receiver is never on a Persistent
object a track might deactivate; it's on the director, which is alive for the whole timeline.

### 16.3 What must NOT be done (enforces R11 + Banned Lazy Work)
- Never hand-edit `m_SceneBindings` / `.playable` YAML to fake a cross-scene fileID — it will
  not resolve and corrupts the asset.
- Never resolve a binding by GameObject-name or track-name string (use type for
  single-instance tracks, or the explicit `TrackAsset`→role Inspector map of §16.1 for
  disambiguation).
- Never put a `SignalReceiver` for a cross-scene action on the Persistent target itself.
- Never bind an Activation Track to a gameplay-logic object or a manager ancestor (R11) — the
  rescue-checkpoint bug (BUG-021) and BUG-W15 are both this.
- Deleted targets (BUG-032's Activation 1/2/10/11/20/21) are **removed by hand** in the
  Timeline window — no code can find a deleted object; do not stub fake targets to silence it.

### 16.4 Acceptance (add to the relevant phase DoD + a BUGS.md Verified line)
Play the tutorial timeline from Bootstrap: camera cuts work (Cinemachine rebind), any twin
animation tracks drive the real Persistent twins, every fade/HUD signal fires once at its
marker, the tutorial starts on its end signal, and the console shows zero null-binding errors.
Then play it a second time after a soft reset to confirm the resolver re-runs cleanly.

---

## 17. EXEMPTION LEDGER — rules the game knowingly violates (do NOT "fix")

Every entry states what, why, and the standing order. A rule violation **not** in this ledger
is still a bug. Adding an entry requires the user's explicit sign-off in-session.

### E1 — AIFramework `MonoBehaviourSingleton<T>` keeps DDOL + lazy fabrication (violates R3)
`CommonCore/Singletons/MonoBehaviourSingleton.cs` unparents + `DontDestroyOnLoad`s every
derived singleton in `OnAwake()`, and its `Instance` getter fabricates a blank GameObject
when none is found. The Phase 1.4 "correction" was implemented once and **completely broke
the enemy/perception system**; the current behaviour is **deliberate and locked by the user
(2026-07)**. Standing order: **do not reattempt, ever** — not in a refactor, not during the
P19 restructure, not because a future review flags it again. Scope: AIFramework-derived
singletons only; project-side hand-rolled singletons follow R3 as written. Mitigations live
in CLAUDE.md's footguns (blank `Singleton<T>` ghosts; never rely on fabrication for gameplay
managers). Phase 1.4c (static-state restart hygiene) is separate and remains valid.

---

## 18. PHASE ROADMAP P10–P19 (2026-07-03 architecture review; execution order = numbering)

| Phase | Deliverable | Spec | DoD essentials |
|---|---|---|---|
| **P10** ✅ 2026-07-03 | Doc-truth + ledger sweep | this revision | CLAUDE.md corrections (R10 live, E1, SoftReset verified-fixed, Phase-4 landed) · game.md §20.4/§23.13–16/§24.8/§25/§26 · ArtStyle.md §10–11 · this ledger + roadmap · BUGS.md sweep |
| **P11** ✅ 2026-07-04 | Manpu held mood loop + `EnemyVFXController` retirement (13 files) | game.md §24.8 | **Code done, 0 CS errors.** Capability (held loop + de-gate + leak fix) landed; controller deleted, all 13 sites migrated; dark-energy = held corruption STATE cue on `EnemyDarkEnergy` (user call, not a mood); commanders kept as `TODO(§24.8)` stubs. **Remaining in-editor (see the two authoring steps below the table)** → then verify: aura survives pool reuse · no leaked loops on despawn/unload · Severed aura ends with grief-rage · dashboard "no EnemyVFXController" green |
| **P12** ✅ 2026-07-04 | GameDebugger v2 + TestLab scene | game.md §23.15.1 | **Built, 0 CS errors.** `GameDebuggerV2.cs` (IMGUI panel, toggle **Ctrl+`** rebindable combo — F9 collided with the profiler; auto-snaps twins to the pad on Start — they fell through at their level-authored position; `DevConfig.Trainer`-gated) + `Scenes/Sandbox/TestLab.unity` (verified NOT in Build Settings; runtime NavMesh bake; zero-config auto-fill). Spawn = the real pooled path; damage/kill/stun/possess/mood/force/perception/cue/points/teleport live. `EnemyPool.Instance` added (standard pair). v1 gaps in-source: TetherBreaker chain (BT-internal), Severed pair-rage, SiphonGhost. **Remaining:** open TestLab → Play → Ctrl+` smoke-run (also doubles as the P11 aura DoD once vocab auras are authored) |
| **P13** ✅ 2026-07-04 | New Input System migration | below + game.md §26.1 | **Code done, 0 CS errors; MCP regression passed** (asset resolves, synthetic-key reads: Move/Attack/Struggle/Pause/SkillTree/Overview/QTEMash/Rescue/Convergence/AnySkip all fire; gate fail-open verified). `PlanetOfTwins.inputactions` (13 Gameplay + 3 UI actions) wired on `PlayerManager`; reader guts swapped to Input System polling — **gate seam untouched** (checks inside getters, actions never per-category disabled); `IInputProvider` +5 ungated methods; ALL raw-Input gameplay consumers migrated (Pause ESC chain, Overview B, SkillTree Tab, QTE mash ×2, intro any-key); dev keys guarded (TutorialDirector Ctrl+F9, DamageDealerDebug Trainer-gated). Move composite = `2DVector(mode=1)` (legacy GetAxisRaw parity). **Remaining human DoD → checklist item 5 below** (full Bootstrap tutorial-unlock run · direct-area fail-open · four entry paths) |
| **P14** ✅ 2026-07-04 | Scene Health Dashboard + NewAreaSceneWindow full kit | game.md §23.15.2/.4 | **Built, 0 CS errors; engine verified on real scenes.** `SceneHealthRules` (5 per-scene recipes: Must-haves/Wiring/Counts/Timelines/Volumes + 2 project recipes: Enemy prefabs/Build Settings) + `SceneHealthDashboardWindow` (Tools ▸ PoT ▸ Scene Health Dashboard — scene×recipe grid, additive Scan All, findings pane w/ Select). First scan: L1_Park Timelines FAIL = **BUG-032's 7 null bindings caught by the tool**; enemy prefabs 9/12 (3 commanders lack ManpuSlot — deferred roster); zero missing-script ghosts post-P11. NewAreaSceneWindow scaffolds the full kit (ZoneConfig+subs assigned, entrance, load trigger pre-wired to the new WorldLocationSO, optional QTE anchor, prio-10 identity volume). **Remaining:** "both areas pass or carry named waivers" = a content-authoring pass over the dashboard's findings (user-paced, not code) |
| **P15** ✅ 2026-07-04 | Upgrade-tier VFX (`_t[n]` suffix model) + upgrade-data editor | game.md §23.16/§23.15.3 | **Built, 0 CS errors; resolver play-verified (7 cases through the real `TryPurchaseNode`).** USER CALL: suffix (not prefix, not per-node override — the interim `cueIdOverride` field was replaced same-day, no assets used it). Tier variants = `<baseId>_t[n]` in the SAME book; `UpgradeCueResolver.Resolve(book, data, defaultId)` tries `_tN`→…→`_t1`→base **per id** — an id without variants serves ALL tiers (sub-id opt-in: tier one of 3 ids, rest untouched). Plumbed at EVERY tree cue id (Stun ×2 · Possess ×2 · Empower ×2 · Accord ×6 · SoulConv ×5 · Gate = pulse_fire + 4 tele_* · Coalesce on_aura); AccordSpirits skipped (no data store). **Authoring hint lives in the editors** (CueBookDataEditor help box on every book + UpgradeDataEditorWindow rule box + per-node `_t{n}` column) per user request. `UpgradeDataEditorWindow` = node table, unused columns auto-hidden, Undo |
| **P16** ✅ 2026-07-04 | `GameplayPool` (categorized) + migrate spawn sites | game.md §25.4 | **Built, 0 CS errors; play-verified** (reuse, double-return guard, version-stamped delayed despawn, reparent-home, `SpawnReady`). All 9 sites pooled: Arrow/Bomb/Chain = Projectiles · orb/spirit/CoalesceAura/SC-shield = AbilityObjects · SiphonGhost = Summons · **Witness minion → `EnemyPool.SpawnReady`** (it's an Enemy — full canonical spawn, was a raw Instantiate). `GameplayPoolRoot` in Persistent (saved). Latent SiphonEnemy ghost-lambda bug fixed (named handler — pooling made it real). FOUND (authoring, not code): `SmartEnemyRanged` prefab has **no `_projectilePrefab`** on its EnemyAttackController — wire the Arrow prefab. **Remaining:** author a `SpawnPrewarmProfile` asset + assign on GameplayPoolRoot (optional, perf); TestLab soak of chain/ghost reuse (user DoD) |
| **P17** ✅ 2026-07-08 | `StoryGradeDirector` + 6 grade profiles + FailureReset profile | ArtStyle.md §11 | **Built, 0 CS errors; play-verified in TestLab** (boot `act1_warm` no-fade · crossfades complete both directions w/ A/B role-swap · `shock` hard-cuts · unknown id LogErrors · sting weight 0→1→0). `Grading/StoryGradeDirector.cs` (Persistent A/B two-Volume crossfade, **unscaled**; rows act1_warm 0 / shock event-only hardCut / early_fear .15 / mid_purpose .35 / late_chaos .60 / ending_losing .85) + `GradeProfileAuthoring` menu (idempotent) + 7 profiles in `Settings/Grading/`. `FailureResetSequencer` reworked to drive its **dedicated prio-30 sting volume's WEIGHT** (slot was unwired+silent; now wired+fail-loud). **No runtime caller yet:** `SetStoryProgress(0..1)`/`PlayGrade("shock")` is the seam — beat→progress mapping lands with checkpoint/story data (CheckpointManager has no story flags today). **Remaining → checklist item 8** (tune profiles in Unity) |
| **P18** ✅ 2026-07-08 | Cue Book control additions | game.md §23.14 | **Built, 0 CS errors; runner harness + play smoke verified.** `isVariant` groups (consecutive marked elements = one group, one random pick per Play; skipped members transparent to scheduling — successors chain off the CHOSEN one; cuts to/from skipped dropped; editor Variant toggle; linter F6/F7). Shake: `Custom` shape honours a per-element `shakeCustomShape` curve; `shakeRange` metres = 0 Uniform (unchanged default) / >0 Dissipating from the cue position (distant cams feel less); per-cam gain = the listener's `Gain` (authoring). **Material-float element DROPPED (user call):** per-object materials make a book-level property field fragile + project-specific (P19 package goal) — `MaterialRevealDriver` on the carrying prefab stays the mechanism (untouched); §23.14 item 3 struck with reasoning |
| **P19** ◐ stages 1–3 ✅ 2026-07-08 | Package prep (seams + asmdef carve) → staged folder restructure | game.md §23.13 + §20.4 | **Seams + carve BUILT, 0 CS errors, play-verified; one isolated commit per stage.** S1: `IFxSceneEvents` (unload scene-name + location-audio events; SceneFlowManager implements; FxManager/MusicManager slots wired in Persistent — Instance fallbacks removed; census correction: MusicManager was a 3rd seam) + `IManpuGlyphTarget`. S2: `ManpuMood`/`ManpuSearchState` mirror enums (int-identical, append-only; vocabulary asset intact 13+5 rows); glue (Director/MoodAmbient/listeners) = the §24.8 adapter, moved project-side. S3: **`PoT.Fx` (89 types) + `PoT.Manpu` (22) asmdefs compile with zero Assembly-CSharp refs**; CameraCueDriver → Fx/; glue → `Scripts/ManpuAdapters/` (GUID-safe moves). **Remaining, deferred with user visibility:** `namespace PoT.Fx` sweep (touches every consumer — do inside the §20.4 restructure), literal empty-URP-project compile (needs a second project; asmdef isolation = in-project proof), package.json/Runtime-Editor layout, §20.4 folder restructure (user-scheduled, between content milestones) |

**USER IN-EDITOR CHECKLIST (the standing "don't forget" list — tick items off here as they
are done; everything is null-safe until done — no errors, just no aura/panel):**
1. ◐ **P11 vocabulary auras — glyphs+accents DONE, sustained loop still open (verified 2026-07-13):**
   `ManpuVocabulary` mood rows ARE authored — 10/13 moods carry a glyph sprite + burst accent
   (Confident/Opportunistic/Aggressive/Enraged/Cautious/Wounded/Frustrated/Panicked/Curious, plus
   Grieving glyph-only; Normal/Contemptuous/Territorial deliberately blank), perception 4/5.
   STILL OPEN (optional polish): the sustained **`loopPrefab`** aura is **0/13** (the held-loop
   mechanism is built but no aura prefab is assigned) and the **ability** layer (Stun/Possess/Grab/
   Freeze `held` glyph + `closingSequence`) is **0/4**. The glyph pulse + burst already read mood, so
   these are enhancement, not blockers. To add a loop: drop a ParticleSystem on `loopPrefab` for
   Enraged/Panicked/Aggressive (starts on mood ENTER, stops on EXIT/pool).
2. ✅ **P11 corruption state — DONE (verified 2026-07-13):** wired on **all 13** `EnemyDarkEnergy`
   prefabs as `_corruptionStateBook = CommonCueBook`, `_corruptionStateCueId = poi_corrupt` — it
   **reuses the Common book's held corrupt aura**, NOT a separate `CorruptionStateCueBook` (that
   idea is superseded — do not create one). Plays once at bond-break, holds until death/despawn.
   (Polish only: give `poi_corrupt` the Voreth-wild palette per ArtStyle.md §10 if it needs a variant.)
3. ☐ **P11 DoD run (after 1+2, in TestLab):** aura survives pool reuse (kill → respawn same
   archetype) · no leaked loops on despawn/unload · Severed aura ends exactly with grief-rage.
4. ☐ **P12 TestLab round-2 test (2026-07-04 fixes):** panel no longer half-cut — it is a
   draggable window clamped to the Game view (drag by title bar) · **Ctrl+`** toggle works ·
   twins snap to the TwinPad (no fall-through) · new **Stop FX on selected** / **STOP ALL FX**
   buttons end lingering code-ended cues (fire a looping cue or set a mood aura, then stop it).
5. ☐ **P13 acceptance (after the input migration lands):** full Bootstrap tutorial run with
   progressive unlock · direct-area play fail-open · all four entry paths · TestLab
   action-by-action regression (the P13 DoD below — machine-checkable parts run by Claude,
   feel/tutorial run by user).
6. ✅ **P16 authoring — DONE (verified 2026-07-13):** ranged enemies shoot. Projectile config lives
   on **`EnemyData`** (`useProjectile` + `projectilePrefab`, applied at runtime via `SetProjectile`),
   NOT a serialized field on `EnemyAttackController` (the old note here was wrong): `E_RangedData →
   Arrow`, `B_RangedData → tahrArrow`, both `useProjectile = true`. A `SpawnPrewarmProfile` asset
   exists (1) — assign it on `GameplayPoolRoot` in Persistent if not already (bomb + chain are the
   heavy first-use prefabs).
7. ☐ **P16 soak (TestLab):** TetherBreaker chain throw ×3 (reuse — marker/beam/drag clean
   each time) · Siphon ghost rescue twice in a row (second ghost = reused instance, fresh
   kill window) · Coalesce aura on a killed-mid-aura enemy (linger detaches, no drag into
   the pool) · bombs from Witness + Siphon (fuse never leaks).
8. ☐ **P17 grade tuning (Unity):** the 7 profiles in `Assets/Settings/Grading/` carry the
   ArtStyle §11 *starting* values — eyeball each on real scenes and tune (esp. Shock's CA 0.4
   and LateChaos split-tone). Test from TestLab: Play → `StoryGradeDirector.Instance
   .SetStoryProgress(x)` / `.PlayGrade("shock")` via the debugger console, or ask Claude to
   drive it over MCP. Trigger a tutorial failure to see the reworked sting (desat+vignette+CA
   ride one weight blend now).
9. ☐ **FOG systems (2026-07-24 — full checklist in [SETUPGUIDE.md](SETUPGUIDE.md) §5D):** there are
   FOUR fog/shaft systems, tuned in different places — don't conflate them (SETUPGUIDE §5 map).
   (a) **Global god-ray fog** = CristianQiu Volumetric Light: verify `VolumetricFogRendererFeature`
   on `PC_Renderer` + **Depth Texture ON** on `PC_RPAsset` + `FogVolume` (Persistent) is a **Global**
   Volume with the **Volumetric Fog** override `enabled` and `enableMainLightContribution` ✓ →
   density/distance/god-rays are tuned on that **override, NOT a material** (the material is hidden).
   (b) **Local placed fog** = `PoT/LocalFogVolume` (reworked to a real raymarched box volume): Cube +
   **no collider** + material, scaled to the area — drifts with the wind in Play; re-tune `_Density`
   (~0.3 → ~0.5+ post-rework) on placed instances. (c) confirm the retired `PoT/VolumetricFog` /
   `GameCameraShadowFullScreenFeature` are **off** `PC_Renderer`. (d) **DECISION:** keep-or-retire the
   old `M_SunShafts` god-rays vs the new CristianQiu shafts (overlap; retiring ends the BUG-077 chore).

**CONTENT COMPLETENESS SNAPSHOT — fresh AssetDatabase audit 2026-07-13 (authoritative; supersedes
any stale checklist assumption above). These are the REAL remaining content gaps:**
- **Enemy pair configs: 2 of ~6+** — only `MeleePairConfig` + `SeveredConfig` exist (both populated),
  referenced only in `streetZone`. The rest of the combat roster (B_/E_ Ranged, GroupGrab, Summoner,
  cross-clan) currently spawns solo. Author more `PairSpawnConfig`s as the roster firms up.
- **Area spawn authoring: 1 of 3 zones populated** — `streetZone` = 4 entries; `L3_Alley` and
  `L4_Mueseum` `AreaZoneConfig`s exist with all sub-SOs assigned but **0 enemy entries** (nothing
  spawns there — empty shells). Only 3 zone configs exist for a world planned past L4.
- **Combo/pact system — decision layer DONE, EXECUTION stubbed (verified 2026-07-13):** pact
  formation (dark-energy-gated `ComboReadyRegistry`) + power SELECTION (`EnemyProximityPower
  .SelectPower` against **9 authored `ProximityPowerProfile` assets = 25 combo powers** with real
  clan / dark-energy-threshold / execution-range / twin-HP-band / partner-raging conditions; every
  combat prefab + all 3 commanders wired to a profile) are fully built and drive the GOAP blackboard
  (`ComboReady`/`NearCompatibleAlly`/etc.). BUT `BTActionComboAttack` executes NONE of it — all **23
  combo cases are `Debug.Log` only** (its own docstring: "replace with real attack logic"). So enemies
  correctly form a pact, gate on dark energy, and pick the right combo id — then do nothing. This is
  the single biggest "built-but-unfinished" item. (Note: the runtime pact system is INDEPENDENT of the
  spawn-time `PairSpawnConfig`; pacts form dynamically from proximity + energy regardless of pair configs.)
- **Stub archetypes (log-only, no gameplay effect):** `ChainCommander.ChainStrike` (damage line
  commented out), `PenitentCommander.DarkShield` (invuln commented out), `GrandSummoner.DivineShaft`
  (buff not re-expressed), `PenitentEnemy` (reflection-rage retired — code says "dropped, needs
  rework"), `BossEnemy` (death / level-complete is a bare TODO), `TimelineSignalRelay.HideHUD/ShowHUD`
  ("not implemented" LogWarning — cutscene HUD toggle). All 3 commanders also lack `ManpuSlot`.
- **Manpu polish (optional, non-blocking):** sustained mood `loopPrefab` auras 0/13; ability glyph
  layer (held + closing) 0/4.
- **VERIFIED DONE (was wrongly flagged open in an earlier non-fresh pass):** corruption state (Common
  `poi_corrupt`, all 13 prefabs) · mood glyphs + burst accents (10/13) · perception glyphs (4/5) ·
  ranged projectiles (`EnemyData` on both ranged variants) · prewarm profile (1 exists).

**✅ DELIVERED 2026-07-08 → [TESTGUIDE.md](TESTGUIDE.md)** (repo root — Section A setup ▸
Section B per-phase what/how/expected ▸ Section C tool shelf ▸ Section D deferred notes).
The original directive, for reference:
**FINAL DELIVERABLE AFTER P19 (user directive 2026-07-08 — Claude writes, user executes):**
one consolidated **USER TEST & SETUP GUIDE** covering, for every phase P10–P19: (a) everything
NOT machine-verified that the user must check — each with *what to check · how to test it ·
the expected result*; (b) every "note to user" accumulated across the phases (this checklist's
items 1–8 folded in); (c) all remaining in-editor setup/wiring steps; (d) every tool built
(GameDebugger v2 + TestLab, Scene Health Dashboard, Upgrade Data Editor, CueBook editor/linter/
verifier, NewAreaSceneWindow, grade-profile authoring, Manpu vocabulary editor) with *how to
use it and when to reach for it*. Write it when P19 closes, before anything else.

**P13 HARD REQUIREMENT (user, 2026-07-03): the migration must not break the player or the
tutorial gating/unlock system.** The gate seam stays exactly where it is: `TutorialInputGate`
is area-resident, push-registers via `TwinInputReader.SetGate()`, filters **per category**,
consumers fail **open** (null gate = all input allowed). In the new implementation the gate
check remains **inside the `IInputProvider` getters** (read action value → apply gate →
return) — **never** by enabling/disabling `InputAction`s themselves (that would break
per-category fail-open semantics and area-scene gate registration). ESC keeps routing through
`PauseMenuController`'s priority chain (the overlay/pause double-consume precedent); Setsuna's
F-hold semantics are preserved. Debug keys L/O/P/I/K go behind the dev guard in the same
pass. **P13 DoD additions:** full Bootstrap-path tutorial run with progressive unlock
verified (each category locked until its step unlocks it) · direct-area play with NO gate
verified (fail-open) · all four entry paths · TestLab action-by-action regression.

P8 (§12) remains the standing production-discipline backlog — P8.7 lists the 2026-07-03
additions. The pre-P10 phases (0–7.x, 9) are complete except where BUGS.md says otherwise.

---

## 19. FUTURE ADDITIONS LEDGER (designed, not scheduled — do not start without asking)

### F1 — PSO / shader prewarming (design ready; build at the first build-perf pass)
Problem: first-use PSO compilation hitches — this is a VFX-heavy game. Unity 6 API:
`GraphicsStateCollection` (trace at runtime → save → warm at boot).
- **`PsoTraceRecorder`** (dev-only, TestLab): `BeginTrace` on scene enter → fire-everything
  pass (every cue book/id + every enemy via GameDebugger v2) → `EndTrace` + `SaveToFile` →
  `Assets/Settings/PsoCollections/<platform>/`.
- **`PsoWarmupStep`** in `GameBootstrapper`: after Persistent loads, before Intro —
  `WarmUpProgressively` async under the boot screen with progress; **fail-open**
  (`LogWarning`, continue) when the collection is missing/version-mismatched.
- Caveats (researched 2026-07-03): API is new/evolving; **coverage gaps** — a trace only
  warms what was traced (TheGamedev.Guru case study), so the trace protocol is TestLab
  fire-everything **plus one full level run** per platform per content update; collections
  are per-platform/quality-tier; Unity 6.5 adds cache-miss **append** (adopt when on 6.5).

### F2 — Couch co-op device mapping (after P13; analysis: game.md §26.1)
### F3 — Networked co-op (game.md §26.2 — stack decision comes first, then a movement+one-enemy vertical slice)
### F4 — Palette application in-engine (ArtStyle.md §10 hexes — the user validates in Unity)

### F5–F12 — Post-playtest scope (user, 2026-07-10 — start AFTER the current playtest batch is user-verified)

- **F5 — Button HUDs.** ✅ DONE (2026-07-16). On-screen input prompts for UI-reachable systems (skill tree etc.) —
  which key/button opens what, shown in context. Should read from the Input System action asset
  (P13) so prompts stay true under rebinding (F6).
  **STATUS: LANDED.** Generic `InputPromptView` (screen-space HUD, Persistent, R9) reads live
  bindings via new `IInputProvider.GetBindingDisplay(actionName, preferGamepad)` on
  `TwinInputReader` (GetBindingDisplayString over the serialized inputactions asset — no
  hard-coded keys, rebinding-safe). Proof: `Persistent/HUD_Canvas/ControlHints` (5 rows). Runtime-
  verified resolving Shift/E/Q/C/F. Reuse the same component for any future contextual prompt.
- **F6 — Keymap screen + rebinding (RESEARCH FIRST).** Options-menu keymap view (button or
  similar entry point). Then research interactive rebinding: Unity Input System
  `PerformInteractiveRebinding` is the standard path — assess (a) conflict detection: a
  conflicting key must NOT save — revert on the button and turn BOTH conflicting UI slots red;
  (b) deliberately-shared bindings: some actions legitimately share a control (user example:
  RMB and LMB both assigned) — need an allow-list/segregated binding groups so "conflict"
  only means unintended collisions; (c) persistence (`SaveBindingOverridesAsJson`).
  Deliverable of the research = feasibility + effort writeup BEFORE building.
- **F7 — Pause button full correct wiring.** Audit the whole ESC/pause chain end-to-end
  (PauseMenuController priority chain + overlay double-consume precedent) — currently not
  fully correctly wired everywhere.
  **Status (2026-07-16): mostly DONE.** ESC chain verified single-owner (only
  `PauseMenuController.Update` reads `GetPauseDown`; overlay/skilltree no longer double-read —
  the §5.6 double-consume is gone), ordered returns = single-consumption. Fixed: pause now
  hooks gameplay audio — `OpenPause`→`AudioManager.SetPaused(this)`+`RequestSnapshot(Paused)`,
  `Resume`/`ExitGame` release (F4 was unwired: `SetPaused`/snapshot had NO callers). Added
  `IInputProvider.ResetBindingsToDefault()` (+gate/reader) and a `RestoreDefaultKeybinds()`
  handler + optional `_restoreKeybindsButton` slot on `PauseMenuController` (refreshes
  `InputPromptView`s), for F6 to rely on. Options/settings panel is fully authored & wired
  (language/resolution/window/cursor/mixer+3 volumes). **REMAINING (Inspector, manual):** add
  the physical "Restore Default Keybinds" Button GameObject under the pause MenuCard and assign
  it to `PauseMenuController._restoreKeybindsButton` (code is a safe no-op until then). Play-mode
  ESC verification still to run by a human.
- **F8 — Trap timing correction.** Current traps cycle too slowly to threaten and their dormant
  window is wrong: activation should come on QUICKER and the dormant phase should last LONGER
  than now. Tunables live on the trap; re-tune + verify like a player.
- **F9 — World-space pickup UI.** ✅ DONE (2026-07-16). A world UI when picking up certain items (e.g. melee pickup) —
  prompt/feedback at the item, R9-compliant (WorldSpaceCanvasCamera).
  **STATUS: LANDED.** Generic `WorldSpacePickupPrompt` (area-resident, self-contained, R2/R9):
  billboarded world prompt, marker mode for auto-walk-over pickups (hides on consume) + optional
  proximity/key-glyph modes. Wired onto both `L1_Park` `Melee` sword pickups. NOTE: the deeper
  billboard/scale/occlusion quality bar is F10 (RESEARCH FIRST) — this is the functional prompt.
- **F10 — World UI quality pass (RESEARCH FIRST).** Existing camera-facing/billboard world UIs
  are not properly made (facing broken/ugly). Research how strong games do readable,
  informative, non-cluttered world UI (health bars, prompts, markers), then rebuild ours to
  that bar: correct billboarding, distance scaling/fading, occlusion behaviour.
- **F11 — UI shader identity (RESEARCH FIRST).** We now have UI shader support — research the
  "Street Fighter-style" stylized UI treatment class (energetic ink/brush/impact UI FX) and
  design a Planet-of-Twins-unique UI effect language from it (bond/soul-light/dark-energy
  motifs). Deliverable: reference board + 1 prototype shader before rolling out.
- **F12 — World-material master ShaderGraph (RESEARCH FIRST — read the Colour Bible before
  designing).** ONE baseline shader for all world materials, per-material inputs:
  (a) its own texture slot(s) + base tint (no textures authored yet — slots must exist);
  (b) a CLAN colour input (ArtStyle §10 ramps); (c) a happy→sad story/mood gradient that
  shifts the material's colour (drivable per-scene/globally — pairs with the P17 grade arc);
  (d) clan ELEMENTS on the material (sigils/patterns — likely UV/mask-based; user asks
  whether UV is the right mechanism or something better exists). Research pass over Unity
  shader examples (triplanar vs UV masks, detail maps, vertex-colour masking, global shader
  variables like `Shader.SetGlobalFloat` for the mood float) → propose THE approach, get
  sign-off, then build the graph as the world-material baseline.
  **STATUS 2026-07-15: substantially LANDED as `PoT/Coexistence` (hand HLSL, not a graph)** —
  base texture+tint, clan edge colours, `_WorldCorruption` global (driven by WorldAmbienceDriver).
  Remaining: the material LIBRARY (Assets/Art/Materials/Coexistence/ named variants, applied
  per-material — NEVER as Unity's default material) + clan sigil/pattern masks (route A/B open).

- **F13 — Both-twin ability cam + split-screen beat (user design, 2026-07-15 — scoped, do not
  build until ability refinement).** ONLY for simultaneous both-twin abilities (Accord state
  activation, joint spirit send): two per-twin ability vcams driven by ONE shared CamRig
  animation clip (author once, anchor to either twin — Valorant-knife model), rendered to two
  viewport halves for the beat, quick wipe in/out, then blend back to the group cam. Guard:
  twin grabbed/stuck/dead → skip split, full-screen focus on the free twin. Costs to solve at
  build time: HUD split-mode layout, single AudioListener, ~2× render for the beat. Common
  CLOSE ability vcam (TargetGroup, separation-gated) is the phase-1 half of this item.
- **F14 — Screen-space sun shafts tuning + celebration/lighting props wind pass (2026-07-15).**
  Shafts BUILT (PoT/SunShafts + SunShaftsDriver + CoexistenceShafts feature) — needs visual
  tuning once the grading white-out is resolved. Wind system for decorative props (shimenawa
  rope, lanterns, leaves) designed in chat — global _WindDir/_WindStrength shader globals +
  vertex-sway shader + Cloth/bone rope + ParticleSystem external forces; build with the
  celebration-prop batch.
