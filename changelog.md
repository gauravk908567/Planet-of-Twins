# Changelog

All notable changes to **Planet of Twins** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project aims to follow [Semantic Versioning](https://semver.org/) once builds are tagged.

This log starts fresh on **2026-06-06** and tracks changes from this point forward. For
how each system works, see [game.md](game.md); for working in the repo, see [CLAUDE.md](CLAUDE.md).

---

## [Unreleased]

### Fixed/Added — Tutorial timeline + cutscene lock + wrong-twin reset (2026-06-14)
Supersedes the intermediate `_twinTrackBindings`/`TwinRole` resolver design in the entry below.
- **Cross-scene Timeline binding — registry-based (final design).** New
  `TimelineTargetRegistry` (Persistent, holds **same-scene R1 refs** to every cross-scene
  target) + `TimelineBindingResolver` (area scene) that finds the registry by type (R4) and
  resolves each track→role by type/singleton — **no name strings**. This solves the
  "two objects of the same type" problem (e.g. two transpose cameras) that `FindAnyObjectByType`
  can't. Roles: `CameraManager`, `FadeCanvas`, `HudCanvas`, `TransposeClose`, `TransposeTop`,
  `SkyboxChanger`; the lone `CinemachineTrack` auto-binds by type (no row). Files:
  [TimelineTargetRegistry.cs](Assets/Scripts/SceneLaoder/TimelineTargetRegistry.cs),
  [TimelineBindingResolver.cs](Assets/Scripts/SceneLaoder/TimelineBindingResolver.cs).
- **BUG-032 classification corrected** (recovered by diffing the pre-multiscene `L1Park.unity`
  and tracing each fileID): of the 11 null bindings —
  *Cinemachine / Signal→CameraManager / Animation 7→FadeCanvas / Activation 8→FadeCanvas /
  Activation 22→HUD_Canvas* → **Persistent, restore via resolver**;
  *Activation 1/2 → `TutorialGroupTransposeClose`/`Top`* → **MOVED to Persistent (NOT deleted)** —
  restore + toggle them off during the cutscene;
  *Activation 20/21 → `Lyra`/`Kai`* (the **twins**, not "nameplates" — my earlier mislabel) →
  **delete the tracks** (twin lock handled in code, below);
  *Activation 10/11 → `MainLvl (1)/(2)`* → **delete** (handled by multiscene streaming).
- **Twins now locked for the cutscene's duration.** `IntroTimelinePositioner` locks both twins'
  movement on `director.played` and unlocks on `director.stopped` (the old single-scene setup
  deactivated the twin GOs via Activation 20/21; in multiscene we must not toggle Persistent
  twin GOs — R11/BUG-W15). Covers every entry path, unlike `IntroController`'s boot-only lock.
  Fixes "could move twins during the cutscene." File:
  [IntroTimelinePositioner.cs](Assets/Scripts/SceneLaoder/IntroTimelinePositioner.cs).
- **Wrong-twin reset crossing fixed (scene).** The SharedHealth dual-checkpoint reset sent Lyra
  to Kai's side and vice-versa: the two checkpoints' `leftResetPoint`/`rightResetPoint` pointed
  at the transforms parented under the *opposite* checkpoint. Swapped the references
  (`leftResetPoint`→`1825711431` under the left checkpoint, `rightResetPoint`→`1481096354` under
  the right) on both checkpoints in `L1_Park.unity`. The reset chain itself (detection →
  `WrongTwinResetHandler` → `FailureResetSequencer`) was verified fully wired.

### Added — Cross-scene Timeline cookbook implementation (instruction.md §16) (2026-06-13)
- **`TimelineBindingResolver` rewritten to the §16.1 spec (role-based, no name strings).**
  Dropped the rejected `namedLookups`/`GameObject.Find` string approach. Now: the lone
  `CinemachineTrack` resolves **by type** (`FindAnyObjectByType<CinemachineBrain>()`, zero
  authoring); twin Animation tracks resolve via an explicit `TrackAsset`→`TwinRole` Inspector
  map (`_twinTrackBindings`) — a real serialized asset link that survives track renames and
  can't be typo-broken. Fails loud (`Debug.LogError`) on any null target. `ApplyBindings()`
  stays public for re-apply after soft reset. R4 `Start()`-time resolve (director is
  `m_InitialState: 0`, so it lands before `Play()`).
  File: [TimelineBindingResolver.cs](Assets/Scripts/SceneLaoder/TimelineBindingResolver.cs).
- **New `TimelineSignalRelay` (§16.2) — the cross-scene *action* bridge.** Forwards Timeline
  Signals to Persistent systems at runtime so the `SignalReceiver` can live on the local
  director GO (never deactivated mid-timeline → dodges the Activation-Track trap, BUG-W15).
  `FadeFromBlack()`/`FadeToBlack()` call the existing Persistent `FadeController`
  (`StartFromBlack`/`StartFromClear`, resolved by type — it has no `Instance`); `StartTutorial()`
  forwards to the local `TutorialDirector`. HUD hide/show left as loud-logging stubs (no
  `HUDController` exists yet — documented in §16.2).
  File: [TimelineSignalRelay.cs](Assets/Scripts/SceneLaoder/TimelineSignalRelay.cs).
- **New custom inspector `TimelineBindingResolverEditor`** (Editor-only) — populates each
  twin-binding row's Track field from a **dropdown of the director's actual Animation tracks**
  so the designer picks a real track by name rather than dragging sub-assets blind; stores the
  `TrackAsset` reference.
  File: [TimelineBindingResolverEditor.cs](Assets/Scripts/SceneLaoder/Editor/TimelineBindingResolverEditor.cs).
- **Not done (user-side):** Timeline-window wiring (leave cross-scene bindings empty, add the
  twin rows, add a SignalReceiver on the director GO mapping signals → relay methods, remove
  the 6 dead tracks). Compile/verify pending Unity (MCP was disconnected this session). BUG-032
  → In-Progress.

### Fixed — Tutorial progression + timeline bindings (2026-06-13)
- **Tutorial stuck at `SwitchUnlocked`, gate QTE never fires** — `TutorialGateQTEBounds`
  (`TutorialZoneTrigger.requiredTwin = Both`) had `leftTwin/rightTwin = {fileID: 0}`. Since
  both refs were null, `player == leftTwin` was always false and `_leftInside/_rightInside`
  were never set, so the `Both` condition never satisfied. Stage never advanced to `GateOpen
  (3)`, which meant `QTEZoneTrigger.requiredStage = GateOpen` never matched and the mash phase
  was unreachable. Fixed: added `Start()` to `TutorialZoneTrigger.cs` that R4-resolves
  `leftTwin`/`rightTwin` from `TwinSelector.Instance` when the serialized slots are null
  (cross-scene refs can't be serialized — R2).
  File: [TutorialZoneTrigger.cs](Assets/Scripts/TutorialSystem/TutorialZoneTrigger.cs).
- **Post-tutorial-timeline twins spawned in wrong area** — `IntroTimelinePositioner.PlaceTwins()`
  used `FindAnyObjectByType<AreaSpawnPoints>()` which, when both L1_Park and L2_Streets are
  loaded additively (SceneFlowManager adjacency streaming), could find L2_Streets'
  `AreaSpawnPoints` and teleport twins there. Fixed: iterate
  `FindObjectsByType<AreaSpawnPoints>()`, prefer the one in `gameObject.scene` (same scene as
  the director), then fall back to FindAnyObjectByType if none is scene-local.
  File: [IntroTimelinePositioner.cs](Assets/Scripts/SceneLaoder/IntroTimelinePositioner.cs).
- **Tutorial timeline Cinemachine Track / twin Animation Track bindings null** — per R11,
  cross-scene track targets (Persistent CinemachineBrain, twin Animators) can't be serialized
  from area scenes. Created `TimelineBindingResolver.cs`: attach alongside the PlayableDirector;
  Start() (R4) resolves `cinemachineBrain` via `FindAnyObjectByType`, twin Animators via
  `TwinSelector.Instance`; iterates timeline tracks and calls `SetGenericBinding` by track type
  (CinemachineTrack → Brain, AnimationTrack containing "Left"/"Right" → matching twin Animator).
  Wired the component onto `TutorialTimelineDirector` GO in L1_Park.unity (fileID 961604963).
  Files: [TimelineBindingResolver.cs](Assets/Scripts/SceneLaoder/TimelineBindingResolver.cs),
  [L1_Park.unity](Assets/Scenes/L1_Park/L1_Park.unity).

### Investigated — TutorialTimelineDirector full binding diagnosis (2026-06-13) → BUG-032
- **Corrects the line above** ("8 Activation Track null bindings are cosmetic" — that was
  wrong). Recovered the original bindings by diffing the pre-multiscene single scene
  `Assets/Scenes/L1Park.unity` (no underscore, still in git at HEAD) and resolving every
  original fileID to a GameObject name. The timeline was authored single-scene **before** the
  multiscene split and **before** the level re-greybox. Of 42 bindings, 31 resolve; the **11
  null** ones break down as:
  - **4 → Persistent (cross-scene, R2):** Cinemachine Track → `CinemachineBrain`/Main Camera;
    Signal Track → `SignalReceiver` on `CameraManager`; Activation 8 + Animation 7 →
    `FadeCanvas`/`FadeController`; Activation 22 → `HUD_Canvas`.
  - **1 → Persistent UI:** Activation 20/21 → `AbilityFeedbackDisplay` nameplate prefab.
  - **4 → deleted by re-greybox (unrecoverable):** Activation 1/2 → `GroupTransposeClose/Top`
    camera-framing groups; Activation 10/11 → `MainLvl (1)/(2)` geometry.
- **Pattern (now canonical, R11):** continuous cross-scene tracks (Cinemachine/Animation) →
  leave empty + runtime `SetGenericBinding` resolving the Persistent target **by type/singleton,
  not by name string**; cross-scene *actions* (fade/HUD) → **Signals** to a **local** receiver +
  **local relay** that forwards to the Persistent system at runtime (Signals alone do not cross
  scenes — the receiver/relay must be local). Dead tracks must be removed by hand in the
  Timeline window. **Never hand-edit `.playable`/scene `m_SceneBindings` YAML.**
- No timeline/scene edits made this session per user direction (user edits the Timeline
  themselves). Logged as **BUG-032**; full plan in `eager-cooking-crane.md`
  (role-based `TimelineBindingResolver` + dropdown custom inspector + usage guide).

### Fixed — QTE camera + gate wiring (2026-06-13)
- **`QTEManager.ReturnCamera()` wrong camera after QTE in tutorial mode** — method hardcoded
  `cm.CinemachineCloseCam` (gameplay cam) but the game runs with `CameraSwitcher.startInTutorialMode
  = true`, causing a one-frame incorrect blend then immediate snap to tutorial cam. Fixed: added
  `CameraManager.DemoteExternalCamera()` which zeros the QTE camera priority and clears the
  external-cam pointer without touching `_currentCam`; `ReturnCamera()` now calls
  `DemoteExternalCamera()` then releases `SuppressAutoSwitch(false)` — `CameraSwitcher.Update()`
  picks the correct camera (tutorial or gameplay) on the very next frame.
  Files: [CameraManager.cs](Assets/Scripts/Camera/CameraManager.cs),
  [QTEManager.cs](Assets/Scripts/QuickTimeEvents/QTEManager.cs).
- **Gate animation never fired after QTE success** — `QTESceneAnchor.activatableMono` was empty
  (`[]`) in L1_Park, so `FireSuccess()` had nothing to call `Activate()` on. Wired
  `GateActivatable` (fileID 391367839) into the `activatableMono` array in
  `L1_Park.unity`. Root cause: the Inspector field was never populated when the anchor was set up.
  File: [L1_Park.unity](Assets/Scenes/L1_Park/L1_Park.unity).
- **`m_targets` red errors on Bootstrap** — were thrown by Cinemachine components on the deleted
  L1_Park duplicate GOs (`Twins`, `SoulTwin`) which had null/invalid Cinemachine target
  references. Resolved as a side-effect of the R3 violation cleanup (previous entry).

### Added — Phase 9 spec (instruction.md §14) (2026-06-13)
- **`instruction.md §14 — Phase 9: FX & Audio Architecture`** — production-grade spec for the
  unified cue system. Data layer: abstract `CueData` + leaves (`ParticleCueData`,
  `VfxGraphCueData`, `SoundCueData`, `CameraShakeCueData`) + composite `CueSequenceData`
  (AfterPrevious / WithPrevious / delay / waitForCompletion — the "3 things one after another"
  contract, instruction.md §14.3). Runtime: `FxManager` (VFX pool, `CueHandle`
  version-stamping, sequence runner — plain C# class `CueSequenceRunner`), `AudioManager`
  (32 pooled voices, voice stealing, snapshot arbiter mirroring `TimeScaleService`, sole
  `AudioListener.pause` writer), `MusicManager` (A/B crossfade, location-change subscribe).
  Failure-class contract F1–F8 (scene unload, pooled enemy reuse, Setsuna, Pause, soft reset,
  Restart, editor direct-play, voice exhaustion). Mixer extension: Master → Music / Ambience /
  SFX / UI / Voice + snapshots Default / Paused / Setsuna / GameOver. VFX migration table
  §14.6 (4 tiers, 23 scripts). Audio hook-up table §14.7 (12 trigger sites). Three Banned Lazy
  Work additions (items 10–12). Sub-phases P9.1–P9.4 with DoD. Rules applied: R3, R4, R7, R8,
  R10; working-method #10 (spec before code). §12 gap table updated: rows 10–12 added (VFX
  system, audio system, assembly definitions).

### Added — Phase 9 spec — cross-doc (2026-06-13)
- **`CLAUDE.md`** — singleton table +3 rows (FxManager, AudioManager, MusicManager); FX/audio
  pattern note added to Notes & Footguns.
- **`game.md §17`** — VFX bullet expanded to Phase 9 migration pointer; Audio (Phase 9)
  bullet added (mixer groups, `WorldLocationSO` fields, 12 silent systems tracked).

### Fixed — L1_Park duplicate twin/soul GOs removed (2026-06-13)
- **`Twins`, `SoulTwin`, `TestPlayer` deleted from L1_Park (R3 violation):** All three root
  GameObjects were area-scene duplicates of Persistent-owned objects. `Twins` contained Kai,
  Lyra, Canvas_KaiUI, and RescueCanvas; `SoulTwin` contained SoulPlayer, PlayerAttackController,
  CharacterController, PlayerMovementController, and PlayerHealthComponent; `TestPlayer` was an
  inactive dev player. L1_Park dropped from 73 → 70 root GameObjects. Scene saved.

### Added — Phase 0 (entry paths)
- `BUGS.md` — living defect ledger seeded with 31 Open entries (all items from game.md §21)
  and 22 Watch entries (all failure-class forecasts from instruction.md §11). First entry
  in this work order; required before any Phase 0 code. Rule applied: instruction.md §13.
- `PersistentSceneAutoLoader.cs` (`SceneLaoder/`) — editor-only static class; fires at
  `RuntimeInitializeLoadType.BeforeSceneLoad`; loads `Persistent.unity` additively when
  Play is pressed directly in an area scene. Guards: no-op if Persistent is already loaded
  (Bootstrap path), no-op if active scene is Bootstrap or Persistent itself. Strips from
  builds entirely via `#if UNITY_EDITOR`. Rule applied: R3 (persistence = Persistent
  residency, not DDOL); satisfies instruction.md §2 step 0.1.

### Fixed — Phase 0
- `L1_Park.asset` / `L2_Streets.asset` (`Scripts/SceneLaoder/Data/`) — `SceneReference._name`
  fields contained pre-rename values `L1Park` and `L2Streets`; updated to `L1_Park` and
  `L2_Streets` to match actual scene filenames. Root cause: `WorldLocationSO` assets were
  created before the rename; `SceneReference` is name-only (no GUID), so the rename was not
  reflected automatically. Produced 6 errors on Bootstrap→Intro run (two in `IntroController`,
  four in `SceneFlowManager`). Build Settings already had correct paths. Rule: R7 (SO = config,
  names must match the live build). BUG-028 partial: assets exist; scene names now correct.

### Changed — Phase 0
- `SceneFlowManager.Start()` — in the Unity Editor, pre-opened area scenes are now also
  adopted as occupied (`_occupantCounts[loc] = 1`) in addition to being marked loaded.
  Prevents `RecalculateLoadedSet()` from unloading a pre-opened area on the first trigger
  notification before any twin has crossed a boundary. Guard is `#if UNITY_EDITOR` only;
  production paths are unchanged. Interim fix: int-count model replaced by per-actor tokens
  in instruction.md Phase 3.7a. Rule applied: instruction.md §2 step 0.2.

### Known issues — Phase 0
- **Build Settings order** — already correct (Bootstrap→Persistent→Intro→L1_Park→L2_Streets).
  User corrected this before the Bootstrap→Intro run. ✓
- **`PerceptionManager` duplicate-destroy on first Play** — `MonoBehaviourSingleton<T>` base
  applies DDOL, causing a ghost from the previous Play session to win over the freshly-loaded
  Persistent one. Logged as `[Error] Destroying duplicate CommonCore.PerceptionManager`.
  Singleton still functional (duplicate is destroyed). Tracked in BUG-019; fixed in Phase 1.4.
- **2 AudioListeners warning** — Intro.unity (or an area scene) contains an AudioListener
  alongside Persistent's. Tracked in Phase 1.5 (R9 sweep). Non-blocking for Phase 0 DoD.

**Phase 0 DoD status:** Verified. Bootstrap→Intro→L1_Park+L2_Streets run completed after Phase 1
work. No R4 fallback errors, no duplicate-manager errors. Remaining pre-existing Watch items:
- `PerceptionManager` auto-create warning fires before Persistent loads — non-blocking, scene
  instance wins (Phase 1.4 fix). BUG-019 resolved.
- `2 audio listeners` warning from Intro.unity cutscene camera — Watch item, Phase 6 scope.
- NavMesh errors — expected, not yet baked for current scene geometry.

---

### Added — Phase 1 (reference triage + singleton instances)
- Static `Instance` property + duplicate-destroy Awake guard + null-in-OnDestroy added to 9
  Persistent managers that lacked it: `TwinInputReader`, `TwinSelector`, `AccordStateSystem`,
  `EmpowerSystem`, `OverviewCamController`, `SharedHealthPool`, `EnemyDeathNotifier`,
  `RescueEventController`, `SkillTreeManager`. Rule: R4 canonical singleton pattern.

### Fixed — Phase 1 (consumer script R4 fallbacks)
- `SkillPointsHUDView` — moved `IPointBank` error from Awake to Start; added
  `_pointBank ??= SkillTreeManager.Instance` R4 fallback; added `enabled = false` on failure.
- `AccordHUDController` — removed Awake error; added Start with `_unlockState ??= SkillTreeManager.Instance`
  and `accordSystem ??= AccordStateSystem.Instance`; added unsubscribe-then-subscribe in Start
  to recover events missed by OnEnable firing before _unlockState resolved.
- `AccordBarView` — added `_unlockState ??= SkillTreeManager.Instance` and
  `accordSystem ??= AccordStateSystem.Instance` fallbacks in Start.
- `SkillTreeUI` — added `_dataStore/purchaser/pointBank ??= SkillTreeManager.Instance`
  fallbacks in Start; added re-subscribe for `_pointBank.OnPointsChanged` in Start.
- `OverviewCamHUDView` — added `overviewController ??= OverviewCamController.Instance` in
  coroutine-Start before yield; updates `_cooldownDuration` and re-subscribes event.
- `SharedHealthPresenter` — removed `FindAnyObjectByType<SharedHealthPool>()` and
  `FindAnyObjectByType<EmergencyTeleportMonitor>()` from Awake; added Start with
  `sharedHealthPool ??= SharedHealthPool.Instance` R4 fallback + re-subscribe pattern.
  `emergencyMonitor` relies on Inspector wiring (R1 same-scene). Rule: R4 bans FindAnyObjectByType
  for managers; emergency monitor is same-scene Persistent (no Instance needed).
- `TutorialStepContext.Resolve()` — replaced `FindAnyObjectByType<TwinSelector>()` with
  `TwinSelector.Instance`; replaced `FindAnyObjectByType<RescueEventController>()` with
  `RescueEventController.Instance`. Rule: R4 bans FAOT for managers.
- `TutorialDirector` — moved `context.Resolve()` and lock calls from Awake to Start. Rule: R8
  (Awake = wire self; Start = resolve others).
- `TutorialInputGate` — removed dead `[SerializeField] _realInputMono`; replaced all
  `FindAnyObjectByType<TwinInputReader>()` (3 sites) with `TwinInputReader.Instance` cached in
  `_reader`; gate registration moved to Start with OnEnable/OnDisable using cached `_reader`.
- `KillParticleSpawner` — added Start with `deathNotifier ??= EnemyDeathNotifier.Instance` R4
  fallback and re-subscribe in case OnEnable fired first.

### Fixed — Phase 1.4 (MonoBehaviourSingleton base-class)
- `MonoBehaviourSingleton<T>` (`AIFramework/CommonCore/Singletons/`) — fixed two bugs:
  a) `OnAwake()` applied `DontDestroyOnLoad` unconditionally, causing Persistent-resident
     singletons to be ripped out of the hierarchy and survive Bootstrap reloads as duplicates
     (live bug: `Destroying duplicate CommonCore.PerceptionManager`). Fix: added
     `protected virtual bool ApplyDontDestroyOnLoad => false`; DDOL + unparent now only
     execute when that property returns true. No derived class overrides it → all current
     Persistent-resident singletons are now DDOL-free.
  b) `Instance` getter silently fabricated `new GameObject($"Singleton<T>")` when no instance
     was found, creating blank unwired managers. Fix: fabrication now logs `Debug.LogWarning`
     so the error is surfaced. Full opt-in suppression deferred to Phase 8 once AI framework
     types are audited. Rule: R3, instruction.md §1.4.
