# BUGS.md — Planet of Twins Defect Ledger

Single source of truth for defects across sessions.
Seeded 2026-06-12 from game.md §21 (known open issues) and instruction.md §11 (failure-mode forecasts).

**Entry states:** Open · In-Progress · Fixed · Verified · Watch  
**Severity:** Blocker · Major · Minor  
**Rules:** Log before fixing. `Fixed` requires a commit/changelog ref. `Verified` requires the matching DoD step to have run in-editor. Regressions reopen the same entry and increment `Regressions:`.

---

## Open / In-Progress bugs (from game.md §21)

---

### BUG-001 — SkillTreeUI missing Persistent refs
Status: Open  
Severity: Major  
System: UI / SkillTree  
Symptom: Skill tree canvas renders blank; purchasing nodes does nothing — all button callbacks hit null.  
Root cause: `_dataStoreMono`, `_purchaserMono`, `_pointBankMono` were serialized to `SkillTreeManager` when both lived in L1Park. `SkillTreeManager` moved to Persistent; refs are now stale None.  
Fix: instruction.md Phase 1 row 1 — re-wire Inspector (both ends in Persistent) + R4 `field ??= SkillTreeManager.Instance` in `Start()`.  
Verified by: —  
Regressions: 0

---

### BUG-002 — AccordBarView missing Persistent refs
Status: Open  
Severity: Major  
System: UI / AccordState  
Symptom: Accord power-bar never fills; Accord icons don't update.  
Root cause: `accordSystem` → `AccordStateSystem`, `unlockStateMono` → `SkillTreeManager`, both moved to Persistent. Serialized refs stale.  
Fix: instruction.md Phase 1 row 2 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-003 — SkillPointsHUDView missing Persistent refs
Status: Open  
Severity: Major  
System: UI / SkillTree  
Symptom: Skill-point counter on HUD always shows 0.  
Root cause: `_pointBankMono` → `SkillTreeManager` in Persistent; ref stale.  
Fix: instruction.md Phase 1 row 3 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-004 — AbilitiesHUDController missing Persistent refs
Status: Open  
Severity: Major  
System: UI / Abilities  
Symptom: Ability icons and Empower/Accord readouts show no state.  
Root cause: `accordSystem`, `empowerSystem`, `skillUnlockState` all live in Persistent; all three refs stale.  
Fix: instruction.md Phase 1 row 4 — re-wire Inspector + R4 Start() resolve for each.  
Verified by: —  
Regressions: 0

---

### BUG-005 — OverviewCamHUDView missing Persistent refs
Status: Open  
Severity: Minor  
System: UI / Camera  
Symptom: Overview camera button does not highlight; overview mode may not activate.  
Root cause: `overviewController` → `OverviewCamController` in Persistent; ref stale.  
Fix: instruction.md Phase 1 row 5 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-006 — KillParticleSpawnner "Scene mismatch" on deathNotifier
Status: Open  
Severity: Major  
System: VFX / Enemy  
Symptom: No kill particles appear on enemy death; console logs "Scene mismatch" for `KillParticleSpawnner.deathNotifier`.  
Root cause: `deathNotifier` → `EnemyDeathNotifier` in Persistent; serialized ref is to the old L1Park instance and now reads as a different scene.  
Fix: instruction.md Phase 1 row 6 — verify KillParticleSpawnner residency (Persistent or area); re-wire deathNotifier via Inspector (if both in Persistent) or R5 registry pattern.  
Verified by: —  
Regressions: 0

---

### BUG-007 — SharedHealthPresenter uses banned FindAnyObjectByType pattern
Status: Open  
Severity: Major  
System: UI / Health  
Symptom: Health bar works incidentally now but will break on scene reload if Awake order changes; forbidden pattern makes order-dependent bugs hard to diagnose.  
Root cause: `sharedHealthPool` and `emergencyMonitor` fallbacks use `FindAnyObjectByType<T>()` in Awake — this pattern is revoked (R4 requires `field ??= Manager.Instance` in `Start()`).  
Fix: instruction.md Phase 1 row 7 — replace FindAnyObjectByType fallbacks with R4; re-wire Inspector (both ends in Persistent).  
Verified by: —  
Regressions: 0

---

### BUG-008 — QTESceneAnchor ParkGateQTEAnchor World UI not wired
Status: Open  
Severity: Blocker  
System: QTE  
Symptom: QTE ring and fill-bar never appear during park gate event.  
Root cause: `QTESceneAnchor` World UI slots (Root Panel, Fill Bar, Timer Ring, labels) are children of `QTEParkCanvasUI` in L1_Park — same scene, but Inspector slots were never wired manually.  
Fix: instruction.md Phase 1 row 11 — wire all UI slots in Inspector; confirm `WorldSpaceCanvasCamera` is on `QTEParkCanvasUI`.  
Verified by: —  
Regressions: 0

---

### BUG-009 — TutorialStepContext stale overlay ref + banned FindAnyObjectByType
Status: Open  
Severity: Major  
System: Tutorial  
Symptom: Console shows "Scene mismatch" for `overlay`; if TwinSelector not wired, tutorial steps that lock twin selection silently fail.  
Root cause: (a) `overlay` serialized slot still points to old L1Park scene object — must be cleared. (b) `Resolve()` added `FindAnyObjectByType<TwinSelector>()` and `FindAnyObjectByType<RescueEventController>()` — both are revoked by instruction.md. (c) `overlay` serialized field itself should be deleted (cross-scene ref, always wrong under R2).  
Fix: instruction.md Phase 1 row 8 — clear stale Inspector slots; delete `overlay` serialized field; add `TwinSelector.Instance` + `RescueEventController.Instance` resolution in `Start()` (both need `Instance` properties added).  
Verified by: —  
Regressions: 0

---

### BUG-010 — FailureResetSequencer component in wrong scene
Status: Open  
Severity: Blocker  
System: Tutorial / UI  
Symptom: `_postProcessVolume` and `_blackOverlay` show Missing refs in Inspector; failure-reset visual (greyscale → black → teleport → colour restore) never plays.  
Root cause: Component sits on `TutorialManager` in area scene but its deps (`BlackOverlay`, `_postProcessVolume`) live in Persistent's `TutorialHUDCanvas`. Cross-scene serialized ref is R2 violation; must be fixed by relocating the component to Persistent.  
Fix: instruction.md Phase 2 — move `FailureResetSequencer` component to `TutorialHUDCanvas` in Persistent; add `static Instance` property; wire same-scene R1 refs; update `TutorialBoundary` / `TutorialOuterBoundary` callers to use `FailureResetSequencer.Instance`.  
Verified by: —  
Regressions: 0

