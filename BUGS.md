# BUGS.md — Planet of Twins Defect Ledger

Single source of truth for defects across sessions.
Seeded 2026-06-12 from game.md §21 (known open issues) and instruction.md §11 (failure-mode forecasts).

**Entry states:** Open · In-Progress · Fixed · Verified · Watch · Won't-Fix (only via the Exemption Ledger, instruction.md §17)  
**Severity:** Blocker · Major · Minor  
**Rules:** Log before fixing. `Fixed` requires a commit/changelog ref. `Verified` requires the matching DoD step to have run in-editor. Regressions reopen the same entry and increment `Regressions:`.

---

## Open / In-Progress bugs (from game.md §21)

---

### BUG-001 — SkillTreeUI missing Persistent refs
Status: Fixed  
Swept 2026-07-03 (P10): Phase 1 landed (changelog "Fixed — Phase 1 (consumer script R4 fallbacks)"); the Phase 0 DoD run recorded **no R4 fallback errors**, and skill-tree UI worked in the 2026-06-21/22 live sessions. In-editor §10 slice re-run pending → not marked Verified.  
Severity: Major  
System: UI / SkillTree  
Symptom: Skill tree canvas renders blank; purchasing nodes does nothing — all button callbacks hit null.  
Root cause: `_dataStoreMono`, `_purchaserMono`, `_pointBankMono` were serialized to `SkillTreeManager` when both lived in L1Park. `SkillTreeManager` moved to Persistent; refs are now stale None.  
Fix: instruction.md Phase 1 row 1 — re-wire Inspector (both ends in Persistent) + R4 `field ??= SkillTreeManager.Instance` in `Start()`.  
Verified by: —  
Regressions: 0

---

### BUG-002 — AccordBarView missing Persistent refs
Status: Fixed  
Swept 2026-07-03 (P10): Phase 1 landed (changelog, consumer R4 fallbacks); no R4 fallback errors in the Phase 0 DoD run. §10 re-run pending → not Verified.  
Severity: Major  
System: UI / AccordState  
Symptom: Accord power-bar never fills; Accord icons don't update.  
Root cause: `accordSystem` → `AccordStateSystem`, `unlockStateMono` → `SkillTreeManager`, both moved to Persistent. Serialized refs stale.  
Fix: instruction.md Phase 1 row 2 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-003 — SkillPointsHUDView missing Persistent refs
Status: Fixed  
Swept 2026-07-03 (P10): Phase 1 landed (changelog, consumer R4 fallbacks); no R4 fallback errors in the Phase 0 DoD run. §10 re-run pending → not Verified.  
Severity: Major  
System: UI / SkillTree  
Symptom: Skill-point counter on HUD always shows 0.  
Root cause: `_pointBankMono` → `SkillTreeManager` in Persistent; ref stale.  
Fix: instruction.md Phase 1 row 3 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-004 — AbilitiesHUDController missing Persistent refs
Status: Fixed  
Swept 2026-07-03 (P10): Phase 1 landed (changelog, consumer R4 fallbacks); ability HUD live in the 2026-06-21/22 sessions. §10 re-run pending → not Verified.  
Severity: Major  
System: UI / Abilities  
Symptom: Ability icons and Empower/Accord readouts show no state.  
Root cause: `accordSystem`, `empowerSystem`, `skillUnlockState` all live in Persistent; all three refs stale.  
Fix: instruction.md Phase 1 row 4 — re-wire Inspector + R4 Start() resolve for each.  
Verified by: —  
Regressions: 0

---

### BUG-005 — OverviewCamHUDView missing Persistent refs
Status: Fixed  
Swept 2026-07-03 (P10): Phase 1 landed (changelog, consumer R4 fallbacks); `OverviewCamController` further reworked in Phase 5 (TimeScaleService owner). §10 re-run pending → not Verified.  
Severity: Minor  
System: UI / Camera  
Symptom: Overview camera button does not highlight; overview mode may not activate.  
Root cause: `overviewController` → `OverviewCamController` in Persistent; ref stale.  
Fix: instruction.md Phase 1 row 5 — re-wire Inspector + R4 Start() resolve.  
Verified by: —  
Regressions: 0

---

### BUG-006 — KillParticleSpawnner "Scene mismatch" on deathNotifier
Status: Fixed  
Swept 2026-07-03 (P10): code-read — `KillParticleSpawnner.cs:21-45` now does R4 `deathNotifier ??= EnemyDeathNotifier.Instance` + R8 named-handler resubscribe with fail-loud LogError; kill particles confirmed working in the kill-sequence cue sessions (game.md §23.11).  
Severity: Major  
System: VFX / Enemy  
Symptom: No kill particles appear on enemy death; console logs "Scene mismatch" for `KillParticleSpawnner.deathNotifier`.  
Root cause: `deathNotifier` → `EnemyDeathNotifier` in Persistent; serialized ref is to the old L1Park instance and now reads as a different scene.  
Fix: instruction.md Phase 1 row 6 — verify KillParticleSpawnner residency (Persistent or area); re-wire deathNotifier via Inspector (if both in Persistent) or R5 registry pattern.  
Verified by: —  
Regressions: 0

---

### BUG-007 — SharedHealthPresenter uses banned FindAnyObjectByType pattern
Status: Fixed  
Swept 2026-07-03 (P10): grep — `SharedHealthPresenter` no longer contains `FindAnyObjectByType` (Phase 1 consumer R4 fallbacks).  
Severity: Major  
System: UI / Health  
Symptom: Health bar works incidentally now but will break on scene reload if Awake order changes; forbidden pattern makes order-dependent bugs hard to diagnose.  
Root cause: `sharedHealthPool` and `emergencyMonitor` fallbacks use `FindAnyObjectByType<T>()` in Awake — this pattern is revoked (R4 requires `field ??= Manager.Instance` in `Start()`).  
Fix: instruction.md Phase 1 row 7 — replace FindAnyObjectByType fallbacks with R4; re-wire Inspector (both ends in Persistent).  
Verified by: —  
Regressions: 0

---

### BUG-008 — QTESceneAnchor ParkGateQTEAnchor World UI not wired
Status: Fixed  
Swept 2026-07-03 (P10): the park QTE ran to **Success** in live play (changelog: "Gate animation never fired after QTE success — `QTESceneAnchor.activatableMono` was empty", a follow-on wiring bug found *and fixed* mid-play) — the anchor UI slots are wired. The Scene Health Dashboard QTE recipe (game.md §23.15.2, P14) becomes the standing lint for this class.  
Severity: Blocker  
System: QTE  
Symptom: QTE ring and fill-bar never appear during park gate event.  
Root cause: `QTESceneAnchor` World UI slots (Root Panel, Fill Bar, Timer Ring, labels) are children of `QTEParkCanvasUI` in L1_Park — same scene, but Inspector slots were never wired manually.  
Fix: instruction.md Phase 1 row 11 — wire all UI slots in Inspector; confirm `WorldSpaceCanvasCamera` is on `QTEParkCanvasUI`.  
Verified by: —  
Regressions: 0

---

### BUG-009 — TutorialStepContext stale overlay ref + banned FindAnyObjectByType
Status: Fixed  
Swept 2026-07-03 (P10): code-read — `TutorialStepContext.Resolve()` is rewritten: overlay/hint/notice resolve via `Instance` with fail-loud LogErrors; the remaining `FindAnyObjectByType` uses are the documented allowed sweeps (area-local `TutorialInputGate` fallback; non-singleton `FadeController`/`CameraRotationGuard`, annotated "the allowed scene-scoped sweep, R4 note" in-source).  
Severity: Major  
System: Tutorial  
Symptom: Console shows "Scene mismatch" for `overlay`; if TwinSelector not wired, tutorial steps that lock twin selection silently fail.  
Root cause: (a) `overlay` serialized slot still points to old L1Park scene object — must be cleared. (b) `Resolve()` added `FindAnyObjectByType<TwinSelector>()` and `FindAnyObjectByType<RescueEventController>()` — both are revoked by instruction.md. (c) `overlay` serialized field itself should be deleted (cross-scene ref, always wrong under R2).  
Fix: instruction.md Phase 1 row 8 — clear stale Inspector slots; delete `overlay` serialized field; add `TwinSelector.Instance` + `RescueEventController.Instance` resolution in `Start()` (both need `Instance` properties added).  
Verified by: —  
Regressions: 0

---

### BUG-010 — FailureResetSequencer component in wrong scene
Status: Fixed  
Swept 2026-07-03 (P10): Phase 2 landed (changelog "Added — Phase 2 (FailureNotice / FailureResetSequencer → Persistent)"); both are Persistent singletons in the CLAUDE.md manager table; the failure-reset visual ran in the 2026-06-19 rescue sessions. Its `_postProcessVolume` slot is the P17 FailureResetProfile home (ArtStyle.md §11.1).  
Severity: Blocker  
System: Tutorial / UI  
Symptom: `_postProcessVolume` and `_blackOverlay` show Missing refs in Inspector; failure-reset visual (greyscale → black → teleport → colour restore) never plays.  
Root cause: Component sits on `TutorialManager` in area scene but its deps (`BlackOverlay`, `_postProcessVolume`) live in Persistent's `TutorialHUDCanvas`. Cross-scene serialized ref is R2 violation; must be fixed by relocating the component to Persistent.  
Fix: instruction.md Phase 2 — move `FailureResetSequencer` component to `TutorialHUDCanvas` in Persistent; add `static Instance` property; wire same-scene R1 refs; update `TutorialBoundary` / `TutorialOuterBoundary` callers to use `FailureResetSequencer.Instance`.  
Verified by: —  
Regressions: 0

---

### BUG-011 — FailureNotice component in wrong scene
Status: Fixed  
Swept 2026-07-03 (P10): Phase 2 landed (same changelog entry as BUG-010); Persistent singleton; failure banner shown in the BUG-033 rescue-fail flow.  
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
Status: Fixed  
Swept 2026-07-03 (P10): Phase 5 landed — `SetsunaSystem.Activate → TimeScaleService.Request(this, factor)`, `BeginRewind`/`ForceEnd → Release(this)` (changelog Phase 5 writer list); grep confirms no direct `Time.timeScale` write remains in SetsunaSystem.  
Severity: Major  
System: Ability / Setsuna / Time  
Symptom: All `Time.deltaTime`-based timers (health drain, cooldowns, enemy AI timers) slow down or stop during Setsuna activation instead of staying on real time.  
Root cause: `SetsunaSystem` writes `Time.timeScale` directly (one of seven independent writers — see BUG-022). No `TimeScaleService` arbitration; callers use `deltaTime` instead of `unscaledDeltaTime`.  
Fix: instruction.md Phase 5 (R10 audit) — route Setsuna's timeScale write through `TimeScaleService`; audit all timers for `unscaledDeltaTime` where real-time behaviour is required.  
Verified by: —  
Regressions: 0

---

### BUG-015 — AbilityUpgradeData.currentNodeIndex stored on SO asset (R7 violation)
Status: Fixed  
Swept 2026-07-03 (P10): Phase 4 landed — `currentNodeIndex` is now a computed property over `SkillTreeManager.GetLevel()` (`AbilityUpgradeData.cs:54`, code-read today); runtime levels live in `SkillTreeRuntimeState` ("Replaces the R7-violating … mutable field", its own header). Leftover `currentNodeIndex:` YAML lines in `.asset` files are dead remnants.  
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
Status: Fixed  
Swept 2026-07-03 (P10): attached and functioning — the 2026-06-14 in-scene work wired it (BUG-032 progress: "twins were movable during the cutscene → now locked via `IntroTimelinePositioner.OnTimelinePlayed`"); post-timeline repositioning ran in every live session since.  
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
Status: Won't-Fix — Exemption E1 (instruction.md §17)  
Swept 2026-07-03 (P10): the Phase 1.4 base-class correction **was applied (changelog "Fixed — Phase 1.4") and then reverted** — it completely broke the enemy/perception system; the user locked the current behaviour (DDOL + lazy fabrication) as deliberate. **Do not reattempt.** Live mitigations: duplicate-destroy `Awake` guards on project singletons, and BUG-040's `FindFirstObjectByType` bootstrapper fix for the fabrication class of failure. Verified today: `MonoBehaviourSingleton.cs` still unparents + DDOLs — that is the *intended* state.  
Severity: Blocker  
System: Architecture / Singletons  
Symptom: On scene reload, duplicate singleton instances accumulate in DDOL limbo. Calling `T.Instance` when no real GO exists fabricates a blank nameless GameObject — components on it have no scene context and serialized fields are all null.  
Root cause: `MonoBehaviourSingleton<T>` base class calls `DontDestroyOnLoad(gameObject)` in Awake (banned by R3) and creates a new instance in a blank GO when `_instance == null`. Should rely on Persistent.unity residency instead.  
Fix: instruction.md Phase 1.4 — remove DDOL call from base class; remove fabrication fallback; add null-Instance guard log; fix `LanguageManager` direct DDOL call; add `StandaloneSingleton` restart safety.  
Verified by: —  
Regressions: 0

---

### BUG-020 — SceneFlowManager uses int occupancy counts, not per-actor transition model
Status: Fixed  
Swept 2026-07-03 (P10): code-read — `SceneFlowManager` runs `HashSet<WorldLocationSO>` loaded/loading/unloading sets (`SceneFlowManager.cs:62-64`), `NotifyTeleported(actor, destination)` exists (`:117`) with a call-site comment map, and `OnLocationWillUnload` is live (FxManager subscribes it). Phase 3 changelog entry covers the conversion.  
Severity: Blocker  
System: SceneStreaming  
Symptom: Mismatched enter/exit calls (e.g., teleport bypasses boundary, or twin dies mid-transition) can leave occupancy count at non-zero forever, preventing area unload or causing premature unload.  
Root cause: `SceneFlowManager` tracks scene occupancy with increment/decrement integers. No per-actor token; any missed exit call corrupts the count permanently.  
Fix: instruction.md Phase 3.7 — replace int counts with `HashSet<string>` actor tokens; add `NotifyTeleported`; add `OnLocationWillUnload` callback; fix `SetActiveScene`; use unscaled unload delay.  
Verified by: —  
Regressions: 0

---

### BUG-037 — Camera flipped 180° (intermittent) — group-transpose vcam occasionally flips
Status: Fixed  
Severity: Major (gameplay-readability; movement still correct because WASD is camera-relative)  
System: Camera / Cinemachine / Timeline  
Discovered: 2026-06-21 (live play session, MCP-inspected — caught while flipped)

**FIX SHIPPED (2026-06-22) — robust workaround that sidesteps the timeline entirely:** Rather than chase the timeline binding (every "fix the value / fix the wire" attempt below recurred, because the timeline animation tracks re-apply the flip each cutscene run — confirmed: no camera *code* writes rotation, and `.playable` YAML is not hand-editable, R11), we **correct the cameras behind the white fade at cutscene end**. `Camera/CameraRotationGuard.cs` (Persistent) snapshots each gameplay/tutorial transpose cam's **authored** local rotation at `Awake` (before the timeline runs) and re-applies it via `RestoreAll()`. `TutorialTimelineStepSO`, after the timeline finishes (screen white), calls `RestoreAll()` (the snap is invisible behind the opaque fade) then `FadeController.FadeOut` reveals the corrected game over 2.3s. The dev tutorial-skip path also restores + clears the fade. So whatever pose the timeline left a cam in, it's corrected to its authored rotation while hidden — the player never sees a flip. Changelog: `[Unreleased] Added — Camera flip fix (BUG-037)`. Verified live: boots into a clean, correctly-oriented camera. The Inspector-rewire fix below remains the *root-cause* option if the timeline is ever reworked; the guard makes it non-urgent.

Live evidence the recurrence is timeline-driven (2026-06-22): paused mid-session with `CameraManager` active cam = `GroupTransposeClose`, its `localEuler=(14,180,0)` and the Brain rendering (14,180,0) — flipped — while `GroupTransposeTop` was correct at `(72,0,0)`. So "sometimes Close, sometimes Top" = whichever the timeline last left flipped; a code grep confirmed only the timeline binding files reference these cams, no camera C# writes Y.
**Corrected understanding (2026-06-21 — user clarification; supersedes the "inconsistent authored Y" hypothesis below):**
Camera flow: tutorial uses distance-driven `Tutorial*` cams (authored Y=180, +Z) from the streets through the park. At the **last timeline step before the rescue step**, the timeline switches the active cams to the **gameplay `GroupTranspose*` cams (Top + Close)** (authored Y=0, −Z). The two sets are intentional mirror images so framing is consistent — **both authorings are CORRECT.**
**The bug:** after the timeline hands off to the GroupTranspose gameplay cams, **one of them (usually Top or Close) renders in the game window at Y=180 instead of its authored Y=0** — the flipped orientation. Gameplay still works (movement is camera-relative forward) but the view faces backward. So this is a **runtime rotation bug at the timeline→gameplay-cam handoff**, NOT an authoring error: the GroupTranspose cam's effective Y becomes 180 at runtime despite being authored 0.
**ROOT CAUSE — PROVEN LIVE (2026-06-21):** The `TutorialTimelineDirector` has **`Activation Track (22)` bound to `GroupTransposeTop`** (the gameplay cam). When the timeline ends and rewinds (`time=0.00/32.55`, extrapolation=None), the Activation Track re-applies its post-playback state to `GroupTransposeTop`, which leaves its **own local transform at `localEuler=(71.88, 180.00, 0)` — flipped from its authored Y=0 to Y=180.** Parent `PlayerCam` stays clean at 0, so the corruption is on `GroupTransposeTop`'s own transform. Decisive evidence: `GroupTransposeTop` is the **ONLY** gameplay cam with an Activation-Track binding, and it is the **ONLY** one that flips; `GroupTransposeClose` and `LevelTopDownCam` (no such binding) always stay Y=0. Live trace: before/mid-timeline `GroupTransposeTop worldY=0.0/0.1`; immediately after timeline end with `CameraManager._currentCam=GroupTransposeTop`, MainCamera renders `euler=(71.9,180.0,0)` and `GroupTransposeTop worldY=180.0`. This is the documented R11 / BUG-032 Activation-Track-on-gameplay-object footgun ("Activation Tracks never control ancestors of gameplay-logic objects ... always set explicit Post-playback state").

**Deeper cause — why BOTH Top and Close flip (2026-06-21, components inspected):** Both `GroupTransposeTop` and `GroupTransposeClose` aim via **`CinemachineHardLookAt`** (+`CinemachineFollow`, `CinemachineGroupFraming`) at `Target Group`. HardLookAt computes a look-rotation every frame with **no "correct side"** — the authored Y (0/180) is only a start value, overridden at runtime. At the **dolly→gameplay handoff** (timeline end, Brain `Cinemachine Track` bound to `Main Camera` releases), the incoming gameplay cam's HardLookAt re-resolves toward `Target Group` and can settle on **either** side (Y=0 or Y=180) depending on the camera's momentary position and the blend's interpolation path. `Activation Track (22)` toggling `GroupTransposeTop` forces Top to re-resolve at the boundary (so Top flips most), but `GroupTransposeClose` shares the same HardLookAt ambiguity and flips on runs where the release-blend resolves to the wrong side — hence "sometimes Close too." Intermittent by nature.

**ACTUAL ROOT CAUSE — MIS-WIRED INSPECTOR REFERENCE (2026-06-21, scene/asset inspected with game stopped) — supersedes "remove the Activation Track" and the Cut-blend fix above:**
The timeline director lives in **L1_Park** but the cameras live in **Persistent** — a cross-scene case handled at runtime by `TimelineBindingResolver` (L1_Park, on the director) + `TimelineTargetRegistry` (Persistent). The saved scene-binding `value`s in L1_Park are all local fileIDs or `0` (no cross-scene GUIDs — R2-legal); the real binding is applied at runtime by the resolver pulling Persistent cameras from the registry **by role**.

The resolver/registry are **designed** to toggle the **Tutorial** transpose cams off during the cutscene:
- `TimelineTargetRegistry.transposeTop` tooltip: *"TutorialGroupTransposeTop camera (toggled off by the timeline)."*
- `transposeClose`: *"TutorialGroupTransposeClose camera…"*

**But at runtime `Activation Track (22)` resolved to `GroupTransposeTop` — the GAMEPLAY cam, not `TutorialGroupTransposeTop`.** So the registry's `transposeTop` field (and likely `transposeClose`) is dragged to the **wrong camera** in the Persistent Inspector: the gameplay `GroupTranspose*` instead of the intended `TutorialGroupTranspose*`. The timeline therefore toggles the **gameplay** camera off/on; on reactivation its `CinemachineHardLookAt` re-resolves toward `Target Group` and can settle on the 180° side → the flip. (Because both gameplay cams share HardLookAt, whichever is mis-wired flips — explains "sometimes Top, sometimes Close.")