- `LanguageManager` — removed direct `DontDestroyOnLoad(gameObject)` call in Awake; Persistent
  residency is sufficient (R3). Instance + duplicate-destroy guard already present.

### Fixed — Phase 1.4 addendum (ConstructIfNeeded scene-resident preference)
- `MonoBehaviourSingleton<T>` — added `_bWasAutoCreated` flag. When `ConstructIfNeeded` fires
  for a scene-resident instance and finds `_Instance` was auto-fabricated (e.g. `PerceptionManager`
  called before Persistent loaded), the auto-created blank GO is destroyed and the scene-resident
  instance becomes the singleton. Fixes `Destroying duplicate PerceptionManager` error that was
  previously fired even after the DDOL removal because auto-created still won the race.
  `GameDebugger` (genuinely not in any scene) correctly remains auto-created.

### Fixed — Phase 1.5 (R9 sweep)
- Area scenes `L1_Park.unity` and `L2_Streets.unity` — confirmed no duplicate AudioListener,
  EventSystem, or MainCamera components present. Persistent.unity has exactly one of each (R9).
- `Intro.unity` — contains a MainCamera+AudioListener on the cutscene camera. This is a
  pre-existing issue outside Phase 1.5 scope (cutscene needs its own camera context). Tracked
  as a Watch item; fix requires converting Intro to use a Cinemachine VCam feeding Persistent's
  Brain (Phase 6 scope).