---

### BUG-011 — FailureNotice component in wrong scene
Status: Open  
Severity: Blocker  
System: Tutorial / UI  
Symptom: `_noticePanel` and `_noticeText` show Missing refs; failure banner never shows on rescue fail.  
Root cause: Component sits on `TutorialManager` in area scene; `NoticePanel` and `FailureText` live in Persistent's `TutorialHUDCanvas`. Same R2 violation as BUG-010.  
Fix: instruction.md Phase 2 — move `FailureNotice` to `TutorialHUDCanvas` in Persistent; add `static Instance` property; wire same-scene R1 refs; delete `failureNotice` and `resetSequencer` slots from `TutorialStepContext`.  
Verified by: —  
Regressions: 0

---

### BUG-012 — Penitent enemy requires rework
Status: Open  
Severity: Major  
System: Enemy / AI  
Symptom: Penitent sometimes deals damage before grab animation completes; Ikari snap reactions fire at wrong timing; grab-to-death sequence can skip.  
Root cause: Ikari timing thresholds and grab-to-death state machine transitions need tuning; flagged in both story bible and commits.  
Fix: instruction.md §19 (designed-but-incomplete) — rework `PenitentEnemy` GOAP brain grab-to-death flow; tune Ikari snap timings.  
Verified by: —  
Regressions: 0

---

### BUG-013 — Soul Convergence counter capped at ~8 (design target 20)
Status: Open  
Severity: Minor  
System: Ability / Soul Convergence  
Symptom: Soul Convergence charges after killing ~8 enemies rather than the designed 20.  
Root cause: Prototype cap left in code; `SoulConvergenceSystem` max-stack tunable not set to final value.  
Fix: Tune `SoulConvergenceSystem` max-stack / count to 20 when progression design is final.  
Verified by: —  
Regressions: 0

---

### BUG-014 — Setsuna drives global Time.timeScale without arbitration
Status: Open  
Severity: Major  
System: Ability / Setsuna / Time  
Symptom: All `Time.deltaTime`-based timers (health drain, cooldowns, enemy AI timers) slow down or stop during Setsuna activation instead of staying on real time.  
Root cause: `SetsunaSystem` writes `Time.timeScale` directly (one of seven independent writers — see BUG-022). No `TimeScaleService` arbitration; callers use `deltaTime` instead of `unscaledDeltaTime`.  
Fix: instruction.md Phase 5 (R10 audit) — route Setsuna's timeScale write through `TimeScaleService`; audit all timers for `unscaledDeltaTime` where real-time behaviour is required.  
Verified by: —  
Regressions: 0

---

### BUG-015 — AbilityUpgradeData.currentNodeIndex stored on SO asset (R7 violation)
Status: Open  
Severity: Major  
System: Progression / SkillTree  
Symptom: Skill tree progress persists between Editor play sessions (SO asset is dirty after play); resetting the game does not reset skill level; could ship with non-zero saved state.  
Root cause: `AbilityUpgradeData` SO stores `currentNodeIndex` as a mutable field — violates R7 (SOs = config only, never runtime state).  
Fix: instruction.md Phase 4 — extract runtime progression holder; reset rigorously or use `CheckpointManager` save slot; SO must be read-only at runtime.  
Verified by: —  
Regressions: 0

---

### BUG-016 — Debug skill-point keys (L/O/P/I/K) still active
Status: Open  
Severity: Minor  
System: Debug / Input  
Symptom: Pressing L/O/P/I/K during gameplay gives free skill points — exploitable by players in shipped build.  
Root cause: Debug key handlers left in `TwinInputReader` (or whichever script reads them); not guarded by `#if UNITY_EDITOR` or a cheat-lock flag.  
Fix: instruction.md §20.3 — move behind `#if UNITY_EDITOR` or remove; controlled by `PoT.Editor` asmdef.  
Verified by: —  
Regressions: 0

---

### BUG-017 — IntroTimelinePositioner not attached to TutorialTimelineDirector
Status: Open  
Severity: Major  
System: Tutorial / Timeline  
Symptom: After the intro cutscene plays, both twins remain at their last position (not at the designed gameplay start); player must manually walk to the start area.  
Root cause: `IntroTimelinePositioner` component written but not added to the `TutorialTimelineDirector` GameObject.  
Fix: instruction.md Phase 7 — attach `IntroTimelinePositioner` to `TutorialTimelineDirector`; wire twin references; set gameplay-start positions.  
Verified by: —  
Regressions: 0

---

### BUG-018 — CommonStatic GO in L1_Park needs post-restore verification
Status: Open  
Severity: Minor  
System: Scene / Rendering  
Symptom: Possible missing meshes, broken occlusion culling, or incorrect static-batching in L1_Park after drag-in from Restore.unity.  
Root cause: `CommonStatic`, `CommonStatic (1)`, `CommonStatic (2)` were accidentally deleted from L1_Park; a new `Restore.unity` scene was created from git history to restore them. Mesh references and occlusion data must be confirmed after drag-in.  
Fix: instruction.md Phase 6 — open L1_Park + Restore.unity additively, drag GOs into L1_Park, delete Restore.unity, re-bake occlusion if needed.  
Verified by: —  
Regressions: 0

---

### BUG-019 — MonoBehaviourSingleton\<T\> applies DDOL and fabricates GOs (systemic R3 violation)
Status: Open  
Severity: Blocker  
System: Architecture / Singletons  
Symptom: On scene reload, duplicate singleton instances accumulate in DDOL limbo. Calling `T.Instance` when no real GO exists fabricates a blank nameless GameObject — components on it have no scene context and serialized fields are all null.  
Root cause: `MonoBehaviourSingleton<T>` base class calls `DontDestroyOnLoad(gameObject)` in Awake (banned by R3) and creates a new instance in a blank GO when `_instance == null`. Should rely on Persistent.unity residency instead.  
Fix: instruction.md Phase 1.4 — remove DDOL call from base class; remove fabrication fallback; add null-Instance guard log; fix `LanguageManager` direct DDOL call; add `StandaloneSingleton` restart safety.  
Verified by: —  
Regressions: 0