**Concrete fix (Inspector references only — NO Timeline/YAML edit, NO blend change, NO track deletion):**
- In **`TimelineTargetRegistry`** (Persistent): set `transposeTop` → **`TutorialGroupTransposeTop`** and `transposeClose` → **`TutorialGroupTransposeClose`** (currently mis-pointed at the gameplay `GroupTranspose*`).
- In **`TimelineBindingResolver._trackBindings`** (L1_Park, on the director): verify each row maps the correct Activation track to role `TransposeTop`/`TransposeClose` (the live track was `Activation Track (22)`, but the code comments expect Activation 1/2 — confirm the right track is in the row and that no row drives a gameplay cam).
After fixing, the timeline toggles only the Tutorial cams; the gameplay `GroupTranspose*` are never touched → no flip. Relates to BUG-032. Regression check: snapshot `GroupTransposeTop`/`Close` `worldEulerY` before vs after timeline end (flip = 0→180 on the active cam) AND confirm `dir.GetGenericBinding(Activation 22)` resolves to a `Tutorial*` cam, not the gameplay one.

**Symptom (player-facing):** The Cinemachine view occasionally faces the *opposite* side of the twins (camera "flipped 180°"), while WASD movement still goes the correct direction (movement is camera-forward-relative). Intermittent — "happens out of nowhere"; stopping and replaying often does not reproduce it.

**Root cause (proven live, NOT a runtime bug — an authoring inconsistency):** The group-transpose virtual cameras (all follow/look-at the same `Target Group`, which is at worldEulerY=0, parent `PlayerCam` at Y=0) are authored with **inconsistent local Y rotation**:
- `GroupTransposeTop` → `localEuler=(71.88, **180**, 0)`  ← flipped
- `TutorialGroupTransposeTop` → `localEuler=(71.88, **180**, 0)`  ← flipped
- `TutorialGroupTransposeClose` → `localEuler=(14.04, **180**, 0)`  ← flipped
- `GroupTransposeClose` → `localEuler=(14.03, **0**, 0)`  ← correct
- `LevelTopDownCam` → `localEuler=(79.51, **0**, 0)`  ← correct

These are fixed-rotation transposers (not FreeLook/composer that auto-aim), so the 180° is baked into the transform. The flip appears **only when `CameraManager` makes a 180°-authored vcam the active one** (`CameraManager._currentCam = GroupTransposeTop` at the moment of capture; MainCamera worldEulerY=180, forward=(0,-0.95,-0.31)). Because `Tutorial*` cams are timeline Activation-Track-toggled (BUG-032 Activation 1/2, BUGS.md line ~779) and `CameraManager` switches cams on game flow, *which* group-transpose cam wins varies run-to-run → intermittent.

**Snapshot when flipped (2026-06-21, realtime≈458):** MainCamera pos=(0,56.08,-26.58) eulerY=180 eulerX=71.9; ActiveVCam-via-CameraManager=`GroupTransposeTop`; `Target Group` worldEulerY=0 pos=(-0.25,1.08,-44.58). Healthy run = `_currentCam` on `GroupTransposeClose`/`LevelTopDownCam` (both Y=0).

**Confirming snapshot (2026-06-21, realtime≈817) — same session, NOT flipped:** `CameraManager._currentCam=GroupTransposeClose` (localEulerY=0) → MainCamera worldEulerY=0, forward=(0,-0.24,0.97), view correct. Cam authored-Y unchanged: `GroupTransposeClose`=0 (correct), `GroupTransposeTop`=180 (flips). **This proves the intermittency is purely *which transpose cam CameraManager selects*, not a runtime rotation** — user confirmed "happens to either close or top, only 1 at a time," which matches: Top is always-flipped by authoring, Close is always-correct.

**Fix (proposed — scene/prefab transform edit, NOT code):** Make the group-transpose cameras consistent — set `GroupTransposeTop`, `TutorialGroupTransposeTop`, `TutorialGroupTransposeClose` local `eulerY` from 180 → 0 to match `GroupTransposeClose`/`LevelTopDownCam` (which view the target from the correct side). Verify after: switch through all camera states and confirm none face the opposite side. (If 180° was deliberate for a specific top-down beat, the alternative is to flip camera-relative input for that cam — but the 0° cams looking correct makes 180° the outlier to fix.) Isolated commit; preserve `.meta` GUIDs.

Verified by: —  
Regressions: 0

### BUG-021 — Rescue checkpoint never activates after timeline completes
Status: Fixed (via BUG-033)  
Swept 2026-07-03 (P10): the reported symptom **was** BUG-033 (the `RescueTrapWatch` prompt/timeScale-0 deadlock), fixed + live-verified 2026-06-19; checkpoint activation itself was live-disproven as the cause (the 2026-06-21 baseline shows both checkpoints armed). Closed with BUG-033; the latent entry-12 wiring slip stays tracked as BUG-030.  
Severity: Blocker  
System: Tutorial / Timeline / Checkpoint  
Symptom: After the intro timeline finishes, the first rescue-event checkpoint does not activate; if a twin is downed before the player passes it manually, game over triggers with no respawn.  
Root cause: suspected — `TutorialDirector` checkpoint-activation step wired to a timeline signal that may not fire, or the `TutorialCheckpointEntry` at index 12 has a wiring slip.  
Fix: instruction.md Phase 7 (7.6a–b) — verify timeline signal → `TutorialDirector` step wiring; fix checkpoint entry 12; test rescue event triggers correctly after timeline.  
Update (2026-06-19): root-cause guess superseded. Live investigation (see **BUG-033**) proved the checkpoint **does** activate (`active:true` / `IsCompleted:true` after the player reaches it) — the post-timeline rescue stall is the `RescueTrapWatch` `WaitUntil(promptDone)` deadlock, not checkpoint activation or the entry-12 slip. Track the real defect under BUG-033.  

**HEALTHY-STATE BASELINE captured 2026-06-21 (bug did NOT happen this run — for diff comparison when it recurs):**
Immediately after `TutorialTimelineDirector` ended (`state=Paused, time=0.00/32.55, extrapolation=None`):
- `CheckpointsRescueR`: `selfActive=True activeInHier=True IsCompleted=False`, collider `enabled=True isTrigger=True`, ancestor `TutorialRescueBounds[ON]`, **marker `selfActive=False`, particle NOT playing (count=0)**.
- `CheckpointsRescueL`: `selfActive=True activeInHier=True IsCompleted=False`, collider `enabled=True isTrigger=True`, ancestor `TutorialRescueBounds[ON]`, **marker `selfActive=True`, particle PLAYING (count=2)**.
- `TutorialRescueBounds`: `activeSelf=True activeInHier=True`. Director has 13 steps. `SHARED IsRescueActive=False`.
- **Asymmetry noted (matches latent BUG-030):** only the **L** checkpoint's marker/particle lit; **R** stayed dark even though both GOs are active+armed. When the bug recurs, capture the SAME fields and diff: chiefly whether `TutorialRescueBounds` flips `[OFF]` (→ R11/BUG-W15 Activation-Track path) and whether either checkpoint is `IsCompleted=True` prematurely or `selfActive=False`.

Verified by: —  
Regressions: 0

---

### BUG-022 — Seven independent Time.timeScale writers with no arbitration (R10 violation)
Status: Fixed  
Swept 2026-07-03 (P10): Phase 5.5 `TimeScaleService` is live (min-value-wins Request/Release) and **all eight** direct writers are migrated (changelog Phase 5 lists each: pause, overlay, game-over, Setsuna, teleport/soul-travel, soft reset, skill tree, overview cam); grep today shows 12 files referencing the service and no stray `Time.timeScale =` writers outside `GameBootstrapper`'s sanctioned boot reset. R10 rewritten as LIVE in CLAUDE.md.  
Severity: Major  
System: Time / Architecture  
Symptom: One system restoring `timeScale = 1` can clobber another system's active slow-mo or freeze, causing gameplay to snap to wrong speed unexpectedly.  
Root cause: Identified writers: `SetsunaSystem`, `GameOverController`, `PauseMenuController`, `TutorialOverlayController`, `TimeFactorManager` (freeze), and at least two others write `Time.timeScale` directly. No `TimeScaleService` arbiter exists.  
Fix: instruction.md Phase 5 (R10) — create `TimeScaleService` with priority stack; route all seven writers through it; no direct `Time.timeScale =` writes anywhere except `TimeScaleService.Apply()`.  
Verified by: —  
Regressions: 0

---

### BUG-023 — Tutorial step SOs leak event subscriptions
Status: Fixed  
Swept 2026-07-03 (P10): Phase 5.2 + 7.6c landed — `TutorialCheckpointStepSO` rewritten with named local handlers + `try/finally` unsubscription (changelog "Changed — Phase 7.6c/7.6h"); pool-reuse subscriber leaks (commanders, spawner, Siphon, RescueEventController) fixed under Phase 5.2 lambda hygiene.  
Severity: Major  
System: Tutorial  
Symptom: After the first playthrough (or on scene reload without domain reload), tutorial steps fire twice; events accumulate across play sessions in Editor.  
Root cause: Tutorial step ScriptableObjects subscribe to events in `OnEnable` (or on `Execute`) but do not unsubscribe in `OnDisable` / `OnDestroy`. SOs survive domain reload — subscriptions accumulate.  
Fix: instruction.md Phase 5 (R8 audit) — add matching unsubscribe in `OnDisable`/`OnDestroy` for every step SO that subscribes to events.  
Verified by: —  
Regressions: 0

---

### BUG-024 — TutorialTrap never unregisters from TutorialBoundary
Status: Fixed  
Swept 2026-07-03 (P10): code-read — `TutorialTrap.OnDisable()` calls `_registry?.UnregisterTrap(this)` (`TutorialTrap.cs:110-112`), with an unregister-then-register guard on the registration path (`:95`). R8-paired.  
Severity: Minor  
System: Tutorial  
Symptom: After a TutorialTrap is destroyed or deactivated, `TutorialBoundary` still holds a stale reference; next boundary query throws MissingReferenceException.  
Root cause: `TutorialTrap.OnDestroy` (or `OnDisable`) does not call `TutorialBoundary.Unregister()`.  
Fix: instruction.md Phase 5 (R8 audit) — add `OnDisable`/`OnDestroy` unregister call in `TutorialTrap`.  
Verified by: —  
Regressions: 0

---