---

### Added — Phase 2 (FailureNotice / FailureResetSequencer → Persistent)
- `FailureNotice` — added `static Instance` + duplicate-destroy Awake guard + null-in-OnDestroy.
  Component added to `TutorialHUDCanvas` in `Persistent.unity`; wired (R1 same-scene):
  `_noticePanel` → `NoticePanel`, `_noticeText` → `FailureText` (TMP), `_canvasGroup` → CanvasGroup
  on `NoticePanel`. Rule: R3 (Persistent residency) + R1 (same-scene refs).
- `FailureResetSequencer` — added `static Instance` + duplicate-destroy guard + null-in-OnDestroy.
  `TriggerReset` is re-entry-rejecting (logs warning, returns if already running) instead of
  restarting the coroutine — prevents double-teleport when both twins exit a boundary simultaneously.
  Component added to `TutorialHUDCanvas` in `Persistent.unity`; wired (R1): `_blackOverlay` →
  `BlackOverlay` Image, `_leftTwin` → Lyra (Player), `_rightTwin` → Kai (Player).
  `_postProcessVolume` left null — greyscale step gracefully skipped; Volume wiring deferred
  until a global Persistent post-process Volume exists. Rule: R3, R1.

### Fixed — Phase 2
- `TutorialStepContext` — `resetSequencer` and `failureNotice` fields changed from
  `[SerializeField]` (cross-scene R2 violation) to `[System.NonSerialized]`; resolved via
  `FailureResetSequencer.Instance` / `FailureNotice.Instance` in `Resolve()` with LogError on
  null. Rules: R2 (no cross-scene serialized refs), R4.
- `TutorialBoundary` — removed `[SerializeField]` `leftTwin`, `rightTwin`, `resetSequencer`,
  `failureNotice` fields (all were cross-scene violations or manual wires). Added `Start()`
  that resolves all four from Instances / `TwinSelector.Instance`. Rules: R2, R4.
- `TutorialOuterBoundary` — same pattern: removed cross-scene serialized fields, added
  `Start()` with Instance resolution. Fixed `WaitForSeconds` → `WaitForSecondsRealtime` (boundary
  delay must be unscaled; guard in `TutorialBoundary.TriggerReset` + re-entry-rejecting sequencer
  removes the need for `_resetting` guard on the outer boundary). Rule: R10 (comment scaled vs
  unscaledDeltaTime at every timer).
- `L1_Park.unity` `TutorialManager` — removed `FailureResetSequencer` and `FailureNotice`
  components (now live in Persistent). `TutorialManager` now has only `[TutorialContext,
  TutorialDirector, TutorialInputGate]`. No orphaned child UI GOs — `NoticePanel`/`FailureText`/
  `BlackOverlay` were always Persistent-side.

### Fixed — Phase 2 (player spawn on boot)
- `GameBootstrapper` (dev mode path) — twins in Persistent had no floor and fell indefinitely
  while the area scene was loading. Fix: after Persistent loads, call `SetMovementLocked(true)`
  on both `PlayerMovementController`s (gravity only applies inside `ExecuteCommand` — locking
  is sufficient, no CC disable needed during load). After area loads, find `AreaSpawnPoints`
  (the per-twin `leftStart`/`rightStart` authoritative for "gameplay begin") and teleport both
  twins using the standard CC-disable/position/enable pattern. `SetMovementLocked(false)` after
  placement. `LocationEntrance` is intentionally NOT used here — it is for streaming transitions
  between areas; `AreaSpawnPoints` is for gameplay-start placement.
- `IntroController` (intro mode path) — same falling bug exists in intro mode: `BackgroundLoad()`
  loads Persistent → `TwinMovementDispatcher.Update()` starts → gravity accumulates before the
  area loads. Fix: `LockTwinMovement(true)` immediately after Persistent load completes (before
  area load begins). `IntroTimelinePositioner` already calls `SetMovementLocked(false)` + teleports
  to `AreaSpawnPoints` when the level timeline stops — that unlock path is unchanged.
  Added `PlaceTwinsAtAreaSpawn()` fallback (fires only when no `IntroTimelinePositioner` is
  present in any loaded scene) so the lock is always lifted even without a level timeline.
  `FindAnyObjectByType<IntroTimelinePositioner>()` is whitelisted in instruction.md §R4.

**Phase 2 DoD status:** Verified. Bootstrap → tutorial boundary exit → black screen fade + notice
message appeared correctly. No null errors on FailureResetSequencer/FailureNotice. TutorialOuterBoundary
mechanism confirmed same-pattern (deferred — only reachable at late tutorial stage). Checkpoint not
wiring on reset is expected (Phase 7.5 scope).

---

### Changed — Phase 3 (registry conversions, pool residency, streaming)

#### 3.1 — EnemySpawner ordering hazard fixed
- `EnemySpawnner.cs` — `OnEnable` had `if (SpawnZoneRegistry.Instance == null) return;` which
  was a silent permanent opt-out when `OnEnable` fired before `SpawnZoneRegistry.Awake()` (same-
  scene ordering is arbitrary). Fix: added `private bool _started` flag; `OnEnable` skips when
  `!_started`; `Start()` performs the first subscription (guaranteed after all Awakes) with
  `LogError + enabled = false` on null. Extracted `SubscribeRegistry()`/`UnsubscribeRegistry()`
  helpers used by `OnEnable`/`OnDisable`/`OnDestroy`. Rule: R4 / instruction.md 3.1.

#### 3.2 — Pool residency (verified)
- `EnemyPool.Awake()`: `poolParent` falls back to `transform` (EnemyPool's own transform in
  Persistent) — instances parented under Persistent ✓. Code is correct; organized wiring of
  `poolParent` to a dedicated `PoolRoot` GO is an editor-side cleanup, not a correctness fix.
  Rule: R3 / instruction.md 3.2.