---

### BUG-020 — SceneFlowManager uses int occupancy counts, not per-actor transition model
Status: Open  
Severity: Blocker  
System: SceneStreaming  
Symptom: Mismatched enter/exit calls (e.g., teleport bypasses boundary, or twin dies mid-transition) can leave occupancy count at non-zero forever, preventing area unload or causing premature unload.  
Root cause: `SceneFlowManager` tracks scene occupancy with increment/decrement integers. No per-actor token; any missed exit call corrupts the count permanently.  
Fix: instruction.md Phase 3.7 — replace int counts with `HashSet<string>` actor tokens; add `NotifyTeleported`; add `OnLocationWillUnload` callback; fix `SetActiveScene`; use unscaled unload delay.  
Verified by: —  
Regressions: 0

---

### BUG-021 — Rescue checkpoint never activates after timeline completes
Status: Open  
Severity: Blocker  
System: Tutorial / Timeline / Checkpoint  
Symptom: After the intro timeline finishes, the first rescue-event checkpoint does not activate; if a twin is downed before the player passes it manually, game over triggers with no respawn.  
Root cause: suspected — `TutorialDirector` checkpoint-activation step wired to a timeline signal that may not fire, or the `TutorialCheckpointEntry` at index 12 has a wiring slip.  
Fix: instruction.md Phase 7 (7.6a–b) — verify timeline signal → `TutorialDirector` step wiring; fix checkpoint entry 12; test rescue event triggers correctly after timeline.  
Verified by: —  
Regressions: 0

---

### BUG-022 — Seven independent Time.timeScale writers with no arbitration (R10 violation)
Status: Open  
Severity: Major  
System: Time / Architecture  
Symptom: One system restoring `timeScale = 1` can clobber another system's active slow-mo or freeze, causing gameplay to snap to wrong speed unexpectedly.  
Root cause: Identified writers: `SetsunaSystem`, `GameOverController`, `PauseMenuController`, `TutorialOverlayController`, `TimeFactorManager` (freeze), and at least two others write `Time.timeScale` directly. No `TimeScaleService` arbiter exists.  
Fix: instruction.md Phase 5 (R10) — create `TimeScaleService` with priority stack; route all seven writers through it; no direct `Time.timeScale =` writes anywhere except `TimeScaleService.Apply()`.  
Verified by: —  
Regressions: 0

---

### BUG-023 — Tutorial step SOs leak event subscriptions
Status: Open  
Severity: Major  
System: Tutorial  
Symptom: After the first playthrough (or on scene reload without domain reload), tutorial steps fire twice; events accumulate across play sessions in Editor.  
Root cause: Tutorial step ScriptableObjects subscribe to events in `OnEnable` (or on `Execute`) but do not unsubscribe in `OnDisable` / `OnDestroy`. SOs survive domain reload — subscriptions accumulate.  
Fix: instruction.md Phase 5 (R8 audit) — add matching unsubscribe in `OnDisable`/`OnDestroy` for every step SO that subscribes to events.  
Verified by: —  
Regressions: 0

---

### BUG-024 — TutorialTrap never unregisters from TutorialBoundary
Status: Open  
Severity: Minor  
System: Tutorial  
Symptom: After a TutorialTrap is destroyed or deactivated, `TutorialBoundary` still holds a stale reference; next boundary query throws MissingReferenceException.  
Root cause: `TutorialTrap.OnDestroy` (or `OnDisable`) does not call `TutorialBoundary.Unregister()`.  
Fix: instruction.md Phase 5 (R8 audit) — add `OnDisable`/`OnDestroy` unregister call in `TutorialTrap`.  
Verified by: —  
Regressions: 0

---

### BUG-025 — SetsunaSystem reads raw Input.GetKey (bypasses Input System)
Status: Open  
Severity: Minor  
System: Ability / Setsuna / Input  
Symptom: Setsuna activation cannot be rebound; does not work with a gamepad; ignores tutorial input gate.  
Root cause: `SetsunaSystem` calls `Input.GetKey(KeyCode.X)` directly instead of using the Input System action already wired through `TwinInputReader`.  
Fix: instruction.md §20.3 — replace raw `Input.GetKey` in `SetsunaSystem` with the appropriate `InputAction` from `TwinInputReader` (same pattern as other abilities).  
Verified by: —  
Regressions: 0

---

### BUG-026 — ESC triple-consume in PauseMenuController
Status: Open  
Severity: Minor  
System: UI / Pause  
Symptom: Pressing ESC once triggers three state changes: closes a modal, opens Settings, then opens Pause — or similar unintended cascade. Priority chain fires on the same key-down event.  
Root cause: `PauseMenuController` reads ESC without consuming it after the first handler matches; all three branches (SkillPreviewModal → Settings → Pause) run in the same frame.  
Fix: instruction.md Phase 5 (R8 / ESC arbiter) — add `break`/`return` after first handler matches; use a single ESC action with one-frame consumed flag.  
Verified by: —  
Regressions: 0

---

### BUG-027 — TutorialDirector.Awake() locks input (R8 violation)
Status: Open  
Severity: Major  
System: Tutorial / Input  
Symptom: If `inputGate` is not wired in Inspector when TutorialDirector starts, `LockInput()` called in `Awake()` before any step runs means players are frozen indefinitely. Also violates R8 (Awake should wire self only; Start resolves others).  
Root cause: `TutorialDirector.Awake()` calls `inputGate.LockInput()` and attempts cross-scene resolution — both belong in `Start()`.  
Fix: instruction.md Phase 1 row 12 — move input lock to `Start()`; cache `TwinInputReader.Instance` in field; delete dead `_realInputMono`.  
Verified by: —  
Regressions: 0

---

## Watch entries (from instruction.md §11 — failure-mode forecasts)

Watch entries are known risk patterns not yet manifesting as confirmed bugs.
They become Open entries if a symptom is observed in-editor.

---