### BUG-025 — SetsunaSystem reads raw Input.GetKey (bypasses Input System)
Status: Fixed  
Swept 2026-07-03 (P10): Phase 7.6j landed ("SetsunaSystem / SoulConvergence / AccordState input hardening"); grep today — zero raw `Input.*` calls remain in `SetsunaSystem` (the F-hold flows through `TwinInputReader`/`IInputProvider`, which is exactly the precedent CLAUDE.md's input footgun cites). Full Input System migration is P13.  
Severity: Minor  
System: Ability / Setsuna / Input  
Symptom: Setsuna activation cannot be rebound; does not work with a gamepad; ignores tutorial input gate.  
Root cause: `SetsunaSystem` calls `Input.GetKey(KeyCode.X)` directly instead of using the Input System action already wired through `TwinInputReader`.  
Fix: instruction.md §20.3 — replace raw `Input.GetKey` in `SetsunaSystem` with the appropriate `InputAction` from `TwinInputReader` (same pattern as other abilities).  
Verified by: —  
Regressions: 0

---

### BUG-026 — ESC triple-consume in PauseMenuController
Status: Fixed  
Swept 2026-07-03 (P10): Phase 5.6 landed — centralized ESC arbiter ("each press closes exactly one layer", `PauseMenuController.cs:59-61`), overlay + SkillTreeUI ESC consumers removed, priority chain overlay → SkillPreviewModal → settings → pause → skill tree (changelog Phase 5.6).  
Severity: Minor  
System: UI / Pause  
Symptom: Pressing ESC once triggers three state changes: closes a modal, opens Settings, then opens Pause — or similar unintended cascade. Priority chain fires on the same key-down event.  
Root cause: `PauseMenuController` reads ESC without consuming it after the first handler matches; all three branches (SkillPreviewModal → Settings → Pause) run in the same frame.  
Fix: instruction.md Phase 5 (R8 / ESC arbiter) — add `break`/`return` after first handler matches; use a single ESC action with one-frame consumed flag.  
Verified by: —  
Regressions: 0

---

### BUG-027 — TutorialDirector.Awake() locks input (R8 violation)
Status: Fixed  
Swept 2026-07-03 (P10): grep — the `LockInput` mechanism no longer exists anywhere in `TutorialSystem/`; input gating moved to the area-resident `TutorialInputGate` push-registration model (`SetGate()`, per-category, consumers fail **open** — null gate = all input allowed), which is R8-safe by construction.  
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
Fix: ~~BUG-019 fix (Phase 1.4)~~ **CANCELLED — Exemption E1 (2026-07-03).** The base class keeps DDOL deliberately. Live mitigations: duplicate-destroy `Awake` guards; restart-loop test (1.4c) remains the canary. Stays Watch.  
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
Root cause: BUG-019 fabrication fallback. Manifested once as BUG-040 (fabricated blank `PerceptionManager`), fixed at the call site.  
Fix: ~~BUG-019 fix (Phase 1.4)~~ **CANCELLED — Exemption E1 (2026-07-03).** Fabrication stays; the pattern for boot-time code is BUG-040's `FindFirstObjectByType` (never `.Instance` before Persistent loads). Stays Watch.  
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
Fix: **landed (Phase 5, 2026-07-03 sweep):** `GameOverController.RestartScene → TimeScaleService.ReleaseAll()` and `GameBootstrapper.BootSequence` sets `timeScale = 1` as its first line. Stays Watch pending a restart-under-Setsuna/pause test.  
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
Fix: **landed — root cause eliminated (2026-07-03 sweep):** `TimeScaleService` live, all writers migrated (BUG-022 Fixed). Stays Watch only as the lint reminder: any future direct write reintroduces it (CodeLintRules covers this).  
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
Fix: **landed — root cause removed (2026-07-03 sweep):** Phase 4 extraction (`SkillTreeRuntimeState`; BUG-015 Fixed). Stays Watch as the R7 pattern sentinel — the residual audit of *other* SOs is game.md §25.2 #5.  
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
Progress (P9.1, 2026-06-14): **code landed** — `FxManager.HandleLocationWillUnload` reclaims every active instance whose `followTarget.gameObject.scene.name` matches the unloading location's `WorldLocationSO.scene.Name`. Subscribed in `Start()` (R4 — `SceneFlowManager.Instance` is not guaranteed at `OnEnable`), named handler, unsubscribed in `OnDestroy`. Pending: in-scene play verification.  
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
Progress (P9.1, 2026-06-14): `FxManager.StopAllOn(Transform)` shipped — stops every cue whose `followTarget` is the target or a child of it. **Remaining:** the `EnemyPool.Return` call site, added alongside the `StunVfxSystem` migration (P9.3 Tier 1).  
Update (P9.3 Tier 1, 2026-06-14): **done in code** — `EnemyPool.Return` now calls `FxManager.Instance?.StopAllOn(instance.transform)`, and `StunVFXSystem` is migrated to a held cue handle per enemy (pooled-reuse safe via handle staleness — a re-stunned enemy whose handle was reclaimed reads `!IsPlaying` and respawns). **Remaining:** in-scene verification that a re-pooled enemy spawns naked (needs `FxManager` + `Cue_Stun` prefab wired).  
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
Progress (P9.2, 2026-06-14): mechanism shipped — `AudioManager.RequestSnapshot/ReleaseSnapshot` via `SnapshotArbiter` (highest-priority-wins), with the arbiter EditMode test (empty→Default, release fallback, 7 cases). **Remaining:** `SetsunaSystem` calls Request on activate + Release on **both** ForceEnd and natural end (call-site, P9.4); the AudioMixer asset needs the `Setsuna` snapshot.  
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
Progress (P9.2, 2026-06-14): `AudioManager.PlayUI` (2D + `ignoreListenerPause`) and `SetPaused/ReleasePaused` (sole `AudioListener.pause` writer, owner set) shipped. **Remaining:** route `SkillNodeButton`/pause buttons through `PlayUI` and `PauseMenuController` through `SetPaused` (call-site, P9.4).  
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
Progress (P9.1, 2026-06-14): `FxManager.StopAll()` shipped. **Remaining:** `AudioManager.StopAllSfx()` (P9.2), then the `SoftResetController` teardown call.  
Update (P9.2, 2026-06-14): `AudioManager.StopAllSfx()` now exists too. **Remaining:** the `SoftResetController` teardown call to both `FxManager.StopAll()` + `AudioManager.StopAllSfx()` (P7.5).  
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
Progress (P9.1, 2026-06-14): `FxManager` follows the R3 contract — duplicate-destroy `Awake` guard, `Instance` nulled in `OnDestroy`, VFX pool rebuilt in `Awake`, **no DontDestroyOnLoad, no static caches**. **Remaining:** `AudioManager` same treatment (P9.2).  
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
Progress (P9.2, 2026-06-14): shipped — `SoundCueData.cooldown` + `maxSimultaneous`, `AudioManager` voice stealing (lowest-priority-then-oldest), editor-only steal log. The snapshot **arbiter** has its EditMode test; voice-pool stealing needs real `AudioSource`s so it's in-scene-verified, not unit-tested. **Remaining:** in-scene check of the 33rd-voice steal under a Setsuna melee burst.  
Verified by: —  
Regressions: 0

---

### BUG-028 — WorldLocationSO assets not created for Park and Streets
Status: Fixed  
Swept 2026-07-03 (P10): the assets exist (`Scripts/SceneLaoder/Data/L1_Park.asset` / `L2_Streets.asset`); both changelog "BUG-028 partial" entries landed (scene-name mismatch corrected; dormant placeholder locations emptied so they can't attempt loads); streaming verified end-to-end in the Phase 0 DoD run (Bootstrap→Intro→L1_Park+L2_Streets).  
Severity: Blocker  
System: SceneStreaming  
Symptom: `SceneFlowManager` cannot stream any areas — it has no `WorldLocationSO` to query for scene refs, adjacency, or entrance definitions; the streaming system is entirely disabled.  
Root cause: The `WorldLocationSO` data assets for `L1_Park` and `L2_Streets` have not been created in the project yet. `SceneFlowManager` is waiting on them.  
Fix: instruction.md Phase 7.1 — create `WorldLocationSO` assets (Park↔Streets adjacency, entrance IDs matching `LocationEntrance` names placed in each scene); wire into `SceneFlowManager`.  
Verified by: —  
Regressions: 0

---

### BUG-029 — Skill snapshot covers only 7 of 9 upgrade trees
Status: Fixed  
Swept 2026-07-03 (P10): Phase 7.5 landed — `SkillTreeManager.AllTrees` exposed (changelog "Phase 7.5 remainder") and snapshot/restore now go through the `SkillTreeRuntimeState.Snapshot` dictionary covering **all nine** trees, no hand-lists (`SoftResetController.cs:173-175`, code-read today). In-editor soft-reset DoD re-run pending → not marked Verified.  
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
Status: Fixed  
Swept 2026-07-03 (P10): code-read — `SetsunaSystem` now calls `_leftTwin.Health?.SetInvincible(value)` + `_rightTwin.Health?.SetInvincible(value)` (`SetsunaSystem.cs:430-431`; Phase 7.6j hardening). Related grace-window scaling bug fixed separately as BUG-044.  
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

### BUG-033 — Rescue tutorial soft-locks the whole game after the cutscene (RescueTrapWatch waits on a prompt that never gets dismissed)
Status: Fixed  
Severity: Blocker  
System: Tutorial / Rescue / Time  
Discovered: 2026-06-19 (live play session, MCP-inspected)  
Fixed: 2026-06-19 — changelog `[Unreleased] Fixed — BUG-033`

**Symptom (player-facing):** After the intro/park timeline ends and the rescue sequence begins, the grabbed twin *can* be rescued (mash F succeeds) but the tutorial never advances. Both twins are frozen in place — the player "cannot walk out of the rescue area / out of bounds" — and it reads as *"the rescue checkpoint didn't load / isn't active."* Intermittent across runs.

**Investigation trail (how we got here — and the two wrong turns):**
1. User reported the rescue checkpoint "didn't activate after the timeline" and **paused** in play mode for inspection.
2. **Wrong hypothesis #1 — inactive-under-Activation-Track (BUG-W15 pattern).** Disproved by live read: `CheckpointsRescueL` was `active: true`, `activeInHierarchy: true` — the timeline did **not** leave it deactivated.
3. Live read showed `TutorialCheckpoint.IsCompleted = true` on `CheckpointsRescueL`. **Wrong hypothesis #2 — "the checkpoint step is stuck because the checkpoint was completed early, so `Activate()` no-ops (line 80) and `OnTriggerEnter` is dead (line 123)."** This *looked* right (`CurrentStage` was `5` = `RescueIntro`, which matches `RescueCheckpoint.asset`'s `stage:5`).
4. **User pushback (correct):** the objective pointer was visible and the twins were confined — if the checkpoint were merely "already done," the confinement wouldn't apply. This forced a re-check.
5. **The correction:** `RescueCheckpoint.asset` (step 10) **and** `RescueTrapWatch.asset` (step 11) **both carry `stage: 5`** — so `CurrentStage == 5` does **not** distinguish the two steps. `IsCompleted = true` therefore meant step 10 (`RescueCheckpoint`) **completed legitimately**, and the sequence was actually parked in **step 11 (`RescueTrapWatch`)**. The earlier checkpoint diagnosis was wrong.
6. **Console evidence (survives play-stop) — the decisive proof.** `RescueEventController.TransitionTo` (logs every transition, line 541) showed: `Triggered → Mashing → Triggered → Mashing → Success`, then `Triggered → Mashing → Success` — i.e. the rescue mechanically reached **`Success` twice**. `WasSuccessful` only latches on `Success` (line 545), so it **was** latched true. The rescue worked; the step still didn't end.
7. **Code analysis of the stall** ([`TutorialRescueWatchStepSO.Execute`](Assets/Scripts/TutorialSystem/TutorialRescueWatchStepSO.cs#L40-L62)):
   ```
   WaitUntil(HasActiveRescueTarget)          // grab — happened
   overlay.Show(..., () => promptDone = true) // prompt; also Requests timeScale 0
   WaitUntil(() => promptDone)                // ← PARKED HERE FOREVER
   while (true) { if (WasSuccessful) yield break; ... }   // never runs
   ```
   The outcome loop that checks `WasSuccessful` is gated behind `WaitUntil(() => promptDone)`. `promptDone` only flips when the player clicks the overlay's **Continue** button ([`OnContinueClicked`](Assets/Scripts/TutorialSystem/TutorialOverlayController.cs#L113-L130)). The player rescued (mash F) instead of clicking Continue, so `promptDone` stayed false → the loop never ran → the **already-latched success was never observed** → permanent stall.
8. **Why everything freezes:** [`TutorialOverlayController.Show` (line 103)](Assets/Scripts/TutorialSystem/TutorialOverlayController.cs#L103) does `TimeScaleService.Request(this, 0f)` and only releases on `OnContinueClicked`. Since Continue was never clicked, `timeScale` stays **0** — that is why the twins can't move and the player "can't leave the bounds." It's a time-freeze, not the `TutorialOuterBoundary`. (Relatedly: the rescue mash *still* completes at `timeScale 0` because `_mashProgress` is input-driven, not `deltaTime`-driven — [line 508](Assets/Scripts/Players/RescueEventController.cs#L508-L513) — which is exactly how `Success` latches while the step is still parked on the prompt wait.)
9. **User confirmation:** no prompt card appeared this run; the card overlay works for other tutorial steps, so this is the **step's ordering/dependency** defect, not overlay wiring.

**Root cause:** `TutorialRescueWatchStepSO` makes step completion **depend on the player dismissing a prompt first** (`WaitUntil(promptDone)` precedes the success/failure watch). A successful rescue that occurs before/without dismissing the prompt is latched (`WasSuccessful`) but never observed. Compounded by the prompt's `timeScale = 0` hold never being released → the entire game soft-locks. A tutorial step must never be able to soft-lock the whole game like this.

**Fix (specified — ready to implement; instruction.md cross-ref pending current file):**

Restructure `TutorialRescueWatchStepSO.Execute` to RACE success against the prompt dismissal
instead of sequencing strictly after it — success is input-driven and ignores the overlay's
`timeScale=0` freeze, so it can land before Continue is clicked:

```csharp
public override IEnumerator Execute(TutorialStepContext ctx, MonoBehaviour executor)
{
    ApplyCommonSetup(ctx);

    var rescue = ctx.RescueProvider;
    if (rescue == null) { Debug.LogError("[TutorialRescueWatch] RescueProvider is null.", this); yield break; }

    rescue.ResetSuccessFlag();
    yield return new WaitUntil(() => rescue.HasActiveRescueTarget);

    bool promptDone = false;
    if (ctx.overlay != null)
    {
        ctx.overlay.Show(
            promptTitle.IsEmpty ? "" : promptTitle.GetLocalizedString(),
            promptBody.IsEmpty ? "" : promptBody.GetLocalizedString(),
            promptClip, () => promptDone = true);
    }
    else
    {
        Debug.LogError("[TutorialRescueWatch] ctx.overlay is null — skipping the explainer; " +
                        "rescue watch still proceeds.", this);
        promptDone = true;   // fail loud, never silently freeze the player on a missing ref
    }

    // RACE, not sequence — see root cause: mash ignores timeScale=0.
    yield return new WaitUntil(() => promptDone || rescue.WasSuccessful);

    if (!promptDone)
        ctx.overlay.Continue();   // success beat the prompt — force-release the timeScale=0 hold

    if (rescue.WasSuccessful)
        yield break;   // advance regardless of prompt order

    // promptDone is true here and not yet successful — original behaviour from this point on.
    // Failure can only be reached here (never during the frozen prompt) because TTK is
    // scaled time (R10) and cannot expire at timeScale=0.
    string failMsg = failureMessage.IsEmpty
        ? "Reach your twin in time — move closer and mash F" : failureMessage.GetLocalizedString();
    while (true)
    {
        yield return null;
        if (rescue.WasSuccessful) yield break;
        if (rescue.CurrentRescueState == RescueState.Failed)
        {
            ctx.failureNotice?.Show(failMsg);
            ctx.resetSequencer?.TriggerReset(ctx.RescueFailLeftReset, ctx.RescueFailRightReset, null);
            rescue.ResetSuccessFlag();
            yield return new WaitForSeconds(0.5f);
            yield return new WaitUntil(() => rescue.HasActiveRescueTarget);
        }
    }
}
```

Plus one addition to `TutorialOverlayController` — a public, idempotent dismiss (existing
`OnContinueClicked` already guards `if (!_isOpen) return;`, so this is safe even if the player
clicks Continue at the same moment):

```csharp
/// <summary>Force-dismiss as if Continue were clicked. Safe to call when already closed.</summary>
public void Continue() => OnContinueClicked();
```

**Verify in-editor, not assumed:** the "active player-facing objective visual" requirement —
confirm the checkpoint marker / hint text aren't visually buried behind the explainer card
during the race window, since that contributed to this reading as "checkpoint didn't activate."

**Should become a named pattern, not a one-off:** any step watcher that gates progression
behind a time-freezing modal's dismissal must RACE the modal against the mechanic it's
freezing for, never sequence strictly after it — the freeze only stops *scaled-time* paths,
not input-driven ones. Pending: fold into instruction.md (current file needed — significant
Phase 9 work has landed since the last copy on hand; don't edit a stale base).

**Relationships:** This is the real mechanism behind **BUG-021** ("rescue checkpoint never activates after timeline") — the checkpoint *does* activate; the stall is here (BUG-021 root-cause guess superseded). The `timeScale 0` leak is a concrete instance of **BUG-022 / R10** (un-arbitrated time writers). **BUG-030** (entry 12 duplicated to `CheckpointsRescueL`) was observed again live and remains latent (Single mode reads only index 11).  
Verified by: —  
Regressions: 0

---

### BUG-034 — Enemies never detect the player after multi-scene boot
Status: Fixed  
Severity: Blocker  
System: AI / Perception  
Discovered: 2026-06-19  
Swept 2026-07-03 (P10): detection works — the real causes were BUG-040 (fabricated blank `PerceptionManager` singleton) and BUG-041 (double sensor registration), both fixed + live-verified 2026-06-21 ("GOAP confirmed running end-to-end live — patrol+detect+attack", that sweep). The Phase 1 backfill fix below also stands; temp diagnostics were removed 2026-06-20. Entry kept for the investigation record.  

**Symptom:** Enemies patrol correctly but never perceive the player — no combat, no reaction. Patrol logic runs; perception never fires.

---

#### Phase 1 fix — PerceptionManager backfill race (code shipped, not yet sufficient)

**Root cause diagnosed:** `PerceptionManager.RegisterPerceivable` only attached perceivables to sensors already in `ActiveSensors` at that moment. After multi-scene split, Persistent (twins) always loads fully before any area scene, so `ActiveSensors` is empty when player Perceivables register → new-sensor backfill never ran → `VisionSensor.Queries` empty for player.

**Exact changes to `PerceptionManager.cs`:**
- Added `private readonly List<IPerceivable> AllPerceivables = new();` field — tracks every ever-registered perceivable.
- Added `private void TryAttachPerceivableToSensor(ISensor, IPerceivable)` helper — checks `IsPerceivableBy`, adds to `ActivePerceivables[sensor]`, calls `sensor.RegisterPerceivable`. Silent return on overlap (expected during backfill).
- `RegisterPerceivable`: guards duplicate in `AllPerceivables`, then calls `TryAttachPerceivableToSensor` for every existing sensor (unchanged behaviour when sensors already exist).
- `DeregisterPerceivable`: also removes from `AllPerceivables`.
- `RegisterListener` new-sensor branch: after `NewSensor.RegisterListener(...)`, loops `AllPerceivables` and calls `TryAttachPerceivableToSensor(NewSensor, perceivable)` — **the actual fix**.

**Diagnostic logs also added to `PerceptionManager.cs` (temp — remove after bug closed):**
- `OnAwake()`: `Debug.Log("[PM] OnAwake instanceId={GetInstanceID()}")` — distinguishes ghost (negative id) from real (positive id) PM.
- `RegisterPerceivable()` after `AllPerceivables.Add(...)`: `Debug.Log("[PM] RegisterPerceivable owner={name} instanceId={GetInstanceID()}")` — confirms correct PM instance receives each registration.

---

#### Phase 2 investigation — detection pipeline still silent after Phase 1

**Diagnostic logs added to `PerceptionListener.cs` (temp — remove after bug closed):**
- New instance fields: `bool _canDetectLogged = false; bool _relationshipLogged = false;`
- Top of `CanDetect()` before any return: one-shot `[CanDetect ENTRY]` — logs listener name, perceivable name, `sameOwner`, both faction DisplayNames, `supRelCount`.
- Faction-null branch: `Debug.LogWarning("[CanDetect] blocked — listenerFaction=… perceivableFaction=…")` — fires if either `Faction` or `InPerceivable.Faction` is null.
- After `GetRelationshipTo` call: one-shot `Debug.LogWarning("[CanDetect] relationship=… supported=[…]")` — logs the actual relationship returned and the full supported list.

**Diagnostic logs added to `VisionSensor.cs` (temp — remove after bug closed):**
- New instance field: `bool _queryCountLogged = false;`
- Top of `Tick()`: one-shot `Debug.Log("[VisionTick] queries=… listeners=… perceivables=…")` — confirms pipeline structure on first tick.
- `RunQuery()` range fail branch: `Debug.LogWarning("[VisionQuery] RANGE FAIL dist=… max=… listener=… perceivable=…")`.
- `RunQuery()` cone fail branch: `Debug.LogWarning("[VisionQuery] CONE FAIL dot=… minDot=… listener=… perceivable=…")`.
- `RunQuery()` raycast hits wrong GO: `Debug.LogWarning("[VisionRaycast] WRONG_GO hit=… expected=… listener=…")`.
- `RunQuery()` raycast misses entirely: `Debug.LogWarning("[VisionRaycast] MISS — no collider hit between … and …")`.

**Confirmed working from first play session (Bootstrap → L1_Park):**
- Ghost PM (`instanceId` < 0) created by `PerceptionManagerBootstrapper`, real PM (`instanceId` > 0) self-corrects via `ConstructIfNeeded` ✓
- Both Kai and Lyra register as Perceivables on the real PM ✓
- Enemies (`SmartEnemyMelee`, `SmartEnemySevered`, etc.) also register as Perceivables — enemies have BOTH `Perceivable` and `PerceptionListener` ✓
- `VisionSensor`: `queries=360, listeners=180, perceivables=2` on first tick — pipeline structure is correct ✓

**Still failing:** Zero `[VisionQuery]` or `[VisionRaycast]` logs after standing face-to-face with enemy → `RunQuery` is never reached → `CanDetect` is returning false for all 360 queries silently.

---

#### Phase 3 investigation — CanDetect confirmed called; SO reference confirmed correct; failure point still hidden

**New runtime evidence (2026-06-19, second play session):**
- `[CanDetect ENTRY]` now fires — 56 unique PerceptionListener instances called for `perceivable=Kai`, `myFaction=AIFaction`, `theirFaction=PlayerFaction`, `supRelCount=1`. CanDetect is definitely being invoked. ✓
- No `[CanDetect] blocked` warnings → both `Faction` and `InPerceivable.Faction` are non-null for every enemy. ✓
- `supRelCount=1` → `SupportedRelationships` list has 1 entry (Hostile). ✓
- Still zero `[CanDetect] relationship=…` logs and zero `[VisionQuery]`/`[VisionRaycast]` logs → `CanDetect` returns false before the relationship check, AND `RunQuery` is never called.

**SO reference verified (static YAML):**
- `FactionAI.asset → DefaultRelationships[0].OtherFaction` GUID = `dadac1b1b0db3024096a5b9e3185f5ba`
- `FactionPlayer.asset.meta` GUID = `dadac1b1b0db3024096a5b9e3185f5ba`
- They match. `GetRelationshipTo` reference-equality check should work. Addressables mismatch hypothesis disproven.

**`Perceivable.Faction` resolution confirmed:** `Perceivable.Start()` calls `AsyncLocateService<IFaction>(LocalOnly)` on its own GO. `FactionComponent.Awake()` registers `IFaction` on the same GO (Twins.prefab wires both to Lyra/Kai root). ENTRY log shows `theirFaction=PlayerFaction` → Faction resolved correctly at call time. ✓

**Root discrepancy:** `supRelCount=1` (list non-empty) means the `SupportedRelationships.Count == 0` early-return should NOT fire. `GetRelationshipTo` should return Hostile (SO reference correct). `Contains(Hostile)` should return true. Yet RunQuery is never reached. The `[CanDetect] relationship=…` log that would confirm the exact return value is not appearing — most likely because the diagnostic edit that added it compiled separately from the ENTRY log edit and the second compilation wasn't picked up by Unity's incremental compiler.

**Next step to unblock:** Exit play mode → right-click `PerceptionListener.cs` in Project window → **Reimport** (or `Assets → Reimport All`) → wait for compilation → re-enter play mode → stand near enemy → look for `[CanDetect] relationship=…` in console. That log will show the exact value `GetRelationshipTo` returns and the supported list, pinpointing whether the failure is at the relationship comparison or elsewhere.

**DoD / Verify:** Cold boot into L1_Park — enemy in player's FOV transitions out of patrol within 1 second. Restart-loop test (1.4c) confirms detection survives Restart.  
Verified by: —  
Regressions: 0

---

### BUG-035 — PerceptionListener registers in Awake() with no OnDisable() deregistration (pooled-enemy stale-query)
Status: Fixed  
Severity: Minor  
System: AI / Perception / EnemyPool  
Discovered: 2026-06-20  
Fixed: 2026-06-20 — `Perceivable` and `PerceptionListener` gain `OnEnable`/`OnDisable` (R8 pairing) with `_hasEverRegistered` guard so first-activation registration stays in `Start`/`Awake`; `PerceptionManager.DeregisterListener` and `DeregisterPerceivable` now purge `AllDetectionData` and call `OnNotifyLostPerceivable` for stale detection entries.

**Symptom:** Pooled-but-undeployed enemies remain permanently registered as `PerceptionListener`s and are continuously queried every frame at their pool-origin position `(0, 1, 0)`. This was the source of the `RANGE FAIL dist≈100` readings during BUG-034 investigation: enemy instantiated into pool at world origin → `Awake()` fires → listener registered → VisionSensor starts querying it → player at z≈99.5 (L2_Streets start) → distance ≈ 99.6 units → fails range check every frame for the lifetime of the session, even after the enemy is deployed to a real spawn position (the query pair persists; only the position updates). Querying stale/idle pool entries wastes per-frame CPU and pollutes perception logs.

**Root cause:** `PerceptionListener.Awake()` calls `LinkedPerceptionManager.RegisterListener(this, Config)` — the listener is registered as soon as the object is instantiated, regardless of whether it has been deployed. There is no corresponding `OnDisable()` call to deregister when the enemy is returned to the pool and deactivated (only `OnDestroy()` deregisters). This violates R8: subscribe and unsubscribe must be paired on `OnEnable`/`OnDisable`.

**Note on fix approach:** `EnemyPool.Return()` already invokes per-component cleanup on return (e.g. `SiphonEnemy.Release()`) — that is the likely right home for a `DeregisterListener` call, or alternatively migrating `PerceptionListener`'s registration from `Awake()` to `OnEnable()`/`OnDisable()` would fix the class itself generically and correctly handle the pool lifecycle without any `EnemyPool` changes. The exact approach (tie registration to deployment lifecycle vs. fix at the `PerceptionListener` level) needs a deliberate decision before implementing — the `OnEnable`/`OnDisable` migration is the cleaner R8-compliant fix but must be verified against all non-pooled listeners (twins, etc.) to confirm it has no unintended registration-order side-effects.

**Not blocking BUG-034:** BUG-034's active failure (WRONG_GO + ground-level SensorOrigin) is independent. This entry is a correctness and performance issue, not the primary detection blocker.  
Verified by: —  
Regressions: 0

---

### BUG-036 — VisionSensor.DeregisterListener/DeregisterPerceivable leave stale entries in Listeners/Perceivables list
Status: Fixed  
Severity: Minor  
System: AI / Perception / VisionSensor  
Discovered: 2026-06-20  
Fixed: 2026-06-20 — `VisionSensor.DeregisterListener` now calls `Listeners.Remove(InListener)`; `VisionSensor.DeregisterPerceivable` now calls `Perceivables.Remove(InPerceivable)`. Both additions precede the existing Queries/ListenerConfigs cleanup in the respective methods.

**Symptom:** `KeyNotFoundException: The given key 'X (CommonCore.PerceptionListener)' was not present in the dictionary` thrown from `VisionSensor.Tick()` at `ListenerConfigs[Listener]`. Observed for `SmartEnemyMelee(Clone)` in the BUG-034 investigation console.

**Root cause:** `DeregisterListener` removed the listener from `Queries` and `ListenerConfigs` but not from the `Listeners` list. `DeregisterPerceivable` similarly removed from `Queries` but not from `Perceivables`. When a new Perceivable later registers, `RegisterPerceivable` iterates `Listeners` and creates a new `VisionQuery` for the now-deregistered listener. On the next `Tick()`, `ListenerConfigs[Listener]` throws because the listener was removed from `ListenerConfigs` but the stale `Listeners` entry caused a new Query to be created for it. Same pattern in reverse for Perceivables/RegisterListener.

**Previously triggered by:** Enemy `OnDestroy()` calling `DeregisterListener`; then a new Perceivable registering afterward. **Made worse by BUG-035 fix:** `OnDisable()` now also calls `DeregisterListener` on every pool-return, so this would throw every cycle of spawn → pool-return → new-perceivable-registers.  
Verified by: —  
Regressions: 0

---

### BUG-W34 — MonoBehaviourSingleton<T>.OnAwake re-registers a service on the doomed duplicate
Status: Watch  
Severity: Minor  
System: AI / ServiceLocator / Singleton  

When a second instance of a `MonoBehaviourSingleton<T>` is Awake-d (e.g. after Restart loads Bootstrap and Persistent re-instantiates managers), `OnAwake()` runs on the duplicate before the duplicate-destroy guard fires. This re-registers the doomed duplicate as the `IPerceptionManager` (or whichever service) in `ServiceLocator`. The surviving instance later wins because it was there first, but there is a window where the service slot points at an object that is about to be Destroyed. No crash observed, but it is a latent source of ghost-service bugs on the Restart path. Fold into instruction.md Phase 1.4 restart-loop verification.  
Verified by: —  
Regressions: 0

---

### BUG-038 — Rescue success leaves shared `IsRescueActive` stuck true → melee enemies never attack
Status: Fixed  
Severity: Blocker  
System: Rescue / AI gating / Blackboard  
Discovered: 2026-06-21 (live play session, MCP-inspected)  
Fixed: 2026-06-21 — changelog `[Unreleased] Fixed — BUG-038`; verified live (post-Success `SHARED IsRescueActive=False`).

**Symptom:** After any successful (or failed) rescue, enemies that mirror the shared `IsRescueActive` flag stop attacking — they detect the twin (target in blackboard, faction Hostile, detection strength 1.0) but `GOAPGoalAttackTwin` hard-gates on `IsRescueActive` and returns `DoNotRun`. Observed: all `SmartEnemyMelee` had `rescueGate=true` while `SmartEnemySevered` had `false` and attacked.

**Root cause (pre-existing — IDENTICAL structure at init-day `5fa951d`; NOT introduced by instruction.md, and NOT the SkeletonHandTrap changes — trap rescue-event wiring is byte-identical init-day vs now):** `RescueEventController.TransitionTo(next)` fires `OnRescueStateChanged(next)` *after* `EnterState(next)`. Terminal states (`Success`/`Failed`) call `CleanupRescueEvent()` inside `EnterState`, which sets `_state = Idle` and fires `OnRescueStateChanged(Idle)`. `TransitionTo` then fires `OnRescueStateChanged(Success)` on top → subscribers' last value is `Success` (non-Idle). `PoTWorldStateWriter` sets shared `IsRescueActive = (state != Idle)` → stuck `true`. The controller's own `_state` correctly ends `Idle` (`IsRescueActive=False`) — only event subscribers desync. Proven live: controller `IsRescueActive=False` / `_state=Idle`, but `SHARED IsRescueActive=True`.

**Fix:** `TransitionTo` only fires `OnRescueStateChanged(next)` if `_state == next` after `EnterState` (skips the stomp when EnterState already cleaned up to Idle). Verified: real Success (`WasSuccessful=True`) → `SHARED IsRescueActive=False`, `HasRescueTarget=False`.  
Verified by: live MCP runtime inspection 2026-06-21  
Regressions: 0

### BUG-039 — Utility attack goals never scored a target (wrong key + bool-reads-GameObject)
Status: Fixed  
Severity: Blocker  
System: AI / GOAP / Utility scoring  
Discovered: 2026-06-21 (live play session, MCP-inspected)  
Fixed: 2026-06-21 — changelog `[Unreleased] Fixed — BUG-039`; verified live (enemies select `GOAPGoalAttackTwin`, chase).

**Symptom:** Enemies detect the twin (target in blackboard) but never attack — every utility goal returns `Priority = DoNotRun` (`int.MinValue`); `ActivePlan.IsValid=False`; enemy stands/wanders. `CalculateScore` for `GOAPGoalAttackTwin` = 34.5, just under `activationThreshold=35`.

**Root cause (two authoring defects, both baked in at the utility system's creation commit `b8accd6` — after init-day; never worked, nothing to revert to. The package has NO utility system; its goals read `CommonCore.Names.Awareness_BestTarget` = `"Self.Awareness.BestTarget.GameObject"` directly and check `!= null`):**
1. The weight-50 "has target" factor's `blackboardKey` was the hand-typed short string `"Awareness.BestTarget"` (mirrored in `UtilityFactorKeys.HasTarget`), which never matched the canonical key perception writes. Factor read an absent key → 0.
2. With the key fixed, `UtilityGOAPGoalBase.ReadBlackboardValue`'s `isBool` branch reads the **bool** dictionary, but the key holds a **GameObject** (Blackboard uses per-type dicts). Bool read missed → 0.

**Fix:** (a) 9 `Attack*UtilProfile.asset` + `UtilityFactorKeys.HasTarget` → `"Self.Awareness.BestTarget.GameObject"`; (b) `ReadBlackboardValue` `isBool` branch falls back to a GameObject read (presence ⇒ 1) when the bool read misses (matches package `!= null`). Verified live: AttackTwin factor reads target ⇒ 50; enemies select `GOAPGoalAttackTwin` and chase.

**Known remaining (separate, filed as BUG-042):** every factor in every util profile has `steepness: 0` (invalid; `[Range(0.5f,8f)]`). Steepness-dependent curve shapes (e.g. `EaseOut` on `HealthNorm`) collapse to flat-0, so curve-factor weight is dead → attack scores hover marginally around the threshold (some enemies 34, some 48 depending on bool factors). Linear/InvertedLinear ignore steepness so they survive.  
Verified by: live MCP runtime inspection 2026-06-21  
Regressions: 0

### BUG-040 — PerceptionManagerBootstrapper fabricates a blank singleton (real manager destroyed)
Status: Fixed  
Severity: Blocker  
System: AI / Perception / Singleton boot  
Discovered: 2026-06-21 (live play session, MCP-inspected)  
Fixed: 2026-06-21 — changelog `[Unreleased] Fixed — BUG-040`; verified live (real manager, populated dicts).

**Symptom:** Enemies never detect the player in multi-scene. Live: `PerceptionManager.Instance` = fabricated `Singleton<CommonCore.PerceptionManager>` in `DontDestroyOnLoad` with `ActiveSensors=0`/`AllDetectionData=0`; listeners' `LinkedPerceptionManager` pointed at a *different* (destroyed) object. Console: `Destroying duplicate CommonCore.PerceptionManager`.

**Root cause:** `PerceptionManagerBootstrapper.Initialize()` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) calls `PerceptionManager.Instance` when Bootstrap (scene 0) loads — before Persistent's real manager exists. `MonoBehaviourSingleton<T>.Instance` line 41 fabricates `new GameObject("Singleton<…>")` when none found, DDOL's it, wins the singleton. Real Persistent manager later `Awake`s → `ConstructIfNeeded` destroys *it* as duplicate. Enemies/perceivables registered into the real one via ServiceLocator → destroyed → surviving fabricated manager is empty → no detection. (This is the silent-fabrication footgun CLAUDE.md flags; instance of BUG-W34.)

**Fix:** `Initialize()` uses `Object.FindFirstObjectByType<PerceptionManager>()` (never fabricates) and calls `OnBootstrapped()` only if found (no-op for `PerceptionManager`). Verified live: `Instance` is the real manager, `ActiveSensors=3`/`AllDetectionData=4`, enemies detect twins at strength 1.0. Note: `PerceptionManager.cs` is no longer byte-identical to the package (this is the sole intentional divergence — the package bootstrapper assumes single-scene).  
Verified by: live MCP runtime inspection 2026-06-21  
Regressions: 0

### BUG-041 — PerceptionListener double-registers sensors on pooled enemies (synchronous-resolve race)
Status: Fixed  
Severity: Major  
System: AI / Perception / Pooling  
Discovered: 2026-06-21 (live play session, MCP-inspected)  
Fixed: 2026-06-21 — changelog `[Unreleased] Fixed — BUG-041`; verified live (spam gone).

**Symptom:** On `EnemyPool` pre-warm, console spams `'SmartEnemyMelee(Clone)' is attempting to register itself multiple times for EnemyVisionSensor/EnemyHearingSensor/EnemyProximitySensor` (×3 per pooled enemy, also `SmartEnemyGroupGrab`).

**Root cause:** In multi-scene the Persistent `PerceptionManager` already exists when the pool pre-warms, so `ServiceLocator.AsyncLocateService<IPerceptionManager>` in `PerceptionListener.Awake` resolves **synchronously** — its callback runs `RegisterListener` and sets `_hasEverRegistered = true` *inside the same `Instantiate()`*. `OnEnable` then fires (still inside `Instantiate`) and, because `_hasEverRegistered` is now true, re-registers. Single-scene didn't hit this because the locate resolved a frame later (after `OnEnable`).

**Fix:** Replaced "ever registered" with "currently registered" (`_isRegistered`) via one idempotent `RegisterAllSensors()`. `Awake` callback + `OnEnable` both call it (no-op if registered); `OnDisable`/`OnDestroy` clear it (pool reuse). Verified live: spam gone, `_isRegistered=True`.  
Verified by: live MCP runtime inspection 2026-06-21  
Regressions: 0

### BUG-042 — Utility factors authored with `steepness: 0` → steepness-dependent curves collapse to flat-zero
Status: Open  
Severity: Major  
System: AI / GOAP / Utility scoring  
Discovered: 2026-06-21 (live play session, MCP-inspected)

**Symptom:** Even after BUG-039, melee attack scores hover marginally around `activationThreshold=35` (some enemies 34.5 → `DoNotRun`, some 48 depending on bool-factor context), so melee engagement is intermittent. `HealthNorm` (curveShape `EaseOut`) evaluates 0 at raw=1; full HP contributes nothing to attack eagerness.

**Root cause (authoring data — every factor in every util profile created at `b8accd6`):** `steepness` is serialized as `0` everywhere, violating its own `[Range(0.5f, 8f)]` (the asset was authored before the clamp or without running it). `CurveGenerator.Evaluate(shape, x, 0, …)` collapses steepness-dependent shapes (EaseOut/EaseIn/exponential) to flat-zero. Linear / InvertedLinear / Step ignore steepness so they still work — which is why `FactionEnergyNorm` (Linear) and the bool factors score but `HealthNorm` (EaseOut) is dead. With ~30–95 weight-points of curve factors permanently at 0, the max achievable attack score barely clears (or fails) the threshold.

**Fix (proposed — needs decision, CLAUDE.md rule 12 tuning):** options — (A) clamp `steepness` to ≥0.5 inside `UtilityFactor.Evaluate` as a code safety net (fixes all profiles, no data edit); (B) set sensible `steepness` (e.g. 2) per factor across the ~18 profile assets; (C) lower attack `activationThreshold`. Recommend (A) as the robust floor, optionally + (B) for intentional shaping. NOT applied pending decision.  
Verified by: —  
Regressions: 0

---

### BUG-044 — Rescued twin stuck invincible (scaled-time grace window) → enemies deal no damage
Status: Fixed  
Severity: Major  
System: Health / Rescue / TimeScale  
Discovered: 2026-06-22 (live MCP session — enemy-damage investigation)

**Symptom:** Enemies hit a twin every frame (`[Health] Kai TakeDamage` spam) but combined health never dropped. Live: `Kai._invincible=True` though rescue was fully complete (`PlayerDeathRescueProxy._isActive=False`, `RescueEventController._state=Idle`, twin active+alive). User noted it *eventually* self-healed after a long wait.

**Root cause:** Post-rescue grace (`PlayerDeathRescueProxy.InvincibilityFrames(2s)` + `SkeletonTrap.TrapRescueInvincibility(1.5s)`) used `SetInvincible(true)` → **`WaitForSeconds`** → `SetInvincible(false)`. `WaitForSeconds` is scaled; opened while `Time.timeScale` was low (Setsuna 0.15 / a transition at 0 around the timescale work) the grace stretched into many real seconds — twin un-damageable far past the window, self-healing only after enough wall-clock. Not a stuck-low timeScale (service requests empty at diagnosis) — a stale *scaled* wait issued earlier. The shared single-bool `_invincible` (no ownership) made it last-writer-wins between Setsuna's both-twins set and the per-twin grace.

**Fix:** Both grace coroutines → `WaitForSecondsRealtime` (real wall-clock grace, Setsuna/pause-immune) + `try/finally` release so the flag clears even on `StopCoroutine`/external reset; trap captures the rescued `Player` up front (a `_grabbedPlayer` clear can't skip release). Removed the per-hit `Debug.Log` in `PlayerHealthComponent.TakeDamage`.  
Fixed: 2026-06-22 — changelog `[Unreleased] Fixed — BUG-044`; verified live (clearing flag restored damage immediately; user confirmed enemies damage + rescue work post-fix).  
Verified by: live MCP (damage resumes) + user confirmation  
Regressions: 0

---

## Summary

| State | Count |
|-------|-------|
| Open | 6 |
| In-Progress | 1 |
| Fixed | 35 |
| Verified | 0 |
| Watch | 31 |
| Won't-Fix (Exemption E1) | 1 |
| **Total** | **74** |

*Last swept: 2026-07-03 (P10 doc-truth + ledger sweep — the architecture-review session. **Every
Open entry re-verified against current code/changelog before any status change** (greps + code
reads cited per entry; no blind flips). **26 stale Opens flipped to Fixed** — the ledger had not
been swept since 2026-06-22 while Phases 1–7.6 + TimeScaleService landed: BUG-001–011 (Phase 1/2
re-wires + relocations), 014/022 (TimeScaleService live, all 8 writers migrated), 015 (Phase 4
`SkillTreeRuntimeState`), 017, 020 (HashSet occupancy + `NotifyTeleported`), 021 (via BUG-033),
023/024 (R8 hygiene), 025 (Setsuna F-hold via input provider), 026 (ESC arbiter), 027 (LockInput
mechanism gone), 028 (WorldLocationSO assets live), 029 (all-9-trees snapshot,
`SoftResetController.cs:173-175`), 031 (`SetInvincible` both twins), 034 (via BUG-040/041).
**BUG-019 → Won't-Fix under Exemption E1** (instruction.md §17): the 1.4 base-class correction
was applied, broke the enemy/perception stack, and is permanently cancelled by the user — W01/W05
annotated with the surviving mitigations. W13/W16/W20 annotated root-cause-landed, kept Watch.
**Still genuinely Open (6):** BUG-012 Penitent rework · BUG-013 SoulConv cap tuning · BUG-016
debug keys (→ P13 dev-guard) · BUG-018 CommonStatic restore verification (Restore.unity still
present) · BUG-030 latent entry-12 wiring · BUG-042 utility steepness:0 (needs tuning decision).
BUG-032 stays In-Progress (Editor wiring with the user). None of the flips are marked Verified —
the ledger rule holds: Verified requires the matching §10 DoD step run in-editor.)* Prior: 2026-06-22 (camera-feel / dev-mode / docs session. **BUG-037 fixed** — recurring 180° camera flip: confirmed timeline-driven (no camera code writes rotation; `.playable` not hand-editable, R11); shipped `CameraRotationGuard` (snapshots authored cam rotation, restores it behind the white fade at cutscene end) + dev-skip clears fade/restores cams. **BUG-044 fixed** (prior in this changelog) — rescued twin stuck invincible (scaled `WaitForSeconds` grace under low timeScale) → enemies dealt no damage; grace coroutines → `WaitForSecondsRealtime` + try/finally. Also landed (features, not bugs): Camera Cue system (shake/depth; real-FOV channel removed because group cams own their FOV — zoom now via post-proc Lens Distortion), DevConfig dev-mode (master + Trainer/SkipTutorial, build-safe), VFX Library layer + `CueContext.scale` + per-element transform overrides, enemy world-space UI + Manpu wiring on variants. game.md updated (§16.1b DevConfig, §23.9 Camera Cue, §23.10 VFX Library, §23.6/§23.8 refreshed). Open→32, Fixed→10.) Prior: 2026-06-21 (live MCP play session — enemy-detection/combat investigation. **BUG-040 fixed** — `PerceptionManagerBootstrapper` fabricated a blank `Singleton<PerceptionManager>` (real Persistent manager destroyed as duplicate, empty dicts, no detection); now `FindFirstObjectByType` instead of `.Instance`. **BUG-041 fixed** — `PerceptionListener` double-registered sensors when ServiceLocator resolved synchronously in multi-scene; `_isRegistered` + idempotent `RegisterAllSensors`. **BUG-039 fixed** — utility attack goals never scored a target: SO `blackboardKey` `"Awareness.BestTarget"`→`"Self.Awareness.BestTarget.GameObject"` (9 assets + `UtilityFactorKeys`), and `ReadBlackboardValue` `isBool` branch now falls back to GameObject-presence read. **BUG-038 fixed** — rescue Success left shared `IsRescueActive` stuck true (gating all melee out of combat); `RescueEventController.TransitionTo` no longer stomps the cleanup `Idle` event with the terminal-state value. **BUG-042 filed (Open)** — every util factor has `steepness:0`, collapsing EaseOut/EaseIn curves to flat-0; needs tuning decision. **BUG-037 filed (Open)** — intermittent 180° camera flip: `GroupTransposeTop`/`TutorialGroupTranspose*` authored at localEulerY=180 while `GroupTransposeClose`/`LevelTopDownCam` are 0; flip appears whenever `CameraManager` selects a 180° cam (scene/prefab transform fix). BUG-021 baseline snapshot recorded. GOAP confirmed running end-to-end live (patrol+detect+attack). **Remaining content gaps (NOT code, not filed as code bugs):** melee deals no damage because `EnemyAttackController.ExecuteHitDetection` is an animation-event callback and greybox enemies have no Animator (`IAnimController=NULL`); enemies clip through walls because the L2/L3 NavMesh predates the current greybox and has 0 NavMeshObstacles — both need a content pass (animator+attack-clip event; NavMesh re-bake with walls not-walkable). Verified all enemy *code* files are accounted for: the 11 reverted files still match init-day except the 4 we deliberately re-added pieces to (EnemyDeathNotifier Instance/OnEnemyDamaged, SkeletonHandTrap registration/ForceReset, EnemySpawner multi-scene wiring, SpawnZone GetNearestRitualSite/self-register); core GOAP brain/goals/actions untouched by instruction.md. Open→33, Fixed→8, Total→73.) Prior: 2026-06-20 (BUG-036 filed+fixed — `VisionSensor.DeregisterListener`/`DeregisterPerceivable` left stale entries in `Listeners`/`Perceivables`; caused `KeyNotFoundException` in `Tick()`; triggered on destroy before BUG-035 fix, on every pool-return after. Fixed by adding `Listeners.Remove`/`Perceivables.Remove` to respective deregister methods. Fixed→4, Total→67. Diagnostic temp logs removed from `VisionSensor`, `PerceptionListener`, `PerceptionManager`. Prior same day: BUG-035 fixed — `Perceivable`/`PerceptionListener` gain `OnEnable`/`OnDisable` with `_hasEverRegistered` guard (R8); `PerceptionManager.DeregisterListener`/`DeregisterPerceivable` purge `AllDetectionData` + fire `OnNotifyLostPerceivable`. Open→31, Fixed→3. Earlier same day: BUG-035 filed — `PerceptionListener` registers in `Awake()` with no `OnDisable()` deregistration; pooled-but-undeployed enemies remain permanently registered and queried at pool-origin position; violates R8; fix approach (OnEnable/OnDisable migration vs EnemyPool.Return call-site) needs deliberate decision before implementing; Open/Minor, not blocking BUG-034. Open→32, Total→66. RANGE FAIL diagnostic reverted — VisionSensor.cs RANGE FAIL line restored to LogWarning.) Prior: 2026-06-19 (BUG-034 fixed — `PerceptionManager` registration-order race: `AllPerceivables` list + `TryAttachPerceivableToSensor` helper + backfill in `RegisterListener` new-sensor branch. BUG-W34 added — `MonoBehaviourSingleton<T>` re-registers doomed duplicate in ServiceLocator; Watch. Fixed→2, Watch→31, Total→65. Prior same day: BUG-033 fixed — `TutorialRescueWatchStepSO` restructured: RACE `WaitUntil(promptDone || WasSuccessful)` replaces the strict sequence; force-dismiss `ctx.overlay.Continue()` releases the `timeScale=0` hold when rescue beats the prompt; null-overlay deadlock path also closed. `TutorialOverlayController` gains `public void Continue()`. Open→31, Fixed→1. Prior same day: BUG-033 added — rescue tutorial soft-lock: `RescueTrapWatch` parks on `WaitUntil(promptDone)` before its success/failure watch, so a rescue that succeeds before the prompt is dismissed is latched but never observed; the prompt's `timeScale 0` hold leaks → whole-game freeze. Diagnosed live via MCP from console `TransitionTo Success`×2 + checkpoint `IsCompleted:true`/`active:true`; corrected two wrong hypotheses (inactive-under-Activation-Track; "checkpoint already completed") after realizing `RescueCheckpoint` and `RescueTrapWatch` share `stage:5`. Supersedes BUG-021 root-cause guess; relates to BUG-022/R10 and BUG-030.) Prior: 2026-06-14 (Phase 9 P9.2 audio engines landed — `AudioManager` (voices/stealing/cooldown/PlayUI/pause-owner/snapshot arbiter), `MusicManager` (A/B crossfade + ambience), `SnapshotArbiter` + 7 EditMode tests (19/19 total), `SceneFlowManager.OnActiveLocationChanged`; W25/W26/W27/W30 annotated — F3/F4/F8 mechanisms shipped, call-site wiring is P9.4. Plus the P9.1 FromPrefab correction. Earlier: Phase 9 P9.1 FX core — F1 unload reclaim in `FxManager`, `StopAllOn`/`StopAll`/R3-singleton; W23/W24/W27/W28 annotated. Earlier same day: BUG-032 classification corrected — Activation 1/2 transpose cameras MOVED to Persistent not deleted, Activation 20/21 are the Lyra/Kai twin GOs not "nameplates"; resolver finalized as registry-based `TimelineTargetRegistry` + roles incl. `SkyboxChanger`; cutscene twin-lock + wrong-twin reset-point swap recorded. Prior: 2026-06-13 BUG-032 added; Phase 9 spec §14; BUG-W23–W30 for F1–F8; L1_Park R3 duplicate GOs deleted)*

### BUG-045 — KillParticleBook.asset deleted (enemy death soul-release silently gone)
Status: Fixed (rebuilt 2026-07-10; user TestLab kill-verify pending for Verified)  
Severity: Major  
System: FX / Enemy death  
Discovered: 2026-07-10 (user report: "cant seem to find the cue book")

**Symptom:** Combat kills played no soul-release sequence. `KillParticleSpawner._cueBook` = None in Persistent; no asset in `Fx/CueBooks/` referenced any EnemyDeath prefab.

**Root cause:** `KillParticleBook.asset` (old `CueBooks/` root) was deleted — almost certainly during the CueBooks → Abilities/Enemy/Environment folder split — and had never been committed, so no git restore path existed. Silent because the spawner null-checks the book and returns (fail-quiet, pre-dating the fail-loud rule). Also latent: the spawner played id `"death"` while the book's only id was `"kill_seq"` — even undeleted it would not have fired.

**Fix:** Asset rebuilt at `Fx/CueBooks/Enemy/KillParticleBook.asset` from the recorded 2026-06-27 spec (`kill_seq`: helixorbs 0.9 Immediate · disintegrate 1.1 Immediate · star WithPrevious +0.45/0.6 · collect AfterPreviousCompletion 0.25); element refs verified resolving in-editor; `_cueBook` rewired in Persistent (scene saved); spawner id `"death"` → `"kill_seq"`.

### BUG-046 — `poi_feed` looping cue leaked; poi auras World-anchored; corruption id mismatch
Status: Fixed (2026-07-10; user TestLab ecology run pending for Verified)  
Severity: Major (leak) / Minor (visual)  
System: FX / POI ecology / Dark energy  
Discovered: 2026-07-10 (integration check after user authored the CommonCueBook ids)

**Symptom (would-have-been):** every POI feed tick spawned an infinite looping `EnergyInteraction` instance (looping prefab + no explicit duration = held forever; `PoiEnergyEmitter` discards the handle) — one leaked loop per enemy per 12 s. `poi_buff`/`poi_corrupt` were authored `attachMode: World`, so the "held aura" would sit at the spawn point while the enemy walked away. And code played `"corruption"` while the authored id is `poi_corrupt` — the bond-break aura never fired.

**Fix:** `poi_feed` element given explicit 1.5 s duration (explicit lifetime auto-stops a loop, FxManager:322); both auras set `attachMode: Follow`; `EnemyDarkEnergy._corruptionStateCueId` default + all 13 enemy prefab serialized values renamed `corruption` → `poi_corrupt` (user's chosen id).

### BUG-047 — Ranged/projectile damage applied at FIRE TIME; arrows never collide; arrow faces backward
Status: Fixed (2026-07-10; user TestLab ranged-attack run pending for Verified)
Severity: Critical
System: Combat / Enemy ranged (Siphon, Ranged, Summoner — every arrow user)
Discovered: 2026-07-10 (user playtest: damage + hit effect at fire time, no trail visible, arrow faces wrong way)

**Symptom:** the moment a ranged enemy fired, the player took damage and the hit spark played; the arrow then flew as pure decoration (no impact ever), facing backward, trail invisible.

**Root cause (three stacked defects):**
1. All enemies share the attack animation `meleeAttack.anim`, which carries the `OnAttackHitFrame` event → `ExecuteHitDetection()` ran the MELEE overlap-sphere with the ranged `attackRange` (7–10 m) — that overlap is the fire-time damage + `on_hiteffect`. The real projectile damage path (`Arrow.OnTriggerEnter` → `OnProjectileHit`) was correct but unreachable, because…
2. `tahrArrow.prefab` had NO collider and NO Rigidbody (and `Weapons/Arrow.prefab` had trigger colliders but no Rigidbody — a moving trigger without a Rigidbody generates no trigger events). Arrows could never register a hit.
3. `tahrArrow.prefab` carried its 180°Y facing correction on the prefab ROOT — `GameplayPool.Spawn` stamps the root rotation with `LookRotation(dir)`, wiping it (this is why the user's prefab-root rotation edits "did nothing"). The sigil flew tip-backward.

**Fix:** `EnemyAttackController._suppressMeleeHitFrame` — set by `TryRangedAttack`, consumed by `ExecuteHitDetection` (one hit-frame eaten per ranged attack; cleared on melee start + `ResetAttack`). `tahrArrow`: kinematic Rigidbody + sphere trigger (~0.2 m), meshes wrapped under a `Model` child carrying the 180°Y correction (root stays identity — pool-stamp-proof), `Tip` child wired to `Arrow._tipAnchor` (trail/head cues follow the tip). `Weapons/Arrow`: kinematic Rigidbody added, `hitLayers` trimmed 192→128 (Enemy bit removed — arrow could self-hit its shooter once colliders worked), `_tipAnchor` → existing `Head` child. `Arrow.Initialise` also self-aligns `LookRotation(direction)` defensively. `Enemy.Awake` gained a firePoint auto-find fallback (descendant named *firepoint*/*muzzle*/*tip*) — serialized slot still wins.

### BUG-048 — Death helix never visible: played at world ORIGIN + reclaimed on its first frame
Status: Fixed (2026-07-10; play-mode verified — helix alive at the death position mid-sequence, screenshot)
Severity: Major
System: FX / FxManager / kill sequence (`kill_seq` helixorbs)
Discovered: 2026-07-10 (user playtest: "helix didn't even play, or I couldn't see even a fraction")

**Symptom:** the `kill_seq` helix-orb beat never appeared; disintegrate/star/collect played without it.

**Root cause (two stacked defects):**
1. `SoulOrbHelix.prefab`'s `OrbPathFollower.pathMesh` references the MeshFilter **inside `SoulCollect.fbx`** — an asset, whose transform sits at the world origin. The follower sampled world positions through that matrix, so the orb spiralled at (0,0,0) no matter where the enemy died, and it moved its own ROOT there, discarding FxManager's placement.
2. Even at the right spot it died instantly: the orb PS simulates in Local space, so `attachMode: FromPrefab` resolved to **Follow**; `KillParticleSpawner` plays a position-only context (no follow target), and `FxManager.ActiveFx.Update` reclaims a Follow cue with a null target on its FIRST frame (FxManager.cs:88). This silently killed EVERY Local-sim particle played via a position-only ctx — a whole failure class, not just the helix.

**Fix:** (a) `OrbPathFollower` — when `pathMesh` is an asset reference (`!scene.IsValid()`), treat the mesh as a SHAPE and play it relative to the orb's spawn position (origin captured on first ApplyProgress, after FxManager's Place; helix axis anchored at the spawn point via ground-centred offset). Also resets `_autoT`/`progress` on enable — pooled reuse restarted at the TOP before. (b) `FxManager.SpawnParticle`/`SpawnVfx` — a resolved Follow attach with `ctx.followTarget == null` degrades to World at spawn time (play at ctx.position); Update's null-check still reclaims cues whose anchor dies later.

### BUG-049 — Summoner spawns nothing (hard EnemySpawner dep + minion count saturates + silent null-entry)
Status: Fixed (2026-07-11; 0 CS errors — user retest in TestLab pending)
Severity: Major
System: Enemy AI / SummonerEnemy / EnemySpawner / EnemyPool
Discovered: 2026-07-11 (user playtest round 2: "Summoner currently spawns NOTHING")

**Symptom:** summon circle plays, no minion ever appears; after a while the Summoner stops even attempting.
**Root cause:** (1) `SummonerEnemy` hard-required a scene `EnemySpawner` (absent in TestLab/direct-play) and skipped silently; (2) `OnMinionDied` had NO caller — `_activeMinionCount` only incremented, so `CanSummon` latched false after `maxMinions`; (3) a null `summonEntry.prefab` was silently skipped.
**Fix:** fallback to canonical `EnemyPool.SpawnReady`; `TrackMinion` self-unsubscribing death handler decrements the count; LogError on null entry/prefab. Circle handle now stopped when the routine completes; summoned minions skip `on_enemyspawn` (playSpawnCue flag; same for Witness ritual allies).

### BUG-050 — Witness/Siphon bomb FX invisible (fuse World-anchored at throw point + looping explosion held forever)
Status: Fixed (2026-07-11; cue path live-verified rendering in play mode)
Severity: Major
System: FX / cue books (WitnessBombCueBook, SiphonBombCueBook) / BombProjectile
Discovered: 2026-07-11 (user playtest round 2)

**Symptom:** bomb instances visible in hierarchy, no FX visible in scene.
**Root cause:** fuse elements were `FromPrefab` with world-sim prefabs → resolved World, FX sat at the throw point behind the enemy; explosion body prefabs are authored LOOPING with no duration override → FxManager held every instance forever (a minutes-old explosion found still looping in the pool).
**Fix:** fuse elements → `Follow`; explode elements → `duration: 1.2` (explicit lifetime beats loop-hold).

### BUG-051 — Weaver's Gate travel helix never played; landing marker never went away; soul spawned half in ground
Status: Fixed (2026-07-11; 0 CS errors — user retest of full gate choreography pending)
Severity: Major
System: Abilities / TeleportAbility / WeaverGate book / HelixFollower
Discovered: 2026-07-11 (user playtest round 2)

**Symptom:** `tele_casttravel` invisible; teleport marker lingered; soul appeared buried to the waist and "popped up" on first move; no readable out→travel→in order.
**Root cause:** travel elements World-forced while code passed Follow AND the two `HelixFollower` orbs were never given endpoints/progress (autoPlay also fought any driver); `tele_castmark` loops but was fire-and-forget → held forever; destination Y = caster ground plane with a mid-body pivot; all cast cues fired the same frame.
**Fix:** `CastSequence`/`ReturnSequence` beat choreography (see changelog); helix wired per cast via new `FxManager.FindAllOnInstance` + progress from actual travel fraction; mark held+stopped on arrival; `SnapToGround` stands the CC on the surface; twins movement-locked during return until teleport-in.

### BUG-052 — Death helix too small / wrong rotation, no per-enemy fit
Status: Fixed (2026-07-11; 0 CS errors — user visual retest pending)
Severity: Minor
System: FX / kill sequence / SoulOrbHelix.prefab
Discovered: 2026-07-11 (user playtest round 2, C-RULING rework)

**Fix:** new `CharacterHelixDriver` (Evori around-character spiral: twin ribbons 180° apart, ease-in ascent, tapering radius, mirror ribbon cloned once under the pooled root) replaces `OrbPathFollower` on the prefab; `OnEnemyCombatKill` now carries a bounds-derived size (0.5–3× of a 2 m humanoid) passed as `CueContext.scale` → driver auto-fits radius/height via lossyScale.

### BUG-053 — Witness/Siphon bombs spawn nothing (rebuilt bomb prefabs broke _bombPrefab fileID refs)
Status: Fixed (2026-07-11; user retest pending)
Severity: Major
System: Enemies / Witness + Siphon / prefab authoring
Discovered: 2026-07-11 (user playtest round 3 — "no witness and siphon bomb still")

`WitnessBomb.prefab` and `SiphonPanicBomb.prefab` were rebuilt (all internal fileIDs regenerated), so the `_bombPrefab` slots on `SmartEnemyWitness`/`SmartEnemySiphon` pointed at fileIDs that no longer exist → Unity resolves them as null → `CanThrowBomb`/`SpawnPanicBomb` silently no-op. **Fix:** repaired both references to the new root GameObject ids (6219126661784368344 / 2641028527894749575); both throw paths now `LogError` on a null bomb slot (fail loud). **Class lesson:** a prefab rebuild breaks every external fileID reference into it — Scene Health Dashboard's missing-ref recipes are the net.

### BUG-054 — VFX-graph one-shot cues held forever (HitVfx accumulation, perf drain)
Status: Fixed (2026-07-11; user retest pending)
Severity: Major
System: FX / FxManager / cue books
Discovered: 2026-07-11 (user playtest round 3 — dozens of HltVfx(Clone) alive under FxPoolRoot)

`FxManager.SpawnVfx` had no duration path — every VFX-graph element got `expireAt = ∞` (Pattern B), so fire-and-forget plays (every melee hit) leaked a live instance until STOP ALL FX. **Fix:** `SpawnVfx` takes an explicit lifetime (element `duration` > 0 → auto-return; 0 → held as before); durations authored on the leaking one-shots (AttackCueBook hits, Common on_hiteffect, radorb_cast, spawn_hit/spawn_disable). kill_seq's existing durations now actually apply.

### BUG-055 — Teleport marker preview leaks a second held castmark on button spam
Status: Fixed (2026-07-11; user retest pending)
Severity: Minor
System: Abilities / Weaver's Gate / TeleportMarkerPreview
Discovered: 2026-07-11 (user playtest round 3)

Spamming aim while the soul was already out called `Show()` again: the new held preview handle overwrote the old one (orphaned forever) and aiming mid-gate was allowed at all. **Fix:** `Show()` early-outs while `TeleportAbility.IsActive`, stops any prior handle before playing a new one, and `OnDisable` stops the handle.

### BUG-056 — Arrows stuck mid-air with FX riding them (pool return aborts on an exception mid-chain)
Status: Fixed (2026-07-11; user retest pending)
Severity: Major
System: SpawnSystem / GameplayPool + EnemyPool + FxManager + Arrow
Discovered: 2026-07-11 (user playtest round 3, diagnosed LIVE in the paused editor)

**TRUE ROOT CAUSE (solved 2026-07-11, via the hit-instrumentation logs — two identical hit logs per arrow):** a twin has MULTIPLE colliders, so one arrow receives TWO `OnTriggerEnter` calls in the same physics pass. Event 1 hits, despawns the arrow (`OnDespawned` resets `_hasHit=false`, instance enqueued). Event 2 still dispatches on the now-inactive instance: it passes the freshly-reset `_hasHit` guard, deals DOUBLE DAMAGE, re-sets `_hasHit=true`, and its own despawn no-ops on the pool's `InPool` guard — so the arrow enters the free queue with `_hasHit=true` baked in. The NEXT spawn of that instance never moves (`Update` early-outs on `_hasHit`) and never triggers: a frozen arrow at the muzzle the instant the enemy fires, with its head/trail cues riding it ("impact/arrow stuck at the enemy hand"). **Fix:** `OnTriggerEnter` is inert when `!gameObject.activeInHierarchy` (kills same-pass double events AND the double damage), and `OnSpawned` resets `_hasHit` at issue (defense-in-depth). The earlier hardening pass (kept): `FxManager.Stop` try/log/finally-reclaim, `ActiveBook.Stop` null-safe, `GameplayPool.Return`/`EnemyPool.Return` per-step try/catch with guaranteed deactivate, `Arrow.OnDespawned` state-first, and the hit/return bracket logs.

### BUG-057 — Pooled enemies respawn with stale state tint (possess/stun/ritual material colour survives pool return)
Status: Fixed (2026-07-11; user retest pending)
Severity: Minor
System: Enemies / EnemyPool reset contract
Discovered: 2026-07-11 (user report — remembered "possessed materials stayed changed" class)

`Enemy.ResetForPool` reset flags/movement/attack but never restored `_renderer.material.color` — an enemy killed while stunned (cyan), possessed (purple), ritual-glowing (Witness), crushing/reflecting/raging (Penitent) or raging (TetherBreaker) re-entered the pool tinted and respawned tinted. All writers use the base `_renderer` vs `_originalColor`, so the **fix** is one generic line in ResetForPool restoring the authored colour.

### BUG-058 — Pooled enemy reuse spawns DEAD: unkillable standing body that never returns to the pool
Status: Fixed (2026-07-11; user retest pending)
Severity: Major
System: Enemies / EnemyHealthComponent / pool reset contract
Discovered: 2026-07-11 (user playtest round 3 — "killed enemy stuck on screen" + "enemies from the pool not taking damage")

**Regression + re-fix (same day):** the first fix called `ResetToFull()` from `Enemy.ResetForPool`, which runs INSIDE the OnDeath event — it reset `LastDamageType` before `EnemyDeathNotifier`'s (later) OnDeath handler read it, so every kill classified Environmental: accord bar, souls and kill helix all went silent. Health reset now happens at ISSUE time in `EnemyPool.Get` — never during the death event.

`EnemyHealthComponent._currentHealth` was initialised ONLY in `Awake`. A pooled reuse of a killed enemy therefore spawned with 0 HP: `IsDead=true` short-circuits `TakeDamage`, so the enemy could neither be damaged nor die again — an immune body standing in the scene forever (it "isn't returning to the pool" because the death that would return it can never fire). Spawn paths that pass `EnemyData` were accidentally healed by `ApplyData → SetMaxHealth`; paths that don't (Witness melee minion, any data-less `SpawnReady`) exposed it. **Fix:** `EnemyHealthComponent.ResetToFull()` called from `Enemy.ResetForPool()` — the reset contract now covers health; every reuse starts alive at max.

### BUG-059 — Manpu never displays (ManpuDirector component absent from every enemy prefab)
Status: Fixed (2026-07-11; user retest pending)
Severity: Major
System: Manpu / enemy prefabs
Discovered: 2026-07-11 (user — "mood changing in logs but no manpu; used to work as particles")

`ManpuDirector` is the sole subscriber routing `EnemyMoodSystem.OnMoodChanged` / perception changes into the `ManpuSlot` (glyph sprite + burst particle + sound). It was on NO enemy prefab, so every mood transition fired into the void — sprite or particle, nothing showed (explains "used to work as particles" — the display path is director→slot regardless of payload). **Fix:** self-wiring `ManpuDirector` added to the mood-system host GameObject on all 9 mood-bearing enemy prefabs via `PrefabUtility` (SiphonGhost excluded — no mood system). Sprite path verified sound (Sprite import, `_vocabulary`/`_glyph` wired, `UIBillboard` faces camera) — no world-space-Canvas migration needed. Remaining authoring: perception rows have no sprites; `escalatingOnly` still gates curated→curated drift (per-row toggle).

**Follow-up cleanup 2026-07-12 (BUG-059b):** the *earlier* failed YAML injection of `ManpuDirector` (before the API redo) had left **1–2 stray `ManpuDirector` components on random child GameObjects** (ManpuGlyph, CanvasEnemyUI, Fill, HealthBarPanel, HealthDisplayText, Background) of every prefab — Melee/Witness/Severed/Siphon/TetherBreaker had 2 strays, the rest 1. All inert (no `EnemyMoodSystem` on their host GO → `GetComponent` self-wire finds null → subscribe to nothing), but confusing (user saw an empty director on the glyph child and asked why). The one valid director on the root was present on all 9 the whole time (why manpu worked). **Fix:** loaded each prefab via `PrefabUtility.LoadPrefabContents`, `DestroyImmediate` every director not on the root-with-mood-system, `SaveAsPrefabAsset`; verified each prefab now has exactly 1 director (`OK=True`), console clean. LESSON: a malformed YAML component injection can *partially* apply onto arbitrary child fileIDs — always re-scan with `GetComponentsInChildren(true)` after, never trust the count on the root alone.

---

## Logged 2026-07-15 — user playtest (Bootstrap run) + debugger session

### BUG-060 — Stun cue rotates with the player
Status: Fixed (2026-07-15)
Severity: Minor
System: FX / Cue attach
Symptom: Stun VFX inherits the player's rotation; should follow position only.
Fix: new `FxAttachMode.FollowPositionOnly` (appended, serialization-safe) — FxManager follows the
target's position with a world-axis offset and keeps the spawn orientation for the cue's life
(faceTarget still wins). StunCueBook `OnStun_Active` element re-set to the new mode.
Verified by: TestLab play — held OnStun_Active kept rot y=137 while Kai spun 30°→210°; position tracked.
Regressions: 0

---

### BUG-061 — Possess cue rotates with the player
Status: Fixed (2026-07-15)
Severity: Minor
System: FX / Cue attach
Symptom: Same class as BUG-060 — possess VFX spins with player rotation; should track position only.
Fix: PossessCueBook `Possess_Active` element re-set to `FollowPositionOnly` (same mechanism as BUG-060).
Verified by: BUG-060's play test covers the shared code path; visual re-check on next user playtest.
Regressions: 0

---

### BUG-062 — Melee slash cues not playing after weapon pickup
Status: Fixed (2026-07-15)
Severity: Major
System: Combat / AttackCueBook
Symptom: After picking up the melee weapon, slash VFX do not play in normal play NOR via GameDebuggerV2.
Root cause: `Twins.prefab` was never re-saved after the `_attackBook` slot was added to
PlayerAttackController — both twins' book slot was null (the stale pre-cue-book `_slashPrefab`/`_hitPrefab`
lines are still serialized in the prefab as dead data). Fix: AttackCueBook assigned on both twins.
Verified by: TestLab play — SetHasWeapon(true) + ExecuteHitDetection spawned the slash cue instance.
Regressions: 0

---

### BUG-063 — Melee hit on SpawnPoint does nothing
Status: Fixed (2026-07-15) — needs in-game verify
Severity: Major
System: Environment / SpawnZone prefab
Symptom: Attacking the spawn point with melee has no effect (no damage/reaction).
Root cause: `SpawnZone.prefab` (the SpawnPointPOI carrier) had NO root collider, layer Default, and no
IDamageable — three independent reasons melee could never touch it (melee overlaps the Enemy layer and
calls GetComponent<IDamageable>; SpawnPointPOI.TakeDamage(float) isn't on that interface).
Fix: root set to layer Enemy + BoxCollider sized from renderer bounds (1.5×7.1×1.5) + PoiDamageAdapter added.
Verified by: prefab audit; needs a live melee swing in L1_Park to confirm spawn_hit fires + HP drains.
Regressions: 0

---

### BUG-064 — Teleport marker only appears on fire, not while holding
Status: Fixed (2026-07-15) — needs in-game verify
Severity: Major
System: Abilities / Weaver's Gate marker
Symptom: Holding the ability button while moving shows no cast marker; it only appears on release. Historical context: the old bug was MULTIPLE markers while holding+moving; the fix appears to have over-corrected (marker suppressed during hold). If it's a prefab lifetime issue, user will set lifetime 0.1s.
Root cause: TwinAbilityDispatcher calls ShowTeleportPreview() EVERY held frame, and the round-3
re-entrancy guard in TeleportMarkerPreview.Show() stopped + respawned the held castmark cue each call —
the particle system was Clear()ed every frame, so no particle ever lived long enough to render. NOT a
prefab lifetime issue (no 0.1s change needed).
Fix: Show() is now idempotent while the preview cue is live (markerObject active + IsPlaying handle) —
the cue keeps riding the marker; Update() keeps moving it.
Verified by: code audit; user verify = hold C during rescue, watch the marker while moving.
Regressions: 0

---

### BUG-065 — Coalesce aura not visible on enemy (regression)
Status: Watch (2026-07-15) — full chain verified WORKING in TestLab; could not repro
Severity: Major
System: Abilities / Coalesce
Symptom: Coalesce aura no longer renders on the enemy; previously worked.
Findings: the entire real path was play-verified end-to-end (real StunAbility cast → OnStunApplied →
CoalesceSystem.HandleApplied → pooled aura spawn → on_aura "Star aura" cue live under FxPoolRoot,
upgraded radius/dps applied). Book/library/prefab wiring all correct. Most likely field cause: the
unlock gate — HandleApplied hard-blocks (log: "[Coalesce] BLOCKED — not unlocked") when the Coalesce
node isn't purchased in that save. Hardened: CoalesceAura now LogErrors when the cue book resolves
null (that path was silent — an invisible aura with working damage).
Verified by: TestLab play 2026-07-15. If it recurs, check the console for the BLOCKED line first.
Regressions: 0

---

### BUG-066 — RadiantSeeker orb can't reach its target
Status: Fixed (2026-07-15)
Severity: Major
System: Abilities / RadiantSeeker
Symptom: Orb approaches but never arrives/triggers on the target.
Root cause (user's suspicion confirmed): the orb prefab's NavMeshAgent radius is 1.75 — avoidance parks
it at radius-sum distance (~2.25 m with an enemy's 0.5) while stoppingDistance is 0, so
`remainingDistance <= stoppingDistance` never fires; reproduced live (orb stalled at 2.25–2.3 m).
Fix: arrival is now proximity-based — detonate when the target is inside
`max(possessionRadius × 0.95, agent.radius + 0.75)`, with the NavMesh-arrival check kept as fallback.
Note: consider shrinking the prefab's agent radius (1.75 also fattens pathing around obstacles).
Verified by: TestLab play — orb travelled, detonated, enemy possessed, orb despawned.
Regressions: 0

---

### BUG-067 — RadiantSeeker casts TWO orbs; one disables shortly after
Status: Fixed (2026-07-15) — cast burst needs re-authoring
Severity: Major
System: Abilities / RadiantSeeker / cue authoring
Symptom: Casting spawns 2 orbs; one gets disabled after the other starts moving.
Root cause: NOT a pool double-spawn — the `radorb_cast` cue element's vfxPrefab pointed at the
VisualEffect on RadiantSeekerOrb.prefab's ROOT (the very prefab the ability spawns), so every cast also
pooled a full visual copy of the orb at the spawn point for its 1 s duration = the "second orb".
Fix: radorb_cast element emptied (empty effect = silent no-op). AUTHORING FOLLOW-UP: give radorb_cast a
real cast-burst prefab (it currently plays nothing at cast).
Verified by: TestLab play — exactly one live orb after cast.
Regressions: 0

---

### BUG-068 — Manpu: curated rows rarely play in real play
Status: Open (authoring — audit complete 2026-07-15)
Severity: Minor
System: Manpu
Symptom: Many authored vocabulary rows don't seem to fire in-game even with escalatingOnly OFF.
Audit findings (ManpuVocabulary.asset + ManpuSlot gating):
1. **loopPrefab is unauthored on ALL 13 mood rows** — the P11 held-aura channel (the SUSTAINED mood
   read) plays nothing anywhere. This is the dominant cause of "manpu feels absent": glyph pulses fire
   only on mood TRANSITIONS by design, so with no aura channel an enemy sitting in Aggressive all fight
   shows one pulse then nothing. (This was user-authoring checklist item 1 from P11 — never done.)
2. Contemptuous (10) + Territorial (11): no sprite, no burst, AND escalatingOnly=1 → dead rows.
3. Grieving (9): sprite only, no burst.
4. Perception rows reuse one placeholder sprite (fd4067cf…) for 3 of 4 states — Alerted/Searching/
   Detected are visually indistinguishable.
5. Gating itself is healthy: R2 debounce 1.5 s unscaled, R1 drops pulses only while an ability glyph
   owns the slot. No code defect found.
Recommended setup recorded in the session report (aura moods: Enraged/Panicked/Wounded(+Aggressive);
pulse-only for the ecology moods with escalatingOnly ON; distinct perception glyphs).
Verified by: — (flips to Fixed after vocabulary authoring + TestLab mood-bench pass)
Regressions: 0

---

### BUG-069 — Enemies spawned during rescue/soul cast are not frozen/slowed
Status: Fixed (2026-07-15) — needs in-game verify
Fix: TimeFactorManager.Register now applies the in-progress effect to the newcomer (OnEffectStarted on
registration while active). Covers spawner, pooled reuse (whose reset wipes the parked freeze), and
debugger spawns — every path funnels into Register.
Severity: Major
System: Rescue / TimeFactorManager
Symptom: During the soul cast (rescue), enemies that SPAWN while the effect is active move at full
speed — existing enemies freeze/slow correctly (playtest 2026-07-15).
Suspect: the freeze/slow is applied to registered ITimeAffected at TriggerEffect time; an enemy
registered AFTER the trigger never receives the current effect. Fix class: on ITimeAffected
registration, apply the active time factor immediately.
Verified by: —
Regressions: 0

---

### BUG-070 — Enemies spawn exactly ON the spawn point (VFX/position mismatch)
Status: Fixed (2026-07-15) — needs in-game verify
Fix: EnemySpawner.GetNextSpawnPoint scatters in a NavMesh-sampled ring (0.4–1× `_spawnScatterRadius`,
default 2 m, serialized) around the point; falls back to the exact point off-mesh.
Severity: Minor
System: EnemySpawner / SpawnZone
Symptom: Spawned enemies appear precisely at the spawn-point origin, mismatching the spawn VFX;
they should appear scattered in a short radius around the point.
Fix class: random NavMesh-sampled offset within a small radius at spawn.
Verified by: —
Regressions: 0

---

### BUG-071 — "Coroutine couldn't be started because the game object 'FadeCanvas' is inactive"
Status: Fixed (2026-07-15) — needs in-game verify
Fix: FadeController.EnsureActiveForFade() — every public fade entry re-activates the (alpha-invisible)
FadeCanvas GO before StartCoroutine; LogErrors if a PARENT is inactive (R11 violation); callback
overloads still invoke onComplete when skipped so waiting flows never strand. Root cause = the tutorial
Timeline Activation track (track 8) leaving FadeCanvas off after a cutscene. Only ONE FadeCanvas exists
(Persistent) — no L1_Park duplicate found.
Severity: Major
System: UI / Fade / scene residency
Symptom: Red error every rescue-ish flow (screenshots 2026-07-15). User also reports a FadeCanvas
appears to exist in BOTH Persistent and L1_Park — R9 says Persistent owns the only screen-space HUD.
Suspect: a fade caller StartCoroutine's on an inactive FadeCanvas GO (SetActive(false) instead of
alpha 0), and/or a legacy duplicate canvas in the area scene.
Verified by: —
Regressions: 0

---

### BUG-072 — Cracks bloom to blinding white, varies with camera position
Status: Fixed (2026-07-15) — needs in-game verify
Root cause: `Assets/Settings/DefaultVolumeProfile.asset` (the URP global default under everything) had
Tonemapping actively overridden to **None** — in area scenes (no ACES volume reaching them) the crack
material's HDR emission (`IniCrackGlowMaterial 1` _EmissionColor intensity ≈12) hard-clamped to pure
white across the whole glow falloff; view-dependent attenuation made it swim with the camera. We never
touched crack post — the LDR→HDR grading work exposed it.
Fix: DefaultVolumeProfile Tonemapping → ACES. TUNING (user): crack emission 12 is still hot — consider
~3–5; SampleSceneProfile bloom threshold 1.0 → docs canon 1.1–1.3.
2026-07-15 follow-up: user reported still not fixed after lowering emission → SECOND cause found, see BUG-073.

### BUG-073 — CrackPark crack still blinding after BUG-072 fix (duplicate ProBuilder faces)
Status: Fixed (2026-07-15) — needs in-game verify
Root cause (user's own suspicion, confirmed): `L1_Park → MainLvl → CrackPark → PolyShapeWall`
(ProBuilder mesh, still live pb_Mesh) contained **284 exact-duplicate faces** — remnants of unmerged
faces the user couldn't hand-delete. Coplanar duplicate emissive faces z-fight: shimmering blinding
patches that change with camera position. Verified by position-multiset face grouping (184 duplicate
tris pre-fix).
Fix: duplicates removed via ProBuilder `DeleteFaces` (1912→1628 faces, 3811→3246 tris, 3 residual
tolerance-level dups), `ToMesh`+`Refresh`, L1_Park saved. All other cracks checked: street/park fbx
cracks clean scene-side; L3_Alley fbx meshes carry 4/2 dup tris in the SOURCE fbx (asset-level,
minor — fix in DCC only if ever visible).
2026-07-16 LIVE-ISOLATION FOLLOW-UP (played with the user; the dup faces were real but SECONDARY):
the dominant cause of the giant white blob + screen-wide milky veil was the OLD
`Shader Graphs/CrackGlow` material rig — ZWrite-off ADDITIVE glow on the enormous canyon meshes:
seen edge-on above the gap, dozens of overlapping wall layers summed additively to white-hot and
hazed the whole frame (proven by toggling the Mask/SeeThrough/CrackLayer stencil trio: without the
mask the giant magenta geometry floods the screen with the same white core). Every other suspect
eliminated live with fresh frames (sun shafts, fog, skybox sun, CrackFlame VFX ×12, twins'
HealthParticleSystem, bloom/post stack, all 5 overlay canvases, world-space canvases, lens flares).
FIX: new `PoT/CoexistenceCrack` shader (Assets/Art/Shaders/) — OPAQUE + ZWrite On + DepthOnly, so
stacking can never blow out; keeps the concept (dark at ground → hot at canyon bottom, per-material
`_DepthRange`/`_GradientPower`) + adds the Colour Bible §7 corruption journey (Pure-Current icy →
Khal-Vor oily, `_WorldCorruption` global) + clan streaks on geometric edges + slow energy scroll.
Both crack materials switched to it (revert = reassign Shader Graphs/CrackGlow — graph untouched).
Verified live in play: blob GONE, veil GONE, see-through-ground rig still works.
METHOD GOTCHA (recorded for future live debugging): with the editor unfocused, play-mode frames
FREEZE — game-view screenshots return stale frames, invalidating toggle tests. Set
`Application.runInBackground = true` FIRST, then isolate.

### BUG-074 — GraphicRaycaster spams "Screen position out of view frustum (-nan)" errors
Status: Fixed (2026-07-17, pending play verify)
16 repeated errors during the 2026-07-16 play session: uGUI GraphicRaycaster gets a NaN screen
position (Camera rect 0 0 1110 632), stacktrace-less, from Raycast at GraphicRaycaster.cs:183/326.
ROOT CAUSE: `PauseMenuController` sets `Cursor.lockState = Locked` on unpause
(PauseMenuController.cs:135); the Persistent EventSystem's `InputSystemUIInputModule` had
`Cursor Lock Behavior = OutsideScreen` (the default), which reports the pointer at (-∞,-∞)
while locked — every world-space canvas GraphicRaycaster then calls ScreenPointToRay with a
non-finite position → the frustum spam. Known Unity issue with exactly this pairing.
FIX: `m_CursorLockBehavior: 0 → 1` (ScreenCenter) on the InputSystemUIInputModule in
Persistent.unity (edited on disk while the scene was unloaded). NOTE: SampleScene's own dev
EventSystem still has the default (left alone — scene was open in the user's editor; dev-only
spam, fix the same way if it ever annoys).
VERIFY: play from Bootstrap, unpause so the cursor locks, wander with world-space canvases
(pickups/rescue ring) on screen — console must stay free of the frustum error.
Severity: Major
System: Rendering / crack material × bloom
Symptom: Crack glow blows out to white over large screen areas; intensity shifts with camera
position (playtest 2026-07-15 screenshots — light poles bloom the same way).
Suspect: post-processing grading changes (ACES/HDR + low bloom threshold) interacting with the crack
material's HDR _EmissionColor; docs canon = bloom threshold 1.1–1.3. We did NOT change crack post.
Verified by: —
Regressions: 0

---

### BUG-075 — Pause menu never pauses gameplay audio (F4 SetPaused/snapshot had no callers)
Status: Fixed
Found during F7 audit (2026-07-16). `AudioManager.SetPaused`/`ReleasePaused` (sole
`AudioListener.pause` writer) and the `Paused` mixer snapshot shipped in P9.2 but were **never
called by any consumer** — grep for `SetPaused`/`RequestSnapshot` found only their definitions.
Opening the pause menu froze time (TimeScaleService) but gameplay SFX/voices kept playing,
contradicting F4 ("gameplay audio halts via `AudioManager.SetPaused`"). The P9.4 call-site
wiring noted in the BUG-`PlayUI` progress note was never completed.
Severity: Minor
System: Audio / Pause (F4)
Symptom: Enemy/ability/ambient one-shots continue audibly while the game is paused.
Fix: `PauseMenuController.OpenPause` → `AudioManager.SetPaused(this)` +
`RequestSnapshot(this, AudioSnapshotId.Paused, 50)`; `Resume`/`ExitGame` release both (owner
pattern). Changelog [Unreleased] 2026-07-16.
Verified by: — (play-mode ESC verification pending a human run)
Regressions: 0

---

### BUG-077 — Entering play mode permanently dirties the committed `M_SunShafts.mat` asset
Status: **WON'T FIX — user decision 2026-07-18. DO NOT REATTEMPT.**
Numbering note: 076/078/079/080 were filed on the reverted UI-swap branch
(`ui-swap-2026-07-19`) and do not exist here; 077 is re-filed because the fix commit
(`67ea526`) was part of that revert, so the defect is LIVE on this branch again.
Found 2026-07-18: `git status` showed `Assets/Art/Materials/M_SunShafts.mat` modified although
nobody had edited it — the only thing that had happened was pressing Play in `Persistent`.
Severity: Minor (cosmetic in-game; the real cost is a noisy `git status`)
System: Grading / Rendering
Symptom: `SunShaftsDriver` writes `_SunUV` / `_SunVisibility` onto the MATERIAL ASSET each frame.
In the editor that is not a transient runtime value — it rewrites the `.mat` file on disk.
Accepted workaround (standing rule): discard the file before every commit —
`git checkout -- Assets/Art/Materials/M_SunShafts.mat`
Why it is Won't-Fix: the god rays work; the fix touches a shader consumed by a fullscreen
renderer feature, and risking a working visual to silence a version-control annoyance is a bad
trade. The per-frame-globals fix WAS implemented and verified once, then reverted at the user's
instruction.
Trap for anyone who reattempts: removing the `_shaftsMaterial` serialized field made Unity save
`Persistent.unity` WITHOUT that reference, so reverting the code left the slot empty and god rays
were **silently disabled** until it was re-assigned by hand. Any future attempt must re-check that
scene reference after reverting.
Verified by: — (Won't Fix; no fix to verify)
Regressions: 0

---

## Playtest batch — 2026-07-31 (GDAI Supernova, deadline extended to Aug 5)

Reported by user from memory (~80% of known issues; more to follow). Logged before fixing per the rules above.

---

### BUG-081 — Passive health regen feels intermittent / "stops"
Status: Fixed — part (A) bar-fill masking (2026-08-01, script-only; pending recompile + play-retest). Part (B) regen combat-only arming = still open/optional (not bundled).
Severity: Major
System: Health
Symptom: Passive regen (should always run out of combat, skill-tree-enhanceable) appears to stop / not work sometimes.
Root cause (fully traced 2026-07-31):
- NO CODE REGRESSION. `HealthRegenHandler.cs` byte-identical to cue-book era (923c195). `PlayerHealthComponent.cs` diff vs 923c195 = ONLY the additive `SurvivalHealth01`/`BondWeakness01` properties + `OnBondWeaknessChanged` event; the regen `Update()` and `TakeDamage` arming are unchanged. "check old git" ruled out.
- NOT clobbered by the shared pool. `SharedHealthPool.CombinedHealth` is DERIVED (`left.DisplayHealth + right.DisplayHealth`, recomputed on change); it never writes back into per-twin `_currentCombatHealth` (except Setsuna `ForceSetHealth`). So regen accumulates on `_currentCombatHealth` fine.
- DOMINANT CAUSE = display masking. Bar shows `DisplayHealth = _currentCombatHealth × _distanceModifier − overMaxDrain×max`. `TwinBondManager.Update()` sets modifier<1 whenever distance>6m, so as soon as the twins are even slightly apart the visible bar stalls/falls WHILE `_currentCombatHealth` is regenerating. Reads as "regen stopped." The fix already exists but is UNWIRED: drive bar FILL off `SurvivalHealth01` (real pool) — this is the World/HUD UI revamp (see project_health_channels, project_worldui_revamp). No UI currently consumes SurvivalHealth01.
- SECONDARY GAP = combat-only arming. `TakeDamage` calls `OnCombatDamageTaken()` only `if (Type == Combat)`, which sets `_hasEverTakenDamage` AND resets the delay. HP lost purely to non-combat (trap/environmental/ability) never ARMS regen (`_hasEverTakenDamage` stays false ⇒ regen never runs) if the twin never took a combat hit. Low-risk safe fix: arm `_hasEverTakenDamage` on ANY damage; keep the delay RESET combat-only so combat pacing is unchanged. (Distance over-max drain does NOT flow through TakeDamage — it's subtracted in the DisplayHealth getter — so arming-on-any won't be spammed by drain.)
**USER DECISION 2026-07-31: do BOTH — "fix the bar masking thing too."** Fix plan:
- (A) Wire the health bar FILL to `SurvivalHealth01` (real pool, distance-independent) instead of `DisplayHealth`. Find the bar view(s) that subscribe to `OnDisplayHealthChanged` (SharedHealthPresenter / UIBarView / world-HUD bars) and switch fill to a SurvivalHealth01-driven channel; optionally drive COLOUR off `BondWeakness01`. Must respect the "one channel one meaning" ruling (see project_worldui_revamp). NOTE: the shared pool is `left.DisplayHealth + right.DisplayHealth` — for a SHARED bar, sum the per-twin SurvivalHealth01 (or expose a combined survival channel) so the shared bar also stops masking.
- (B) Arm regen on ANY damage (set `_hasEverTakenDamage` on any TakeDamage), keep the delay RESET combat-only (unchanged combat pacing). Small safe change in PlayerHealthComponent/HealthRegenHandler.
**Fix APPLIED — part (A) 2026-08-01 (script-only; NO scene wiring — both refs already existed):**
- `SharedHealthPool.cs`: added `CombinedSurvival01` (distance-independent real pool 0..1 = mean of the two `SurvivalHealth01`) + `OnSurvivalChanged` event; both twins' change events now route through `HandleTwinChanged()` → `RecalculateCombined()` (masked combined + game-over path UNCHANGED) then pulses `OnSurvivalChanged`.
- `SharedHealthPresenter.cs`: bar FILL now subscribes to `OnSurvivalChanged` and reads `CombinedSurvival01` (was `OnCombinedHealthChanged` → masked `CombinedHealth/Max`). `OnSharedPoolEmpty` (game-over) + emergency handlers untouched. Per-half DRAIN stays owned by `BondWeaknessPresenter` (already wired to `BondWeakness01`) — the intended split, per that class's own doc ("FILL ← SurvivalHealth01, driven by SharedHealthPresenter").
- Net: walking apart no longer drops the bar FILL (only greys the halves); regen shows on the bar as real HP rises. Masked `CombinedHealth` still drives game-over (over-max-drain kill preserved).
- Part (B) NOT done (not bundled per CLAUDE.md #7): arm `_hasEverTakenDamage` on ANY damage — awaiting user go-ahead.
- NOTE: verify `BondWeaknessPresenter` is actually attached+wired on the shared emblem (drain/grey channel); the fill fix is independent of it.
Verified by: — (pending: exit Play → Unity recompiles the Play-time edit → re-enter → stretch twins at full HP: bar stays FULL, halves grey; combat-damage then back off: bar refills as regen runs)
Regressions: 0

---

### BUG-082 — Rescue unfreezes enemies before the soul returns to the caster
Status: Fixed (2026-08-01) — pending in-game play-retest
Severity: Major
System: Rescue / AI (soft freeze via shared blackboard)
Symptom: After a rescue completes, enemies unfreeze immediately — they should stay frozen until the rescuing soul travels back to the twin that cast it, THEN unfreeze.
Root cause (traced 2026-07-31): The rescue "freeze" is NOT `EnemyFreezeService` (that's QTE-only, and the twin-rescue is a SEPARATE state machine, not a QTE). It is the SOFT freeze: `PoTWorldStateWriter.OnRescueStateChanged` writes `IsRescueActive = (state != Idle)` to the shared blackboard; `GOAPGoalAttackTwin`/`GOAPGoalGrabTwin` return `DoNotRun` when it's true; enemies idle → look "frozen." Timeline: `IsRescueActive` goes TRUE at `Triggered` (soul arrived at the grabbed twin, mash begins) and FALSE the instant the rescue succeeds (`Success → CleanupRescueEvent → Idle`). BUT the soul only travels BACK to the caster LATER, in `TeleportAbility.End() → ReturnSequence()` (soul lingers at the rescue spot until its timer expires or the player X-holds to cancel; return ends at ReturnSequence line ~418 where `SetTwinsMovementLocked(false)` = "player can move only after teleport-in"). So enemies resume attacking during the whole soul-return trip.
Fix design: keep the enemy-facing freeze flag TRUE from rescue-trigger until the soul is HOME (end of ReturnSequence). Cleanest low-blast-radius route: drive the shared enemy-freeze flag off `rescueActive || soulDeployed`, where `soulDeployed` is a new signal that is true from `TeleportAbility.Activate()` until ReturnSequence completes. MUST fail-safe: soul death (`HandleSoulDied`→ForceEnd), gate cancel, and scene unload must all clear `soulDeployed` or the flag STICKS TRUE and enemies freeze forever (the exact stuck-true failure the RescueEventController:551 comment already warns about). Do NOT reuse the dead `SoulIsActive` flag blindly (it's read by PoTBTActionBase/UtilityFactorKeys and wiring it on could wake dormant behaviours) — add a dedicated signal. Touches: TeleportAbility (emit deploy/home), RescueEventController or PoTWorldStateWriter (aggregate), the two GOAP goals (read combined flag). Fragile AI layer — implement carefully with all End() paths covered.
Fix APPLIED 2026-08-01 (0 CS errors, MCP): chose to **extend the existing `PoTNames.IsRescueActive` blackboard key** rather than add a new one — verified (grep) its only real readers are the three freeze goals (AttackTwin/GrabTwin/DefendSpawn); `PoTBTActionBase.IsRescueActive` and the `UtilityFactorKeys.IsRescueActive` const have **no consumers**, so extending its true-duration is confined to the enemy freeze. The C# `IRescueActive.IsRescueActive` (player-ability gate: Setsuna/Empower/Accord/SoulConv) is a SEPARATE property and was left untouched (player-ability blocking still ends at Success). Changes: (1) `TeleportAbility.IsSoulDeployed` — set true at the `Activate()` commit (after guards), cleared at the end of `ReturnSequence()`. (2) `RescueEventController` stores both registered abilities in `_teleportAbilities` and exposes `IsAnySoulDeployed` (distinct from `_activeSoulAbility`, which CleanupRescueEvent nulls at Success). (3) `PoTWorldStateWriter` stores `_rescueActive` (event-driven) and **polls** `IsAnySoulDeployed` each frame in `Update()`, writing `IsRescueActive = _rescueActive || anySoulDeployed` only on change (`_lastRescueFreeze` de-dupe). Poll (not event-pair) is the fail-safe: cancel (X-hold→ForceEnd→End→ReturnSequence), soul death (invincible during travel anyway, same path), re-cast (re-sets true), and destroy/Restart (fresh instances default false) all self-heal — the flag can't stick true. Reset: soft-reset `enemySpawner.DespawnAll()` removes enemies during the ~1s self-heal, so no visible over-freeze. KNOWN minor: `DamageDealerDebug` (dev-only) sets `IsRescueActive` directly for testing — the poll now stomps that within a frame; use the real rescue flow to test.
Verify (in play): (a) grab a twin, cast Gate, mash to success — enemies stay idle through the soul's RETURN flight and resume the moment it lands home; (b) X-cancel mid-deploy — enemies stay frozen until the (interrupted) return completes, then resume; (c) no enemy stuck permanently frozen after any rescue (flag not stuck true).
Verified by: compile clean (0 errors, MCP console) — in-game verification pending
Regressions: 0

---

### BUG-083 — Summoned enemies (Siphon + anything the Summoner spawns) skip the rescue freeze
Status: FIXED (DefendSpawn rescue-gate) 2026-07-31 — user-authorised; TestLab-only repro remains unconfirmed (see note)
Severity: Major
System: AI (soft freeze) / Summon
Symptom: During rescue the Siphon (and other Summoner-spawned enemies) don't freeze.
Root cause (traced 2026-07-31): The rescue freeze is the SOFT freeze (shared `IsRescueActive` → GOAP `DoNotRun`), NOT `EnemyFreezeService` (which is QTE-only — my earlier snapshot theory was WRONG for the twin-rescue). Findings:
- BOTH `GOAPGoalAttackTwin` AND `GOAPGoalGrabTwin` gate on `IsRescueActive`. Every enemy brain re-reads the shared flag each tick (`PoTGOAPBrainBase.SyncSharedStateToBlackboard`, global `GetSharedBlackboard` lookup), so a summoned MELEE/RANGED minion SHOULD freeze the moment it ticks after `IsRescueActive` goes true. If summoned attack-twin minions genuinely don't freeze, the bug is in the summon SPAWN path (brain not initialised / IsBrainPaused stuck / blackboard link missing) — needs a spawn-path audit (GOAPActionSummon/BTActionSummon → EnemyPool.SpawnReady).
- The SiphonGhost is DIFFERENT: its brain `GOAPBrainSiphonGhost` runs `GOAPGoalGhostBind`/`GOAPGoalGhostPursuit`, which target the SOUL, not a twin — so they are (by design) NOT gated by `IsRescueActive`. The ghost is MEANT to chase/bind the rescuing soul during the rescue. "Siphon didn't freeze" may be this by-design behaviour, not a bug.
USER CLARIFICATION 2026-07-31: it is the **SIPHON ENEMY ITSELF** (not the ghost) that keeps attacking during a rescue, specifically **when spawned as a pair with a Summoner / summoned by a Summoner**. User: "just check the logic chain."
LEADS (traced 2026-07-31): `GOAPBrainSiphonEnemy` runs goals `GOAPGoalPossessed (Max 100)` + `GOAPGoalDefendSpawn (Critical 90)` + `GOAPGoalAttackTwin (High 75)`. `GOAPGoalAttackTwin` IS gated on `IsRescueActive` (would DoNotRun during rescue), BUT:
  1. PRIME SUSPECT — `GOAPGoalDefendSpawn` (priority 90 > AttackTwin 75) is likely NOT gated on `IsRescueActive`; if it stays valid during rescue and its action attacks/pursues, the Siphon keeps acting. CHECK `GOAPGoalDefendSpawn.cs` + `GOAPActionDefendSpawn.cs` for an IsRescueActive gate.
  2. SPAWN-PATH — the Summoner spawns via `GOAPActionSummon`/`BTActionSummon` → `EnemyPool.SpawnReady`. Confirm the summoned Siphon's brain actually re-syncs the shared blackboard (`PoTGOAPBrainBase.SyncSharedStateToBlackboard`) — a pooled/summoned Siphon whose brain isn't ticking / IsBrainPaused stuck / blackboard unlinked would never see `IsRescueActive`.
  3. Why "when summoned specifically" — a hand-PLACED Siphon freezes but a SUMMONED one doesn't → points at the spawn-path init (lead 2) more than the goal gating (lead 1). Trace both; start with the DefendSpawn gate (cheap) then the summon spawn-init.
CONFIRMED MECHANISM 2026-07-31 (full static trace):
- Twin rescue freeze = SOFT ONLY. `RescueEventController` has ZERO `EnemyFreezeService`/QTE calls (grep) — so the per-tick shared `IsRescueActive` covers late-summoned enemies automatically. The "summoned enemy escapes a one-shot snapshot" theory is DEAD for the twin-rescue.
- `SmartEnemySiphon.prefab` carries BOTH goal+action pairs (GUID-verified in the prefab): GOAPGoalAttackTwin+GOAPActionAttackTwinSiphon (rescue-GATED, shoots) AND GOAPGoalDefendSpawn+GOAPActionDefendSpawn (NOT rescue-gated; `BTActionDefendSpawn` = rush to spawn at 1.2× speed, deals NO damage). The Siphon brain docstring listing "only GOAPActionAttackTwinSiphon" is STALE.
- `GOAPGoalDefendSpawn` (pri 90 > AttackTwin 75) only gated on possessed/stunned/`SpawnUnderAttack`. `SpawnUnderAttack` is a single GLOBAL shared bool flipped true by `SpawnPointPOI.NotifySpawnUnderAttack` → true around a Summoner whose spawn point is being attacked. So during a rescue the Siphon keeps running DefendSpawn (movement, not damage) while AttackTwin is suppressed. A hand-placed Siphon in a quiet zone never hits this (SpawnUnderAttack stays false → DefendSpawn DoNotRun → truly idle). THAT is the summoner-specificity in real levels.
Fix APPLIED 2026-07-31: `GOAPGoalDefendSpawn.PrepareForPlanning` now `DoNotRun` when `IsRescueActive` (mirrors GOAPGoalAttackTwin/GrabTwin). Generic — fixes every enemy carrying DefendSpawn (Witness, TetherBreaker, Severed, Ranged, Penitent, GroupGrab, Summoner, commanders), not just the Siphon. Compiles clean (0 console errors).
CAVEAT (user, 2026-07-31): the observed repro was in TestLab, where there is NO SpawnPointPOI → `SpawnUnderAttack` was FALSE → DefendSpawn would already have been DoNotRun there. So the gate does NOT explain the TestLab-only movement; user said the Siphon "was attacking but might not be dealing damage" and told me to add the DefendSpawn guard and LEAVE the uncertain remainder. Re-verify the summoner case in a REAL area scene (spawn point present) + watch whether any Siphon movement persists in TestLab during a genuinely-active rescue (IsRescueActive true).
Verified by: static trace + prefab GUID check + clean compile (2026-07-31). In-game verification pending (needs area-scene summoner + active rescue).
Regressions: 0

---

### BUG-084 — Group Grab plays grab animation before grabbing (Summoner-spawned path)
Status: PARKED by user 2026-08-01 (root cause found — proximity gate missing; fix deferred). Real bug (NOT a TestLab artifact).
Severity: Minor
System: GroupGrabEnemy / Summon / VFX
Symptom: A Summoner-spawned GroupGrab enemy starts its grab animation/VFX before it has actually grabbed a player. USER NOTE 2026-08-01: reproduced in **TestLab** via the "Summon" button. USER REFRAME: the **grab VFX (`On_wardenGrab`) should only play when the grab actually succeeds in grabbing the player.**
ROOT CAUSE — MISSING PROXIMITY GATE (2026-08-01, traced end-to-end): the grab VFX plays in `GroupGrabEnemy.StartGrab()` (`PlayCue(On_wardenGrab, Follow(player))`) at the commit point, and the ENTIRE path to that commit has **no distance/contact check anywhere — it is purely ANGULAR**:
- `BTActionGetBehindTarget.IsBehindTarget`: `Vector3.Dot(target.forward, toEnemy) < behindDotThreshold (-0.3)` — rear-arc angle only, NO distance. A warden 5 m behind already counts as "behind" and the node Succeeds (it never has to close the gap; `behindPos` is only the MoveTowards target, not the success condition).
- `BTActionWaitBehind`: same angle-only check held for `behindTimeRequired` (1.5 s). No distance.
- `GroupGrabEnemy.StartGrab()`: guards only `Target != null`, `!_grabOnCooldown`, `player != null && !player.IsGrabbed`. NO distance.
So the grab commits (SetGrabbed + `On_wardenGrab` VFX + `On_wardenGrabSoulConsume`) whenever the warden has merely been in the player's rear arc for 1.5 s, **at any distance**. Summoner-specificity: a summoned warden (`SpawnReady(..., playSpawnCue:false)` → skips `SpawnRevealRoutine` brain-pause) spawns near the twin, often already inside the rear arc, so `GetBehind` succeeds instantly WITHOUT closing distance → grab VFX fires at range. A hand-placed warden circles in from the front (closing distance) so it's usually in contact when it grabs — hiding the gap. TestLab repro is genuine (GameDebuggerV2 Spawn = real pooled lifecycle; "Summon" = real `TriggerSummon`).
PROPOSED FIX (deferred — user parked 2026-08-01): add a proximity guard so the grab commits (and the VFX plays) only on genuine contact. Cleanest: gate `StartGrab()` on distance to the player (return false if beyond a grab radius) — that single point owns both the commit and the VFX. Optionally also require proximity in `IsBehindTarget` (angle AND distance) so the warden keeps closing instead of stalling behind-at-range. GroupGrabEnemyData has no explicit grab-range field yet; use the enemy's `AttackRange` (Data.attackRange) or add a `grabRadius` (~1.5 m, matching the `behindPos` offset). NEEDS a new data field OR reuse AttackRange — decide at fix time.
Prior diagnosis (superseded — kept for context):
- The grab "animation" is NOT code-triggered by the grab. `GroupGrabEnemy.StartGrab()` plays only cues (`On_wardenGrab`, soul-consume) — no animator call. `EnemyAnimationController` exposes ONE trigger, `Attack` (`PlayAttack()`), fired by `EnemyAttackController` on a melee/ranged attack. No code sets a "grab" animator parameter anywhere — so the pose is either the generic **Attack** animation or an Animator-Controller state entered by a parameter, which needs the animator asset to confirm.
- Concrete summoned-specific code difference: **summoned minions skip the spawn-reveal brain-pause.** `SummonerEnemy.SummonRoutine` spawns via `SpawnReady(..., playSpawnCue:false)`; `Enemy.SetPoolProvider` early-returns on `!playSpawnCue`, so the minion never runs `SpawnRevealRoutine` (which hides renderers + `PauseBrain()` for `_spawnRevealDelay` ≈1.2s + reveal). A normal/zone-spawned warden is hidden + brain-paused for ~1.2s; a summoned one is visible + brain-active from frame 1. So a summoned warden spawned near/in-front-of the twin runs its brain immediately and can hit the front-attack fallback (`BTActionAttack` in `GOAPActionGrabTwin`) → `PlayAttack()` before maneuvering behind to actually `StartGrab` — reading as "grab animation before grabbing."
- TestLab repro is the GENUINE path, not a debugger artifact: GameDebuggerV2 Spawn mirrors `EnemySpawner.SpawnEnemy` (real pooled lifecycle), and the "Summon" button calls the real `SummonerEnemy.TriggerSummon()` (minion spawned `playSpawnCue:false`). So this WILL occur in real play.
Fix options (pick after confirming the animator state in-editor):
  1. Give summoned minions the same brief brain-pause on spawn (a short `PauseBrain()` window even when `playSpawnCue:false`) so they don't attack/grab-pose the instant they appear — cheapest generic fix, but changes summon feel slightly.
  2. If the pose is the front-attack fallback firing too eagerly, gate the GroupGrab warden's `BTActionAttack` fallback so it can't play the attack animation until it has failed to get behind (or only after a short settle), keeping the grab sequence clean.
Verified by: code path read-through + GameDebuggerV2 path check (2026-08-01). Needs in-editor Animator Controller confirmation of the exact state before applying a fix.
Regressions: 0

---

### BUG-085 — Dying-enemy VFX not cleared (Possess, Radiant Seeker orb hit)
Status: Parked / Watch (2026-08-01) — observed in **TestLab only**; cleanup path verified correct, watch for recurrence in real play
Severity: Minor
System: VFX / FxManager
Symptom: When an enemy dies, lingering VFX from Possess and Seeker-orb-hit remain in the world. USER NOTE 2026-08-01: seen in **TestLab** (debugger spawn/kill), not confirmed in a real level — parked to see if it recurs.
Diagnosis (2026-08-01, no code change): The death-cleanup mechanism is already present and looks correct for POOLED enemies — `EnemyPool.Return` calls `FxManager.StopAllOn(instance.transform)` + `ManpuSlot.Clear()` (`Assets/Scripts/SpawnSystem/EnemyPool.cs:175-179`), so every **Follow-attached** held cue on a dying pooled enemy (Possess_Hit, stun aura, mood loops, Manpu) is swept. Two things that mechanism can NOT catch, either of which fits the symptom:
  1. **Non-pooled death** — `Enemy.HandleDeath` falls back to `Destroy(gameObject, 0.1f)` when `_pool`/`SourcePrefab` are null, and that branch has no `StopAllOn`. A hand-placed / unpooled enemy leaks its follow cues. (Cheap generic fix if it recurs: add `FxManager.Instance?.StopAllOn(transform)` at the top of `HandleDeath` so both paths clean up.)
  2. **World-anchored fire-and-forget cues** — the Radiant Seeker `radorb_hit`/`radorb_hiteffect` (`RadiantSeekerCueBook.asset`) are Particle cues played at world positions (`new CueContext(pos)`, no follow target) with `duration: 0` / `useDefaultDuration: 0`, and `RadiantSeekerOrb.Detonate` keeps no handle. Their lifetime is entirely the particle prefab's — if the PS **loops**, nothing ever reclaims it and it lingers at the detonation spot forever. `StopAllOn(enemy)` cannot reach these (followTarget is null). Fix if it recurs: make those two particle prefabs one-shot (non-looping) OR give the cue elements a finite `duration` so FxManager auto-stops them.
Verified by: cleanup path read-through (2026-08-01). PARKED per user — revisit if the lingering VFX shows in a real level.
Regressions: 0

---

### BUG-086 — TetherBreaker chain marker artifact / wrong placement
Status: Fixed (2026-08-01)
Severity: Major
System: TetherBreaker / VFX
Symptom: The chain target marker shows a big visual artifact. It should be a GROUND marker (where the chain lands / falls if the throw misses), not floating.
Root cause: two parts. (1) The reveal setup — `TargetMark` had no reveal decal/driver (floating particle burst). The user rebuilt the prefab: a `RevealDisc` child with the reveal material + `MaterialRevealDriver` (Property `_val`, From 0 → To 1, Play On Enable), and fixed its rotation. (2) Placement — `ChainProjectile` spawned the marker at the raw `_targetPosition`, which is the target twin's pivot (mid-body), so the whole disc floated above the floor.
Fix (code): `ChainProjectile.Launch` now spawns the marker at `GroundUnder(_targetPosition)` (`Assets/Scripts/Combat/ChainProjectile.cs`) — a ground-projection helper that snaps only Y via `NavMesh.SamplePosition` (primary; can't be occluded by the twin standing on the spot), a player-excluded downward raycast (off-navmesh fallback), then the raw point. The chain still travels to the twin; only the ground telegraph is grounded. Compiles clean.
Verified by: compile clean (0 errors, MCP console). User to retest the on-ground placement in play.
Regressions: 0

---

### BUG-087 — TetherBreaker whole-chain VFX logic broken → remove for playtest
Status: Fixed (2026-08-01)
Severity: Major
System: TetherBreaker / VFX
Symptom: The VFX meant to run along the whole chain is broken (reported earlier — see project_chain_beam). User request: REMOVE it for now so the playtest build is clean; redo properly later.
Root cause: the "whole chain" effect is the `On_TetherChainDrag` cue — a single `ChainDrag.prefab` Follow-attached to the dragged twin. A fixed-size prefab riding the player cannot render along the live chain span (span length changes every frame during the drag), so it clumps on the twin instead of running down the chain.
Fix: commented out (not deleted) the drag-cue play in `ChainProjectile.Connect` (`Assets/Scripts/Combat/ChainProjectile.cs`) with a block documenting why it was pulled and where the redo goes (span-stretched driver like `ChainGlowDriver`/`ChainBeamDriver`, then restore the one-line `_dragHandle = PlayChainCue(...)`). `_cues.drag` id stays wired for the redo; `StopChainCue(ref _dragHandle)` is a safe no-op while the handle is None. The `ChainGlowDriver` span stream (the working per-frame drag glow) is unaffected. Compiles clean.
Verified by: compile clean (0 errors, MCP console). In-game read-back pending user playtest.
Regressions: 0

---

### BUG-088 — drawOnTop see-through overrides player + enemy layers
Status: Fixed (2026-08-01) — pending user play-retest
Severity: Minor
System: Rendering / stencil
Symptom: The draw-on-top (see-through) effect correctly draws on top, but also draws OVER the player + enemy layers so they read wrong on screen. Fix: exclude player + enemy layers from the draw-on-top pass.
Root cause: the `GroundVFXOnTop` RenderObjects feature (`Assets/Settings/PC_Renderer.asset`, Event 400) renders the GroundVFX layer with `depthCompareFunction 8` (Always) and no stencil test, so gameplay-critical ground telegraphs draw over everything — including a twin/enemy standing on them. Player (layer 7) and Enemy (6) carry the same default opaque stencil (2) as the world, so the pass couldn't tell them apart.
Fix (renderer, generic): added a `CharacterMask` RenderObjects feature (Event 300, after CrackLayer, before GroundVFXOnTop) that re-renders the character layers (Enemy 6 + Player 7 + SoulLayer 9 + TrapEnemy 12 = mask bits 4800) with `depthCompareFunction Equal` + no depth/colour write, stamping stencil reference **3** onto only the *visible* character pixels. `GroundVFXOnTop` now tests `stencilCompareFunction NotEqual` ref 3 — so it still draws over grass/props (stencil 2) but is masked out of character pixels. Feature added via MCP (`feature_add`, so Unity maintained `m_RendererFeatureMap`), nested settings hand-authored. Renderer reimports clean (0 errors/warnings). Non-destructive to the see-through (tests `!=1`) and crack (character pixels are depth-rejected there anyway) passes — reasoned through, not just observed. Bonus: the now-grounded chain marker (BUG-086) sits under the twin, so this also stops that telegraph over-drawing the twin's feet.
Verified by: renderer reimport clean (0 errors, MCP console). USER PLAY-RETEST PENDING — check in play: (a) a ground telegraph (enemy AOE cast circle / chain marker) no longer paints over a character standing on it, (b) it STILL draws over grass/props, (c) cracks + camera see-through look unchanged.
Regressions: 0 (watch the three checks above)

---

### BUG-089 — Accord State / Setsuna activation timings out of sync with visuals
Status: FIXED 2026-07-31 (reverted via MCP + Persistent.unity saved + re-read to confirm)
Severity: Major
System: Time / Ability (Accord / Setsuna)
Symptom: User accidentally changed activation/duration timings, so gameplay timings feel off. Restore correct values from an OLD commit around when the cue-book system started.
Git archaeology (2026-07-31): diffed ALL ability-timing serialized fields (`Persistent.unity`) against the cue-book-era commit 923c195. These are serialized MonoBehaviours, so the RUNTIME value is the scene value (code defaults like AccordStateSystem `_chargeTime = 1.25f` and the Setsuna "hold F 0.75s" doc comment are STALE, overridden by the scene). The ONLY drift from cue-book era → HEAD (b25c59d, committed; working tree does not touch these):
- AccordStateSystem `_chargeTime` (hold X): 1.5 → **2.0**
- SetsunaSystem `_chargeHoldTime` (hold F): 0.75 → **2.0**
- AccordSpiritSystem `_chargeHoldTime`: 0.75 → **2.0**
- SoulConvergence / Empower `_chargeHoldTime`: 0.75 → 0.75 (UNTOUCHED)
- `_activeDuration` 7 / `_rewindDuration` 2 / `_damageWindowBlock` 0.9 / `_retryCooldown` 0.25 — all UNCHANGED.
**USER DECISION 2026-07-31: REVERT to the original (cue-book-era) timing.** The 2.0s WAS the accidental change. Set in Persistent.unity:
- AccordStateSystem `_chargeTime`: 2 → **1.5**
- SetsunaSystem `_chargeHoldTime`: 2 → **0.75**
- AccordSpiritSystem `_chargeHoldTime`: 2 → **0.75**
(Serialized fields — edit through the Unity editor via MCP, NOT the code defaults, which are already stale/lower and overridden by the scene. Do NOT touch SoulConvergence/Empower — already 0.75.)
APPLIED 2026-07-31 via MCP `set_property` on GO `SkillTreeManager` (id 69992) in Persistent, then saved + re-read each component to confirm: AccordStateSystem `_chargeTime` = 1.5 ✓, SetsunaSystem `_chargeHoldTime` = 0.75 ✓, AccordSpiritSystem `_chargeHoldTime` = 0.75 ✓. SoulConv/Empower untouched (0.75). Console clean.
OBSERVATION (not changed — outside BUG-089 scope): SetsunaSystem `_rewindDuration` is 2.0 in the scene but the cue-book-era code default was 1.5 — that's the Setsuna rewind-playback length, not a charge/hold. Left as-is; flag to user if the rewind also feels long.
Verified by: MCP re-read of all three serialized fields post-save (2026-07-31). In-editor feel test pending user playtest.
Regressions: 0

---

### BUG-090 — Melee SwordPickup stays disabled after Load Checkpoint (melee ungrabbable again)
Status: In-Progress (fix written 2026-08-05; awaiting in-editor DoD)
Severity: Major
System: Checkpoint / Combat (SoftResetController ↔ SwordPickup)
Symptom: Enter a checkpoint BEFORE picking up the melee → pick up the melee → die → Load Checkpoint. The twin correctly loses the sword (the checkpoint had none), but the melee pickup GameObject stays hidden and can never be grabbed again for the rest of the run.
Root cause: Collecting the sword changes TWO things — `PlayerAttackController.SetHasWeapon(true)` (the twin's flag) AND `SwordPickup` self-`gameObject.SetActive(false)` (SwordPickup.cs:47). A soft reset does NOT reload the scene, and `SoftResetController.RestoreSwords` restored only the twin flag (`SetHasWeapon(data.*HasSword)`), never re-enabling the pickup GO. The existing `OnSoftReset` event can't cover it — a collected pickup is inactive, so it can't self-re-enable; an external actor that can find inactive objects must do it. Only Load Checkpoint is affected; Restart (full reload) recreates the pickup fresh.
Fix: In `SoftResetController.RestoreSwords`, after restoring the twin flags, sweep `FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None)` and set each `pickup.gameObject.SetActive(!collectedAtCheckpoint)` where `collectedAtCheckpoint = IsForLeftTwin ? data.leftHasSword : data.rightHasSword`. Availability mirrors whether the sword was collected at save time (checkpoint-before-pickup ⇒ pickup re-enabled; checkpoint-after-pickup ⇒ pickup stays hidden). Scene-scoped non-singleton sweep (R4). Bonus: also closes a latent double-grab — if the checkpoint area streams in FRESH (active pickup) while the twin already has the sword, the pickup is now force-hidden instead of grabbable.
Scenario B check (pickup THEN checkpoint): verified correct BEFORE the fix and unchanged BY it — `RestoreSwords` calls `SetHasWeapon(true)` which re-activates the twin's sword GO, and the pickup correctly stays hidden.
Verified by: — (in-editor DoD: (A) collect-before-checkpoint → die → Load Checkpoint → melee grabbable again; (B) collect-after-checkpoint → die → Load Checkpoint → sword present + pickup hidden)
Regressions: 0

---

### BUG-091 — Shared-health bar display bug (survival-channel rework); display rolled back for playtest
Status: In-Progress (mitigated by rollback 2026-08-05; correct fix deferred to post-playtest)
Severity: Major
System: UI / Health (SharedHealthPresenter, BondWeaknessPresenter ↔ SharedHealthPool / PlayerHealthComponent)
Symptom: Confirmed by user — the shared-health display misbehaves. Exact symptom not yet characterised. Git bisection attributes 100% of the two-channel rework to the UI-shader era (after 26a192e / 2026-07-17); the pre-UI-shader health system (== pre-multiscene behaviour) is confirmed-good by the user.
User repro (2026-08-10, two rescue-completion paths compared): (A) let an enemy damage a twin, then rescue by KILLING the trigger enemy (HandleKillerDied → ReleasePlayer) — health regened AND the bar filled visually ✓. (B) let an enemy kill the twin (trigger rescue), then rescue via F-mash (RescueState.Success → ReleasePlayer) while also killing the enemy — health "did not regen" AND Unity hard-crashed on that run. Hypothesis: (A) is done with twins close (distMod≈1, bar shows regen) while (B) uses the soul/Gate path which leaves the twins far apart (distMod small → masked bar hides real regen); the F-mash/Gate path also fires the VFX most likely to hit the documented D3D12 hang. Code diff vs 26a192e confirms regen/heal logic is byte-identical and PauseRegen is never called — so real regen should run. Diagnostics added: [HealthRegen] real-HP-vs-displayHP+distMod in PlayerHealthComponent.Update; [DeathProxy] ReleasePlayer HP before/after in PlayerDeathRescueProxy.
Scenario A CONFIRMED LIVE (2026-08-10 re-run): killing-method rescue → real HP regened AND the bar filled visually (twins were close, so distMod≈1 and the masked bar tracked real HP). A subsequent freshly-spawned enemy hit the rescued twin mid-regen (expected regen-timer reset via OnCombatDamageTaken — NOT a bug, user flagged to ignore). Console lines could not be captured this run: the Editor was unfocused → play-mode ticks frozen → read_console returns 0 (Application.runInBackground / focus the Game view to capture). Scenario B RESOLVED (2026-08-10 fresh run, full chain logs, 0 errors, no crash): F-mash death-rescue works end-to-end — TTK PAUSED/RESUMED during mash, ReleasePlayer heal=35 (HP 0→35), ResetToAlive, then regen 35→43.4→…→100 (full recovery). Killing-method also re-confirmed. distMod=1.00 on EVERY line in both scenarios → displayHP==HP → the bar tracks real HP; the masked-bar case did NOT trigger because the twins never separated during these rescues. Every non-recovery correlates with a logged "took N Combat dmg → regen delay RESET" (incidental enemy re-hit, user said to ignore). CONCLUSION: regen is NOT broken; the earlier "F-mash → no regen" was the crash freeze and/or the incidental re-attack, consistent with the code diff (regen byte-identical to 26a192e). Remaining true bug = the masked display (BUG-091) itself, only visible when twins are far apart (distMod<1) — not yet reproduced live; still fixed by restoring the survival-fill bar. Debug logs still in (untick _debugRegen/_debugRescue to silence).
HEISENBUG LEAD (2026-08-10, user observation, repeated): the "no regen" symptom appears with logs OFF and DISAPPEARS with the diagnostic logs IN — user has seen this before. Signature = timing/state-dependent bug masked by instrumentation. Prime suspect is NOT the logs themselves but the RECOMPILE + DOMAIN RELOAD that every script edit triggers, which resets stale carried-over state. Strong hypothesis: Editor "Enter Play Mode Options → Reload Domain" is DISABLED (fast-iteration), so stale state (statics / ServiceLocator / a carried field) survives BETWEEN play sessions and breaks regen; a script edit forces a true reset → "logs fix it". Consequence: NOT actually fixed; may differ in a build (builds always reload). Zero-code test to confirm: run twice back-to-back with NO recompile between (logs already compiled in) — if run #2 breaks while logs are present, it's stale-state, not the logs. Also check whether it repros in a build. User decision: LEAVE logs in for now as a stopgap for the playtest.
Root cause (era attribution): between 26a192e and now the DISPLAY was re-architected — `PlayerHealthComponent` gained `SurvivalHealth01`/`BondWeakness01`; `SharedHealthPool` gained `CombinedSurvival01`/`OnSurvivalChanged`; `SharedHealthPresenter` fill moved `CombinedHealth` → `CombinedSurvival01`; `BondWeaknessPresenter` was added to grey each half by distance. The defect lives in that surface. The multiscene era barely touched health (only the `SharedHealthPool` singleton + cosmetic BOM/comment damage).
Mitigation (2026-08-05 — hypothesis test + playtest safety): rolled the DISPLAY path back to 26a192e. `SharedHealthPresenter` fills from the masked `CombinedHealth` again (`OnCombinedHealthChanged`), and `BondWeaknessPresenter` is disabled via a serialized `_rollbackDisabled = true` kill-switch (else distance double-counts — bar shrinks AND greys). The survival/bond code (`SurvivalHealth01`, `CombinedSurvival01`, `OnSurvivalChanged`, `BondWeakness01`, `OnBondWeaknessChanged`, `UIBarHealthView.SetBondWeakness`) is LEFT INTACT but unused, so the proper two-channel fix is a straightforward re-enable later. HP / damage / regen / over-max drain / game-over / singleton / authored bar art all UNCHANGED. If the bug disappears under this rollback, the hypothesis (defect is in the survival-channel rework) is confirmed.
Fix: deferred — proper two-channel fix after the playtest characterises the exact symptom.
Verified by: — (playtest: bar behaves like the known-good 26a192e build; no shrink-AND-grey double-count)
Regressions: 0

---

### BUG-092 — Enemies intermittently don't spawn for long periods (under investigation)
Status: Watch (diagnostic logging added 2026-08-05; root cause not yet identified)
Severity: Major
System: Spawn (EnemySpawner, SpawnZone)
Symptom: User reports enemies sometimes stop spawning in a zone for a long time.
Investigation: six silent-return paths can stall spawning with zero console output — (1) `ActivateZone` bails if the SpawnZone has no `AreaZoneConfig`; `TrySpawnOnSide` bails on (2) null side-config, (3) `active >= maxTotalActive`, (4) `active >= RespawnThreshold`, or (5) null `GetRandomEntry` (empty/exhausted type table); (6) `SpawnEnemy` bails if `GetNextSpawnPoint` returns `Vector3.zero` — no spawn points assigned OR an intermittent `NavMesh.SamplePosition` miss (the strongest candidate for the "sometimes" nature).
Instrumentation added (greppable): `[SpawnZone]` player ENTER/EXIT (zone name + areaConfig-null flag); `[EnemySpawner]` zone ACTIVATED / areaConfig-null WARN / spawn-position-fail WARN / SPAWNED success; `[SpawnDebug]` per-interval skip reason (behind `EnemySpawner._debugSpawns`).
Fix: pending — reproduce, read the logs to see which return path fires during a stall, then fix that path.
Verified by: —
Regressions: 0

---

### BUG-093 — Failed rescue doesn't end the game; leaves a movable dead "zombie" twin
Status: Fixed (2026-08-10) — needs in-game verify
Severity: Major
System: Rescue / Game-over / AI world-state
Discovered: 2026-08-10 (live play — Kai rescued, then killed by a Severed enemy; rescue failed but game continued with Kai movable, not healing, not switchable)
Symptom: When a rescue fails (TTK expires / soul out of range), the game does not show game-over. The downed twin is left `_isDead`/HP 0 (no regen — `Update()` early-returns) but movable (`TTKCountdown` unfreezes on fail) and unselectable — a walking corpse.
Root cause (git archaeology, pre-multiscene 5fa951d vs now): `GameOverController.HandleRescueState` triggers game-over on `OnRescueStateChanged == RescueState.Failed`. But `RescueEventController.TransitionTo` gained an `if (_state == next)` guard AFTER 5fa951d — because `EnterState(Failed)`→`CleanupRescueEvent()` sets `_state = Idle` and fires `OnRescueStateChanged(Idle)` first, the guard sees `Idle != Failed` and never fires the terminal `Failed`. The guard is CORRECT and must stay: without it, `PoTWorldStateWriter.OnRescueStateChanged` lands on a non-Idle value and latches `IsRescueActive` true forever → every enemy freezes (the exact bug the guard fixed; `PotWorldStateWriter` existed pre-multiscene but `IsRescueActive` became attack-gating in the AI-ecology layer). So the guard silently turned `GameOverController`'s Failed branch into dead code — a regression from the world-state fix, not the original design.
Fix: dedicated `RescueEventController.OnRescueFailed` event fired inside `EnterState(Failed)` BEFORE `CleanupRescueEvent`; `GameOverController` subscribes to it (`OnRescueFailed += TriggerGameOver`) instead of the swallowed state value; removed the dead `HandleRescueState`. No guard revert → `IsRescueActive`/enemy-freeze untouched; `WorldSpaceRescueUI`/`RescueButtonUI` (explicit Success/Failed hide branches) and Siphon (`OnRescueResolved`) unaffected. Fixing game-over moots the zombie state (game freezes → Restart / Load Checkpoint discards it).
Related: enemies-attack-only-after-soul-home requirement is already satisfied by BUG-082 (`TeleportAbility.IsSoulDeployed` → `RescueEventController.IsAnySoulDeployed` → `PoTWorldStateWriter.IsRescueActive` → `GOAPGoalAttackTwin`/`GrabTwin`/`DefendSpawn` DoNotRun). No change needed.
Not fixed here (separate): `_ttkPaused` is never reset in `PlayerDeathRescueProxy.Activate()` — a rescue resolved via the killer-died free-rescue while in Mashing leaves `_ttkPaused` stuck true (killer-died path skips `ExitState(Mashing)`→`ResumeTTK`), freezing the NEXT rescue's TTK. One-line fix (`_ttkPaused = false` in Activate) pending user go-ahead. Original bug, not a multiscene regression (proxy TTK code byte-identical at 5fa951d).
Files: RescueEventController.cs (OnRescueFailed event + fire in EnterState(Failed)), GameOverController.cs (subscribe OnRescueFailed, drop HandleRescueState). validate_script: 0 errors both.
Verified by: — (needs live: fail a rescue → game-over panel shows, timeScale 0, Restart/Load Checkpoint works)
Regressions: 0 (watch: enemies still un-freeze after a normal successful rescue + soul return)

---

## Couch Co-op Conversion — watch / test ledger (branch `couch-multiplayer`, opened 2026-08-16)

Pre-emptive watch entries for the single→couch-co-op conversion (plan:
`couch_multiplayer_conversion_analysis.md`, staged M0–M7). Most are **not defects yet** — they are
"will break unless handled" risk surfaces to verify as each milestone lands. Flip to Fixed/Verified when
the owning milestone's DoD runs. M0 (input-ownership seam) is additive/non-breaking.

**Milestone status:** M0 input seam ✅ **COMPLETE** — M0.1 seam (`6aa2c1b`) · M0.2a shared-UI routing
(`3745fbc`) · M0.2b Persistent wiring (`f23ca68`); **sandbox-verified 7/8 (2026-08-16)** — ESC/Tab/B/F-mash/H/
input-glyph/world-pickups all behaviour-identical, intro N/A (no intro scene active). ⏳ one `Bootstrap`-Play run
still owed for the §10 two-entry-path sign-off · M1 ownership+dispatch ☐ · M2 char-select ☐ ·
M3 rescue+joint+Empower ☐ · M4 tutorial ☐ · M5 HUD ☐ · M6 F13 cam ☐ · M7 sync-puzzles ☐

### BUG-094 — [Watch] Tutorial breaks when TwinSelector dies (the #1 breakage)
Status: Watch (M1/M4)
Severity: Blocker
System: Tutorial / Input gate / Selection
Risk: `TutorialDirector`/`TutorialStepContext`/`TutorialTimelineStepSO`/`TutorialStepBase`/`TutorialUnlockAllStepSO`
all Lock/Unlock selection; the tutorial teaches the (deleted) switch mechanic; `TutorialInputGate` registers
into the ONE reader; `TutorialTimelineDirector` rebinds by type/singleton — ambiguous with two owned twins.
Decision: **SHARED progression (D6)** — both players advance together.
Test (M4 DoD): full Bootstrap tutorial run, 2 players, progressive unlock advances jointly; direct-area play
(no gate) fine; four entry paths.
Verified by: —

### BUG-095 — [Watch] Selection-consumer sweep completeness
Status: Watch (M1)
Severity: Major
System: Abilities / Rescue / Streaming / UI
Risk: 14 consumers of `SelectedTransform`/`ForceSelect`/`Lock` (plan §1 grep list). Miss one → null
`SelectedTransform` or a stuck selection-lock after `TwinSelector` is removed.
Test: grep clean for the selection API after M1; no null-ref in play on two entry paths.
Verified by: —

### BUG-096 — [Watch] Input reclassification (per-player vs shared-UI)
Status: Watch (M1) — shared-UI half routed (M0.2a); per-player gameplay half pending M1
Severity: Major
System: Input
Risk: routing a shared-UI consumer to only P1 (P2 can't pause) or a gameplay consumer to shared (both twins
react to one player). 26 consumers split via `PlayerInputRouter.For(twin)` vs `.SharedInput`.
Progress (M0.2a): the 9 shared-UI consumers (Pause, SkillTree, Overview, Intro, QTE Manager+Controller,
ControlHints, InputPrompt, WorldSpacePickup) now read `PlayerInputRouter.SharedInput` — still behaviour-neutral
(SharedInput == TwinInputReader.Instance in M0). The per-player GAMEPLAY split (`For(twin)`) is M1.
Test: P2 can pause / open skill tree; each player's attack/ability moves only their own twin.
Verified by: sandbox 2026-08-16 — shared-UI routing is behaviour-neutral PASS (pause / skill tree / overview /
hints / QTE mash / input glyph "Press F" / world pickups + checkpoint save all identical to pre-M0). FULL
Verified still deferred to M1 (the real per-player split) + a Bootstrap-path run.

### BUG-097 — [Watch] Empower single-driver redesign
Status: Watch (M3)
Severity: Major
System: Abilities / EmpowerSystem
Risk: `EmpowerSystem` force-selects + anchors one twin + Shift-dashes — whole model is single-driver.
Decision D1: **caster's twin anchors, PARTNER gets the buff**; rebind dash off the freed Shift.
Test: Empower buffs the partner twin; dash works on its new key; no selection calls throw.
Verified by: —

### BUG-098 — [Watch] GetSwitchDown orphan (Shift freed)
Status: Watch (M1/M3)
Severity: Minor
System: Input
Risk: Shift (switch-twin) is deleted but still read by `EmpowerSystem` dash + `TutorialInputGate` passthrough.
Test: no dangling `GetSwitchDown` consumer; Shift rebound per-player where still needed.
Verified by: —

### BUG-099 — [Watch] SceneFlowManager active-location by selected twin
Status: Watch (M1)
Severity: Minor
System: Streaming / Music
Risk: `SceneFlowManager.ResolveActiveLocation` picks the active location by `SelectedTransform`
(music/active scene). No selection in couch → needs the D4 rule (prefer P1's twin, else first loaded).
Test: music/active-scene resolves sanely with the two twins in different loaded areas.
Verified by: —

### BUG-100 — [Watch] SelectedPlayerUI dead "selected" state
Status: Watch (M1/M5)
Severity: Minor
System: UI
Risk: `SelectedPlayerUI` swaps a material to show the "selected" twin — meaningless with no selection.
Decision D3: repurpose as P1/P2 identity, or delete.
Test: no material-swap referencing a dead selection; per-player identity reads correctly.
Verified by: —