#### 3.3 — Despawn-on-unload
- `EnemySpawnner.cs` — `HandleZoneUnregistered` did not despawn live enemies when a zone's area
  unloaded; pooled instances survived on a deleted NavMesh (agent-not-close errors). Fix:
  - Added `_instanceZoneMap: Dictionary<GameObject, SpawnZone>` to track each instance's origin
    zone; populated in `SpawnEnemy`, `SummonerSpawn`, `TrySpawnPartner`, `SpawnCommanderGroup`
    (commander + each soldier); cleared in death handlers, `DespawnAll`, and `DespawnZone`.
  - Added `DespawnZone(SpawnZone zone)`: per-zone pool return with the same cleanup as
    `DespawnAll` (deathNotifier unregister, time-factor unregister, bond clear, pool return).
    Side/type counters intentionally NOT decremented — they reset on next `ActivateZone`.
  - Added `HandleLocationWillUnload(WorldLocationSO)` subscriber on
    `SceneFlowManager.OnLocationWillUnload` (subscribed in `Start()`, unsubscribed in
    `OnDestroy`); finds all zones whose `gameObject.scene` matches the unloading location and
    calls `DespawnZone` for each. This is the primary despawn signal (fires before
    `UnloadSceneAsync`).
  - `HandleZoneUnregistered` now also calls `DespawnZone` as belt-and-braces (fires during
    unload lifecycle, after `OnLocationWillUnload`). Rule: R5 / instruction.md 3.3.

#### 3.4 — POIManager residency (verified)
- `POIBase.cs` — already registers `OnEnable`, unregisters `OnDisable/OnDestroy` ✓.
  `POIManager.GetNearest()` already has `if (poi == null) continue;` null-purge ✓. No changes
  needed. Rule: R5 / instruction.md 3.4.

#### 3.5 — LocationEntrance & flow (verified)
- `LocationEntrance.GetFor()` uses `FindObjectsByType` (whitelisted) — correct pattern; no
  registry needed for this cold-path query. No changes needed. Rule: instruction.md 3.5.

#### 3.7 — SceneFlowManager required changes
- `SceneFlowManager.cs` — full rewrite implementing the transition model:
  a) **Occupancy: int counts → per-actor location dictionary.** `_occupantCounts` removed;
     replaced by `_currentLocation: Dictionary<Player, WorldLocationSO>`. Each tracked actor
     (twins, SoulPlayer while travelling) maps to exactly one location. Assignment = transition;
     previous location is vacated implicitly. `BuildDesiredSet()` iterates values.
     `IsOccupied()` uses `_currentLocation.Values.Any(v == location)`. Deleted
     `NotifyTwinExited` and `LoadStartLocation` (both had broken semantics).
  b) **`NotifyTeleported(Player actor, WorldLocationSO destination)`** — identical to an enter
     transition; call sites: boot seeding, SoftResetController, Weaver's Gate, debug warps.
  c) **`event Action<WorldLocationSO> OnLocationWillUnload`** — raised inside
     `UnloadLocationAsync` after re-checks pass and before `UnloadSceneAsync`. Primary
     despawn/cancel signal for EnemySpawner (3.3) and QTEManager.
  d) **Active scene update.** `UpdateActiveScene()` called from `RecalculateLoadedSet()` and
     after each `LoadLocationAsync` completes; prefers the selected twin's area (via
     `TwinSelector.SelectedTransform`), falls back to any occupied location.
  e) **R10: `WaitForSeconds` → `WaitForSecondsRealtime`** in `UnloadLocationAsync` with a
     comment explaining why unscaled is correct (Setsuna/pause must not delay unload grace).
  f) Added `OnDestroy` that nulls `Instance` (was missing). Added `_byScene` per-actor
     debug display in editor `OnGUI`.
- `SceneLoadTrigger.cs` — `OnTriggerEnter` now extracts `Player` via
  `GetComponentInParent<Player>()` and passes it to `NotifyTwinEntered(location, actor)`.
  `SoulPlayer` excluded (tracked separately). `OnTriggerExit` removed entirely — transition
  model makes exits redundant. Rule: instruction.md 3.7a.
- `IntroController.cs` — replaced `LoadStartLocation(firstAreaLocation)` with two
  `NotifyTeleported` calls for left/right twins via `TwinSelector.Instance`. Rule: 3.7b.
- `GameBootstrapper.cs` — added `[SerializeField] WorldLocationSO firstAreaLocation` (dev mode
  only); added two `NotifyTeleported` calls after `PlaceTwinsAtAreaSpawn()`. Rule: 3.7b.

#### 3.8 — TimeFactorBootstrapper unload purge
- `TimeFactorBootStrapper.cs` — added `_byScene: Dictionary<Scene, List<ITimeAffected>>`; all
  registrations go through `RegisterForScene(scene, affected)` which populates the map;
  added `OnChunkUnloaded` subscribed to `SceneManager.sceneUnloaded` in `Start()` — unregisters
  all entries for that scene and removes the map entry. `OnDestroy` unsubscribes both events.
  Rule: instruction.md 3.8.
- `TimeFactorManager.cs` — added `PurgeDestroyed()` using Unity-aware null check
  (`e is Object uo && uo == null`) called at the start of `TriggerEffect` and `ResolveEffect`
  as a belt-and-braces for any race conditions between unload and event fire. Rule: 3.8.

**Phase 3 DoD status:** Code complete. Scene-level verification pending (Play Bootstrap →
walk L1↔L2 repeatedly, streaming loads/unloads with no NREs, no orphaned enemies). Scene
Inspector items requiring MCP/editor work: verify `EnemyPool.poolParent` wired to PoolRoot
in Persistent (3.2); verify CameraManager tutorial cam pair residency (3.6).

#### 3.9 — WorldLocationSO scene name cleanup (streaming bug)
- `L0_CityWatersSide.asset`, `L1_side.asset`, `L2_side.asset`, `L3_side.asset`,
  `L4_side.asset`, `L5_side.asset`, `L6_side.asset` — all seven "side" placeholder assets
  had `scene._name: L1Park` (no underscore). Root cause: assets were created when the scene
  was still named `L1Park`; the rename to `L1_Park` was applied only to the main
  `L1_Park.asset` and not to the side assets. `SceneReference.IsValid` returns `true` for
  any non-empty string, so `BuildDesiredSet()` added them to the desired set and
  `LoadLocationAsync` attempted `LoadSceneAsync("L1Park")` each time any twin triggered a
  boundary — producing 12+ `[SceneFlowManager] Cannot load 'L1Park'` errors per play session.
  Fix: cleared `_name` to empty in all seven assets; `IsValid` now returns `false`, and the
  `if (!location.IsValid) yield break` guard in `LoadLocationAsync` prevents any load attempt.
  These are future placeholder scenes with no corresponding `.unity` file; empty `_name` is
  the correct sentinel until those scenes are created. Rule: R7 (SO = config only, must match
  live build). BUG-028 partial (streaming load errors eliminated; these locations remain
  scaffolded but dormant).

---

### Added — Phase 4 (skill-tree runtime state off the SO)
- `SkillTreeRuntimeState` (`Scripts/SkillTree/`) — new plain C# class; owns
  `Dictionary<AbilityUpgradeData, int> _levels` at runtime. Exposes `GetLevel`,
  `SetLevel`, `TakeSnapshot()`, and `RestoreSnapshot(Snapshot)`. `Snapshot` nested class
  is a copy-on-take `IReadOnlyDictionary` — immutable once taken. Rule: R7.

### Changed — Phase 4
- `AbilityUpgradeData.currentNodeIndex` — changed from mutable public field to a
  read-only property delegating to `SkillTreeManager.Instance?.GetLevel(this) ?? 0`.
  The field was a live R7 violation: buying a skill node mutated the `.asset` file, leaving
  it dirty in the editor and coupling reset semantics to SO serialization. The property is
  a transparent drop-in — all existing callers (`CoalesceSystem`, `SoulConvergenceSystem`,
  `SoulPulseSystem`, `SkillNodeButton`, `SkillPreviewModel`) continue to work with zero
  changes. `UnlockNextNode()` and `ResetToBase()` removed from the SO; callers were
  `SkillTreeManager.TryPurchaseNode` and `ResetAllSOs` (now `InitRuntime`) respectively.
- `SkillTreeManager` — added `private readonly SkillTreeRuntimeState _runtimeState`;
  `TryPurchaseNode` uses `_runtimeState.SetLevel` instead of `data.UnlockNextNode()`;
  `ResetAllSOs()` renamed `InitRuntime()` and resets via `_runtimeState.SetLevel(d, 0)`
  instead of `d.ResetToBase()`. Added `GetLevel`, `TakeSkillSnapshot`,
  `RestoreSkillSnapshot` (calls `_runtimeState.RestoreSnapshot` then `RebuildUnlockFlags`).
- `CheckpointData` — `int[] nodeUnlockLevels` replaced by
  `SkillTreeRuntimeState.Snapshot skillTreeSnapshot`. The `int[]` was a hand-ordered
  parallel array matching `SkillTreeManager.AllData()` — adding a new tree broke it
  silently. The snapshot is a dictionary keyed by SO reference; order-independent and
  automatically covers all 9 trees.
- `CheckpointManager` — removed `CaptureNodeLevels()` (7 of 9 trees, brittle order);
  `SaveCheckpoint` now calls `skillTreeManager.TakeSkillSnapshot()`.
  **Bug fixed:** `CaptureNodeLevels` omitted `EmpowerData` and `AccordData` — the snapshot
  captures all 9 entries from the runtime dictionary.
- `SoftResetController` — removed `RestoreNodeLevels()` (same 7-of-9 bug);
  `RestoreSkillTree` now calls `tree.RestoreSkillSnapshot(data.skillTreeSnapshot)`.
  `FindAnyObjectByType<SkillTreeManager>()` in `AutoFindRefs` replaced with
  `SkillTreeManager.Instance` (R4).