### BUG-W01 — Duplicate managers after restart
Status: Watch  
Severity: Blocker  
System: Architecture / Singletons  
Symptom: After `SceneManager.LoadScene("Bootstrap")`, Persistent managers exist twice — the DDOL survivor and a fresh one loaded by Bootstrap. Both receive events; the wrong one is referenced by `Instance`.  
Root cause: `MonoBehaviourSingleton<T>` calls DDOL (see BUG-019). Not yet confirmed to manifest in multi-scene boot flow.  
Fix: BUG-019 fix (Phase 1.4) eliminates root cause.  
Verified by: —  
Regressions: 0

---

### BUG-W02 — Static-event double-fire after domain-reload-free restart
Status: Watch  
Severity: Major  
System: Architecture / Events  
Symptom: Events defined as `static event Action` fire twice per trigger after the second play session in Editor (without domain reload), because delegate lists accumulate across sessions.  
Root cause: Static events are never cleared between play sessions unless domain reload runs. Subscribers added in `OnEnable` without a matching `OnDisable` unsubscribe remain on the static delegate.  
Fix: Phase 5 (R8 audit) — ensure all static-event subscribers follow R8 (OnEnable subscribe, OnDisable unsubscribe). Consider `[RuntimeInitializeOnLoadMethod]` reset for critical static state.  
Verified by: —  
Regressions: 0

---

### BUG-W03 — Awake-order nulls on direct-area play
Status: Watch  
Severity: Blocker  
System: Architecture / Lifecycle  
Symptom: When pressing Play with an area scene open (no Bootstrap), scripts that resolve manager refs in `Awake()` receive null because Persistent managers haven't called their own `Awake()` yet.  
Root cause: Unity's Awake-order across additive scenes is undefined. Scripts that resolve other scripts in `Awake()` instead of `Start()` are order-dependent.  
Fix: Phase 0.1 (`PersistentSceneAutoLoader` — implemented 2026-06-12) ensures Persistent is loaded via `BeforeSceneLoad` before any scene's `Awake()` runs. Phase 1 (R8) will eliminate the remaining `Awake()` resolves in gameplay scripts.  
Verified by: Phase 0 DoD — needs in-editor confirmation (pending user test).  
Regressions: 0

---

### BUG-W04 — PersistentSceneAutoLoader double-load in Editor
Status: Watch  
Severity: Minor  
System: Editor / Scene Loading  
Symptom: Opening the Editor with both Persistent.unity and an area scene already loaded, then pressing Play, loads a second copy of Persistent.  
Root cause: `PersistentSceneAutoLoader` runs without checking if Persistent is already loaded.  
Fix: Phase 0.1 (implemented 2026-06-12) — guards with `SceneManager.GetSceneByName(PersistentScene).isLoaded` and also returns early if the active scene is Bootstrap or Persistent.  
Verified by: Phase 0 DoD — needs in-editor confirmation (pending user test).  
Regressions: 0

---

### BUG-W05 — Wrong-instance grabs from MonoBehaviourSingleton fabrication
Status: Watch  
Severity: Blocker  
System: Architecture / Singletons  
Symptom: A system calls `Manager.Instance` before the real GO has woken up; `MonoBehaviourSingleton` fabricates a blank GO. Subsequent calls return the blank instance — all serialized fields null.  
Root cause: BUG-019 fabrication fallback. Not yet confirmed to cause a visible failure in the current scene setup.  
Fix: BUG-019 fix (Phase 1.4).  
Verified by: —  
Regressions: 0

---

### BUG-W06 — EnemyPool corruption on area unload
Status: Watch  
Severity: Blocker  
System: Enemy / Pool  
Symptom: After an area scene unloads, pooled enemies returned to a pool whose host GO was destroyed cause MissingReferenceException on the next borrow.  
Root cause: `EnemyPool` may live in the area scene rather than Persistent; its GOs are destroyed with the scene but `EnemySpawner` in another scene still holds a reference.  
Fix: Phase 3.2 — verify `EnemyPool` residency; move to Persistent if area-scene-resident; add despawn-on-unload (Phase 3.3).  
Verified by: —  
Regressions: 0

---

### BUG-W07 — Registry iteration over destroyed entries
Status: Watch  
Severity: Major  
System: Architecture / Registries  
Symptom: `SpawnZoneRegistry` or similar registry iterates a list that contains destroyed GOs; `MissingReferenceException` in Update or OnEnable.  
Root cause: Components registering in `OnEnable` but not unregistering in `OnDisable`/`OnDestroy` leave stale refs. Registry doesn't null-check before iteration.  
Fix: Phase 3.1a (SpawnZoneRegistry ordering hazard); R8 rule enforcement for all registries.  
Verified by: —  
Regressions: 0

---

### BUG-W08 — LocationEntrance registered after SceneFlowManager queries it
Status: Watch  
Severity: Major  
System: SceneStreaming / Tutorial  
Symptom: `SoftResetController` requests an entrance from a `WorldLocationSO` before `LocationEntrance.OnEnable()` has run — returns null; twins teleport to world-origin (0,0,0).  
Root cause: `SceneFlowManager` may query entrance positions before the area scene's `Start()`/`OnEnable()` chain completes. R8 says entrance registration belongs in `OnEnable`; queries must happen in `Start()` or later.  
Fix: Phase 3 — ensure `LocationEntrance` uses `OnEnable`/`OnDisable`; `SoftResetController` queries in a coroutine that waits one frame after scene-load.  
Verified by: —  
Regressions: 0

---

### BUG-W09 — Duplicate EventSystem / AudioListener in area scenes
Status: Watch  
Severity: Major  
System: Architecture / Scene (R9)  
Symptom: Two EventSystems active simultaneously → console spam "Multiple EventSystems detected"; UI input processed twice. Two AudioListeners → Unity warning and possible doubled audio.  
Root cause: Area scenes were built as standalone scenes and contain their own `EventSystem` and `AudioListener`; these were not removed when Persistent took ownership (R9).  
Fix: Phase 1.5 — delete `EventSystem`, `AudioListener`, and standalone `MainCamera` from all area scenes; verify only Persistent owns each.  
Verified by: —  
Regressions: 0

---