- `CheckPointLoader` (DEPRECATED) — updated to compile against new API:
  `nodeUnlockLevels` → `skillTreeSnapshot`, removed `RestoreNodeLevels` method.
  Pending deletion in Phase 6.

**Phase 4 DoD status:** Verified. `git status` showed no modified `.asset` files after buying nodes.

---

### Added — Phase 5 (lifecycle & time audit)
- `TimeScaleService` (`Scripts/SceneLaoder/`) — Persistent singleton. Min-value-wins
  `Time.timeScale` arbiter. API: `Request(owner, scale)`, `Release(owner)`, `ReleaseAll()`.
  Empty request table → timeScale = 1. Resolves the 8-writer stomping war (R10).

### Changed — Phase 5
- **All 8 `Time.timeScale` direct writers migrated to `TimeScaleService`:**
  - `PauseMenuController.OpenPause` → `Request(this, 0f)`; `Resume` → `Release(this)`;
    `ExitGame` → `ReleaseAll()`.
  - `TutorialOverlayController.Show` → `Request(this, 0f)`; `OnContinueClicked` → `Release(this)`.
  - `GameOverController.TriggerGameOver` → `Request(this, 0f)`; `RestartScene` → `ReleaseAll()`;
    `LoadCheckpoint` (on success) → `Release(this)`.
  - `SetsunaSystem.Activate` → `Request(this, _timeScaleFactor)`; `BeginRewind` and `ForceEnd`
    → `Release(this)`.
  - `TeleportAbility.Activate` (onArrival) → `Request(this, _soulTravelTimeFactor)`; `End` →
    `Release(this)`.
  - `SoftResetController.ResetSequence` → `ReleaseAll()` (clears all outstanding requests on entry).
  - `SkillTreeUI.Update` (open/close) → `Request(this, 0f)` / `Release(this)`.
  - `OverviewCamController.StartOverview` → `Request(this, 0f)`; `EndOverview` → `Release(this)`.
- `GameBootstrapper.BootSequence` — added `Time.timeScale = 1f` as first line (direct write;
  Persistent/TimeScaleService not yet loaded at Bootstrap boot). Ensures leftover values from
  prior sessions are cleared on every restart. Rule: R10 / instruction.md 5.4.
- `QTEManager.Update` — `Time.deltaTime` → `Time.unscaledDeltaTime` for both timer ticks
  (`_windowTimer`, `_mashTimer`). Timers must run at real time — QTE runs during enemy freeze
  (which may also coincide with slow-motion Setsuna). Rule: R10 / instruction.md 5.3.
- `QTEController.UnfreezeAfterDelay` — `WaitForSeconds(delay)` → `WaitForSecondsRealtime(delay)`.
  Post-success 1.5 s grace before enemies resume; must not stretch 6.7× under Setsuna 0.15. Rule: R10.
- `TutorialDirector.RunSequence` — `WaitForSeconds(0.3f)` → `WaitForSecondsRealtime(0.3f)`.
  Tutorial step delay must survive tutorial overlay timeScale=0. Rule: R10 / instruction.md 5.3.
- `TeleportAbility.TravelToTarget` — `WaitForSeconds(0.3f)` → `WaitForSecondsRealtime(0.3f)`.
  This wait fires while timeScale = _soulTravelTimeFactor (0.85); unscaled avoids stretching. Rule: R10.
- **ESC triple-consumer eliminated (Phase 5.6):**
  - `TutorialOverlayController` — removed own `Update()` ESC consumer; `PauseMenuController`
    arbitrates. Added `public bool IsOpen => _isOpen` and `public void TriggerContinue()`.
  - `SkillTreeUI` — removed ESC from `Update()` (Tab toggle retained); ESC now mediated by
    `PauseMenuController`. Added `public static Instance`, `public bool IsOpen`,
    `public void Close()` (also releases TimeScaleService request).
  - `PauseMenuController.Update` — extended priority chain: overlay (highest) → SkillPreviewModal
    → settings → pause → skill tree → open pause. Uses `TutorialOverlayController.Instance` and
    `SkillTreeUI.Instance`.
- `GameOverController` — `FindAnyObjectByType` calls in `Awake()` moved to `Start()` (R4/R8).
  `rescueEventController` fallback uses `RescueEventController.Instance`; `sharedHealthPool`
  and `checkpointManager` use `FindAnyObjectByType` (no Instance yet — tagged for Phase 8).
  Button listener wiring stays in `Awake()` (pure UI, no manager deps). Event subscriptions
  moved to `Start()` alongside manager resolution.
- **Phase 5.1 remaining (Awake/Start R4/R8 violations):**
  - `EnemySpawner` (`EnemySpawnner.cs`) — added `public static Instance` + duplicate-destroy
    guard + null-in-OnDestroy. Was the only Persistent singleton missing Instance.
  - `SummonerEnemy` — removed `FindAnyObjectByType<EnemySpawner>()` from `Awake()`; replaced
    with lazy `_spawner ??= EnemySpawner.Instance` inside `SummonRoutine()` (the sole call
    site), which runs during gameplay well after all Starts. Rule: R4.
  - `RescueButtonUI` — removed FAOT from `Awake()`; replaced with `Start()` that resolves
    `rescueEventController ??= RescueEventController.Instance` then unsubscribes+subscribes
    all five events (recovers events missed by OnEnable firing before resolution). Rule: R4/R8.
  - `WorldSpaceRescueUI` — same pattern: FAOT removed from `Awake()` (which retains only
    self-wiring for RectTransform and `GetComponentInParent`); `Start()` extended to resolve
    Instance and unsubscribe+subscribe all eight events before calling `HideAll()`. Rule: R4/R8.
  - `Enemy.cs` — `TimeFactorManager.Instance` fallback kept in `Awake()` (pragmatic exception:
    `OnEnable` fires before `Start` and needs the ref; moving to `Start` would drop the first
    registration). Noted as accepted trade-off; full fix deferred to Phase 8.

- **Lambda subscription hygiene — Phase 5.2 (pool-reuse delegate leak):**
  - `RescueEventController` — twin `Health.OnDeath` subscriptions replaced with named handlers
    `HandleLeftTwinDeath` / `HandleRightTwinDeath`; both unsubscribed in existing `OnDestroy`.
    Lambdas were un-removable if the component was ever reassigned.
  - `ChainCommander`, `GrandSummoner`, `PenitentCommander` — `RegisterSoldier` lambdas replaced
    with local named functions stored in `_soldierHandlers: Dictionary<Enemy, System.Action>`.
    Added `ClearSoldiers()` that unregisters all stored handlers and clears both collections.
    Pool reuse previously accumulated stale death listeners per deployment.
  - `EnemySpawnner` — `_spawnDeathHandlers` dictionary existed but was never populated or used
    for unregistration. Commander spawn and soldier spawn now store named local functions in the
    dict. `DespawnAll` and `DespawnZone` unregister each handler before clearing/removing.
  - `SiphonEnemy` — `Health.OnDeath` lambda capturing `ghost` local variable replaced with
    named `HandleSiphonDeath` method; `_ghost` promoted to field. `OnDisable` unsubscribes
    and nulls `_ghost`.

**Phase 5 DoD status:** Code complete. Verify: pause + tutorial overlay + skill tree all open/close
cleanly at correct timeScale; Setsuna 0.15 + pause 0 = timeScale stays 0 (min-wins); soft reset
restores 1; QTE timers run while game is paused/slowed; ESC closes exactly one layer per press.

---

### Removed — Phase 6 (dead-code deletion)
- `AreaManager.cs` + `AreaNode.cs` (`Scripts/SceneLaoder/`) — deleted. Both classes replaced
  by `SceneFlowManager` (Phase 3). Zero external C# consumers. The `AreaManager` component
  must also be removed from L1_Park.unity in the Unity Editor (scene file still references the
  now-missing script; shows as missing MonoBehaviour until removed).
- `CheckPointLoader.cs` (`Scripts/CheckPointSystem/`) — deleted. Replaced by `SoftResetController`
  and the new `CheckpointManager` + `SkillTreeRuntimeState` flow (Phases 3–4). Only comment
  references remained in `SoftResetController.cs` and `SwordPickup.cs`; no live code callers.
  CLAUDE.md and game.md §19 both marked it obsolete.

### Fixed — Phase 6 (verified absent)
- `TutorialHUDProvider` — confirmed does not exist as a class or component; the reference in
  changelog.md's old "Changed" entry (added under multi-scene scaffolding) was an anticipated
  TODO. `TutorialStepContext.Resolve()` already resolves overlay/hint display via Instances
  — no provider class is needed. Entry updated to reflect current state.

**Phase 6 DoD (code-side):** `AreaManager.cs`, `AreaNode.cs`, `CheckpointLoader.cs` and their
`.meta` files deleted. Grep confirms zero remaining C# references. Editor task outstanding:
remove the AreaManager component from L1_Park in the Unity Editor to clear the missing-script
warning.

---

### Added — Phase 7.5 (SoftReset completion — code-side)
- `CheckpointManager` — added `public static Instance` + duplicate-destroy guard + null-in-OnDestroy.
- `EnemySpawner` — added `public static Instance` + duplicate-destroy guard + null-in-OnDestroy.
- `SetsunaSystem` — added `public static Instance` + duplicate-destroy guard + null-in-OnDestroy.
  `ForceEnd()` changed from `private` to `public` (required by SoftResetController).
- `SoulConvergenceSystem` — added `public static Instance` + duplicate-destroy guard + null-in-OnDestroy.
- `AccordStateSystem` — added `public void ForceDeactivate()` wrapper (calls private `DeactivateAccord`
  when active). Required by SoftResetController soft-reset sequence.
- `EmpowerSystem` — added `public void ForceEnd()` wrapper (calls private `EndAbility()`).
- `SoftResetController` — added `public event Action OnSoftReset` (consumed by SpawnZone and
  SkeletonTrap). Added `[SerializeField] _leftTwin/_rightTwin` (R1 Persistent direct refs).
  Added `private void OnDestroy()` null-Instance.