### BUG-W10 — World-space canvas with dead Event Camera in area scene
Status: Watch  
Severity: Major  
System: UI / Camera  
Symptom: World-space canvas doesn't receive click/hover events or renders without proper depth; Event Camera slot shows None at runtime in area scenes other than L1_Park.  
Root cause: `WorldSpaceCanvasCamera` was only attached to `QTEParkCanvasUI` in L1_Park; other World Space canvases in area scenes have no runtime Event Camera assignment.  
Fix: Phase 1.5 — add `WorldSpaceCanvasCamera` to every World Space canvas in every area scene (BUG-008 is the specific L1_Park instance already tracked).  
Verified by: —  
Regressions: 0

---

### BUG-W11 — Coroutine killed by host GO deactivation
Status: Watch  
Severity: Major  
System: Tutorial / Time  
Symptom: `FailureResetSequencer.TriggerReset()` coroutine stops mid-sequence when the host GO is deactivated (e.g., by a Timeline Activation Track), leaving the twins in greyscale with black overlay permanently.  
Root cause: Unity kills all coroutines on a MonoBehaviour when its GO is deactivated. If `TutorialHUDCanvas` or `TutorialManager` GO is toggled by a Timeline track mid-coroutine, the reset sequence never completes.  
Fix: Phase 2 (relocation to Persistent TutorialHUDCanvas) + Phase 7 (Activation Track audit — R11).  
Verified by: —  
Regressions: 0

---

### BUG-W12 — Scaled-time stalls during Setsuna time-slow
Status: Watch  
Severity: Major  
System: UI / Time  
Symptom: Animations, timers, and UI transitions that use `WaitForSeconds` or `deltaTime` freeze or slow to a crawl when Setsuna reduces `timeScale` to 0.2.  
Root cause: These systems must use `WaitForSecondsRealtime` or `unscaledDeltaTime`; they currently use the scaled variants. Linked to BUG-014 (Setsuna timeScale) and BUG-022 (no arbitration).  
Fix: Phase 5 (R10 audit) — audit all coroutines and UI tween callers; switch to unscaled time where real-time behaviour is required.  
Verified by: —  
Regressions: 0

---

### BUG-W13 — timeScale leak through GameOverController restart
Status: Watch  
Severity: Major  
System: UI / Time  
Symptom: If game-over triggers while `timeScale != 1` (pause, Setsuna, freeze), the Bootstrap reload inherits the wrong `timeScale`, causing the new session to run at incorrect speed from frame 0.  
Root cause: `GameOverController.RestartScene()` loads Bootstrap without resetting `Time.timeScale = 1` first (it was only partially fixed — see changelog). DontDestroyOnLoad scenarios may also carry stale timeScale.  
Fix: Phase 5 (R10) — `GameOverController` must call `TimeScaleService.ForceResetToOne()` before `SceneManager.LoadScene(bootstrap)`.  
Verified by: —  
Regressions: 0

---

### BUG-W14 — Cross-scene VCam serialization
Status: Watch  
Severity: Minor  
System: Camera / Cinemachine  
Symptom: Cinemachine virtual cameras in area scenes have serialized `Follow`/`LookAt` target refs that point to Player GOs in Persistent — these show "Scene mismatch" in Inspector and may lose tracking after scene reload.  
Root cause: Cinemachine 3 VCam `Follow`/`LookAt` resolved by name at runtime if the serialized ref is null — but only if `CinemachineCore.SoloCamera` is not interfering. Cross-scene serialized refs violate R2.  
Fix: Phase 7 — replace cross-scene serialized VCam targets with runtime assignment (`vcam.Follow = TwinSelector.Instance.LeftTwin.transform`) in a `CinemachineTargetSetup` MonoBehaviour in the area scene.  
Verified by: —  
Regressions: 0

---

### BUG-W15 — Activation Track ancestor deactivates manager GO (R11 violation)
Status: Watch  
Severity: Blocker  
System: Timeline / Architecture  
Symptom: A Timeline Activation Track that targets the parent of a singleton manager deactivates it, stopping all coroutines and making `Instance` return a deactivated object.  
Root cause: R11 states Timeline bindings must be scene-local; Activation Tracks must never control GO hierarchies that contain gameplay-logic ancestors (managers, singletons, event dispatchers).  
Fix: Phase 7 (Timeline audit) — review all Activation Track bindings; replace any that target manager ancestors with a dedicated visual-only proxy GO.  
Verified by: —  
Regressions: 0

---

### BUG-W16 — timeScale stomping (two systems write in same frame)
Status: Watch  
Severity: Major  
System: Time  
Symptom: Two systems both write `Time.timeScale` in the same frame (e.g., Pause sets 0 while Setsuna sets 0.2 in the same Update); one silently wins, the other's state is lost.  
Root cause: No arbitration — raw `Time.timeScale =` writes anywhere in the codebase can collide. Directly linked to BUG-022.  
Fix: Phase 5 `TimeScaleService` (BUG-022 fix) eliminates root cause.  
Verified by: —  
Regressions: 0

---

### BUG-W17 — Straddle unload (twin exits and enters boundary in same frame)
Status: Watch  
Severity: Major  
System: SceneStreaming  
Symptom: A fast-moving twin crosses a boundary trigger both ways in one physics step; `NotifyTwinEntered` and `NotifyTwinExited` fire in the same frame, leaving occupancy count wrong.  
Root cause: Trigger OnEnter/OnExit both fire within a single FixedUpdate when a twin passes completely through a thin boundary. Integer occupancy goes +1 then −1, net zero, but the intended semantics require the twin to be "inside."  
Fix: Phase 3.7 (actor-token model) — per-actor `HashSet` is immune to this; set semantics (add / remove) are idempotent.  
Verified by: —  
Regressions: 0

---

### BUG-W18 — Teleport occupancy desync
Status: Watch  
Severity: Major  
System: SceneStreaming / Ability  
Symptom: `AbilityController.ActivateTeleport()` moves a twin across a boundary without triggering `SceneLoadTrigger`, leaving occupancy count wrong and the wrong area loaded.  
Root cause: Emergency teleport bypasses the `SceneLoadTrigger` boundary; `SceneFlowManager` never receives the occupancy change notification.  
Fix: Phase 3.7 (`NotifyTeleported` API) — `AbilityController` calls `SceneFlowManager.NotifyTeleported(twin, newPosition)` after each teleport.  
Verified by: —  
Regressions: 0

---