- `SpawnZone` — added `public void ClearOccupants()` + subscribes to `SoftResetController.OnSoftReset`
  in `OnEnable`/`OnDisable`. A teleport never fires the exit trigger, so occupancy sticks until a
  soft-reset explicitly clears it. The re-enter after teleport re-arms naturally.
- `SkeletonTrap` — added `public void ForceReset()`: stops rearm coroutine, releases any grabbed
  player (unfreeze + SetGrabbed(false)), transitions directly to Dormant, clears timers. Subscribes
  `SoftResetController.OnSoftReset` → `HandleSoftReset` in `OnEnable`/`OnDisable`.
  `RearmRoutine`: `WaitForSeconds` → `WaitForSecondsRealtime` (pacing, not gameplay — must not
  stretch under Setsuna slow or rescue-tutorial 0.25× scale). Self-registers with
  `RescueEventController` via `OnEnable`/`OnDisable` (see Changed below).
- `IRescueTrapRegistry` — added `void UnregisterTrap(IRescueTarget trap)` to the interface.
- `CheckpointData` — added `WorldLocationSO checkpointLocation` field (the area where the checkpoint
  lives; null-safe, only populated when `CheckpointTrigger` has the field wired).
- `CheckpointTrigger` — added `[SerializeField] WorldLocationSO location`; replaced
  `FindAnyObjectByType<CheckpointManager/TwinSelector>` with `CheckpointManager.Instance` /
  `TwinSelector.Instance`; passes `location` to `SaveCheckpoint`. Rule: R4.

### Changed — Phase 7.5
- `RescueEventController.Start()` — removed one-time `FindObjectsByType<SkeletonTrap>` scan.
  Traps now self-register via `OnEnable`/`OnDisable` so streamed-in traps auto-register without
  a boot-time sweep. Rule: R5.
- `SoftResetController` — restructured reset sequence:
  - Added step: abort QTE (`QTEManager.Instance?.AbortQTE()`).
  - Added step: force-end all four power states (Setsuna, Accord, Empower, SoulConvergence)
    via their new public wrappers.
  - Added step: fire `OnSoftReset` before despawn so SpawnZones/traps clean up.
  - `AutoFindRefs()` now uses `.Instance` for EnemySpawner and RescueEventController (both
    have Instance now); SharedHealthPool remains FAOT (no Instance yet — Phase 8).
  - `ApplyTwinState` converted from `void` to `IEnumerator`: yields one frame after teleport,
    then calls `SceneFlowManager.NotifyTeleported(twin, checkpointLocation)` for both twins
    (3.7b) when `data.checkpointLocation` is non-null.
  - Added `WaitForLocation` coroutine: if checkpoint's area is not loaded, calls
    `SceneFlowManager.NotifyTeleported` to trigger occupancy-based loading, then awaits
    `OnLocationLoaded` before continuing. Prevents teleporting into a void when the saved
    checkpoint is in a scene that streamed out.
  - Twin identification: replaced proximity-scan (`FindObjectsByType`) with direct
    serialized `_leftTwin/_rightTwin` fields (R1 Persistent), falling back to
    `TwinSelector.Instance.LeftTwin/RightTwin`.
- `IntroTimelinePositioner` — replaced `FindAnyObjectByType<TwinSelector>()` with
  `TwinSelector.Instance` (R4).
- `TutorialCheckpoint.Start()` — replaced `FindAnyObjectByType<TwinSelector>()` with
  `TwinSelector.Instance` (R4).
- `TutorialCheckpoint.Activate()` — added fail-loud guard (Phase 7.6b): after `SetActive(true)`,
  if `!gameObject.activeInHierarchy`, walks the transform chain and `Debug.LogError`s the first
  inactive ancestor. Silent no-op promoted to pinpointing log.

**Phase 7.5 DoD (code-side):** All force-end wrappers added, SoftReset sequence restructured,
streaming-blind teleport protected, trap and spawn-zone soft-reset hooks wired in code. Editor
tasks outstanding:
- Wire `_leftTwin`, `_rightTwin` on SoftResetController in Persistent.
- Wire `location` on each `CheckpointTrigger` in area scenes.
- Wire `EnemySpawner`, `SharedHealthPool`, `SkillTreeManager`, `RescueEventController` on
  SoftResetController (delete `AutoFindRefs()` once Inspector is wired — Phase 8 cleanup).

---

### Added — Phase 7.6c (lambda subscription hygiene)
- `TutorialCheckpoint.Suspend()` / `Resume()` — toggle only the trigger `Collider.enabled`
  (preserves marker/particle state). `FullReset()` now calls `Resume()` after reactivating
  the GO so a Suspended checkpoint is fully re-armed after the reset sequence.
- `WrongTwinResetHandler.cs` — plain C# class (no MonoBehaviour) extracted from
  `TutorialCheckpointStepSO`. One instance per step, subscribed to every checkpoint that can
  fire `OnWrongTwinReached`. Holds the `_resetting` guard (prevents simultaneous-entry races),
  uses identity-based reset positions (`cpA.LeftResetPosition` / `cpB.RightResetPosition`),
  suspends all checkpoints before `TriggerReset`, and calls `FullReset` + optional `_onReset`
  callback in `onComplete`. Instruction.md Phase 7.6h.

### Changed — Phase 7.6c / 7.6h (checkpoint step hardening)
- `TutorialCheckpointStepSO` — full rewrite of `RunSingle` and `RunDual`:
  - Lambda subscriptions (`OnCorrectTwinReached`, `OnWrongTwinReached`) replaced with named
    local functions + `try/finally` unsubscription. Eliminates stale handler accumulation on
    soft-reset / restart. Instruction.md Phase 7.6c.
  - `WrongTwinResetHandler` replaces four duplicated wrong-twin lambda bodies. Both modes share
    one handler; Dual mode subscribes the same instance to both checkpoints so the internal
    `_resetting` guard is shared automatically. Instruction.md Phase 7.6h.
  - `RunSingle` now uses identity-based positions (via handler) — no more position-swapping.
  - Deleted `GetSwappedResets` and `GetDualResets` helpers (now dead code).
  - Removed `failureMessageB` field — single handler uses `failureMessageA` for both events
    (existing scenes: migrate failureMessageB text to failureMessageA if different).
- `TutorialRescueWatchStepSO` — `WaitForSeconds(0.5f)` → `WaitForSecondsRealtime(0.5f)` in
  retry loop; removed debug log that fired every frame. Phase 7.6i.

### Changed — Phase 7.5 remainder (SkillTreeManager.AllTrees exposure)
- `SkillTreeManager.AllTrees` — private `AllData()` iterator now backed by a cached
  `IReadOnlyList<AbilityUpgradeData> AllTrees` public property (lazy-built in `BuildTreeList()`).
  Instruction.md §9.5 explicitly required this so no caller ever hand-lists the 9 trees again;
  future trees added to `AllData()` are automatically included in `AllTrees`. Rule: R7.

### Changed — Phase 7 item 7 (WIRING_GUIDE.md rewrite)
- `WIRING_GUIDE.md` — fully regenerated from current source. Previous version had five
  categories of stale content: scene names used `L1Park`/`L2Streets` (actual: `L1_Park`/
  `L2_Streets`); §2 listed five QTE UI fields on QTEManager that no longer exist (UI moved
  to `QTESceneAnchor` per-area, world-space); §3 `IntroTimelinePositioner` listed
  `leftTwinStart`/`rightTwinStart` fields that do not exist (it finds `AreaSpawnPoints` at
  runtime); §3 `LocationEntrance.location` is actually `comesFrom` with opposite semantics
  (the area arriving FROM, not the current area); §8 still described `CheckpointLoader` as
  persisting via DDOL (deleted in Phase 6). Known-issues section corrected: SO-runtime-state
  and Setsuna-timeScale entries removed (both fixed by Phase 4 and Phase 5.5).

### Changed — Phase 7.6j (SetsunaSystem / SoulConvergence / AccordState input hardening)
- `IInputProvider` — added `GetConvergenceHeld()` (hold F, gated by `IsAbilityAllowed`).
  Callers: `TwinInputReader`, `TutorialInputGate`.
- `TwinInputReader.GetConvergenceHeld()` — `AbilityAllowed && Input.GetKey(KeyCode.F)`. Gated
  through tutorial gate like all other ability inputs.
- `TutorialInputGate.GetConvergenceHeld()` — `_abilityAllowed && (_real?.GetConvergenceHeld())`.
- `SetsunaSystem.HandleIdle/HandleCharging` — replaced two `Input.GetKey(KeyCode.F)` raw reads
  with `_input.GetConvergenceHeld()`. Also removed redundant `GetAbilityDown()` check in Idle
  (only convergence-hold should start Setsuna charge). Rule: ban raw input outside
  `TwinInputReader` (CLAUDE.md Notes).
- `SetsunaSystem.SetInvulnerable()` — now calls `Health.SetInvincible(value)` on both twins in
  addition to movement lock. Prevents enemy hits during the 1.5 s rewind from emptying the
  shared pool before health snapshot is restored. Method name stays `SetInvulnerable` — it now
  truly does what the name says. Instruction.md Phase 7.6j.
- `SetsunaSystem` — deleted dead `EaseInOutCubic` private method (never called). Phase 7.6j.
- `SoulConvergenceSystem` — added `[SerializeField] _inputProviderMono` + `IInputProvider _input`;
  resolved in `Start()` via `TwinInputReader.Instance` (R4). Replaced raw `Input.GetKey(_activateKey)`
  with `_input?.GetConvergenceHeld() ?? false`. **Editor task: wire `_inputProviderMono` on
  SoulConvergenceSystem in Persistent.** Rule: R4; ban raw input outside TwinInputReader.