### BUG-W19 — Soft-reset ghosts (enemies / pickups persist after SoftResetController)
Status: Watch  
Severity: Major  
System: SceneStreaming / Respawn  
Symptom: After `SoftResetController` resets the twins' positions, enemies or soul-orbs from the previous area state remain active in the scene, causing duplicate spawns or incorrect combat state.  
Root cause: `SoftResetController` resets twin positions but does not trigger a full enemy / pickup despawn sweep; pooled enemies not returned to pool; area-content registry may not reset.  
Fix: Phase 3.3 (despawn-on-unload) + `SoftResetController.PerformReset()` should call `EnemyPool.DespawnAll()` for the old area.  
Verified by: —  
Regressions: 0

---

### BUG-W20 — SO state bleeding between Editor sessions (R7 violation)
Status: Watch  
Severity: Major  
System: Progression / SkillTree  
Symptom: Opening the Editor after a play session shows skill tree nodes already unlocked in a fresh run; shipped build could start with wrong progression if asset is dirty.  
Root cause: Same as BUG-015 — `AbilityUpgradeData.currentNodeIndex` is written at runtime to the SO asset. Confirmed regression risk for SO-state pattern across the codebase.  
Fix: Phase 4 (BUG-015 fix) eliminates root cause.  
Verified by: —  
Regressions: 0

---

### BUG-W21 — NavMesh seam drops between additive scene boundaries
Status: Watch  
Severity: Major  
System: AI / Navigation  
Symptom: Enemy pathfinding stops or enemies teleport/snap at the boundary between two loaded area scenes; NavMesh Agent loses path.  
Root cause: Each area scene has its own NavMesh bake; additive loading does not automatically stitch seams. NavMesh Link components or a merged bake strategy required.  
Fix: Phase 3 / production polish — add `NavMeshLink` at area boundaries; verify NavMesh bake settings align across seams.  
Verified by: —  
Regressions: 0

---

### BUG-W22 — Lighting pop on additive area load
Status: Watch  
Severity: Minor  
System: Rendering / Scene  
Symptom: When a new area scene is loaded additively and set as the active scene, the ambient light, fog, and skybox snap to the new area's render settings, causing a visible pop.  
Root cause: `SceneManager.SetActiveScene()` swaps render settings atomically. No cross-fade or blending.  
Fix: Phase 3.7e (unscaled unload delay) — also add a brief render-settings blend coroutine (or pre-match area render settings) when `SetActiveScene` is called.  
Verified by: —  
Regressions: 0

---

### BUG-W23 — FX scene-unload leak (F1: stale follow-target after area unload)
Status: Watch  
Severity: Major  
System: FX / SceneStreaming  
Symptom: After an area scene unloads, a pooled VFX or particle instance under Persistent that had `followTarget` pointing into the unloaded scene now follows a destroyed Transform; MissingReferenceException spam or phantom effect frozen at world origin.  
Root cause: `FxManager` has not yet subscribed `SceneFlowManager.OnLocationWillUnload` to stop and reclaim instances whose follow-target lives in the unloading scene (§14.4 F1).  
Fix: P9.1 — subscribe in `FxManager.OnEnable`, named handler, unsubscribe in `OnDestroy` (R8); iterate active handles, stop any whose `context.followTarget` is in the unloading scene's root.  
Verified by: —  
Regressions: 0

---

### BUG-W24 — Stale VFX children on pooled enemy reuse (F2)
Status: Watch  
Severity: Major  
System: FX / EnemyPool  
Symptom: A pooled enemy returned to the pool carries live particle/VFX children from its previous life (e.g., stun corona, aura); on next despawn the effects are double-active, orphaned, or counted twice in the pool's handle table.  
Root cause: `EnemyPool` return path does not call `FxManager.StopAllOn(enemy.transform)` before deactivating the GO. An existing instance of this exact bug is the `StunVfxSystem` stale-child report (§14.6 Tier 1).  
Fix: P9.1 — add `FxManager.StopAllOn` call in `EnemyPool` return; guaranteed naked despawn for all enemy archetypes. Resolve before `StunVfxSystem` is migrated to cue handles.  
Verified by: —  
Regressions: 0

---

### BUG-W25 — Setsuna snapshot never released on ForceEnd (F3)
Status: Watch  
Severity: Major  
System: FX / Audio / Setsuna  
Symptom: After Setsuna is cancelled via `ForceEnd`, the `Setsuna` mixer snapshot remains active (low-pass + pitch shift persist); or the snapshot is released but a second call to `RequestSnapshot(Setsuna)` from a subsequent activation tries to release a handle that was never registered.  
Root cause: `SetsunaSystem` must call `AudioManager.ReleaseSnapshot(this)` on **both** `ForceEnd` and natural end paths (§14.4 F3 — the teleport-cancel precedent from §11 applies verbatim).  
Fix: P9.2 — add release on both end paths in `SetsunaSystem`; add an arbiter EditMode test that verifies empty → Default after release.  
Verified by: —  
Regressions: 0

---

### BUG-W26 — UI sounds muted during pause (F4)
Status: Watch  
Severity: Minor  
System: Audio / Pause  
Symptom: Button clicks and confirm sounds are inaudible while the pause menu is open; player receives no feedback from UI interactions while paused.  
Root cause: Gameplay audio correctly pauses via `AudioListener.pause`, but UI cues must use `AudioManager.PlayUI` (unscaled + `ignoreListenerPause = true`). Until P9.2 lands, no such path exists.  
Fix: P9.2 — implement `AudioManager.PlayUI`; wire all `SkillNodeButton`, pause-menu buttons, and confirm sounds through it.  
Verified by: —  
Regressions: 0

---

### BUG-W27 — Orphaned FX after soft reset (F5)
Status: Watch  
Severity: Minor  
System: FX / Audio / SoftReset  
Symptom: After `SoftResetController` restores twins to checkpoint, pooled particle/VFX instances (e.g., mid-cast ability aura, enemy hit-flash) continue playing in the scene; audio from the prior encounter bleeds through.  
Root cause: `SoftResetController.PerformReset()` does not call `FxManager.StopAll()` + `AudioManager.StopAllSfx()` (§14.4 F5). These calls do not yet exist.  
Fix: P9.1 (FxManager.StopAll) + P9.2 (AudioManager.StopAllSfx) — once both exist, P7.5 SoftResetController teardown adds both calls alongside its existing ForceEnd list.  
Verified by: —  
Regressions: 0