- `AccordStateSystem` — added `[SerializeField] _inputProviderMono` + `IInputProvider _input`;
  resolved in `Start()` via `TwinInputReader.Instance` (R4). Replaced two `Input.GetKey(KeyCode.X)`
  calls in `HandleIdle` / `HandleCharging` with `_input?.GetCancelHeld() ?? false`. X is ungated
  (cancel must always work) — routed through `GetCancelHeld()` for DIP. **Editor task: wire
  `_inputProviderMono` on AccordStateSystem in Persistent.** Rule: R4; DIP.

### Added — Editor task 7 (AudioMixer)
- `Assets/Audio/GameAudioMixer.mixer` — AudioMixer asset created. Hierarchy: Master (root)
  → Music (child) → SFX (child). Exposed parameters via internal `AudioMixerController`
  API (reflection + `SerializedObject`): `MasterVolume`, `MusicVolume`, `SFXVolume` —
  each mapped to the respective group's Attenuation effect `m_MixLevel` GUID. Wired to
  `SettingsMenuController._audioMixer` in Persistent.unity. Closes editor task 7; volume
  sliders in Settings panel now route to actual mixer groups at runtime.
- `Persistent.unity` saved after `_audioMixer` wire.

### Added — Phase 5.5 TimeScaleService (2026-06-13)

**`TimeScaleService` MonoBehaviour placed in Persistent.unity** (all code was already complete).

- `TimeScaleService.cs` (`SceneLaoder/`) — Persistent singleton, min-value-wins arbiter for all
  `Time.timeScale` writes. `Request(owner, scale)` / `Release(owner)` / `ReleaseAll()`. Only the
  `Apply()` method writes `Time.timeScale` directly. `GameBootstrapper` retains one pre-service
  direct write (`Time.timeScale = 1f`) with comment; Persistent isn't loaded at that point.
- New GO `TimeScaleService` added to Persistent.unity at sibling index 10 (after
  `SoftResetController`, before `EnemyPool`). Saved.

All 7 writers confirmed migrated (verified via grep — zero direct `Time.timeScale =` writes
outside the service and GameBootstrapper boot-init):

| Writer | Migration |
|---|---|
| `PauseMenuController` | `Request(this,0)` / `Release(this)` / `ReleaseAll()` ✓ |
| `TutorialOverlayController` | `Request(this,0)` / `Release(this)` ✓ |
| `GameOverController` | `Request(this,0)` / `ReleaseAll()` / `Release(this)` ✓ |
| `SetsunaSystem` | `Request(this,_timeScaleFactor)` / `Release(this)` (2 exit paths) ✓ |
| `TeleportAbility` | `Request(this,_soulTravelTimeFactor)` / `Release(this)` ✓ |
| `SoftResetController` | `ReleaseAll()` ✓ |
| `SkillTreeUI` | `Request(this,0)` / `Release(this)` (open + close paths) ✓ |
| `OverviewCamController` | `Request(this,0)` / `Release(this)` ✓ (bonus writer found) |

Play-mode verification: `TimeScaleService.Instance` non-null in Persistent ✓;
`Request("test",0.5)→timeScale=0.5`; `Release("test")→timeScale=1` ✓.

**R10 satisfied.** All timeScale reads during Setsuna (0.15), pause (0), game-over (0), soul
travel (0.85), skill tree (0) now correctly compose via min-value-wins — no stomp possible.

### Fixed — L1_Park duplicate twin/soul GOs removed (2026-06-13)
- **`Twins`, `SoulTwin`, `TestPlayer` deleted from L1_Park (R3 violation):** All three root
  GameObjects were area-scene duplicates of Persistent-owned objects. `Twins` (containing `Kai`,
  `Lyra`, `Canvas_KaiUI`, `RescueCanvas`) and `SoulTwin` (with `SoulPlayer`, movement, health,
  animation components + `Canvas_Soul`, `RescueCanvas`) belong exclusively in `Persistent.unity`
  per the scene architecture. `TestPlayer` was a stale dev object. L1_Park dropped from 73 → 70
  root GameObjects. Scene saved.

### Fixed — §10 play-mode verification (2026-06-13)
- **L1_Park screen-space canvas duplicates (R9 violation):** `HUD_Canvas`, `FadeCanvas`,
  `PauseMenuCanvas`, and `SkillTreeCanvas` were still present as `ScreenSpaceOverlay` canvases
  in `L1_Park.unity` — leftovers from single-scene era that were copied but never deleted when
  the HUD was migrated to Persistent. All four deleted. L2_Streets was already clean.
- **PauseMenuCanvas inactive in Persistent:** `PauseMenuCanvas` GO was `SetActive(false)` in
  Persistent, so `PauseMenuController.Awake()` never ran and `PauseMenuController.Instance` was
  always null. Fix: activated the GO in the scene — `Awake()` already calls
  `_pauseRoot.SetActive(false)` so the visual panel stays hidden at runtime. `Instance` now
  non-null on boot.

### Added — §10 play-mode smoke tests (Bootstrap path, 2026-06-13)

Tested via MCP `execute_code` in Unity play mode (Bootstrap → Intro → L2_Streets). Items
requiring keyboard/controller input (trap mash, ability combos, tutorial flow, streaming soak)
are marked as needing manual follow-up.

| # | Item | Result | Notes |
|---|------|--------|-------|
| 1 | Move/switch/singletons | ✓ PASS | All 11 singletons non-null; HP=200; timeScale=1; twins placed correctly |
| 2 | Skill tree purchase | ✓ PASS | AllTrees.Count=9, 0 null; purchase fires; Accord+Empower unlock flags set |
| 3 | Accord activate/deactivate | ✓ PASS | `ActivateAccord()`→active; `ForceDeactivate()`→inactive; AccordBarView found |
| 4 | SetsunaSystem/SoulConvergence | ✓ PASS | Both found in Persistent; `_input` wired (7.6j) |
| 5 | RescueEventController | ✓ PASS | Instance non-null |
| 6/7 | Tutorial singletons | ✓ PASS | TutorialOverlayController + TutorialHintDisplay live |
| 8 | Gate QTE | ⚠ MANUAL | QTEManager Instance ✓; full QTE flow needs manual test |
| 9 | Streaming | ✓ PASS | 7 scenes loaded (Persistent+L2_Streets+L2_Side+L1_Park+L1_Side+L3_Alley+L3_Side); Active=L2_Streets; SceneLoadTriggers×2, LocationEntrances×2 |
| 10 | Checkpoint / soft reset | ✓ PASS | BeginSoftReset(CheckpointData) works; HP 80→200; stun=1+Accord+Empower restored; `_resetting=False`; 9-tree snapshot confirmed (Empower+AccordState included) |
| 11 | Restart canary | ✓ PASS | LoadScene(Bootstrap)→re-enter; all singletons x1 (no duplicates); Pts=2 (not 83 — state cleared); HP=200 |
| 12 | Pause | ✓ PASS | PauseMenuController.Instance now non-null (see fix above) |

**Known open items after §10:**
- Intro.unity has its own camera/AudioListener → transient "2 audio listeners" warning during
  intro loading each session. Clears when Intro unloads. Watch item; fix: remove or disable the
  Intro camera's AudioListener component.
- `TimeScaleService` not implemented (Phase 5.5 scheduled but deferred). Seven `timeScale`
  writers still stomp each other. Known planned work.
- `Application.runInBackground = false` in Project Settings means game freezes when editor
  window unfocused — affects testing only, not production.
- Manual smoke items pending (require Unity Editor with focused game window): input/attack
  combos (1), skill tree UI tab (2), Setsuna hold-F+rewind (4), trap mash (5), tutorial
  overlay flow (6/7), Gate QTE full cycle (8), streaming boundary walk ×10 (9), full restart
  playthrough (11), pause-during-ability (12).

### Added — §10 verification protocol (MCP inspection)
- Verified via MCP all key Persistent singleton wiring (edit-mode inspector values):
  - `SceneFlowManager` — `allLocations[]` has 13 WorldLocationSO assets (L0–L6 main + side);
    `unloadDelay = 0.5` ✓
  - `SoftResetController` — `_leftTwin` (Lyra), `_rightTwin` (Kai), `fadeImage`, `enemySpawner`,
    `sharedHealthPool`, `skillTreeManager`, `rescueController` all wired ✓
  - `QTEManager` — 3 fields only: `enemyFreezeServiceMono`, `cameraControllerMono`,
    `cameraSwitcher` — no phantom UI fields ✓
  - `SkillTreeManager` — all 9 `AbilityUpgradeData` trees assigned; `_startingPoints = 2` ✓
  - `PlayerManager` — `TwinInputReader`, `TwinSelector`, `TwinMovementDispatcher`,
    `TwinAttackDispatcher`, `TwinAbilityDispatcher`, `TwinAbilitySetup`, `EmergencyTeleportMonitor`
    all wired ✓
  - `GameSystem` — 15 components (`TimeFactorManager`, `TwinBondManager`, `SharedHealthPool`,
    `CheckpointManager`, `RescueEventController`, `GameOverController`, `LanguageManager`,
    `EnemyFreezeService` etc.) all wired ✓
  - `GameBootstrapper` — `persistentScene` (Persistent), `introScene` (Intro),
    `firstAreaScene` (L2_Streets), `firstAreaLocation` (L2_Streets.asset),
    `unloadBootstrapWhenDone = true` ✓
  - `SettingsMenuController` — all UI refs wired; `_audioMixer` now wired (see above) ✓
  - Console: 0 errors, 0 warnings ✓
- `isStartLocation: 1` on `L2_Streets.asset` confirmed intentional — `GameBootstrapper`
  uses explicit `firstAreaScene/firstAreaLocation` fields, not the flag; flag is a dev
  annotation only.

---