---

### BUG-W28 — FX/audio pool rebuild on Restart fails if static Instance not nulled (F6)
Status: Watch  
Severity: Major  
System: FX / Audio / Bootstrap  
Symptom: After a full Restart (Bootstrap reload), `FxManager.Instance` or `AudioManager.Instance` returns a stale reference; VFX pool contains pre-Restart prefab instances that were destroyed; any `Play` call throws MissingReferenceException.  
Root cause: If `FxManager`/`AudioManager` do not null their static `Instance` in `OnDestroy`, the Restart path finds the old pointer and skips `Awake` re-initialization (§14.4 F6).  
Fix: P9.1 — both managers must follow the `MonoBehaviourSingleton<T>` contract: null `Instance` in `OnDestroy`, rebuild pool in `Awake`. No static caches in the FX/audio layer.  
Verified by: —  
Regressions: 0

---

### BUG-W29 — FX/audio manager null on editor direct-play (F7)
Status: Watch  
Severity: Major  
System: FX / Audio / EditorDirectPlay  
Symptom: Playing directly in L1_Park without Bootstrap, `_fx` or `_audio` is null at the first gameplay event; consumer is silently disabled.  
Root cause: R4 `field ??= FxManager.Instance` in consumer `Start()` requires `PersistentSceneAutoLoader` to have loaded Persistent first. If that loader fires too late, `FxManager.Instance` fabricates a blank unwired GO (§0 footgun — instruction.md §1.4).  
Fix: P9.1 — verify `PersistentSceneAutoLoader` fires before any area `Start()` in the editor; R4 consumers LogError + `enabled = false` if null after `Start()` (fail loud, never silent).  
Verified by: —  
Regressions: 0

---

### BUG-W30 — Voice exhaustion from one high-frequency system (F8)
Status: Watch  
Severity: Minor  
System: Audio  
Symptom: A combat encounter with many melee hits or spawn events in one frame exhausts the 32-voice pool; low-priority sounds (music, ambience) are incorrectly stolen; or the stealer log floods the editor console.  
Root cause: Per-cue `cooldown` and `maxSimultaneous` fields do not yet exist; voice stealing has no per-system budget. Without them, 20+ melee hits in a 0.15 s Setsuna slow-window all request voices simultaneously.  
Fix: P9.2 — implement `SoundCueData.cooldown` / `maxSimultaneous`; stealing logs in editor only (P8.3 channel); add an arbiter EditMode test for the 33rd-voice steal path.  
Verified by: —  
Regressions: 0

---

### BUG-028 — WorldLocationSO assets not created for Park and Streets
Status: Open  
Severity: Blocker  
System: SceneStreaming  
Symptom: `SceneFlowManager` cannot stream any areas — it has no `WorldLocationSO` to query for scene refs, adjacency, or entrance definitions; the streaming system is entirely disabled.  
Root cause: The `WorldLocationSO` data assets for `L1_Park` and `L2_Streets` have not been created in the project yet. `SceneFlowManager` is waiting on them.  
Fix: instruction.md Phase 7.1 — create `WorldLocationSO` assets (Park↔Streets adjacency, entrance IDs matching `LocationEntrance` names placed in each scene); wire into `SceneFlowManager`.  
Verified by: —  
Regressions: 0

---

### BUG-029 — Skill snapshot covers only 7 of 9 upgrade trees
Status: Open  
Severity: Major  
System: Progression / Checkpoint  
Symptom: After a soft reset or checkpoint load, Empower upgrades and Accord State upgrades are reset to base (never saved nor restored); players re-enter with downgraded abilities.  
Root cause: `SoftResetController.RestoreNodeLevels` and `CheckpointManager.CaptureNodeLevels` both hand-list trees — Stun, Possess, Gate, HealthRegen, AccordSpirits, Coalesce, SoulConv are listed; **Empower and Accord State are missing** (verified against source).  
Fix: instruction.md Phase 7.5 — expose `SkillTreeManager.AllTrees` (`public IReadOnlyList<AbilityUpgradeData>`) and have both snapshot and restore iterate it rather than hand-listing.  
Verified by: —  
Regressions: 0

---

### BUG-030 — TutorialCheckpoint entry 12 wired to wrong checkpoint (latent Dual-mode slip)
Status: Open  
Severity: Minor  
System: Tutorial / Checkpoint  
Symptom: In Dual-rescue mode, entry 12 "Rescue point B" points to `CheckpointsRescueL` — the same as entry 11 — so both rescue points are the left one; Kai never teleports to the correct right reset.  
Root cause: Inspector wiring slip on `TutorialDirector.checkpoints[12]`; accidentally set to `CheckpointsRescueL` instead of `CheckpointsRescueR`. Single mode only reads index 11 so this does not manifest today, but enabling Dual mode would expose it.  
Fix: instruction.md 7.6f — wire entry 12 to `CheckpointsRescueR` in Inspector.  
Verified by: —  
Regressions: 0

---

### BUG-031 — SetsunaSystem rewind is not actually invulnerable
Status: Open  
Severity: Major  
System: Ability / Setsuna  
Symptom: An enemy hit landing during the 1.5 s rewind window empties the shared health pool and triggers game over before the health snapshot is restored — despite the code comments and flow claiming invulnerability.  
Root cause: `SetInvulnerable()` only locks twin movement; it does not call `Health.SetInvincible(true)` on either twin. An enemy can still deal damage during the entire rewind coroutine.  
Fix: instruction.md 7.6j — call `Health.SetInvincible(true/false)` on both twins inside `SetInvulnerable`; rename the method to match what it actually does; the API exists (`TeleportAbility` already uses it).  
Verified by: —  
Regressions: 0

---

### BUG-032 — TutorialTimelineDirector bound to a pre-multiscene world (11 null track bindings)
Status: In-Progress  
Severity: Major  
System: Tutorial / Timeline / SceneStreaming  
Symptom: The tutorial intro cinematic plays wrong — camera does not cut (Cinemachine Track dead), screen fade never fires, HUD/nameplate toggles do nothing, and several geometry/framing flourishes are missing. In the Inspector the `PlayableDirector` shows many `None` bindings; "scene mismatch" / null-binding behaviour.  
Root cause: The timeline was authored in the **single-scene** era (`Assets/Scenes/L1Park.unity`, no underscore — still in git at HEAD) **before** the multiscene split and **before** the level was re-greyboxed. The live director now lives in untracked `Assets/Scenes/L1_Park/L1_Park.unity`. Of its **42** track bindings, **31 still resolve**; **11 are null** (recovered by diffing the old scene's `m_SceneBindings` and resolving each original fileID to a GameObject name):
- **Moved to Persistent (cross-scene, R2 forbids serializing them) — rebound at runtime:** Cinemachine Track → `CinemachineBrain` on Main Camera; Signal Track → `SignalReceiver` on `CameraManager`; Activation 8 + Animation 7 → `FadeCanvas`/`FadeController` (verified attached, Persistent.unity line ~3402); Activation 22 → `HUD_Canvas`; **Activation 1/2 → `TutorialGroupTransposeClose`/`Top` camera framing groups — MOVED to Persistent, NOT deleted (verified Persistent.unity lines 26340/41859); the timeline toggles them off to avoid framing conflict**; Activation 9 → `SkyboxChanger` (moved to Persistent so the skybox persists across levels).
- **The Persistent twins (NOT "nameplates"):** Activation 20/21 toggle the actual twin GOs `Lyra` (7057096503670301586) / `Kai` (7062396770087208489) on prefab `Twins.prefab` (`1a6989451c8ad6b45ae5126cfc5ab821`) — the single-scene cutscene **deactivated the twins to "lock" them**. In multiscene we must never toggle the Persistent twin GOs (R11/BUG-W15); these tracks are **deleted** and the lock is done in code (`IntroTimelinePositioner` lock-on-play, fixed 2026-06-14).
- **Deleted by the re-greybox (unrecoverable — no code can "find" a deleted object):** Activation 10/11 → `MainLvl (1)`/`(2)` (geometry).

Note: this timeline does **not** animate the Persistent twins directly (no Kai/Lyra bindings); the real twins are repositioned afterward by `IntroTimelinePositioner`. The new scene already has `TimelineDollyCam/1/2` vcams and mostly-wired CinemachineShot exposed refs (one null).  
Fix (mechanism — never hand-edit `.playable`/scene `m_SceneBindings` YAML; that is what corrupts it):
- **Continuous cross-scene tracks (Cinemachine/Animation):** leave the binding empty; `TimelineBindingResolver` (on the director GO) rebinds at runtime via `SetGenericBinding` resolving the Persistent target **by type/singleton, not by name string** (R11/R4). The lone `CinemachineTrack`→Brain needs zero authoring.
- **Cross-scene actions (fade, hide HUD):** use **Signals** — a `SignalEmitter` (asset, serializes fine) on a track bound to a **local** `SignalReceiver` on the director GO, whose UnityEvent calls a **local relay** that forwards to the Persistent system at runtime (e.g. `FadeController.StartFromBlack`). The Signal Track binding alone is still scene-local, so the receiver/relay must be local — Signals do not cross scenes by themselves.
- **Dead/anti-pattern tracks:** remove Activation 10/11 (deleted geometry) and Activation 20/21 (the twin toggles — lock is done in code now). Activation 1/2 (transpose) and 9 (skybox) are **not** dead — they rebind to their now-Persistent targets via the registry (below).
- User edits the Timeline themselves; deliverable is the role-based resolver + custom inspector + usage guide (see plan `eager-cooking-crane.md`). **Implementation reference: instruction.md §16** — `TimelineBindingResolver` (continuous tracks + role map, §16.1) + `TimelineSignalRelay` (cross-scene actions/the Signal fix, §16.2) + the not-to-do list (§16.3) + the **per-track wiring map §16.5** (exactly which of the 11 tracks rebinds vs becomes a Signal vs is deleted). Related: BUG-W14 (cross-scene VCam), BUG-W15 (Activation Track ancestor), BUG-021 (rescue checkpoint after timeline), BUG-017 (IntroTimelinePositioner).  
Progress (2026-06-14, changelog ref): final design is **registry-based** — new `TimelineTargetRegistry` (Persistent component holding R1 same-scene refs to the cross-scene targets) + `TimelineBindingResolver` rewritten to `BindingRole { CameraManager, FadeCanvas, HudCanvas, TransposeClose, TransposeTop, SkyboxChanger }`, each role resolved through the registry. The registry disambiguates same-type targets (e.g. the two transpose cameras) without name strings — which `FindAnyObjectByType` alone cannot. Cinemachine auto-binds by type; also shipped `TimelineSignalRelay` + `TimelineBindingResolverEditor` dropdown. **Two related gameplay bugs fixed in-scene (2026-06-14):** twins were movable during the cutscene → now locked via `IntroTimelinePositioner.OnTimelinePlayed`; wrong-twin reset teleported to crossed points → `leftResetPoint`/`rightResetPoint` swapped under the two tutorial checkpoints (user-verified correct). **Remaining (user, Editor):** wire `TimelineTargetRegistry` fields in Persistent; add resolver rows for the cross-scene tracks (Transpose Close/Top, CameraManager, FadeCanvas, HudCanvas, SkyboxChanger); delete the 4 dead tracks (Activation 10/11 geometry, 20/21 twins). Compile/in-editor verify pending.  
Verified by: —  
Regressions: 0

---

## Summary

| State | Count |
|-------|-------|
| Open | 31 |
| In-Progress | 1 |
| Fixed | 0 |
| Verified | 0 |
| Watch | 30 |
| **Total** | **62** |

*Last swept: 2026-06-14 (BUG-032 classification corrected — Activation 1/2 transpose cameras MOVED to Persistent not deleted, Activation 20/21 are the Lyra/Kai twin GOs not "nameplates"; resolver finalized as registry-based `TimelineTargetRegistry` + roles incl. `SkyboxChanger`; two same-session scene fixes recorded — cutscene twin-lock + wrong-twin reset-point swap. Prior: 2026-06-13 BUG-032 added; Phase 9 spec §14; BUG-W23–W30 for F1–F8; L1_Park R3 duplicate GOs deleted)*