### Added
- `WorldSpaceCanvasCamera` — tiny component that assigns `Camera.main` to a World Space
  canvas `worldCamera` in `Start()`; solves cross-scene Event Camera wiring. Attached to
  `QTEParkCanvasUI` in L1_Park.
- `Restore.unity` — temporary scene holding the restored `CommonStatic`, `CommonStatic (1)`,
  and `CommonStatic (2)` hierarchies (1 592 transforms, 9 628 blocks extracted from git
  history). Open additively and drag into L1_Park, then delete.
- `Trees.unity` — temporary scene holding 6 `japanese_maple` prefab instances (2 world
  positions) that were accidentally deleted from L1_Park. Open additively and drag into
  L1_Park, then delete.

### Fixed
- `GameOverController.RestartScene()` — previously called
  `SceneManager.LoadScene(GetActiveScene().name)`, which only reloaded the area scene and
  lost Persistent in multi-scene. Now loads the Bootstrap scene via a `SceneReference` field,
  which tears everything down cleanly and re-enters through the normal boot path.
- `TutorialStepContext.Resolve()` — `twinSelectorMono` had no runtime fallback (unlike all
  other cross-scene refs in the same method). Added `FindAnyObjectByType<TwinSelector>()`
  so TutorialDirector in any area scene finds TwinSelector from Persistent automatically.

### Added
- `game.md` — full game systems reference (input, health/bond, combat, abilities, Accord
  State + Setsuna, time-freeze, rescue events, hybrid GOAP/BT/FSM AI + enemy ecology,
  enemy roster, spawning, progression, tutorial, QTE, dialogue/localization, camera, area
  streaming, UI/VFX), an implementation-vs-design-docs diff, and a recommended
  production-grade folder structure.
- `changelog.md` — this file.
- `WIRING_GUIDE.md` — complete multi-scene Inspector wiring checklist (SOs to create,
  per-scene wiring, build settings order, render settings, occlusion culling, known gaps).
- Multi-scene **area streaming** architecture (branch `multiscenesetup`):
  - `Bootstrap.unity` + `GameBootstrapper` — entry point, two modes (intro / dev).
  - `Intro.unity` + `IntroController` — skippable cutscene that background-loads scenes.
  - `Persistent.unity` — never-unloaded scene; holds all manager singletons.
  - `SceneFlowManager` — occupant-based streaming (Graves pattern); keeps occupied area + adjacents loaded.
  - `WorldLocationSO` — per-chunk SO; holds scene ref, adjacency, entrance definitions.
  - `LocationEntrance` — named spawn-point registry; lets `SoftResetController` place twins on arrival.
  - `SceneLoadTrigger` — boundary trigger that calls `SceneFlowManager.NotifyTwinEntered/Exited`.
  - `SoftResetController` — replaces CheckpointLoader; soft-reset without scene reload.
  - `IntroTimelinePositioner` — snaps twins to gameplay start after Timeline stops.
- **QTE system** (new, replaces old QTEController):
  - `QTEManager` (Persistent singleton) — state machine, shared screen-space UI.
  - `QTESceneAnchor` (per-QTE, area scene) — bundles trigger points, camera, activatables.
  - `QTEDefinitionSO` — data: mash duration/count/key, instruction text, event ID.
  - `QTEZoneTrigger`, `QTESuccessWatcher` — zone entry and success reaction hooks.
  - `QTE_ParkGate.asset` — first QTE definition (Park Gate event).
  - `EnemyFreezeService` — implements `IEnemyFreezeService`; uses `FindObjectsByType<Enemy>`
    across all loaded scenes; lives on QTEManager GO in Persistent.
- **Pause / Settings UI** (Persistent.unity):
  - `PauseMenuController` — ESC priority chain: SkillPreviewModal → Settings → Pause.
  - `SettingsMenuController` — language, resolution, window mode, cursor, master/music/sfx volume.
  - Full canvas hierarchy built in Persistent: PauseRoot (DimPanel + MenuCard + 3 buttons)
    + SettingsPanel (scroll view with 7 rows + Apply/Back). All Inspector refs wired.
- `TutorialOverlayController` (Persistent singleton) — schedule-1-style video+text overlay.
- `TutorialHintDisplay` (Persistent singleton) — inline hint text strip.
- `TutorialDirector` — lightweight per-area step sequencer (not a singleton; add one per level).
- `TutorialStepContext` — per-area context bag; `overlay`/`hintDisplay` now auto-resolve from
  Persistent singletons when not wired in Inspector.

### Changed
- Scenes renamed/restructured: `L1Park.unity` → `L1_Park/L1_Park.unity`,
  `L2Streets.unity` → `L2_Streets/L2_Streets.unity`. Folder per scene keeps navmesh
  and occlusion assets co-located.
- `EnemySpawner` — removed redundant serialized `barrierTransform` field; barrier side
  is now always resolved at call time via `POIManager.GetNearest(POIType.Barrier)`,
  eliminating stale cross-scene refs when the area scene unloads.
- `TutorialHUDCanvas` (`TutorialOverlayController`, `TutorialHintDisplay`, `BlackOverlay`,
  `NoticePanel`, `FailureText`) moved from L1Park to Persistent so tutorial HUD is
  available in every area scene. `FailureResetSequencer` and `FailureNotice` now need
  a `TutorialHUDProvider` singleton (pending) to find their UI refs cross-scene.
- `AbilityController.ActivateTeleport()` — barrier Transform is now resolved lazily from
  `BarrierPOI/POIManager` (DIP). Removed the serialized `barrierTransform` field from
  `TwinAbilitySetup`; no cross-scene Inspector ref needed.
- `EnemySpawner.GetSideForPosition()` — barrier fallback via `POIManager.GetNearest(POIType.Barrier)`.
- `TutorialStepContext.Resolve()` — `overlay`/`hintDisplay` fall back to Persistent singletons
  so `TutorialDirector` in any area scene works without cross-scene Inspector wiring.
- `CheckpointLoader` — marked `[Obsolete]`; replaced by `SoftResetController`.

### Removed
- `MainCameraProvider.cs` and `ILookTargetProvider.cs` — unused dead code. Nothing called
  `GetTargetTransform()`; `MainCameraProvider` also returned `Camera` while the interface
  declared `Transform`, making it a broken non-implementation. `UIBillboard` caches
  `Camera.main` directly and needs neither.

### In progress
- `TutorialHUDProvider` singleton needed in Persistent — `FailureResetSequencer` and
  `FailureNotice` on TutorialManager (L2_Streets) reference `BlackOverlay`, `NoticePanel`,
  and `FailureText` that now live in Persistent; cross-scene serialized refs are broken
  until `TutorialHUDProvider` is written and wired.
- Scene Editor wiring: WorldLocationSO assets not yet created; QTEManager UI refs, Bootstrap
  and Intro Inspector refs, and L1_Park QTE wiring still outstanding (see `WIRING_GUIDE.md`).
- Player repositioning after the intro timeline finishes — `IntroTimelinePositioner` exists but
  not yet added to TutorialTimelineDirector GO.
- **Penitent** enemy rework (Ikari + grab-to-death timing) — present but flagged unstable.

### Removed
- `AreaManager` — removed from Persistent.unity (legacy streaming approach replaced by
  `SceneFlowManager`). Still present in L1Park.unity — needs deletion there too.

### Known issues
- Soul Convergence counter cap toned to ~8 for the prototype (design target: 20).
- ~~Setsuna drives global `Time.timeScale`; timers that must keep real time need `unscaledDeltaTime`.~~ Fixed Phase 5.
- ~~`AbilityUpgradeData` stores runtime state (`currentNodeIndex`) on the ScriptableObject asset.~~ Fixed Phase 4.
- Debug skill-point keys (L/O/P/I/K) are still active and must be removed before release.
- **HUD scripts missing `FindAnyObjectByType` fallbacks** — the following scripts have
  serialized refs to Persistent singletons that are now null/mismatched after the multi-scene
  move and need `Awake()` fallbacks added before they will function:
  `SkillTreeUI` (`_dataStoreMono`/`_purchaserMono`/`_pointBankMono` → `SkillTreeManager`),
  `AccordBarView` (`accordSystem`, `unlockStateMono`),
  `SkillPointsHUDView` (`_pointBankMono`),
  `AbilitiesHUDController` (`accordSystem`, `empowerSystem`, `skillUnlockState`),
  `OverviewCamHUDView` (`overviewController`),
  `KillParticleSpawnner` (`deathNotifier` — "Scene mismatch").
- `QTESceneAnchor` (ParkGateQTEAnchor) World UI refs (Root Panel, Fill Bar, Timer Ring,
  labels) not wired — children of `QTEParkCanvasUI` in L1_Park, same scene, needs manual wiring.
- `TutorialStepContext.overlay` shows "Scene mismatch" — stale serialized ref to old L1Park
  scene; clear the Inspector slot so the `Resolve()` fallback (`TutorialOverlayController.Instance`)
  takes over.
- `FailureResetSequencer` and `FailureNotice` show Missing refs — their UI targets
  (`BlackOverlay`, `NoticePanel`, `FailureText`) moved to Persistent; blocked on
  `TutorialHUDProvider` (see In progress above).
- `TutorialDirector.Awake()` locks input immediately — if `inputGate` is not wired in Inspector,
  players can move freely during the opening cutscene.

---

## How to log changes

Add entries under `## [Unreleased]` using these groups (omit empty ones):

- **Added** — new features.
- **Changed** — changes to existing behaviour.
- **Fixed** — bug fixes.
- **Removed** — removed features or deleted code.
- **Deprecated** — soon-to-be-removed features.
- **Security** — security-relevant changes.

Write entries from the player/maintainer's perspective, newest first. When a build is cut,
rename `[Unreleased]` to `## [x.y.z] - YYYY-MM-DD` and start a new empty `[Unreleased]`
section above it. Dates use `YYYY-MM-DD`.
