# Changelog

All notable changes to **Planet of Twins** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project aims to follow [Semantic Versioning](https://semver.org/) once builds are tagged.

This log starts fresh on **2026-06-06** and tracks changes from this point forward. For
how each system works, see [game.md](game.md); for working in the repo, see [CLAUDE.md](CLAUDE.md).

---

## [Unreleased]

### Added — Couch co-op M0: input-ownership seam (2026-08-16, branch `couch-multiplayer`)
- First slice of the couch multiplayer conversion (plan: `couch_multiplayer_conversion_analysis.md`,
  staged M0–M7). New **input-ownership seam** so the eventual per-player split is a change in ONE place
  rather than across the ~26 `IInputProvider` consumers:
  [IPlayerInputRouter.cs](Assets/Scripts/Players/Multiplayer/IPlayerInputRouter.cs) +
  [PlayerInputRouter.cs](Assets/Scripts/Players/Multiplayer/PlayerInputRouter.cs). `ProviderFor(Player twin)`
  returns the twin's input provider; `Shared` returns the shared-UI provider. **M0 stage = behaviour-neutral**:
  both return the single `TwinInputReader`, so the game plays identically on one device. Persistent R3 singleton
  (duplicate-destroy Awake guard, null on OnDestroy, no DDOL); lazy resolve (Awake-order safe, R8) with a
  `TwinInputReader.Instance` fallback so it works even before the Persistent slot is wired.
- Additive only — **no existing consumer modified yet** (that's M1). Verified: forced script compile, domain
  reload completed, **0 errors**.

### Changed — PoT/Coexistence wind sway: angular model + per-object WindAnchor authoring (2026-08-16)
- Wind sway is now an **angular** model instead of flat metres-of-displacement. `PoTApplyWind`
  ([CoexistenceCommon.hlsl](Assets/Art/Shaders/CoexistenceCommon.hlsl)) rotates the free side about the
  attachment plane by up to `_WindMaxAngle` (± half-angle, clamped), with natural vertical foreshortening
  (`leverArm*(cos θ−1)`) so it reads as a swing, not a shear. `_WindAmount` is kept as a small **additive**
  positional flutter layered on top (the "natural feel"); `_WindResponse` is repurposed to a base→tip bend
  **stiffness**. Applied identically across all four passes (ForwardLit/ShadowCaster/DepthOnly/DepthNormals)
  via the shared include, so shadows + the depth-prime prepass stay aligned. New property `_WindMaxAngle`
  (`Range(0,180)`, default 8); `_WindAmount` default lowered 0.12→0.06. **Migration:** existing wind
  materials keep their `_WindAmount` (now additive) and pick up the 8° default swing — re-tune or drive them
  via WindAnchor. Deliberately **no collision** — the clamp is the optimisation (clearance baked at author
  time), not a cloth/joint sim.
- New `WindAnchor` component ([WindAnchor.cs](Assets/Scripts/Environment/WindAnchor.cs)) makes the anchor +
  swing envelope **specific to one placed object** by overriding the per-material wind fields through a
  `MaterialPropertyBlock` (no shader change; no component = material defaults, fail-safe). Enable/disable
  toggle (off = this object goes still while neighbours sway), Standing/Hanging mode, mesh-local pivot Y, a
  swing-arc **preset** (Free 360 / Half 180 / Narrow 60 / Custom — the FULL aperture; the shader gets the ±
  half-angle), additive metres, and stiffness. Cost note in the file: MPB overrides of a CBUFFER field
  disable SRP batching for that renderer — fine for props (lanterns/banners/signs), use vertex-baked weights
  for dense foliage.
- New scene editor ([WindAnchorEditor.cs](Assets/Scripts/Environment/Editor/WindAnchorEditor.cs)): a
  draggable handle on the attachment plane + a swing-**cone** gizmo (sphere at 360) showing the exact
  aperture the object can rotate through, so a designer tunes the angle to clear neighbours by eye. (Live
  sway needs the Persistent WindDriver running — the gizmo is the edit-mode authoring feedback.)
- Verified: forced reimport + compile, domain reload completed, **0 errors / 0 warnings** (console clean,
  no Coexistence-specific messages).

### Fixed — Failed rescue now ends the game again (BUG-093) (2026-08-10)
- A failed rescue no longer leaves a movable-but-dead "zombie" twin with no game-over. Root cause
  (git-confirmed regression since pre-multiscene 5fa951d): `GameOverController` triggered game-over off
  `OnRescueStateChanged == Failed`, but `RescueEventController.TransitionTo` was later given an
  `if (_state == next)` guard so the terminal `Failed` value is **never delivered** (EnterState(Failed)→
  `CleanupRescueEvent` already flips `_state` to `Idle`). That guard is correct and stays — it's what keeps
  `PoTWorldStateWriter.IsRescueActive` from latching true (which would freeze every enemy forever). But it
  silently made `GameOverController`'s Failed check dead code.
- Fix: added a dedicated `RescueEventController.OnRescueFailed` event, fired inside `EnterState(Failed)`
  **before** `CleanupRescueEvent`; `GameOverController` now subscribes to it instead of the swallowed
  state value. Surgical — no guard revert, so `IsRescueActive`/enemy-freeze semantics are untouched; the
  two rescue UIs and Siphon (`OnRescueResolved`) are unaffected. The "zombie twin" (no heal / can't switch)
  is mooted because game-over freezes and hands off to Restart / Load Checkpoint.
- Verified: both files validate_script 0 errors. Enemy-attack-after-soul-home requirement was already met
  by the existing BUG-082 chain (`IsSoulDeployed` → `IsRescueActive` → `GOAPGoalAttackTwin` DoNotRun) — no
  change needed there.
- Files: [RescueEventController.cs](Assets/Scripts/Players/RescueEventController.cs) ·
  [GameOverController.cs](Assets/Scripts/UI/GameSystems/GameOverController.cs) · BUGS.md BUG-093.

### Added — Full rescue + regen chain diagnostics (BUG-091 investigation) (2026-08-10)
- Instrumented the whole rescue→heal→regen chain so a single console capture explains a run end-to-end.
  All new logs are compile-verified (validate_script: 0 errors) and behind serialized toggles.
- `[HealthRegen]` (PlayerHealthComponent, behind `_debugRegen`, default on): regen STARTED / throttled
  healing tick every 0.5s / regen STOPPED — each printing REAL `_currentCombatHealth` next to the
  distance-masked `DisplayHealth` and `distMod`; plus damage-taken (with COMBAT-resets-regen-timer note),
  DIED, Heal(+n), ResetToAlive, and RestoreToFull. If HP climbs while displayHP stays low → bar is masking
  real regen by distance (the BUG-091 rollback), not a regen failure.
- `[DeathProxy]` (PlayerDeathRescueProxy): ReleasePlayer HP before/after + isDead; TTK PAUSED/RESUMED with
  remaining time; TTK EXPIRED. (Activate/HandleKillerDied already logged.)
- `[Rescue]` (RescueEventController, behind `_debugRescue`, default on): every state transition
  `A → B` with target/player/mash%; rescue BEGAN (grab); soul-arrival with distance vs trigger radius;
  per-mash-press progress; SUCCESS (heal amount), FAILED, SoulDied.
- Diagnosis confirmed in code first: `HealthRegenHandler` byte-identical to last good commit (26a192e);
  `PauseRegen` never called; release heals + `ResetToAlive`. So real regen should run — logs prove it.
- Scenario A confirmed live (killing-method rescue → regen + bar fill worked, twins close so distMod≈1).
- Diagnostic only — untick `_debugRegen` / `_debugRescue` (or strip) once the cause is confirmed.
- Files: [PlayerHealthComponent.cs](Assets/Scripts/Heath/PlayerHealthComponent.cs) ·
  [PlayerDeathRescueProxy.cs](Assets/Scripts/Players/PlayerDeathRescueProxy.cs) ·
  [RescueEventController.cs](Assets/Scripts/Players/RescueEventController.cs) · BUGS.md BUG-091.

### Added — Spawn diagnostics: player-enter + spawn/skip logging (BUG-092 investigation) (2026-08-05)
- To chase the intermittent "enemies don't spawn for a long time" report, added greppable logging across
  the spawn flow: `[SpawnZone]` player ENTER/EXIT (zone name + whether `areaConfig` is null);
  `[EnemySpawner]` zone ACTIVATED / no-AreaZoneConfig WARN / no-spawn-position WARN / SPAWNED success; and
  `[SpawnDebug]` per-interval skip reasons (at-cap / null-config / empty-entry) behind a serialized
  `EnemySpawner._debugSpawns` toggle (default on).
- Also fixed a misleading `SpawnZone` log that fired for **every** collider before the player-layer check.
- Diagnostic only — toggle `_debugSpawns` off (or strip these) once the cause is found.
- Files: [SpawnZone.cs](Assets/Scripts/SpawnSystem/SpawnZone.cs) ·
  [EnemySpawnner.cs](Assets/Scripts/SpawnSystem/EnemySpawnner.cs) · BUGS.md BUG-092.

### Changed — Shared-health bar display rolled back to the pre-UI-shader (26a192e) behaviour (BUG-091) (2026-08-05)
- **Why:** the user confirmed a shared-health display bug. Git bisection shows the health *math* is
  unchanged since before multiscene; **all** of the two-channel display rework (survival fill vs
  bond-weakness colour) landed in the UI-shader era (after `26a192e`, 2026-07-17), and the
  pre-UI-shader behaviour is confirmed-good. This is a hypothesis test + a safe bar for the playtest;
  the correct two-channel fix comes after the playtest pins the exact symptom.
- **What:** `SharedHealthPresenter` FILL reverted from `CombinedSurvival01` (`OnSurvivalChanged`) back
  to the masked `CombinedHealth / MaxCombinedHealth` (`OnCombinedHealthChanged`) — so the bar shrinks
  with distance again, as it did at `26a192e`. `BondWeaknessPresenter` disabled via a serialized
  `_rollbackDisabled = true` kill-switch so distance isn't double-counted (bar shrinking **and**
  greying).
- **Left intact (unused, ready to re-enable):** `SurvivalHealth01`, `CombinedSurvival01`,
  `OnSurvivalChanged`, `BondWeakness01`, `OnBondWeaknessChanged`, `UIBarHealthView.SetBondWeakness`.
  **Unchanged:** HP/damage/regen/over-max drain, game-over (`OnSharedPoolEmpty`), the `SharedHealthPool`
  singleton, and the authored `UIBarHealthView` bar art.
- Files: [SharedHealthPresenter.cs](Assets/Scripts/UI/Health/SharedHealthPresenter.cs) ·
  [BondWeaknessPresenter.cs](Assets/Scripts/UI/Health/BondWeaknessPresenter.cs) · BUGS.md BUG-091.

### Fixed — Melee pickup stayed disabled after Load Checkpoint (BUG-090) (2026-08-05)
- **Symptom:** enter a checkpoint *before* grabbing the melee → grab it → die → Load Checkpoint. The
  twin correctly lost the sword (the checkpoint had none), but the melee **pickup never reappeared** —
  ungrabbable for the rest of the run.
- **Cause:** collecting the sword does two things — `PlayerAttackController.SetHasWeapon(true)` and
  `SwordPickup` self-`SetActive(false)` ([SwordPickup.cs:47](Assets/Scripts/Combat/SwordPickup.cs#L47)).
  A soft reset never reloads the scene, and `SoftResetController.RestoreSwords` restored only the twin
  flag, never re-enabling the pickup GO. (The `OnSoftReset` event can't help — a collected pickup is
  inactive and can't self-re-enable.) Only Load Checkpoint was affected; Restart recreates it fresh.
- **Fix:** `SoftResetController.RestoreSwords` now sweeps `FindObjectsByType<SwordPickup>(Include
  inactive)` and sets each pickup's active state to `!collectedAtCheckpoint`
  (`IsForLeftTwin ? data.leftHasSword : data.rightHasSword`) — availability mirrors whether the sword
  was collected at save time. Also closes a latent double-grab (fresh-streamed active pickup while the
  twin already holds the sword now force-hidden). Scenario "pickup THEN checkpoint" verified unchanged
  (sword restored via `SetHasWeapon(true)`, pickup stays hidden).
- Files: [SoftResetController.cs](Assets/Scripts/SceneLaoder/SoftResetController.cs#L178) · BUGS.md BUG-090.

### Removed — Unused Unity sample folders that broke the player build (2026-08-02)
- Player builds aborted with "Error building Player because scripts had compiler errors." Root cause:
  `Assets/Samples/Scriptable Render Pipeline Core/17.3.0/Common/Scripts/InstallPackage.cs` — a Unity
  **sample** `MonoBehaviour` that `using UnityEditor.PackageManager` from a **runtime** (non-`Editor/`)
  script. `UnityEditor` exists only in the editor, so it compiled in-editor but threw `CS0234` in a
  player build → player scripts failed → build stopped. The two Addressables errors in the console were
  downstream cascade of the failed script compile.
- Fix (user-approved): deleted the two unused sample folders (+ their `.meta`):
  - `Assets/Samples/Scriptable Render Pipeline Core/` (contained the culprit `InstallPackage.cs` + sibling
    sample scripts) — **stays deleted**.
  - `Assets/Samples/Shader Graph/` — **partially reverted, see below**.
- Unity recompiled + domain-reloaded → **0 console errors**. Build unblocked past this point.
- CORRECTION (2026-08-02, same session): the `Shader Graph` sample was **not** fully unused — `L0_CityWater`
  applies **`WaterLake.mat`** (from `…/Production Ready Shaders/Environment/Water/`) to its water planes, and
  that shader depends on shared subgraphs in `Common/Subgraphs/` (GerstnerWave/MainLight/Hash…). Deleting the
  sample left those planes with a Missing material. **Restored the whole `Shader Graph` sample from git HEAD**
  (original GUIDs → scene refs auto-resolve) and **re-removed only its 2 scripts** (`Common/Scripts/
  DisableGizmos.cs`, `Rotate.cs`) so the build stays fixed. Reimport clean (0 errors). Follow-up (CHECKLIST #33
  keeper-extraction): extract `WaterLake` + its subgraph closure into `Assets/Art/`, then re-delete the sample.

### Changed — Settings button disabled for the playtest (2026-08-02)
- The pause **Settings** panel's individual controls are still stubs: every `TMP_Dropdown`
  (Language/Resolution/WindowMode + the 7 graphics dropdowns) is a bare component with **no
  Template child, no caption Label, no item Toggle**, so opening the panel throws
  `TMP_Dropdown.SetupTemplate` ("dropdown template is not assigned") and the controls render as
  blank bars. The toggles/sliders are likely missing their checkmark/fill/handle children too.
  Backend (controllers, logic, wiring, panel layout) is complete; only the **control visuals** are
  unbuilt. Decision: build them **after** the playtest.
- For the playtest, `SettingsButton` (`PauseMenuCanvas/PauseRoot/MenuCard/SettingsButton`) is made
  non-clickable by turning off `raycastTarget` on its `Image` **and** its child `Text` (TMP), so no
  pointer can reach it and the broken panel can't open. The `Button` component + its onClick wiring
  are left intact — re-enable is a two-flag flip. Resume/Exit/Restore-Keybinds are unaffected.
  (`Assets/Scenes/Persistent.unity`.)

### Fixed — Pause Settings panel showed empty (broken layout, not missing UI) (2026-08-02)
- The pause **Settings** panel rendered blank even though every control and both controllers were
  already built and wired (`SettingsMenuController` 8 slots + `GraphicsSettingsController` 11 slots,
  all pointing at real objects; ScrollRect content/viewport correctly bound). Root cause was
  layout-only, inside `PauseMenuCanvas/PauseRoot/SettingsPanel/SettingsScroll/Viewport/Content`:
  - `Content` had a `VerticalLayoutGroup` but **no `ContentSizeFitter`** and was pinned to a fixed
    320px height. The 18 control rows need ~800px, so they overflowed the Viewport `Mask` (clipped to
    nothing) and the `ScrollRect` saw content==viewport (never scrolled) → panel looked empty.
  - Several rows carried a stray `localScale ≈ 1.719×` from hand-authoring, blowing them up and
    disrupting the stack (VLG `childScaleHeight=false` doesn't account for scale → overlap).
- Fix (layout-only, **zero wiring changes**, `Assets/Scenes/Persistent.unity`):
  - Added a `ContentSizeFitter` to `Content` (`verticalFit=PreferredSize`, `horizontalFit=Unconstrained`).
    Content now sizes to its 800px of rows; ScrollRect scrolls if the list ever exceeds the 962px viewport.
  - Reset `localScale` to (1,1,1) on all 18 `Row_*` objects.
  - Verified numerically: VLG now stacks all 18 rows uniformly 44px apart (36 height + 8 spacing),
    full-width (1864), scale 1, layout-driven; 800px content fits the 962px viewport so all controls
    display at once. `CanvasScaler` was already correct (Scale With Screen Size, 1920×1080, match 0.5) —
    the panel already scales to any resolution; no resizer script needed.
  - No controls recreated, no serialized slots re-pointed. Overlay UI only renders on the real path
    (Play → ESC → Settings); edit-mode Game/Scene view will not repaint a freshly-activated pause canvas.

### Fixed — Pause menu buttons unclickable (canvas below the tutorial/skill-tree layers) (2026-08-01)
- `PauseMenuCanvas` (Persistent, Screen Space Overlay) sat at `m_SortingOrder: 0`, while
  `TutorialHUDCanvas` and `SkillTreeCanvas` are at `20`. During an active tutorial the higher
  tutorial canvas renders above the pause menu and its input layer intercepts every click on
  Resume/Settings/Exit — the buttons show through (transparent centre) but never receive the
  pointer. A pause menu must be the topmost UI layer.
- Fix: raised `PauseMenuCanvas.m_SortingOrder` 0 → 100 (`Assets/Scenes/Persistent.unity`), so the
  pause menu and its child SettingsPanel win both render and raycast order. Edited on disk with
  Persistent unloaded (only Bootstrap was open) to avoid a scene-conflict. No script/scene-graph
  changes — one serialized field.

### Fixed — Shared health bar filled from distance-masked health, not real HP (BUG-081 part A) (2026-08-01)
- The shared-emblem bar FILL was driven by `SharedHealthPool.CombinedHealth` = `Kai.DisplayHealth +
  Lyra.DisplayHealth`, and `DisplayHealth = realHP × distanceModifier − overMaxDrain×max`. So merely
  walking the twins apart dropped the bar (modifier<1) even though real HP was unchanged, and passive
  regen was invisible while stretched — read by players as "we're dying" / "regen stopped."
- Fix (script-only, no scene wiring — both refs already existed): the bar FILL now reads a
  distance-independent survival channel; the recoverable bond-stretch shows only as the per-half
  DRAIN/grey (already owned by `BondWeaknessPresenter`).
  - `SharedHealthPool` (`Assets/Scripts/Heath/SharedHealthPool.cs`): new `CombinedSurvival01` (mean of
    the two twins' `SurvivalHealth01`, 0..1) + `OnSurvivalChanged`; both twins' change events route via
    `HandleTwinChanged()` which still runs `RecalculateCombined()` (masked value + game-over path
    unchanged) then pulses `OnSurvivalChanged`.
  - `SharedHealthPresenter` (`Assets/Scripts/UI/Health/SharedHealthPresenter.cs`): FILL now subscribes
    to `OnSurvivalChanged` → `SetFill(CombinedSurvival01)` (was `OnCombinedHealthChanged` → masked
    `CombinedHealth/Max`). Game-over (`OnSharedPoolEmpty`) + emergency handlers untouched.
- Masked `CombinedHealth` still drives game-over via `OnSharedPoolEmpty` (over-max-distance-drain kill
  preserved). Part (B) of BUG-081 (arm regen on ANY damage, not combat-only) is NOT bundled — separate
  change, awaiting go-ahead.

### Fixed — Enemies unfroze at rescue-success instead of when the soul reached home (BUG-082) (2026-08-01)
- The rescue enemy freeze is the **soft** path: the shared blackboard flag `PoT.Game.IsRescueActive`
  (written by `PoTWorldStateWriter` from the rescue state), which `GOAPGoalAttackTwin` /
  `GOAPGoalGrabTwin` / `GOAPGoalDefendSpawn` read to `DoNotRun`. It went **true** when the soul reached
  the grabbed twin (state → `Triggered`) and **false** the instant the mash succeeded (`Success` →
  `CleanupRescueEvent` → state `Idle`). But the soul only travels **back to the caster** later, inside
  `TeleportAbility.End() → ReturnSequence()` — so for the whole ~1s+ return flight the flag was already
  false and enemies resumed attacking.
- Fix keeps the freeze on until the soul is **home**, by extending the *same* flag (goals unchanged):
  `IsRescueActive = rescueActive || anySoulDeployed`.
  - `TeleportAbility` gains a live `IsSoulDeployed` (`Assets/Scripts/Players/Ability/TeleportAbility.cs`):
    set true at the `Activate()` commit point, cleared at the end of `ReturnSequence()` (soul home).
  - `RescueEventController` stores both twins' registered abilities and exposes `IsAnySoulDeployed`
    (`Assets/Scripts/Players/RescueEventController.cs`) — distinct from `_activeSoulAbility`, which
    `CleanupRescueEvent` nulls at Success (too early for the return trip).
  - `PoTWorldStateWriter` (`.../Core/PotWorldStateWriter.cs`) now **polls** `IsAnySoulDeployed` each
    frame and writes the freeze flag only on change. Polling (not an event pair) means a cancelled,
    re-cast, or destroyed ability can't leave the flag stuck true.
- Untouched: the separate C# `IRescueActive.IsRescueActive` that gates *player* abilities
  (Setsuna/Empower/Accord/SoulConv) — player-ability blocking still ends at rescue-success, unchanged.
  Reset paths self-heal (soft-reset despawns all enemies; a Restart makes fresh instances). Compiles
  clean, 0 errors. **In-game verification pending** (enemies stay idle through the soul's return trip,
  then resume the moment it lands home).

### Fixed — Ground telegraphs no longer draw over the player/enemies (BUG-088) (2026-08-01)
- The `GroundVFXOnTop` RenderObjects feature (`Assets/Settings/PC_Renderer.asset`) draws the GroundVFX layer with
  depth-Always so ground telegraphs aren't hidden by grass/props — but that also painted them over any character
  standing on them (player/enemy carry the same default opaque stencil = 2 as the world, so the pass couldn't
  exclude them).
- Added a **`CharacterMask`** RenderObjects feature (Event 300, between CrackLayer and GroundVFXOnTop) that
  re-renders the character layers (Enemy + Player + SoulLayer + TrapEnemy, mask bits 4800) at `depthCompareFunction
  Equal` with no depth/colour write, stamping **stencil 3** onto only the visible character pixels. `GroundVFXOnTop`
  now tests `NotEqual 3`, so it still draws over grass/props (stencil 2) but is masked out of characters. Added via
  MCP `feature_add` (Unity maintained the feature map); nested settings hand-authored. Reimports clean, and is
  non-destructive to the see-through (`!=1`) and crack passes. Generic — every ground telegraph benefits, incl. the
  now-grounded chain marker (BUG-086). **User play-retest pending** (telegraph off characters, still over world,
  cracks/see-through unchanged).

### Fixed — TetherBreaker chain marker floats instead of sitting on the ground (BUG-086) (2026-08-01)
- The reveal decal itself was rebuilt by the user (`TargetMark` → `RevealDisc` child with the reveal material +
  `MaterialRevealDriver`, Property `_val`, From 0 → To 1, Play On Enable) and its rotation corrected. The
  remaining defect was **placement**: `ChainProjectile` spawned the marker at the raw `_targetPosition` = the
  target twin's pivot (mid-body), so the flat disc floated above the floor.
- `ChainProjectile.Launch` now grounds it: `new CueContext(GroundUnder(_targetPosition))`
  (`Assets/Scripts/Combat/ChainProjectile.cs`). `GroundUnder` snaps only Y — `NavMesh.SamplePosition` first
  (the twins/enemies walk the navmesh, and it can't be occluded by the target twin standing on the spot),
  a **player-excluded** downward raycast as the off-navmesh fallback, raw point as last resort. The chain still
  travels to the twin; only the ground telegraph is grounded. Compiles clean.

### Changed — TetherBreaker: removed the broken "whole chain" drag VFX for the playtest (BUG-087) (2026-08-01)
- The `On_TetherChainDrag` cue (a single `ChainDrag.prefab` Follow-attached to the dragged twin) was meant to
  render *along the full chain length* while a twin is being hauled in, but a fixed-size prefab riding the
  player cannot stretch to the live span (which changes every frame), so it read as a broken clump on the twin.
- Removed its play call in `ChainProjectile.Connect` (`Assets/Scripts/Combat/ChainProjectile.cs`) — **commented,
  not deleted**: the block documents *why* it was pulled and *where the redo goes* (rebuild as a span-stretched
  driver like `ChainGlowDriver`/`ChainBeamDriver`, then restore the one-line `_dragHandle = PlayChainCue(...)`).
  The `_cues.drag` id stays wired in the TetherBreaker book so it survives for the redo; `ReleasePlayer`'s
  `StopChainCue(ref _dragHandle)` is a safe no-op while the handle is `None`. The working drag visual — the
  `ChainGlowDriver` span stream (hand↔twin, stretched each frame) — is unaffected. Compiles clean, 0 errors.

### Fixed — Summoner-spawned enemies could act during a twin rescue (BUG-083) (2026-07-31)
- The twin rescue freeze is the **soft** path only — the per-tick shared `IsRescueActive` flag
  (`RescueEventController` makes no `EnemyFreezeService`/QTE calls), which automatically covers
  late-summoned enemies. `GOAPGoalAttackTwin`/`GOAPGoalGrabTwin` already `DoNotRun` on it, but
  **`GOAPGoalDefendSpawn` (priority 90, above AttackTwin's 75) did not** — so whenever the global
  `SpawnUnderAttack` flag was set (e.g. a Summoner's `SpawnPointPOI` under attack), an enemy kept
  running `GOAPActionDefendSpawn` → `BTActionDefendSpawn` (a no-damage 1.2× rush to the spawn) right
  through a rescue, while a lone Siphon in a quiet zone idled correctly. That is the summoner-specificity.
- Added the same `IsRescueActive → DoNotRun` gate to `GOAPGoalDefendSpawn.PrepareForPlanning`
  (`Assets/Scripts/AIFramework/PlanetOfTwinsAI/GOAP/Goals/GOAPGoalDefendSpawn.cs`). Generic across every
  enemy that carries DefendSpawn (Witness, TetherBreaker, Severed, Ranged, Penitent, GroupGrab, Summoner,
  commanders). Compiles clean. **Caveat:** the reported repro was in TestLab where no `SpawnPointPOI`
  exists (so `SpawnUnderAttack` was already false there) — the gate closes the real in-level case;
  the TestLab-only observation stays unconfirmed and was not chased (user call). See BUGS.md BUG-083.

### Fixed — Accord / Setsuna charge-hold timings reverted to cue-book-era feel (BUG-089) (2026-07-31)
- Three serialized charge/hold times on `SkillTreeManager` (Persistent) had accidentally drifted from
  the cue-book-era values to 2.0 s. Reverted via MCP `set_property` (then saved + re-read to confirm):
  `AccordStateSystem._chargeTime` 2.0 → **1.5**, `SetsunaSystem._chargeHoldTime` 2.0 → **0.75**,
  `AccordSpiritSystem._chargeHoldTime` 2.0 → **0.75**. `SoulConvergenceSystem`/`EmpowerSystem`
  `_chargeHoldTime` were already 0.75 (untouched). Scene value overrides the code default, so this is a
  serialized-field edit, not a code change. (Noted but left as-is: `SetsunaSystem._rewindDuration` is 2.0
  vs the cue-book default 1.5 — rewind-playback length, outside BUG-089's scope.)

### Fixed — Body tints are shader-agnostic (`PoT/Coexistence` has no `_Color`) (2026-07-30)
- Enemy / trap / ghost / mood state tints wrote `renderer.material.color` — the legacy built-in
  `_Color` property. After the Twins material moved to the **`PoT/Coexistence`** shader (URP-style
  `_BaseColor`, **no `_Color`**), every tint threw *"Material '…' doesn't have a color property
  '_Color'"* — and the first one crashed `EnemyPool` prewarm at `Enemy.Awake` (reading `.color`).
- Added **`MaterialTint`** (`Assets/Scripts/EnemyAI/MaterialTint.cs`, Assembly-CSharp): resolves the
  material's real colour property (`_BaseColor` preferred → `_Color` fallback → no-op if neither),
  with `GetColor`/`SetColor` helpers. Routed **all 14 tint sites** through it: `Enemy`
  (original/restore/stun/possess), `WitnessEnemy` (ritual), `TetherBreakerEnemy` (rage),
  `PenitentEnemy` (crush/reflection/rage ×6), `SiphonGhost`, `SkeletonHandTrap`, `MoodAmbient` (aura).
  Tints now work on any shader without error spam. `EnemyVisionCone` left as-is (legacy/dead, game.md §19).

### Added — Story-beat sky channel + sun-direction control + world/grade/grayscale debug benches (2026-07-24)
- `SkyboxMaterialChange` (the repurposed story-beat trigger, `Assets/Scripts/Environment/`) gained a
  **Sky** channel: `_skyStateId` (+ `_skyBlendSeconds`, 0 = instant) calls `SkyStateDriver.BlendTo/
  ApplyState` on fire, alongside its existing corruption + grade dials — so the Timeline-activation beat
  (Act 9 SkyboxChanger) can blend the sky to dusk too. Empty = sky untouched (opt-in, like the grade id).
  One shot per activation, R4 fail-loud, NOT persisted across Restart. Rename to `StoryBeatTrigger`
  deferred post-playtest (game.md §20.3). Fills the wiring gap CHECKLIST #58 flagged. (A standalone
  `StoryBeatTrigger.cs` trigger-volume variant was prototyped and removed same session as redundant.)
- Sun-direction control: `SkyStateData.sunDir` + `SkyStateDriver._sunLight`. The skybox sun disc reads
  the main light (unlike the moon's own `_MoonDir`), so `sunDir` rotates the actual directional light —
  disc + shadows stay in sync. Blended (Slerp) alongside moonDir; zero target = sun untouched (old
  states safe). `SkyStateDriver.StateIds()` added for benches.
- GameDebuggerV2 (`Ctrl+\``, Trainer-gated + release-stripped): **World & Sky** bench (corruption slider
  + sky-state buttons) and **Grayscale world test** slider (dev-owned global `ColorAdjustments` volume at
  max priority, saturation 0→-100; runtime-instance profile, destroyed in `OnDestroy` — dirties no
  asset). Checklist item 21 is now one click. Benches are TestLab-resident (open decision: move the panel
  to Persistent for real-area use).

### Added — PoT/Coexistence generic HDR emission (2026-07-24)
- `Coexistence.shader` + `CoexistenceCommon.hlsl`: `[HDR] _EmissionColor` + optional `_EmissionMap` mask
  (black default → every existing material unchanged). Additive; HDR > 1 blooms into a light source —
  the perf-friendly alternative to placing/baking real lights. `M_Decor_LanternCore` set to a warm amber
  starter `(3.5, 2.17, 0.98)` (tune the HDR intensity to taste; drop a mask in `_EmissionMap` to glow
  only the paper). Compiles 0 errors; emission verified applied on the material.

### Fixed — Torii props rendered see-through (backface culling) (2026-07-24)
- `M_Decor_Torii` + the 5 `TorriGatesDistant/M_Decor_Torii{,1,2,4,5}` materials had **`_Cull: 2`
  (Back)**; the gate meshes are thin/single-sided, so back-face culling dropped the away-facing
  faces and the trees behind showed straight through (at any distance — not the layer-18 SeeThrough
  system; the props are on Default). Set **`_Cull: 0`** (double-sided) — the `PoT/Coexistence`
  shader flips the normal on back faces so lighting stays correct. Play-confirmed solid on the near
  ParkGate. STILL `_Cull: 2` and unchecked (flip only if they visibly show through): `M_Decor_Lantern*`,
  `M_Decor_Rope`, `M_Decor_Stone`, `M_Decor_Wood`.

### Changed — PoT/LocalFogVolume: flat sheet → raymarched 3D wind-driven box volume (2026-07-24)
- `Assets/Art/Shaders/PoTLocalFogVolume.shader` **reworked in place** (GUID + every property name
  preserved → `ParkLocalFogVolume.mat` keeps all its tuned values, no re-authoring). It was a
  single-sample transparent sheet faking depth with 2D xz-noise; it is now a true **raymarched box
  volume**: `Cull Front` back-face shading (shaded once, works from inside or outside), per-ray slab
  intersection of the cube's object-space AABB, per-ray opaque-depth occlusion (`SampleSceneDepth`),
  premultiplied `Blend One OneMinusSrcAlpha` accumulation, `ZTest Always`.
- New dials: `_GradientMode` (Uniform/Bottom/Top/Scattered), `_Fill`, `_DriftPeriod`,
  `_LightInfluence`, `_Anisotropy`, `_Steps`. Kept: `_FogColor`/`_Density`/`_NoiseScale`/`_NoiseAmount`/
  `_DriftSpeed`/`_DriftSecondary`/`_DepthFade`/`_CameraFade`/`_EdgeFade`/`_HeightFade`/corruption trio.
- **3D wind drift** scrolls the noise domain along the `_PoTWind` global with a **BOUNDED**
  `fmod(_Time.y*speed, _DriftPeriod)` offset (two opposed octaves) — this is the fix for the
  precision dither/stipple that broke the old fullscreen fog once the coordinate grew large.
- Verified over MCP: compiles 0 errors; `ParkLocalFogVolume.mat` renders correctly at density 0.3
  (thin walk-through haze) and 1.6 (full pocket) with soft edges + correct ground occlusion.
  **NOTE:** a placed density of ~0.3 reads thinner after the raymarch rework — bump toward ~0.5+.
- Deleted a duplicate `PoTLocalBoxFog.shader` + `M_LocalFog.mat` created earlier in the session
  before finding this existing shader (do NOT reintroduce a parallel local-fog shader).

### Added — fog setup + verification runbook (2026-07-24)
- **SETUPGUIDE §5** rewritten into a full fog-systems runbook: the **four-system map** (CristianQiu
  global god-ray fog [Volume-driven] · `PoT/CoexistenceFog` distance · `PoT/LocalFogVolume` placed ·
  old `M_SunShafts` sun-shafts), how to tune the **hidden-material** god-ray fog via its **Volume
  override** (answers "how do I change density when the material is hidden"), the LocalFogVolume
  recipe + the new dials, a from-scratch **"build a new box-fog shader"** guide (render state · slab
  test · depth clamp · the bounded wind-drift footgun · includes), and a **§5D verification
  checklist** (what to assign/make/verify for both fog systems).
- **instruction.md §18** standing checklist **item 9** added (fog systems + the pending decision to
  keep-or-retire `M_SunShafts` now that the CristianQiu god-rays overlap it).

### Revert map — UI round 2 commit chain (2026-07-21)
Recorded for safe rollback. The five commits are ADDITIVE and layered; revert from the top down,
never from the middle (later commits reference files introduced by earlier ones).

| Commit | Contents | Safe to revert alone? |
|---|---|---|
| `8b93e40` | SETUPGUIDE §21 ability-icon runbook (docs only) | YES — pure docs |
| `e903483` | UI integration 1–2: audited bar stack (shader trough-remap Y-axis fix in `PoTUIBar.shader`), shared emblem built in Persistent.unity, ControlHints rework (Hint_SkillTree/Settings/ToggleHints rows, Ability+Teleport rows deactivated), H toggle end-to-end (`ToggleHints` action in `PlanetOfTwins.inputactions` + `IInputProvider.GetHintsToggleDown` + `TwinInputReader` + `TutorialInputGate` + new `ControlHintsVisibility.cs`), 14 UI PNGs, 11 materials, 4 prefabs, Twins/SoulTwin/10 enemy prefab wiring | NO in isolation — Persistent.unity in this commit also carries the user's small pre-existing sprint diff (~26 lines, disclosed in the commit message). To revert the UI work, `git revert e903483` then re-apply that sprint diff by hand, or cherry-pick the scene file back selectively. Reverting also removes the `IInputProvider` method — any later implementor added after this date would break. |
| `dead2a2` | changelog entry for the QTE seam (docs only) | YES |
| `ebd05de` | QTE seam: `QTEManager.cs` caches `UIRingTimerView` from the anchor's TimerRing; falls back to legacy fillAmount path when absent | YES — additive-slot pattern, empty slot = legacy behaviour |
| `e1f81cc` | Foundation: `PoTUIRingTimer.shader` + `UIRingTimerView.cs`, glass panel V2 material, 4 ring materials, SampleScene judging board, docs (§17.3–17.5, SETUPGUIDE §16–§20) | Only if `ebd05de`/`e903483` are reverted first (they reference these files) |

Known content warnings inside the chain: `M_SunShafts.mat` diff must be discarded before any
user commit (BUG-077 rule, `git checkout -- Assets/Art/Materials/M_SunShafts.mat`); the world-sprint
working tree (terrain, scenes, deleted HDRP defaults) is the USER's uncommitted work — never
sweep it into a revert commit.

### Added — UI round 2: glass panel V2 + ring-timer widget + SampleScene board (2026-07-21)
- **`PoT/UIRingTimer` + `UIRingTimerView`** (`Assets/Art/Shaders/`, `Assets/Scripts/UI/`) — the
  ONE timer/cooldown widget (game.md §17.5 FINALISED): solid disc (never hollow), 4 fill modes
  (sweep / centre-out cooldown / L→R / T→B, invertible), dual-clan half toggle,
  Overwatch-rez rim (faint backing circle + progress arc + leading tip + inward glow),
  top→bottom ready flash on UNSCALED time, and the QTE extras — Sekiro closing ring,
  rim ticks, urgency heat+pulse. Replaces the retired branch-only `PoTUIRadial` (never used).
- **Glass panel recovered + approved V2** — `PoTUIGlassPanel.shader`/`UIGlassPanelView.cs`/
  `M_UIGlassPanel.mat` recovered verbatim from `ui-swap-2026-07-19`; new
  `M_UIGlassPanel_FableV2.mat` = the user-approved sleek look (values in SETUPGUIDE §19).
- Materials `M_UIRingTimer_{Kai,Lyra,Shared,QTE}` (ACES colour rule applied — SETUPGUIDE §20).
- **SampleScene judging board** `GlassPanelSample` + `GlassSampleCam` (disposable): 2 panels
  (round-1 vs V2), 4 mock icons, 4 live editor-demo rings.
- `PoT/GroundFull` learning shader (`Assets/Art/Shaders/PoTGroundFull.shader`) — cavity/opacity/
  fuzz comparison tool from the texture sessions; unused in production, kept for A/B.
- Docs: game.md §17.3–17.5 (MicroSplat verdict · fuzz corruption lead · UI authoritative spec
  incl. FINALISED rulings) · SETUPGUIDE §16–20 (APV · occlusion · post-process map · glass ·
  ring) · CHECKLIST items 57–66 · BUGS-077 re-filed.
- **QTE ring seam** (`QTEManager.cs`) — fail-open per anchor: TimerRing carrying a
  `UIRingTimerView` is driven via `SetProgress` (shader owns colour/urgency/closing ring);
  without one the legacy fillAmount+colour path is byte-identical. Authoring per area anchor
  = CHECKLIST 66a.

### Added — UI integration phases 1–2: health bars + shared emblem + hints panel (2026-07-21)
- **Health bars (66e) AUDITED then reused** — the round-1 bar stack was Opus-built (execution
  split, not Fable's as earlier misstated); per user ruling it was audited file-by-file before
  use. Verdicts: `UIBarView`/`UIBarHealthView`/`HealthBarView` PASS unchanged; `PoT/UIBar`
  PASS with ONE real bug found+fixed (trough remap applied X-axis bounds to Y — latent, wrong
  for vertical fills). Recovered from `ui-swap-2026-07-19`: user art (14 PNGs), 11 bar/frame
  materials, 4 UIBar prefabs, wired Twins/SoulTwin/10 enemy prefabs (wiring verified on disk).
- **Shared-health emblem (66 shared)** — rebuilt in Persistent inside `SharedHealthPanel` from
  the branch's extracted YAML (210×210, broken-heart pivot 0.4766/0.1768): Half_L gold flower /
  Half_R violet triangle, per-half fill+frame, `UIBarHealthView` + `BondWeaknessPresenter`
  (Lyra→left, Kai→right), `SharedHealthPresenter` repointed via the audited MonoBehaviour→
  IHealthBarView seam. `PlayerHealthComponent` gained `SurvivalHealth01`/`BondWeakness01`
  (+`OnBondWeaknessChanged`) — fill = real pool only, bond weakness = grey drain channel.
  Old shared slider now static (superseded; deactivate when judged).
- **ControlHints panel** — `Hint_Ability` + `Hint_Teleport` deactivated (redundant with the
  ability panel's own labels); added `Hint_SkillTree` (Tab), `Hint_Settings` (ESC) and
  `Hint_ToggleHints` (H). NEW `UI/ToggleHints` action (H) in PlanetOfTwins.inputactions +
  `IInputProvider.GetHintsToggleDown` (ungated) + gate passthrough + `ControlHintsVisibility`
  (hides all rows except the H row, flips its label, never resurrects retired rows).

### Reverted — the entire UI swap attempt, 2026-07-18/19 (branch `ui-swap-2026-07-19`)

**Net effect on this branch: nothing.** `vfxsounds` was reset to `09c5328`, the last commit whose
`Persistent.unity` carries the original working HUD. Five commits were undone
(`2d86679`, `e7639a8`, `6a7f711`, `7a66a4e`, `67ea526`). All of that work — plus the uncommitted
final state — is preserved at **`ui-swap-2026-07-19`** (tip `c66c6d4`) and can be pulled back a
file at a time with `git checkout ui-swap-2026-07-19 -- <path>`. Full engineering account:
**game.md §17.2**.

**What was asked.** The ability HUD, the health bars and the accord bar all already WORKED. The
user's art (`Assets/Textures/UI/UI_*.png`) and Fable's `PoT/UIBar` shader already existed. The job
was to put that art in front of the existing logic — *"JUST SWAP THE UI WITH THE NEW ONE"*,
*"NO CODE CHANGE NEEDED"*. The scope never changed at any point.

**What was built, and why that shape.**
- Four reusable bar prefabs (`UIBar_Enemy/_Kai/_Lyra/_Soul`) — one asset per clan, instanced into
  10 enemy prefabs, `Twins.prefab` and `SoulTwin.prefab`, so tuning happens in one place rather
  than per enemy.
- `HealthBarView` and `AccordBarView` each gained ONE optional `barView` slot. Empty ⇒ the legacy
  Slider path is byte-identical; assigned ⇒ `UIBarView` draws. Chosen specifically so no consumer
  signature changed and the swap stayed reversible from the Inspector.
- `UIRadialView`, the ring counterpart to `UIBarView`. Wired nowhere.
- Old Sliders were deactivated, never deleted.
- Sizing: world-space canvases carry a NON-uniform scale `(0.008, 0.03, 0.02)`, so a rect of ratio
  **3.75 : 1** is what renders the square 1024² art square in world (382×102, 385×103, 300×80).

**Three things wrongly diagnosed as bugs — all were correct code.**
1. *"The ability HUD has no binder."* It had one. `AccordHUDController` + `AccordIconSlot` +
   `AbilityIconUI` were live with all six slots, the Accord swap and unlock handling. ~1,200 lines
   of parallel re-implementation were written and deleted.
2. *"Ability slots fail to bind at Start."* Not a defect — `TwinAbilitySetup` creates abilities in
   `Start`, so a one-frame-later read is correct and already happened. Logged and closed as
   **NOT A BUG**; do not re-raise.
3. *"`WorldSpaceHealthUI.healthBarView` should be an interface."* The concrete `HealthBarView` type
   is a deliberate Inspector-level guarantee — typed concretely, the Inspector refuses anything that
   is not a health-bar view. Widening it traded a compile-time guarantee for a runtime cast and
   broke the enemy bars. Reverted; the concrete type stays.

**Why the whole thing was reverted.** The bars were mechanically correct and verified on disk, but
the delivered result did not look right, and "mechanically correct" was never the requirement. The
user's call, after roughly sixteen hours, was to return to the known-good UI rather than keep
iterating.

**Time sinks worth not repeating** (detail in game.md §17.2): a claimed editor crash that never
happened (a truncated process listing); two wrong root causes for MCP hangs (memory, then editor
focus) when the real cause was a missing script blocking a prefab save; and wiring two prefabs
(`Kai.prefab`, `Lyra.prefab`) that turned out to have zero instances anywhere.

**Still true on this branch after the revert:** BUG-077 (sun shafts dirty `M_SunShafts.mat` on
Play) is live and WON'T FIX — discard the file before each commit.

### Added — Rain system runtime + rig in Persistent (2026-07-18)
- `RainController` (Persistent singleton on new `RainSystem` GO, scene saved) — one
  toggle + one 0..1 intensity: camera-following world-sim drop emitter with WORLD
  collision (drops die on roofs — correct sheltering) + collision sub-emitters
  (`RippleOnHit` ring via generated `ripple_ring.png`, `SplashOnHit` spray), lagged
  `_PoTWetness` global (soak 12 s / dry 45 s, zeroed OnDestroy), looping 2D
  `RainLoopAudio` child (clip = user authoring), ContextMenu tests. `RainDripLine`
  (area-resident eave dripper, fail-open, ~6 s after-rain lag). Materials
  `M_RainDrop/M_RainRipple/M_RainSplash` in `Assets/Art/VFX/Rain/`.
- `TerrainQualityService` now mirrors `_POT_HEX`/`_POT_PARALLAX` per-material keywords
  with GLOBAL `Shader.EnableKeyword` — reaches the hidden auto-generated add-pass
  material (layers 5–8).

### Added — VFX draw-on-top: GroundVFX layer + cue flag (2026-07-18, user ruling)
- Ground telegraphs (cast circles, meteor decals) can no longer be hidden by grass/props:
  new `GroundVFX` layer (21) + `GroundVFXOnTop` RenderObjects feature on PC_Renderer
  (After Skybox · transparent queue · depth test Always, write off; layer excluded from
  the normal transparent pass — no double draw). Fog/rain still veil it (drawn before
  transparents).
- `CueElement.drawOnTop` (+ editor toggle, tooltip): FxManager stamps the pooled
  instance's hierarchy onto GroundVFX on spawn and RESTORES prefab layers on pool
  return (pool-reuse-safe; missing layer = one LogError, flag ignored). Linter F8 flags
  the flag on Sound/Manpu elements.

### Added — TempleMountVista greybox + backdrop ring + MetricsGym (2026-07-18)
- `Assets/Art/Props/Vista/TempleMountVista.prefab` (Erdtree-principle landmark):
  SkyboxMountains03 mesh re-centred on its true highest VERTEX (peak exactly under the
  prefab origin, scaled to 420 m), 3 greybox temple tiers with HDR glow bands + spire,
  3 additive god-beam cylinders, `PoT/LocalFogVolume` mist skirt, zero colliders.
  5 `M_Vista*` materials. Staged in SampleScene at (80, −20, 620) + the extracted
  `SkyboxMountains` horizon ring (`SkyboxMountainsRing_Staged`).
- `MetricsGym` in SampleScene (x −40…−28, z −20): 2 m capsule, 2.5 m door, 3.6 m storey,
  6 m torii, 3 m lantern, 1 m bush, 12 m tree — the scale-audit reference row.
- `M_CoexistenceSkybox._CloudTex` = extracted `clouds.png` @ influence 0.6 (user may
  swap the texture).
- Both temp authoring tools (BuildRainSystemTool / BuildTempleVistaTool) deleted after
  their successful runs.

### Added — Modular building kit from the six greybox houses (2026-07-17)
- `Assets/Art/Props/Kit/` — 51 individual baked prefabs (`Kit_<Category>_<Variant>`)
  extracted and deduplicated from the six inactive `*_Source_PB` ProBuilder houses in
  SampleScene (AccordCoexHouse_F, TwinHouse_Fantasy, TwinHouse_Wuxia, FantasyHouse_Stilt,
  RyokanInn, TwinShrine). ~275 source children collapse to 51 distinct (shape-role ×
  material) types: wall/body blocks (plaster/cream/shoji-lit), plinth/deck/engawa/balcony
  slabs, shrine tiers, roofs (wuxia swept hip large+medium, ryokan skirt, thatch main +
  awning, fantasy gold cap + violet peak), ridge cap / finial / chimney, pillars / red
  posts / stilts / brackets, band beams / trims / bond-seam, wood & red railings + posts,
  wood/red/round doors, twin-colour door posts & lintels, wood/shoji/round/round-glass
  windows, stone steps, shrine back-wall / altar / emblems (violet+gold) / modern plaque,
  and the Accord coexistence accents (conduit / junction box / AC unit).
- Each piece baked to its own mesh (`Assets/Art/Props/Kit/Meshes/*.asset`) via the
  established combine→CreateAsset→single-MeshFilter/MeshRenderer-prefab pipeline; existing
  `M_Coex_*` materials preserved; source rotation+scale baked into the mesh; box pieces
  (24-vert) snapped to 0.25 m increments where non-distorting (thin dims kept native,
  shaped pieces left untouched). Added a BoxCollider matched to bounds.
- **Pivot convention: bottom-centre** (X/Z centred, Y at the mesh's bottom) — every piece
  sits on the ground plane at its placement point for grid snapping.
- Left an inactive `KitPreview_Row` staging root (all 51 instances, x≥155) in SampleScene
  for user inspection. Six `*_Source_PB` hierarchies and the six assembled house prefabs
  untouched (sources left inactive as found).

### Added — Terrain add-pass shader: PoT features on layers 5–8 (2026-07-17)
- `Assets/Art/Shaders/PoTTerrain/PoTTerrainLitAdd.shader` ("Hidden/PoT/TerrainLit
  (Add Pass)") — port of the URP 17.3 stock add pass reusing `PoTTerrainLitPasses.hlsl`,
  so terrain layers 5+ get the same hex break-up / parallax / corruption film instead of
  silently falling back to the featureless stock shader. `PoT/TerrainLit`'s
  `AddPassShader` dependency now points at it. PoT keywords in the add pass are GLOBAL
  scope (the auto-generated add-pass material is hidden — per-material state can't reach
  it); `TerrainQualityService` gets a global keyword mirror (pending this session's C#
  batch). Authoring rules (SETUPGUIDE §3): layers 1–4 broad, 5–8 accents only, hard cap
  8, distant terrains ≤ 4.

### Added — PoT/LocalFogVolume: walk-through ground-haze shader (2026-07-17)
- `Assets/Art/Shaders/PoTLocalFogVolume.shader` — placeable box-mesh haze layer
  (Tsushima/WWM "smoke the player passes through"): soft-particle depth fade, camera
  approach fade, object-space edge + height fades, two fbm layers drifting along the
  `_PoTWind`/`_PoTWindGust` globals (haze moves with the grass), corruption stain
  (Voreth-lifted tint, no-op at 0). Unit-cube convention, dial docs in SETUPGUIDE §5.

### Added — Rain wetness pass in the world shaders (2026-07-17)
- New `_PoTWetness` shader global (0 dry → 1 soaked; RainController will own it):
  `PoT/Coexistence` darkens albedo + adds a Blinn-Phong glint (custom-lit path),
  `PoTTerrainLitPasses.hlsl` (base + add pass) darkens albedo + pushes smoothness toward
  a wet ceiling, `PoT/DetailFoliage` darkens grass with the ground. Exact no-op at 0.
- Rain texture set extracted GUID-intact (disk move, same pattern as the sample-pack
  extraction) from the ShaderGraph weather sample into `Assets/Art/VFX/Rain/`:
  rain_ripples / rain_drops / rain_drips (+mask) / puddle_norm / clouds (the last is the
  checklist-#7 skybox `_CloudTex` candidate).
- `RainController.cs` + `RainDripLine.cs` authored and staged (scratchpad) — land in
  `Assets/Scripts/Environment/` after the modular-kit agent frees the editor.

### Added — SETUPGUIDE.md sprint runbook (2026-07-17)
- Repo-root usage guide for the world-building sprint systems (user-requested, like
  TESTGUIDE.md): grading, sky states + moon, 8-layer terrain rules, detail foliage,
  local fog, rain, wind, time-of-day arc, VFX-vs-grass answer, scale/camera metrics,
  temple-mountain vista recipe. Temporary — fold into game.md when absorbed.

### Added — SkyStateDriver: authored time-of-day states + driver (2026-07-17)
- `SkyStateData` SO (R7 config) + `SkyStateDriver` Persistent singleton (new GO in
  Persistent.unity, scene saved): applies/blends states onto the shared
  `M_CoexistenceSkybox` — `ApplyState(id)` hard cut, `BlendTo(id, seconds)` UNSCALED
  smooth blend from CURRENT values (mid-blend retarget-safe), throttled
  `DynamicGI.UpdateEnvironment` so ambient follows, in-editor material snapshot/restore
  (play mode never dirties the asset — GraphicsSettingsController pattern). Seam-only like
  StoryGradeDirector: no runtime caller yet (story wiring calls it later); boots to "day".
  ContextMenu test items on the component (Apply golden_hour / Blend to dusk / Apply day).
- Three authored states in `Assets/Settings/Sky/`: `SkyState_Day` (current sky),
  `SkyState_GoldenHour` (festival — amber horizon, warm clouds, sun 3.5),
  `SkyState_Dusk` (wake-up — indigo top, ember horizon, sun 0, MOON on 1.6/0.14).
- Area scenes L1_Park / L3_Alley / L4_MuseumStart / L5_Fields: `m_SkyboxMaterial` swapped
  from the built-in default placeholder to `M_CoexistenceSkybox` (YAML edit, scenes were
  closed) so the driver/corruption/moon work in the real levels. L2_Streets deliberately
  NOT touched — it uses the user-placed AllSkyFree "Epic_GloriousPink" HDRI (also the
  origin of the off-bible pink fog): user decision whether to swap it (recommended).

### Changed — Wind-ready fabric + swaying RyokanInn lanterns (2026-07-17)
- **Fabric subdivision for shader wind sway.** The three decor fabric meshes were flat
  24-vert thin boxes that the PoT/Coexistence `_WindAmount` vertex sway cannot bend.
  Rebuilt each via 3-pass midpoint tessellation (≈8×8 per face) written back into the
  SAME mesh asset (GUIDs preserved, single submesh/material unchanged, UV/normal/tangent
  interpolated): `Assets/Art/Props/Decor/ClothBanner_A_Mesh.asset`,
  `Ribbon_A_Mesh.asset`, `Ofuda_A_Mesh.asset` — each 24→486 verts, 12→768 tris.
- **RyokanInn lanterns un-baked so they can sway.** `Lantern_0`, `Lantern_1`,
  `LanternCord_0`, `LanternCord_1` were baked into `Assets/Art/Props/Houses/RyokanInn_Mesh.asset`.
  Rebaked the house from `RyokanInn_Source_PB` (28 of 32 renderers) excluding those four,
  back into the SAME mesh asset (768→672 verts; 4 submeshes; material order preserved
  Stone/PaperLit/WoodRed/HouseRoof — matches the prefab MeshRenderer array exactly, no
  prefab material change needed; AABB unchanged).
- Added two `LanternHook_L/R` child GameObjects to `Assets/Art/Props/Houses/RyokanInn.prefab`
  at the original lantern attach points (local x=±3.2, y=3.3, z=3.3), each with
  `WindSwayPivot` (maxAngle 8, frequency 1.1, gustResponse 0.8) parenting a
  `HollowLantern_A` instance hanging 0.45 m below (restoring lantern centres at the original
  y=2.85). Lanterns now rock via the wind system instead of being frozen in the mesh.

### Changed — Story grade profiles LOCKED + applied (2026-07-17)
- All six grade profiles in `Settings/Grading/` rewritten to the locked table (Tsushima/
  Wukong/Where-Winds-Meet reference synthesis + the golden-hour→blue-dusk arc, user-approved):
  every profile now **actually overrides Tonemapping = ACES** (previously present but
  override OFF — never applied), gains Bloom + SplitToning (the "two natures" carrier) +
  **MotionBlur** (user call — 0.2 warm / 0.25–0.3 mid / 0.5 shock). Act1_Warm = golden-hour
  celebration (sat +10, temp +15, warm gain, violet-shadow/gold-highlight split toning);
  EarlyFear = blue-dusk wake-up (postExposure −0.25, temp −12, blue-violet shadows, lantern
  bloom 0.55/1.1); MidPurpose = dusk equilibrium (split-toning balance 0 — the two natures
  evenly held); LateChaos shadow cast corrected blue-teal → **green oil-teal** (Khal-Vor
  family; old value collided with healthy Pure Current); Ending_Losing = drained cold +
  magenta tint drift, highlights split-toned to pale grey. Effects intentionally NOT in
  grades: DoF (cutscenes only), Panini/lens distortion (never), white-balance in Shock kept
  (hard cut wants the crude cold snap). `FailureReset_Sting` untouched.
- Four area identity profiles (Settings/Grading/Areas) authored from empty: WhiteBalance +
  ShadowsMidtonesHighlights only (stack-safe under any story grade) — L1Park temp +6/warm
  midtones · L2Streets temp +2/violet-whisper shadows · L3Alley temp −4/deepened violet
  shadows · L4Museum pale-gold highlights/cool shadows.
- game.md §1.1 += time-of-day canon (golden hour → blue dusk → fog/mist/rain progression)
  and the "two natures in one frame" environment colour master rule.
- FINDING (L1_Park terrain): `Assets/New Terrain 1.asset` carries 13–16 layers (4 splat
  alphamaps). PoT/TerrainLit applies hex/parallax/corruption to layers 1–4 only; 5+ fall
  back to the stock hidden AddPass shader (feature mismatch + one extra full geometry pass
  per 4 layers). Ruling: ≤4 layers per terrain (checklist #35–37).

### Changed — Sample-pack keeper extraction (GUID-preserving moves, 2026-07-17)
- All keeper content MOVED (AssetDatabase.MoveAsset — GUIDs preserved, every reference incl.
  L1_Park's terrain layers survives) into `Assets/Art/Terrain/`: Layers_Samples/Layers_Demo/
  Terrain_ShaderGraphSamples (30 terrain layers), Textures_Samples/Textures_Demo (72 layer
  textures), Details_Demo + Details_Samples + Materials_Samples (grass/detail assets),
  Foliage_Demo, Rocks_Demo, Trees_Demo (pines/cypress/conifer), Water_Demo (WaterStream/
  WaterLake — checklist #9), SkyboxMountains_Demo (checklist #5), VFX_Demo (Fog.vfx —
  checklist #6), ShaderGraphs_Demo (the demo prefabs' shader dependencies). UGUI Shaders
  sample → `Assets/Art/Shaders/UGUI_SG_Samples` (verified zero dependencies on the samples'
  Common folder). Remaining pack shells (TerrainDemoScene_URP, TerrainSampleAssets,
  Samples/Shader Graph Common+Custom Lighting+Production Ready+Terrain Shaders) are now
  safe for the user to delete. GOTCHA: manage_asset move reports "failed unexpectedly" on
  FOLDER moves but succeeds — verify on disk, don't retry (a retry errors "source not found").
- Corruption-colour unification DONE: `M_PoTVolumetricFog._FogColorCorr` (was 0.30/0.12/0.24),
  `M_CoexistenceFog._CorruptionColor` (0.30/0.09/0.27), `M_CoexistenceSkybox._CorruptionColor`
  (0.30/0.09/0.27) → all (0.26, 0.07, 0.19) — the Voreth family colour lifted slightly for
  thin-media (fog/sky) visibility; solid-surface film stays (0.22, 0.05, 0.16). 0 console
  errors after reimport.

### Fixed — World-space UI billboard dies on boot order (rescue ring sideways) (2026-07-17)
- `UIBillboard` resolved `Camera.main` ONCE in `Start()` and gave up forever when the canvas
  woke before the Persistent MainCamera (Bootstrap/direct-play boot order) — the twins'
  RescueCanvas + Canvas_KaiUI then rotated with their player ("rescue ring turns sideways",
  playtest report). Now resolves lazily in `LateUpdate` until found. No prefab changes needed —
  all four canvases already carry UIBillboard. Verify in the checklist #26 play-run.
- Area identity volumes verified wired in ALL FOUR area scenes (L1 live inspection: global,
  prio 10, profile assigned + new overrides active; L2/L3/L4 profile GUIDs confirmed in scene
  YAML). Checklist #19's verify-half is done — only taste-tuning remains.

### Changed — Queue completion pass (2026-07-17, post-Opus mesh work)
- `M_PoTDetailGrass` (Assets/Art/Materials/) created on PoT/DetailFoliage with
  Grass_A_BaseColor — the starter material for terrain detail painting (checklist #45).
  Shader compile verified clean.
- SampleScene's `RyokanInn` showpiece was a DETACHED mesh-only copy (not a prefab instance) —
  it showed the rebaked house but couldn't receive the new LanternHook children. Replaced
  with a true prefab instance at the same transform (saved inactive, matching the user's
  all-off prop state). Screenshot-verified: rebaked house has no holes; lantern hangs at the
  eave (`ryokan_lantern_closeup2.png`).

### Added — PoT/DetailFoliage shader (2026-07-17, compile-verified)
- `Assets/Art/Shaders/PoTDetailFoliage.shader` — instanced two-sided alpha-cutout detail-mesh
  shader (terrain "Vertex Lit" detail prototypes): main-light half-Lambert + SH, WindDriver
  globals sway (tip-weighted by mesh-local Y; `_WindAmount 0` = rigid pebbles), and the
  blood-moon corruption film (`_WorldCorruption` global, same maths as Coexistence/TerrainLit)
  so painted grass stains in lockstep with the terrain. ForwardLit + DepthOnly (sway
  replicated so depth matches). No shadow casting by design. Material authoring + terrain
  detail-prototype hookup = user step once detail meshes are chosen.

### Added — Moon in CoexistenceSkybox (2026-07-17)
- `PoT/CoexistenceSkybox` += oversized stylized moon block (the Where-Winds-Meet giant-moon
  register): `_MoonDir/_MoonColor/_MoonSize/_MoonIntensity/_MoonHalo/_MoonHaloColor/
  _MoonDetail/_MoonHorizonVeil`. Procedural crater mottling (reuses the cloud fbm), wide cool
  halo (bloom finishes the glow), horizon veil melts the low edge into the haze, cloud layer
  passes IN FRONT (density attenuates the disc), corruption dims it via the existing
  `_CorrSunDim`. **Default `_MoonIntensity = 0` — the day sky is byte-identical until the
  dusk state turns it on.** Preview values: checklist #42. Gotcha hit: `[Header(...)]` text
  cannot contain parentheses/commas — parse error at the Properties block.

### Fixed — BUG-074 GraphicRaycaster NaN frustum spam (2026-07-17)
- Root cause: cursor locked in gameplay + Persistent `InputSystemUIInputModule` default
  `Cursor Lock Behavior = OutsideScreen` → pointer at (-∞,-∞) → every world-space canvas
  raycast spams "Screen position out of view frustum (-nan)". Fix: ScreenCenter
  (`m_CursorLockBehavior: 1`) on the Persistent EventSystem (scene YAML edit while unloaded).
  BUGS.md entry updated; play verify pending (user checklist #26 run covers it).

### Added — Festival time-of-day look-test rig (2026-07-17)
- `FestivalLookTest` root in SampleScene: three toggleable variants for the Accord-festival
  time-of-day decision — `Sun_Morning`+`Vol_Morning`, `Sun_Afternoon`+`Vol_Afternoon`,
  `Moon_Dusk`+`Vol_Dusk` (light + global Volume prio 50 each; enable ONE pair, disable the
  scene's `Directional Light` while testing). Profiles in `Assets/Settings/Grading/Tests/`
  (Test_FestivalMorning/Afternoon/Dusk — ACES + exposure/WB/saturation/bloom/split-toning per
  variant). All variants saved DISABLED; scene light re-enabled. Comparison screenshots in
  `Assets/Screenshots/festival_*.png`. NOTE: a true dusk also needs `M_CoexistenceSkybox`
  gradient values + ambient changed (skybox is gradient-driven, ignores lights) — dusk shot
  used temporary values, restored after capture. Finding for the corruption-unification list:
  `M_CoexistenceSkybox._CorruptionColor` = (0.30, 0.09, 0.27), off the Voreth family like the
  two fog materials.

### Added — GRAPHICS settings section (2026-07-17)
- `GraphicsSettingsController` (`Assets/Scripts/SettingMenu/`) — new sibling of `SettingsMenuController`,
  hosted on a dedicated always-active `GraphicsSettings` GameObject in `Persistent.unity` (NOT on the
  inactive `SettingsPanel`, so saved settings apply at boot, R3-resident, no statics). 11 options, each
  a control over a persisted `gfx_*` PlayerPref; the UI is only an editing surface — when a control is
  unwired the applied value comes straight from PlayerPrefs (defaults keep AO/shafts/fog High ON).
  - Quality Preset (Low/Med/High/Custom) master switch → drives 2–11; changing any control flips to Custom.
  - VSync → `QualitySettings.vSyncCount` 0/1 (gates FPS-cap interactable).
  - FPS Cap Off/60/120/144 → `Application.targetFrameRate` (−1/60/120/144).
  - Texture Quality High/Med/Low → `QualitySettings.globalTextureMipmapLimit` 0/1/2.
  - Shadow Quality Low/Med/High → URP `shadowDistance` 20/35/50 + soft-shadow enable/tier via reflection
    (`m_SoftShadowsSupported` / `m_SoftShadowQuality` — no public setters).
  - Anti-Aliasing Off/SMAA → Main Camera `UniversalAdditionalCameraData.antialiasing` (None / SMAA).
  - Ambient Occlusion → SSAO renderer feature (`ScreenSpaceAmbientOcclusion`) `SetActive`.
  - Volumetric Fog Off/Low/High → `PoTVolumetricFog` feature `SetActive` + `M_PoTVolumetricFog` `_Steps` 12/24.
  - Sun Shafts → `CoexistenceShafts` feature `SetActive`.
  - Terrain Quality Low/Med/High → `TerrainQualityService.Instance.SetTier(0..2)`.
  - Render Scale 0.7–1.0 → URP `renderScale`.
  - EDITOR-SAFETY: Awake snapshots every mutated asset value (URP asset, 3 renderer features, fog `_Steps`,
    QualitySettings vSync/mipmap, targetFrameRate); OnDestroy restores them under `#if UNITY_EDITOR` only.
  - BUILD asset resolution: URP asset, PC_Renderer `UniversalRendererData`, and fog material are serialized
    slots on the component (no `Resources.Load`, no duplicate pipeline assets). Camera falls back to `Camera.main`.
  - UI: 11 rows cloned from the existing `Row_*` templates into the settings `Content` (VerticalLayoutGroup),
    labelled and wired to the controller. ESC/pause flow untouched (`PauseMenuController` still owns ESC).

### Changed
- CHECKLIST.md (TEMPORARY, delete when done): 2026-07-17 user working board — terrains, bakes, value tweaks, authoring, verification runs, parked decisions.

### Fixed / Added — F7 pause/ESC panel wiring + build-readiness audit (2026-07-16)
- **F4 pause audio now actually pauses.** `AudioManager.SetPaused`/`ReleasePaused` and the
  `Paused` mixer snapshot had **no callers** — gameplay audio kept playing through a pause.
  `PauseMenuController.OpenPause` now calls `AudioManager.SetPaused(this)` +
  `RequestSnapshot(this, AudioSnapshotId.Paused, 50)`; `Resume`/`ExitGame` release both (owner
  pattern, sole `AudioListener.pause` writer — R10-mirror). `PauseMenuController.cs`.
- **Restore Default Keybinds (F6 groundwork).** New `IInputProvider.ResetBindingsToDefault()`
  → `TwinInputReader` clears all binding overrides via `_actions.RemoveAllBindingOverrides()`
  (fail-loud if asset null); `TutorialInputGate` passthrough. `PauseMenuController` gains a
  `RestoreDefaultKeybinds()` handler (refreshes every `InputPromptView` via a scene sweep) and
  an optional serialized `_restoreKeybindsButton` slot. Safe no-op today (no rebinding UI yet).
  Files: `IInputProvider.cs`, `TwinInputReader.cs`, `TutorialInputGate.cs`, `PauseMenuController.cs`.
- **ESC chain verified single-owner** (instruction.md §5.6 double-consume regression is gone):
  only `PauseMenuController.Update` reads `GetPauseDown`; overlay/skill-tree/modal are dispatched
  through its ordered `return` chain — one press closes exactly one layer.
- **Build-readiness audit (no player built):** Build Settings order correct (Bootstrap 0,
  Persistent, Intro, then L0–L5 + Side scenes); `SampleScene` disabled; no TestLab/Restore/Trees
  present — no change needed. All debug surfaces confirmed guarded: skill keys L/O/P/I/K
  (`SkillPointDebug`, DevConfig.Trainer), `GameDebuggerV2` Ctrl+` (DevConfig.Trainer, Awake+Update),
  `DamageDealerDebug` (DevConfig.Trainer), `TutorialDirector` Ctrl+F9 (`#if UNITY_EDITOR`). Console
  clean of errors/warnings after compile.

### Added — F5 Button HUD input prompts + F9 world-space pickup UI (2026-07-16)
- **F5 — Button HUD input prompts.** New `InputPromptView`
  (`Assets/Scripts/UI/InputPrompts/InputPromptView.cs`): a generic, reusable screen-space HUD
  element that shows the ACTUAL bound key/button for one input action, read live from the
  Input System action asset — never hard-coded, so prompts stay true under rebinding (F6).
  Resolves `IInputProvider` in `Start()` (R4) via `TwinInputReader.Instance`; fails loud +
  disables self if unresolved. `Refresh()`/`SetAction()`/`Show()`/`Hide()` API. Serves every
  contextual consumer (interact, teleport hold, attack, ability, switch, …) identically.
- **Binding resolution API.** Added `IInputProvider.GetBindingDisplay(string actionName, bool
  preferGamepad = false)` implemented on `TwinInputReader` (picks the first keyboard/mouse — or
  gamepad — binding for the action and returns `GetBindingDisplayString`) and passed through by
  `TutorialInputGate`. Reads from the serialized `PlanetOfTwins.inputactions` asset.
- **ControlHints HUD panel** built under `Persistent/HUD_Canvas/ControlHints` (5 `InputPromptView`
  rows: Switch/Attack/Ability/Teleport/Interact). Verified at runtime resolving Shift/E/Q/C/F.
- **F9 — World-space pickup UI.** New `WorldSpacePickupPrompt`
  (`Assets/Scripts/UI/WorldSpaceUI/WorldSpacePickupPrompt.cs`): area-resident, self-contained
  (no cross-scene refs, R2), billboarded (Y-lock default) world prompt over pickupable items.
  Marker mode (always-visible, hides when the pickup GameObject deactivates on consume) for
  auto-walk-over pickups; optional proximity-trigger mode and optional live key glyph
  (F5-consistent). World-space canvas uses `WorldSpaceCanvasCamera` (R9). Wired onto both
  `Melee` sword pickups in `L1_Park` with a "Pick up Sword" label.

### Added — Decorative props set (2026-07-16)
- **Nine standalone decor prefabs** under `Assets/Art/Props/Decor/` (each an INDIVIDUAL prop, not
  a connected assembly), placed as a spaced row in SampleScene from x=110, ~4m apart, under a
  `DecorProps_Row` container (scene saved):
  - `HollowLantern_A` (hollow paper body + emissive core + interior Point Light) @ (110,2.5,0)
  - `Shimenawa_A` — rope prop driven by `DecorativeRope` (Verlet LineRenderer, `_endAnchor` child,
    `_slack` 1.3, `M_Decor_Rope`) @ (114,3,0)
  - `ClothBanner_A` (hanging cloth slab, `_WindEnable=1`/`_WindMode=1` Hanging) @ (118,3,0)
  - `Ofuda_A` (small hanging paper tag, Hanging wind) @ (122,2.5,0)
  - `Ribbon_A` (thin hanging strip, Hanging wind) @ (126,3,0)
  - `StoneMonument_A` (rough slab on stepped pedestal, 72 verts) @ (130,0,0)
  - `StoneLantern_A` — NEW: tōrō, stacked stone base/post/platform/firebox/roof/finial + warm
    interior Point Light (144 verts) @ (134,0,0)
  - `LampPost_A` — NEW: wooden post + arm + hanging lantern box with Point Light (96 verts wood
    mesh + lantern box) @ (138,0,0)
  - `ToriiGate_A` — NEW: two posts + nuki/kasagi double lintel, vermilion wood (96 verts) @ (142,0,0)
- **Materials** (all `PoT/Coexistence`, `Assets/Art/Materials/Props/`): reused the prior session's
  Banner/Ofuda/Ribbon/Rope/Stone/LanternPaper/LanternCore set; **added `M_Decor_Wood` (dark brown)
  and `M_Decor_Torii` (vermilion)** for the three new props.
- Geometry follows the anti-clutter ruling (simplest boxes that read); every mesh baked to a saved
  `_Mesh.asset` via `Mesh.CombineMeshes` (no transient pb_Mesh — GPU-Resident-Drawer safe).
  Positioned capture verified with `PoTVolumetricFog` temporarily off, restored ON.

### Added — PoT/TerrainLit + terrain quality tiers (2026-07-16)
- **`PoT/TerrainLit`** (`Assets/Art/Shaders/PoTTerrain/` — `PoTTerrainLit.shader` +
  `PoTTerrainLitPasses.hlsl`, port of URP 17.3 TerrainLit): stock terrain everything
  (instancing, holes, height blend, GBuffer/Depth/Meta passes) + PoT additions behind
  RUNTIME keywords (`multi_compile` — both variants ship, options-menu switchable):
  `_POT_HEX` hex-grid tile break-up per splat layer (3 grad-sampled rotated taps, sharpened
  blend), `_POT_PARALLAX` cheap one-tap parallax from mask height (y-up approximation), and
  always-compiled **world corruption tint** (feathered noisy front from `_WorldCorruption`,
  luminance-shaped, no-op at 0). Known limit: beyond Base Map Distance the stock hidden
  basemap shader renders (no hex/corruption) — tiers keep basemapDistance ≥ zone size.
  Benchmark that sized this: sample "20×" Hex Parallax Height measured ≈ equal GPU ms to
  stock Terrain Lit on the ground-level worst case (terrain pixels are a small frame slice;
  grass details dominate).
- **`TerrainQualityService`** (`Assets/Scripts/Environment/`, Persistent singleton):
  Low/Med/High tiers → material keywords + pixel error/detail density/detail+tree
  distance/basemap distance on ALL loaded terrains; re-applies on sceneLoaded (streamed
  areas); `SetTier(int)` = the future options-menu hook. USER WIRING: add to a Persistent
  GO; assign `PoT/TerrainLit` material to each terrain.

### Added — WindSwayPivot + camera occlusion revival (2026-07-16)
- **`WindSwayPivot`** (`Assets/Scripts/Environment/`): third leg of the wind system — rigid
  pendulum sway for whole hanging objects (lanterns, signs). Rotates around the hook pivot,
  reads `WindDriver.EvaluateWind` (incl. LocalWindZone boosts), per-instance phase so rows
  never sync, scaled time (R10). Fabric = shader sway; ropes = DecorativeRope; rigid = this.
- **`CameraObstruction`** (`Assets/Scripts/Camera/`) REVIVED — the drafted-but-commented
  spherecast fader is now live code: camera→LookAt corridor spherecast; blocking renderers
  whose material has the ObstacleFadeOut `_seeThroughDistance` dither get faded via
  MaterialPropertyBlock and restored to their material default when clear (also on
  OnDisable). Camera never moves — the shot/Z/framing stay authored. WIRED: on Main Camera
  in Persistent (CM3 fix: LookAt via CinemachineVirtualCameraBase cast). Also wired the
  physical "Restore Default Keybinds" button (ExitButton clone, onClick cleared, assigned to
  PauseMenuController._restoreKeybindsButton) — closes the F7 agent's manual leftover.

### Added — RyokanInn + TwinShrine (2026-07-16)
- **`RyokanInn`** (`Assets/Art/Props/Houses/`, 768 verts, @ (120,0,45) SampleScene): two-storey
  wooden-inn read from the user's ref — dark red timber frame (posts + bands) over warm lit
  shoji-paper bodies, mid skirt roof, first-floor balcony with railing, dark lattice windows,
  top hip roof, two hanging paper lanterns. New materials `M_Coex_PaperLit`, `M_Coex_WoodRed`.
- **`TwinShrine`** (728 verts, @ (120,0,70)): 3-tier stepped stone platform, 4 timber pillars,
  back wall with the enshrined **impaled emblem (violet half + gold half)**, altar, sweeping
  hip roof, flanking stone lanterns, front steps.

### Added — FantasyHouse_Stilt + genre canon (2026-07-16)
- **`FantasyHouse_Stilt`** (`Assets/Art/Props/Houses/`, 1052 verts, 6 submeshes, @ (90,0,70)
  SampleScene): pure-fantasy stilt house from the user's Where-Winds-Meet hillside refs —
  raised wooden deck on 6 stilts, layered droopy thatch roofs (main + annex + tilted veranda
  strip), railing, round window, violet/gold trim accents, free-standing stone steps, rock
  pad. Normal-size door (oversized-door idea parked). New `M_Coex_Thatch` material.
- **game.md §1.1 NEW — Genre & tone canon:** stylized East-Asian fantasy action-adventure;
  bittersweet, never cute; 40-40-20 modern axis retired; corruption = expanding tint film;
  per-scene Terrains ruling recorded.

### Added — House direction exploration ×3 (2026-07-16)
- **`TwinHouse_Wuxia`** (`Assets/Art/Props/Houses/`, 904 verts, 6 submeshes, @ (90,0,45) in
  SampleScene) — CURRENT direction candidate per user's wuxia/fantasy references (Where Winds
  Meet, Perceiver): two-tier hipped roofs with upturned corners, stone plinth, porch columns,
  balcony rail, gold finial, **twin-scale oversized double door** framed by a violet post +
  gold post + split lintel (impaled emblem on the entrance = "twins live here"). No modern
  layer — user ruling: fantasy world + clan colours, no modernization.
- **`TwinHouse_Fantasy`** (same folder, 2240 verts, @ (90,0,20)) — cute/joyful experiment
  (sun-cap + moon-peak interlocked roofs, face-reading front). REJECTED by user ("cute isn't
  achievable/right for the genre") — kept in scene for reference, candidate for deletion.
- All three houses keep their disabled ProBuilder sources (`*_Source_PB`) for edits; 4 new
  materials `M_Coex_RoofGold/RoofViolet/CreamWall/BondSeam` in `Assets/Art/Materials/House/`.

### Added — Comparison house AccordCoexHouse_F (2026-07-16)
- **`AccordCoexHouse_F`** (`Assets/Art/Props/Houses/` — `_Mesh.asset` + `.prefab`, 984 verts,
  5 submeshes): new exterior-only comparison house, own design at ~40 JP (hipped deep-eave roof,
  engawa deck, plaster wall body) / 40 CN (raised stone plinth + step, four round pillars with
  dougong bracket caps, ridge cap beam, door + impaled emblem plaque, window slabs) / 20 modern
  (conduit seam vertical+horizontal, junction box, AC unit — the bond made physical, hottest clan
  glow). Anti-clutter rule applied (single boxes as walls). Built with ProBuilder then baked to a
  saved Mesh asset per the GPU-Resident-Drawer workflow; PB source kept disabled in SampleScene as
  `AccordCoexHouse_F_Source_PB`; prefab instance at (90,0,0) next to the old test house (untouched).
- **5 house materials** (`Assets/Art/Materials/House/`, all `PoT/Coexistence`): Plaster / Wood /
  Roof / Stone / Modern (modern = kintsugi seam, ClanIntensity 2.2 vs 0.4–0.8 elsewhere).
- **Capture gotcha found:** MCP positioned-screenshot temp cameras are `CameraType.Game`, so
  `PoTVolumetricFog` runs on them against stale edit-mode depth → whole-frame pale wash. For
  authoring captures, temporarily deactivate the PoTVolumetricFog renderer feature (restored ON).

### Added — Wind system + Persistent skybox ownership (2026-07-15)
- **`WindDriver`** (`Assets/Scripts/Environment/`, Persistent singleton, R3): one owner of ambient
  world wind. Sets shader globals `_PoTWind` (xz dir + strength) / `_PoTWindGust` (layered-sine
  gusts, SCALED time — wind slows under Setsuna in sync with the shader's `_Time` sway),
  syncs a child `WindZone` (leaves ParticleSystems use External Forces against it), and exposes
  `EvaluateWind(worldPos)` (Perlin spatial variation) for ropes. Globals zeroed in `OnDestroy`.
  Wired in Persistent: `WindDriver` GO + `WindZone` child (scene saved).
- **Vertex wind sway in `PoT/Coexistence`**: new `CoexistenceCommon.hlsl` holds the (previously
  ForwardLit-only) `UnityPerMaterial` CBUFFER + `PoTApplyWind()`, included by ALL THREE passes —
  shadows sway with the mesh and the DepthOnly prepass keeps matching under depth priming.
  Material opt-in: `_WindEnable`, `_WindMode` (Standing = base anchored / Hanging = top anchored),
  `_WindPivotY` (object-space attachment height), `_WindAmount`, `_WindResponse` (mask falloff).
  Per-object phase from world origin — rows of lanterns never swing in lockstep.
- **`DecorativeRope`** (`Assets/Scripts/Environment/`): shimenawa/bunting rope — real Verlet sim
  (gravity + `WindDriver.EvaluateWind` + distance constraints) on a mesh-free `LineRenderer`
  (same vocabulary as `ChainBeamDriver`). Pinned to this transform + an end-anchor (same scene, R2);
  `_slack` sets the droop; `GetPoint(t)` pins decorations onto the rope; `[ExecuteAlways]` draws a
  static catenary preview in edit mode. Scaled-time sim clamped at 1/30 s (R10 comment).
- **Skybox is Persistent-owned:** `WorldAmbienceDriver` gained a `_skyboxMaterial` slot (wired to
  `M_CoexistenceSkybox`) and assigns `RenderSettings.skybox` at boot **and on every
  `activeSceneChanged`** (RenderSettings belong to the active scene — SceneFlowManager swaps it
  while streaming). Area scenes no longer need any Lighting-panel skybox setup. Play-mode only —
  edit-mode OnValidate never dirties the open scene's lighting.
- **Optional authored cloud texture on `PoT/CoexistenceSkybox`**: `_CloudTex` +
  `_CloudTexInfluence` (0 = pure procedural, byte-identical) + `_CloudTexScale` — luminance blends
  into the fbm density for both the cloud body and its sun-side self-shading sample (the user's
  imported cloud PBR set is the intended source).

### Added — Story beats, local wind, area volumes (2026-07-15, second pass)
- **`SkyboxMaterialChange` generalized into the STORY BEAT TRIGGER** (user call): one
  activation-fired checkpoint object driving any combination of `_driveCorruption`
  (WorldAmbienceDriver.TransitionTo) + `_gradeId` (StoryGradeDirector.PlayGrade). `Fire()` also
  callable from code; only latches once a dial actually ran. Rename to `StoryBeatTrigger` =
  future isolated GUID-preserving commit.
- **`LocalWindZone`** (`Assets/Scripts/Environment/`): sphere where world wind blows harder
  (crack energy leaks). R5 self-registers into WindDriver; the FOUR camera-nearest zones pack
  into `_PoTWindPoints`/`_PoTWindPointRadii` globals — Coexistence sway gains amplitude + a fast
  13 Hz flutter inside; `WindDriver.EvaluateWind` (ropes) gets the same boost.
- **Area identity volumes**: `AreaIdentityVolume` GO (global, priority 10) created + saved in
  L1_Park / L2_Streets / L3_Alley / L4_MuseumStart, wired to new profiles in
  `Assets/Settings/Grading/Areas/` (WhiteBalance temp/tint + ColorAdjustments saturation only —
  contrast/bloom stay on the story/baseline layers). Starting values: Park +8/0/+6,
  Streets −6/+2/0, Alley −14/+4/−10, Museum +4/−2/−4.
- **`PoT/Coexistence` double-sided support**: `_Cull` material toggle on all three passes
  (matching — depth priming) + back-face normal flip (`SV_IsFrontFace`) in ForwardLit; both
  test materials set to Off (greybox ProBuilder meshes have mixed windings).

### Added — PoT volumetric fog, own implementation (2026-07-16)
- **`PoT/VolumetricFog`** (`Assets/Art/Shaders/PoTVolumetricFog.shader` + `M_PoTVolumetricFog.mat`
  + `PoTVolumetricFog` feature on PC_Renderer, `GameCameraFullScreenFeature`, Before-Transparents,
  Game cameras only): raymarched height fog written from scratch after STUDYING the Volumetric Fog
  (Lite) asset (Mirza Beig) — the asset itself was fully removed (not licensed for this use; only
  public-domain techniques kept: interleaved-gradient-noise jitter, Henyey-Greenstein phase,
  per-step realtime-shadow sampling). OURS ADDS: exponential HEIGHT falloff, animated 3D noise
  density drifting with `_PoTWind`, corruption stain from `_WorldCorruption`
  (pure→corrupt fog colour), start-distance guard, transmittance early-out. Dials on the
  material: density/base height/falloff/start/max, noise scale/amount/speed, sun in-scatter
  strength/anisotropy/shadowing, step count (default 24). Per-step shadow sampling = real light
  shafts through geometry. Full-res single pass; downsample+composite is the known perf path.
- `CoexistenceShafts` feature re-ENABLED by default (user call — the screen-space shafts stay as
  a second, cheaper layer). `CoexistenceFog` stays off (superseded by the volumetric + built-in).
- Overview cam cooldown restored to 4 s default (anti-spam, user call). Projectile-freeze gap
  accepted by user: an in-flight projectile lands normally during the hold; the player reacts.

### Changed — Overview cam hold-to-view + fog switched to built-in (2026-07-16)
- **Overview cam (B) reworked** (user spec): HOLD-to-view, unlimited — release returns the camera
  and resumes instantly (old 5 s timer removed; cooldown now default 0, still serialized). The
  world freeze is no longer `timeScale = 0`: enemies freeze via `TimeFactorManager.TriggerEffect()`
  (entity freeze — ongoing VFX keep playing) and player gameplay input freezes via new
  `IInputProvider.SetGameplayFrozen(bool)` (checked inside the TwinInputReader getters, same seam
  philosophy as the tutorial gate; Pause/Overview/AnySkip stay live). New
  `IInputProvider.GetOverviewHeld()` (+ TutorialInputGate passthroughs). Guard: if the soul cast
  already owns the entity freeze, overview rides along without trigger/resolve.
  `TimeFactorManager.IsEffectActive` exposed for that guard. KNOWN GAP: already-flying projectiles
  run on scaled time and keep travelling during overview — needs ITimeAffected on pooled
  projectiles (backlog).
- **Distance fog switched to Unity built-in** (user verdict: screen-space fog still ghosted faintly
  in game + "doesn't look like fog"): `WorldAmbienceDriver` now owns `RenderSettings.fog`
  (ExponentialSquared, `_fogDensity` 0.006 default) alongside the corruption-staining fogColor it
  already drove. Per-object fog — no depth-texture involvement, correct in every view.
  `CoexistenceFog` + `CoexistenceShafts` renderer features DEACTIVATED by default (assets kept —
  re-enable for hero shots once tuned).

### Added — New crack shader (2026-07-16, live session with user)
- **`PoT/CoexistenceCrack`** (`Assets/Art/Shaders/`) replaces the old `Shader Graphs/CrackGlow` on
  both crack materials (graph asset untouched — revert = reassign). OPAQUE + ZWrite On + DepthOnly
  (the old ZWrite-off ADDITIVE rig was the real blinding-blob cause — overlapping canyon walls
  summed to white and veiled the whole frame; see BUG-073 follow-up). Keeps the concept: dark at
  the ground line → hot at the canyon bottom (`_DepthRange`, `_GradientPower`, per-material), plus
  NEW: Colour Bible §7 corruption journey (Pure-Current icy blue → Khal-Vor oily green, driven by
  the `_WorldCorruption` global), clan violet/gold streaks on geometric edges, slow energy scroll.
  Verified live in play: blob gone, veil gone, see-through-ground stencil rig intact.
  Same-day extension (user asks): the CURRENT is now its own layer — `_CurrentColor` +
  `_CurrentColorCorr` (HDR, corrupt variant), `_CurrentStrength/Speed/Scale`,
  `_CurrentThreshold/Softness` (stream coverage), optional `_CurrentTex` noise texture
  (`_CurrentTexInfluence` 0 = procedural); plus `_CorruptionMax` (0 = this material never
  takes the world corruption tint).
- Fog/shafts material retune after the play session: fog density 0.006 / max 0.45 / start 25 /
  colour = sky horizon (fixes distant buildings reading as "transparent" ghosts against the crisp
  sky); shafts intensity 0.3 / threshold 0.85 / decay 0.9.
- BUG-074 logged: GraphicRaycaster NaN screen-position error spam (16×) — open, log-discipline +
  NaN source hunt pending.
- **"Transparent buildings" ROOT-CAUSED + FIXED (2026-07-16):** solid opaque buildings appeared
  see-through (back-silhouettes visible through front faces) in the SCENE VIEW — the
  CoexistenceFog fullscreen pass reconstructs world position from the camera depth texture,
  which is stale/mismatched per pixel for edit-mode Scene-view cameras, so fog painted the
  BEHIND object's depth onto the FRONT object's pixels (fake transparency; also the pale wash).
  Fix: new `GameCameraFullScreenFeature` (`Assets/Scripts/Rendering/`) — subclass of
  FullScreenPassRendererFeature that skips every camera except `CameraType.Game`; both
  CoexistenceFog + CoexistenceShafts features on PC_Renderer swapped to it (same materials/
  settings). Proven by A/B Scene-view captures over the L0_CityWater building row. In play the
  passes run as before (depth is real there).

### Fixed
- **BUG-073 crack blinding white (CrackPark)** — root cause CONFIRMED as the user suspected:
  `L1_Park → MainLvl → CrackPark → PolyShapeWall` (ProBuilder) contained **284 exact-duplicate
  faces** (unmerged-face remnants that couldn't be hand-deleted) — coplanar emissive faces
  z-fighting = blinding shimmer that changes with camera position. Removed via the ProBuilder
  API (`DeleteFaces` on duplicate-position face groups): 1912 → 1628 faces, 3811 → 3246 tris,
  3 residual dups (tolerance noise). Scene saved. NOTE: L3_Alley fbx cracks carry 4/2 duplicate
  tris inside the source FBX meshes — minor, fix in the DCC file if ever visible.
- **House "transparency" diagnosed NOT a hole**: top-down capture proves walls solid; the
  shadow side is lit only by blue sky ambient (SampleSH) while the SampleScene grading
  white-out blows everything else to white → wall matches the sky = reads as glass. Real fixes:
  finish the SampleScene grading repair, give the material a real base texture / darker base
  colour. The `_Cull` toggle above additionally covers any genuinely flipped greybox faces.

### Changed
- **`SkyboxMaterialChange` repurposed as the corruption first-enabler** (user call): it no longer
  swaps `RenderSettings.skybox`/fog (WorldAmbienceDriver owns that). The Persistent `SkyboxChanger`
  GO (inactive; activated by the intro timeline's Act 9 Activation Track) now fires ONE
  `WorldAmbienceDriver.TransitionTo(_targetProgress, _transitionSeconds)` on activation
  (defaults 0.15 over 20 s, `_onlyIncrease` guards re-activation). Old serialized
  material/fog fields are dead data. Place more instances at later story beats for later steps.

### Fixed — Playtest round-3 bug pass BUG-060…067 (2026-07-15, TestLab play-verified)
- **BUG-060/061 (stun/possess cue spins with player):** new `FxAttachMode.FollowPositionOnly`
  (appended enum value — serialization-safe): FxManager follows the target's position (world-axis
  offset) but keeps the spawn orientation for the cue's life; `faceTarget` still overrides.
  `StunCueBook.OnStun_Active` + `PossessCueBook.Possess_Active` re-set to the new mode.
  Play-verified: held cue kept its yaw while Kai spun 30°→210°, position tracked.
- **BUG-062 (melee slash never plays):** `Twins.prefab` had a null `_attackBook` on BOTH twins (prefab
  never re-saved after the slot replaced the old `_slashPrefab`/`_hitPrefab` fields — those remain as
  dead serialized lines). AttackCueBook assigned on both PlayerAttackControllers. Play-verified.
- **BUG-063 (melee on spawn point does nothing):** `SpawnZone.prefab` root had no collider, layer
  Default and no IDamageable. Now: layer Enemy + BoxCollider (from renderer bounds) +
  `PoiDamageAdapter`. Needs one in-game swing to confirm.
- **BUG-064 (no teleport marker during hold):** the dispatcher calls `ShowTeleportPreview()` every held
  frame and `TeleportMarkerPreview.Show()` restarted the held castmark cue each call, clearing its
  particles every frame. `Show()` is now idempotent while the preview cue is live. Not a prefab
  lifetime issue — no 0.1 s change needed.
- **BUG-066 (RadiantSeeker orb never arrives):** the orb prefab's NavMeshAgent radius (1.75) parks it
  at radius-sum distance (~2.25 m) with stoppingDistance 0 — arrival check never fired. Arrival is now
  proximity-based: `max(possessionRadius × 0.95, agent.radius + 0.75)`; NavMesh arrival kept as
  fallback. Play-verified (detonation + possession).
- **BUG-067 (double orb on cast):** `radorb_cast` pointed at the VisualEffect on the ORB PREFAB ROOT —
  every cast pooled a 1 s visual copy of the orb ("the one that disables"). Element emptied;
  **authoring follow-up: radorb_cast needs a real cast-burst prefab** (cast is currently visual-silent).
- **BUG-065 (Coalesce aura invisible):** could NOT repro — full real path play-verified working
  (stun → events → pooled aura → held Star-aura cue). Likely field cause = Coalesce not unlocked in
  that save (`[Coalesce] BLOCKED` log). Hardened: `CoalesceAura` now LogErrors when its cue book
  resolves null (was silent). Status → Watch.
- **BUG-068 (Manpu under-firing):** audited, root causes are authoring: `loopPrefab` empty on all 13
  mood rows (the sustained aura channel plays NOTHING — dominant cause), Contemptuous/Territorial rows
  fully empty with escalatingOnly, perception rows share one placeholder sprite. Gating (R1/R2) is
  healthy. Stays Open pending vocabulary authoring.

### Fixed — Playtest follow-ups BUG-069…072 + Manpu editor gap (2026-07-15)
- **BUG-069:** `TimeFactorManager.Register` applies an in-progress freeze/slow to newly registered
  entities — enemies spawned (or pool-reused) during the soul cast now join the effect immediately.
- **BUG-070:** enemy spawns scatter in a NavMesh-sampled ring (`_spawnScatterRadius`, default 2 m)
  around the spawn point instead of materialising exactly ON it (spawn-VFX mismatch).
- **BUG-071:** `FadeController` re-activates the FadeCanvas GO on any fade request (Timeline Activation
  track could leave it inactive → "Coroutine couldn't be started" every later fade); skipped fades
  still fire their onComplete. Only one FadeCanvas exists (Persistent).
- **BUG-072 (crack blinding-white glow):** `DefaultVolumeProfile` had Tonemapping actively overridden
  to **None** — area scenes rendered the crack's HDR emission (~12) unclamped → white-out that swam
  with the camera. Default profile now ACES. User tuning still recommended: crack `_EmissionColor`
  intensity ~3–5, bloom threshold 1.1–1.3.
- **ManpuVocabularyEditor now draws the mood `loopPrefab` ("Loop aura") field** — it existed in data
  since P11 but the custom inspector never rendered it, which is why the aura channel was never
  authored (BUG-068's dominant cause). Channel (Both/SpriteOnly/ParticleOnly) was already drawn inside
  each Glyph foldout — no gap there.

### Added — Sun shafts (god rays) (2026-07-15)
- **`PoT/SunShafts`** + `M_SunShafts.mat` + **CoexistenceShafts** FullScreenPassRendererFeature on
  PC_Renderer (after CoexistenceFog): screen-space radial gather toward the sun's screen position —
  bright SKY pixels only (depth-masked, geometry occludes), per-step decay, moving clouds crossing
  the sun modulate the beams naturally. Dims with `_WorldCorruption` (`_CorrDim`).
  **`SunShaftsDriver`** (Persistent, material wired) feeds `_SunUV` + `_SunVisibility` per frame
  from `RenderSettings.sun` via `Camera.main` (switch-proof, edge-fade so shafts never smear from
  an off-screen sun). VISUAL TUNING PENDING — SampleScene play view is washed white by the
  pre-existing grading issue (confirmed NOT the new features: identical frame with both disabled);
  verify shafts in L1_Park after the grading/bloom tuning pass. instruction.md §19 gained
  F13 (both-twin ability cam + split beat) and F14 (shafts tuning + prop wind pass); F12 marked
  substantially landed as PoT/Coexistence.

### Added — Skybox spread controls + CoexistenceFog render feature (2026-07-15)
- **Skybox tint spread (house-shader parity, user call):** the corruption stain now has
  `_CorrDistribution` {Uniform / FromCentreDir / LeftRight / TopDown} + `_CorrReverse` +
  `_CorrCenterDir` + `_CorrSpreadFeather` — the stain STARTS where authored (aim FromCentreDir at
  the crack) and expands across the dome with a wide feathered front, still as a tint. Sun size
  range widened to 0.5.
- **`PoT/CoexistenceFog`** + `M_CoexistenceFog.mat` + a **FullScreenPassRendererFeature
  ("CoexistenceFog")** added to PC_Renderer (Before Post Processing, fetch color, Depth
  requirement): two-tone clan distance fog — base fog colour tinted Vethara-violet on one side of a
  world axis, Luminari-gold on the other (`_FogAxis`, `_ClanTint`), exponential distance falloff
  (`_FogDensity`/`_FogStart`/`_FogMax`), skybox pixels skipped (the sky owns its haze), and the same
  `_WorldCorruption`-driven stain as every Coexistence shader. Verified working in PLAY mode (the
  edit-mode temp-camera depth path is unreliable — judge fog in play only). Defaults left subtle.

### Added — Skybox v2 (tint model) + WorldAmbienceDriver + HitStopService (2026-07-15)
- **Skybox v2 REWRITE (user verdict on v1: "doesn't feel like a sky; corruption blindsides it"):**
  `PoT/CoexistenceSkybox` is now a believable happy sky — planar-projected domain-warped fbm cloud
  layer (perspective compression toward the horizon), self-shaded billows, thin clan haze trails at
  the horizon, sun disc with a clan-split halo (gold sun-side / violet shadow-side). Clan colours on
  clouds live ONLY on the silhouette edge band (kintsugi rule, same as the house shader). Corruption
  is a progressive uneven TINT (clouds stain harder than the gradient; sun dims) — never a front/wall.
  Verified at w=0 / 0.5 / 1.0 (Captures/skybox_v2_*.png).
- **`WorldAmbienceDriver`** (`Assets/Scripts/Grading/WorldAmbienceDriver.cs`, Persistent GO, R3):
  the ONE runtime owner of the `_WorldCorruption` story dial. `SetProgress(0..1)` /
  `TransitionTo(target, seconds)` (unscaled) drive together: the shader global (surfaces + skybox),
  `RenderSettings.fogColor` (distance fog stains pure→corrupt), and optionally
  `StoryGradeDirector.SetStoryProgress` (toggle off by default; slot wired). Inspector slider
  previews in edit mode; global reset to 0 on destroy (Restart safety).
- **`HitStopService`** (`Assets/Scripts/SceneLaoder/HitStopService.cs`, Persistent GO): micro
  time-freeze on impact — a new OWNER of TimeScaleService (R10, min-value-wins). `Punch()` default
  0.05× for 0.06s unscaled; overlapping punches extend the window. First wired consumer: melee —
  `MeleeAttackStrategy` punches once per swing that CONNECTS (never on whiffs). Ability hits /
  finishers pass custom strength via `Punch(scale, duration)`.
- **Cue-book hit-stop (user call):** `CueElement.CameraCue` gains `useHitStop` + `hitStopScale` +
  `hitStopDuration` — author the freeze on the IMPACT element of any effect, next to shake/depth.
  Fired in `FxManager.PlayElement` through the new **`FxManager.HitStopHook`** static seam (P19 seam
  3 — the Fx package can't reference the game's HitStopService; HitStopService registers the hook in
  Awake, clears it in OnDestroy; unregistered = silent no-op so the package still compiles alone).

### Added — Coexistence SKYBOX (2026-07-15, EXPERIMENTAL — not finalized)
- **`PoT/CoexistenceSkybox`** (`Assets/Art/Shaders/CoexistenceSkybox.shader`) + `M_CoexistenceSkybox.mat`
  — the sky as a story dial, sharing the Coexistence surface shader's grammar. **Celebration (Day of
  Accord):** gradient sky + two azimuth-stretched fbm ribbon-trail layers tinted Vethara violet /
  Luminari gold drifting in opposite directions, confined to a horizon band; optional sun disc from the
  main light. **Corruption:** driven by the SAME `Shader.SetGlobalFloat("_WorldCorruption", 0..1)`
  global (outside the CBUFFER) — a noisy feathered front expands angularly from `_CorrCenterDir` (aim
  it at the crack), swaps the gradient to the corruption palette, glows at the front edge, and eats the
  sun. Hard gate `corr *= saturate(w*12)` stops the front glow leaking at w=0 (caught in the first
  render). Assigned as SampleScene's skybox for testing; verified at w=0 and w=0.55 (front boundary
  screenshot in `Captures/`). Corruption colours = Voreth wild placeholder — validate vs Colour Bible.

### Added — Coexistence shader + fused-house design TEST (2026-07-14, EXPERIMENTAL — not finalized)
- **`PoT/Coexistence` shader** (`Assets/Art/Shaders/Coexistence.shader`) — ONE generic URP shader for
  the whole world's "coexists as two" look (corrected leaf model, v3). **Base** = the object's own
  albedo + optional texture, DOMINANT (keeps identity — a house looks like a house). **Clan energy** =
  Vethara **violet** (A) + Luminari **gold** (B) shown ONLY on the object's **energy edges** —
  silhouette (fresnel) + creases (`fwidth` of normal) + an optional **leaf-vein mask** — split
  **half/half** by a `Distribution` mode {LeftRight, TopDown, Radial, +Reverse}; `GlowMode` toggle
  (additive glow vs flat colour). **Corruption** = a GLOBAL `_WorldCorruption` float (0..1, meant to be
  driven by story progress like the crack `_Corruption`/StoryGradeDirector) that tints every coexistence
  object's base toward `_CorruptionColor` — how far corruption has taken the world. `_Grayscale` uniform
  = silhouette test. ForwardLit + ShadowCaster + DepthOnly, SRP-batcher CBUFFER (global kept out of it).
  0 compile errors, `isSupported`. Material: `Assets/Art/Materials/M_Coexistence_Test.mat`.
  (Superseded interim v1 full-surface marble + v2 base-with-clan — both washed the object identity; the
  correct model per the user's leaf spec is base-dominant, clan on edges/veins only.)
- **v4 (2026-07-15):** Radial split FIXED — remapped by `_RadialInner`/`_RadialOuter` radii (v3's
  `length×scale` saturated to a single colour on building-sized meshes). **Corruption sweep** — the
  global tint now has its own distribution {Uniform, LeftRight, TopDown, Radial, +Reverse} with a
  feathered, noise-broken front (`_CorrFeather`/`_CorrNoiseScale`/`_CorrNoiseAmount`) + optional
  `_CorrMap` pattern texture, so corruption CREEPS across an object dissolve-style instead of fading
  uniformly. **Edge detection boosted** — crease term (normal-derivative, the toon-outline signal) got
  `_CreaseSharp` shaping and a 0–30 strength ceiling so hard edges actually light. BUG-060…068 logged
  from the user's Bootstrap playtest (BUGS.md).
- **`AccordCoexHouse_Test`** greybox (`Assets/Art/Props/Houses/AccordCoexHouse_Test.prefab` +
  `_Mesh.asset`, 1008 verts, baked single MeshRenderer) — the JP+CN+modern fusion test surface: JP
  hipped (yosemune) deep-eave roof + plaster body + engawa deck; CN round pillars + dougong caps +
  lattice windows + door with clan-plaque placeholder; modern conduit/pipe/AC seam (= the bond made
  physical). Placed in `SampleScene` at world (60,0,0), untextured coexistence material applied.
- **Purpose:** validate the world-design language before authoring. Result: silhouette reads as one
  coherent pavilion; bond reads in colour AND grayscale. These are throwaway TEST assets pending user
  sign-off on the design; not wired into any gameplay scene.

### Fixed — Manpu glyph: particle-only playback + per-trigger channel toggle (2026-07-14)
- **A glyph can now play its particle without a sprite.** `ManpuVocabulary.GlyphStyle.HasVisual` was
  `sprite != null`, so `ManpuSlot` treated any row without a sprite as "no glyph" and never fired —
  a `burstPrefab` with no sprite showed **nothing** (the P11 sprite de-gate never covered the glyph
  pulse's burst). `HasVisual` is now `PlaySprite || PlayParticle`, so an authored particle alone
  triggers the pulse.
- **New `channel` toggle on every glyph — `Both` (default) / `SpriteOnly` / `ParticleOnly`.** Lets one
  trigger show just the sprite, just the particle, or both, even when both are authored. `Both` is the
  previous behaviour, so existing vocabularies are unchanged. Appears automatically in the
  `ManpuVocabulary` inspector (the editor draws the glyph with include-children `PropertyField`).
- `ManpuGlyph.Begin` disables the `SpriteRenderer` when `!PlaySprite` (ParticleOnly hides the image but
  the particle still plays); `PlayAccents` gates the burst on `PlayParticle`. `ManpuSlot` unchanged (its
  six `HasVisual` gates now mean "has a sprite OR a particle to show"). The mood **aura** `loopPrefab`
  was already sprite-independent. 0 compile errors.

### Changed — Content-completeness audit + stale-checklist correction (2026-07-13)
- **Fresh AssetDatabase audit** of the enemy/spawn/Manpu authoring surface; corrected the stale
  `instruction.md` §18 in-editor checklist (an earlier pass reported false gaps by searching wrong
  ids/fields). **Verified DONE (were wrongly flagged open):** corruption state cue is wired on all
  13 `EnemyDarkEnergy` prefabs as `_corruptionStateBook = CommonCueBook` / `_corruptionStateCueId =
  poi_corrupt` (reuses Common — no separate `CorruptionStateCueBook`); `ManpuVocabulary` mood glyphs
  + burst accents authored (10/13 moods) + perception glyphs (4/5); ranged projectiles wired on
  `EnemyData` (`E_RangedData → Arrow`, `B_RangedData → tahrArrow`, `useProjectile=true`). **Real
  remaining content gaps recorded:** enemy pair configs (2 of ~6+; only `MeleePairConfig` +
  `SeveredConfig`, used only in `streetZone`); `L3_Alley` + `L4_Mueseum` zone configs have 0 enemy
  entries; commander/Penitent/Boss archetypes are log-only stubs; Manpu sustained `loopPrefab` auras
  (0/13) + ability glyph layer (0/4) unauthored (optional polish). **Combo/pact system: decision
  layer DONE (9 `ProximityPowerProfile` assets / 25 combo powers, dark-energy-gated pact formation +
  condition-based selection, all prefabs wired), but `BTActionComboAttack` execution is 23 `Debug.Log`
  stubs — enemies pick the right combo and do nothing.** Snapshot lives in instruction.md §18
  (`CONTENT COMPLETENESS SNAPSHOT — 2026-07-13`). No code changed.

### Added — Scene Health Dashboard "not-required" waivers (2026-07-13)
- **Mark any finding not-required (suppress false positives without hiding them).** Backdrop areas
  (L0_Water, L1_Side) legitimately fail entrance/adjacency recipes; the dashboard now lets you accept
  a finding so it stops colouring the cell red/yellow while staying visible. `ValidationFinding` gains
  `Waived` + `WaiverReason`; `RecipeResult.Status` excludes waived findings (a recipe whose only issues
  are waived reads green), and the cell E/W badge counts active findings only. New
  `SceneHealthWaivers` store persists waivers as JSON under `ProjectSettings/PoTSceneHealthWaivers.json`
  (committable team knowledge), keyed by scope (scene path / `PROJECT`) + recipe + message so a waiver
  survives re-scans. Detail pane: **"Not required"** button on each finding → demotes it to a neutral
  white info line reading **"✓ Marked not-required"** with an editable **reason** field (saved live) and
  an **Un-waive** button; a **"Show not-required"** toolbar toggle (default on) can collapse them; the
  recipe header shows a `(N not-required)` count. Files:
  [SceneHealthWaivers.cs](Assets/Scripts/Editor/Validation/SceneHealthWaivers.cs),
  [ValidationCore.cs](Assets/Scripts/Editor/Validation/ValidationCore.cs),
  [SceneHealthRules.cs](Assets/Scripts/Editor/Validation/SceneHealthRules.cs),
  [SceneHealthDashboardWindow.cs](Assets/Scripts/Editor/Validation/SceneHealthDashboardWindow.cs).

### Added — GameDebuggerV2 melee bench (2026-07-13)
- **Melee weapon toggle + per-twin slash buttons.** TestLab has no `SwordPickup`, so the twins
  woke weaponless (`PlayerAttackController._hasWeapon == false`) → `PerformAttack` no-ops → E did
  nothing and the per-twin slash cues (`on_meleeSlashKai`/`on_meleeSlashLyra`) never fired, so the
  slashes were untestable. New global-row **"Melee weapon (E swings + slashes)"** toggle grants the
  weapon on both twins exactly as `SwordPickup`/`SoftResetController` do (`SetHasWeapon` →
  each twin activates its own sword GO, R1), re-enabling the REAL E-input path (dispatcher →
  `PerformAttack` → attack anim → `OnAttackHitFrame` event → `ExecuteHitDetection` →
  `MeleeAttackStrategy` → slash + hit cues). **Slash L (Lyra) / Slash R (Kai) / Slash both**
  buttons drive `PerformAttack` directly (auto-grant the weapon) for isolated single-twin slash
  testing — each controller's own `isKai` still picks electro=Kai / stone=Lyra. `SetTwinsWeapon`/
  `Slash` use `GetComponentInChildren<PlayerAttackController>(true)` (mirrors `SoftResetController`).

### Fixed — Playtest round 2, batches 1–3 (2026-07-11)
- **poi_feed stream direction flipped** — element `localRotation.y = 180` in CommonCueBook
  (prefab streams opposite its authored forward); **poi_corrupt aura raised** to
  `localOffset.y = 1` (was sitting in the ground).
- **Warden (GroupGrab) absorb** — `On_wardenGrabSoulConsume` element: `localOffset.y = 1.2`
  (fire position up) + `localScale = 0.7` uniform (smaller radius/range). New
  `Enemy.SuppressBasicAttackCues` virtual (default false); GroupGrab overrides with
  `_isGrabbing` — the slash cue (`Enemy.PlayMeleeAttackCue`) AND the shared `on_hiteffect`
  spark (`EnemyAttackController.ExecuteHitDetection`) are muted during a grab: the warden is
  absorbing, not slashing. Gameplay damage unchanged.
- **arrow_Head cue now follows the arrow** — ArrowCueBook element attachMode `FromPrefab → Follow`
  (world-sim prefab degraded to World anchor: the known FxAttachMode failure class).
- **Ranged muzzle clearance** — `EnemyAttackController.FireProjectile` spawns the arrow
  `_muzzleClearance` (0.75 m, serialized) forward of the fire point so it never starts inside the
  enemy's own collider ("stuck arrows"). Muzzle-flash note: the plumbing already plays
  `On_RangedAttack`/`On_SiphonRangedAttack`/`On_smmAttack` at the fire point — author a flash
  element into those ids.
- **GameDebuggerV2 additions** — dark-energy section resolves `EnemyDarkEnergy` via
  `GetComponentInChildren` (root-only lookup hid the slider) + shows a why-absent label;
  **UNFREEZE all** button; **Soul→pad** raw SoulPlayer activation (SiphonGhost benching);
  **POI bench**: Spawn point / Ritual / Barrier POI spawn buttons (optional prefab slots,
  greybox primitive fallback with real components) + per-spawn-point **Recharge in 10s**
  (`SpawnPointPOI.DebugSetRechargeRemaining` — coroutine now takes a remaining-time overload,
  RechargeProgress back-dated so the ramp reads correctly). New `PoiDamageAdapter`
  (IDamageable → `SpawnPointPOI.TakeDamage`) so twin melee can actually damage spawn points.
- **Chain marker (TetherBreaker + SiphonGhost)** — the "old pink ring" was the `GroundMark`
  disc INSIDE ChainProjectile.prefab (riding the chain = "marker moves with the chain"): now
  inactive. Marker cue reveal is synced to THIS throw's windup:
  new `FxManager.FindOnInstance<T>(CueHandle)` (walks book-runner element handles — new
  `CueBookRunner.Handles`) lets `ChainProjectile.Launch` drive the marker's
  `MaterialRevealDriver.Reveal(0,1,windup)` — fully revealed = the chain throws.
  TargetMark.prefab gained a `RevealDisc` child (Quad + `reveal.mat` `_val` + MaterialRevealDriver)
  as the reveal visual (greybox — swap the quad/texture for the final SDF marker art).
  Chain glow Z: verified already updated per-frame during pull (`ChainGlowDriver.SetSpan` from
  `UpdateChainLine` in the FollowPlayer loop) — retest after the GroundMark fix; if it still reads
  static the cause is inside the ChainGlowFx prefab (particle lifetime), not code.

### Fixed — Playtest round 2, batch 4: Summoner/Witness spawn language + bomb FX (2026-07-11)
- **Summoned/ritual minions no longer play the generic `on_enemyspawn`** — the summoner's
  circle / Witness ritual IS their spawn tell. New `playSpawnCue` flag threaded through
  `Enemy.SetPoolProvider` → `EnemyPool.SpawnReady` → `EnemySpawner.SummonerSpawn` (false) and
  `WitnessEnemy.SummonAlly` (false); the reveal-delay hide is skipped with it (minions appear
  immediately at the circle). All other spawn paths unchanged (default true). This also explains
  the "witness self spawn effect incorrect" report: the initial ritual ally's on_enemyspawn fired
  one frame after (and on top of) the fresh Witness.
- **Summon circle auto-stops** — `SummonerEnemy.TriggerSummon` now holds the `On_smnSummon`
  handle and stops it when `SummonRoutine` completes (pool-return `StopAllOn` remains the
  mid-channel safety net).
- **Summoner spawns nothing — two causes fixed**: (1) it hard-required a scene `EnemySpawner`
  (absent in TestLab/direct-play) — now falls back to the canonical `EnemyPool.SpawnReady`;
  a missing `SummonerEnemyData.summonEntry.prefab` now LogErrors instead of silently skipping.
  (2) `OnMinionDied` had NO caller — `_activeMinionCount` only ever incremented, so after
  `maxMinions` summons `CanSummon` was false forever. New self-unsubscribing minion-death
  handler (`TrackMinion`) decrements it. `SummonerSpawn` now returns the spawned instance.
- **Witness/Siphon bomb FX "invisible"** — live-verified the cue path renders (played
  `On_WitnessBombExplode` through FxManager in play mode — fully visible), then root-caused the
  books: both FUSE elements were `FromPrefab` with world-sim prefabs → degraded to World, so the
  fuse FX sat at the throw point behind the enemy instead of riding the rolling bomb → now
  `Follow`. Both EXPLOSION "body" prefabs are authored looping with no duration override →
  FxManager held them forever (confirmed a minutes-old instance still looping in the pool):
  explode elements now carry `duration: 1.2` so the loop auto-stops. WitnessBombCueBook +
  SiphonBombCueBook.

### Fixed — Manpu director stray-cleanup (BUG-059b, 2026-07-12)
- **Removed 1–2 stray `ManpuDirector` components** that a prior failed YAML injection had scattered
  onto random child GameObjects (ManpuGlyph, CanvasEnemyUI, Fill, HealthBarPanel, HealthDisplayText,
  Background) of all 9 mood-bearing enemy prefabs. They were inert (no `EnemyMoodSystem` on their host
  → self-wire resolved to null) but cluttered the prefabs and confused authoring (an empty director on
  the glyph child looked like the real one). Each prefab always had the one valid director on the root
  — that is untouched and is what drives manpu. Now exactly 1 director per prefab, on the root; console
  clean. Done via `PrefabUtility.LoadPrefabContents`/`DestroyImmediate`/`SaveAsPrefabAsset`.

### Fixed — Playtest round 3e: Manpu never displayed — ManpuDirector missing from every enemy prefab (2026-07-11)
- **ROOT CAUSE of "no Manpu ever shows" (BUG-059):** `ManpuDirector` is the ONLY thing that
  subscribes `EnemyMoodSystem.OnMoodChanged`/`PoTPerceptionMemory.OnSearchStateChanged` and forwards
  them to the `ManpuSlot` — and it was on ZERO enemy prefabs. Moods changed (logs confirm), but
  nothing was listening, so neither the sprite glyph NOR the burst-particle accent ever fired. This
  is why it "used to work as particles" too — the display path is director→slot regardless of
  whether the payload is a sprite or a particle. Added a self-wiring `ManpuDirector` to the mood-
  system host GameObject of all 9 mood-bearing enemy prefabs (SiphonGhost has no mood system, by
  design) via the prefab API. The sprite path itself was verified sound: glyphs import as Sprites,
  `ManpuSlot._vocabulary`/`_glyph` are wired, and a `UIBillboard` already faces the glyph at camera.
  So no move to world-space Canvas is required — the SpriteRenderer + billboard is correct; only the
  subscriber was missing.

### Fixed — Playtest round 3d: accord-bar regression + Manpu triage (2026-07-11)
- **Accord bar stopped filling — regression from the BUG-058 fix, same session:** `ResetToFull()`
  ran inside `Enemy.ResetForPool`, which executes INSIDE the OnDeath event (HandleDeath → pool
  Return is the FIRST subscriber). It reset `LastDamageType` to Environmental before
  `EnemyDeathNotifier`'s handler (a LATER subscriber) read it — every kill classified as a zone
  despawn: no `OnEnemyDied` (accord charge, souls), no `OnEnemyCombatKill` (death helix). Health
  reset moved to ISSUE time (`EnemyPool.Get`), out of the death window; `ResetForPool` no longer
  touches health.
- **Manpu reactions dead for whole sessions (init order):** `ManpuReactionListener` resolved
  `EnemyDeathNotifier.Instance` in `OnEnable` — which can run before the notifier's Awake during
  Persistent's own load (Unity is per-object Awake→OnEnable) — and permanently self-disabled.
  Now retries in `Start` (R8) and only fails loud there.
- **Manpu "not showing" triage (data, user-side):** (1) *enemy spotted player* = PERCEPTION glyphs
  — all 5 perception rows in `ManpuVocabulary.asset` have NO sprite assigned, so R3 gates them
  out; assign sprites to the perception states. (2) Mood bench pulses: with sprites now on nearly
  ALL moods, the `escalatingOnly` gate (R2: skip curated→curated drift) suppresses every
  transition between two sprite-bearing moods — untick `escalatingOnly` on rows that should pulse
  on every entry (exposed in the Vocabulary inspector). (3) *Dark energy* visuals are latch-only
  by design: POI-buff aura at the low threshold, corruption aura + Aggressive mood at bond-break —
  below threshold silence is correct; the corruption book IS assigned (poi_corrupt).

### Fixed — Playtest round 3c: unkillable pooled enemies, dead asset refs, SoulConv shield/souls, seeker orb (2026-07-11)
- **Reused enemies unkillable + "killed enemy stuck on screen" (BUG-058):** `EnemyHealthComponent`
  initialised health only in `Awake` — a pooled reuse of a killed enemy spawned with 0 HP, so
  `IsDead=true` made `TakeDamage` a no-op: an immune, undying body standing in the scene (both
  reported symptoms are this one bug; only spawn paths passing an `EnemyData` were healed by
  `ApplyData`'s SetMaxHealth — the Witness melee minion passes none). New
  `EnemyHealthComponent.ResetToFull()` called from `Enemy.ResetForPool()` — every reuse starts
  alive at max; ApplyData still refines the max after.
- **RadiantSeeker orb reference repaired:** `AccordStateSystem._seekerOrbPrefab` in Persistent
  pointed at a deleted prefab guid (the orb prefab was rebuilt — same class as the bombs,
  BUG-053). Re-pointed to the current `RadiantSeekerOrb.prefab` root.
- **SoulConvergence old shield removed:** the gameplay `_shieldPrefab` slot still spawned the old
  full-visual `Shield01_Purple 1` on top of the new per-twin cue shields (SheildKai/ShieldLyra).
  Disabled its MeshRenderer + Point Light in the prefab — collider-only now, as designed.
- **PlayerVfxLibrary.AccordMelee slot deleted** (pointed at a deleted book since the accord-melee
  consolidation into AttackCueBook; nothing read it).
- **GameDebuggerV2:** the cooldown button became "Make abilities READY (cooldowns + SoulConv
  souls)" — also fills the Soul Convergence counter to cap via new
  `SoulConvergenceSystem.DebugFillSouls()` (skill-unlock and rescue gating stay real).

### Fixed — Playtest round 3b: stuck arrows (pool-return abort) + enemy pool tint reset (2026-07-11)
- **Stuck arrows root-caused live (BUG-056)** — paused-game forensics: stuck arrows had
  `_hasHit=true` while still ACTIVE with `InPool=false`, i.e. the hit ran but the pool return
  aborted halfway (the head/trail cues kept following the frozen arrow = the "hit fx at the enemy
  hand"). One thrown exception anywhere in the return chain left the instance live and half-reset.
  Hardened every layer so a throw can no longer abort a return: `FxManager.Stop` try/finally
  (always reclaims the registry slot, logs the offender), `ActiveBook.Stop` null-safe runner,
  `GameplayPool.Return` per-poolable try/catch (always deactivates + reparents + `InPool=true`),
  `EnemyPool.Return` same treatment, and `Arrow.OnDespawned` resets state BEFORE stopping cues.
  The offender's exception now logs loudly instead of manifesting as a floating arrow.
- **BUG-056 SOLVED (via the hit instrumentation — paired identical hit logs):** a twin's multiple
  colliders dispatch TWO `OnTriggerEnter`s per arrow in one physics pass; the second ran on the
  already-returned instance, dealt double damage, and re-armed `_hasHit=true` inside the free
  queue — the arrow's NEXT use spawned permanently frozen at the muzzle ("arrow/impact stuck at
  the enemy hand"). Fix: trigger inert on inactive instances + `_hasHit` reset in `OnSpawned`.
  Earlier same-day hardening kept: instrumented hit/return logs, try/finally pool return,
  `FxManager.Stop` try/finally-reclaim, per-step try/catch in both pools' Return.
- **Slash direction (round-2 item E, was missed):** the slash cues were correctly twin-aligned —
  the art itself sprayed sideways: Sparks + Flash shape modules in `KaiMeleeSlash`/`LyraMeleeSlash`
  were authored at Y=148.9°. Zeroed to fire front (×2 per prefab). The Electro arc stays at
  Y=180 (arc sweep authoring — retune with the new asset pass if it still reads backwards).
- **GameDebuggerV2:** Meta section gained "Refresh ability cooldowns (both twins)"
  (`AbilityBase.DebugClearCooldown` + `AbilityController.DebugClearCooldowns` bench seams) —
  ability gating itself (e.g. the Gate's rescue requirement) stays real.
- **Enemy pool tint reset (BUG-057)** — the remembered "possessed material stays" class:
  `Enemy.ResetForPool` never restored `_renderer.material.color`, so an enemy killed mid-state
  respawned tinted (stun cyan, possess purple, Witness ritual, Penitent crush/reflect/rage,
  TetherBreaker rage — all write the same base renderer against `_originalColor`). ResetForPool
  now restores the authored color; one generic line covers every archetype.

### Fixed — Playtest round 3: bomb prefab refs, VFX-graph one-shot leak, marker spam, travel easing, circle linger (2026-07-11)
- **Witness/Siphon bombs spawned NOTHING (BUG-053):** the rebuilt `WitnessBomb.prefab`/
  `SiphonPanicBomb.prefab` regenerated every internal fileID, silently breaking the
  `_bombPrefab` references on `SmartEnemyWitness`/`SmartEnemySiphon` (old root fileIDs no longer
  exist → slot reads null → `CanThrowBomb` false, no error). Repaired both references to the new
  root ids; `WitnessEnemy.ThrowBomb` + `SiphonEnemy.SpawnPanicBomb` now `LogError` on an
  unassigned bomb slot instead of silently doing nothing.
- **HitVfx instances accumulated forever (BUG-054):** `FxManager.SpawnVfx` had NO duration path —
  every VFX-graph element was Pattern-B held-until-Stop, so fire-and-forget graph one-shots
  (`on_meleeHit`, `On_AccordMeleeHit`, `on_hiteffect` → `HItVfx`) leaked an instance per hit.
  `SpawnVfx` now takes an explicit lifetime (element `duration` > 0 = auto-return; 0 = held as
  before); books patched: AttackCueBook hit ids + Common `on_hiteffect` 0.6 s, RadiantSeeker
  `radorb_cast` 1 s, SpawnPoint `spawn_hit` 0.6 s / `spawn_disable` 1 s. `kill_seq`'s already-
  authored durations (1.1/0.6) now actually apply.
- **Teleport marker spam leak (BUG-055):** `TeleportMarkerPreview.Show()` now refuses to open
  while the gate is active (soul already sent) and stops any prior held preview handle before
  playing a new one (a second Show without Hide orphaned the first castmark forever); `OnDisable`
  also stops the handle.
- **Travel easing (user spec, Kiriko feel):** new shared `TravelEase` velocity profile — speed
  ramps 0→max over the first 15% of the route on a steepening x² curve, full speed through the
  middle, falls max→0 over the last 10% on a steepening √x curve (0.12 floor so endpoints never
  stall). Applied to the gate soul flight (`TeleportAbility.TravelToTarget`, helix follows the
  soul so it inherits the easing) and to the death helix (`CharacterHelixDriver` life advance,
  normalized by `TravelEase.AverageMultiplier` so the authored duration stays the true total).
- **Summon/ritual circle linger:** the Summoner's summon circle and the Witness's ritual circle
  now linger 0.75 s (scaled) after the summoned enemy lands before stopping, so the player reads
  whose power spawned it (instant stop was too abrupt). Interrupt/death paths still stop
  immediately (Witness `finally`, pool `StopAllOn`).
- **Bomb fuse anchor:** `BombProjectile` gained a serialized `_fuseAnchor` slot — the fuse cue now
  Follows the rope tip instead of the bomb's pivot; wired to the `BombRope` child in
  `WitnessBomb.prefab` + `SiphonPanicBomb.prefab` (null anchor still falls back to the root).
- **GameDebuggerV2:** fire-any-cue section gained an `emitterScale ×0.5–4` slider so
  `CueScalableEmitter` plumbing can be eyeballed on any cue (at ×1 nothing changes by design —
  authored base = base gameplay radius).

### Fixed — Playtest round 2, batches 5–7: Weaver's Gate sequencing + helix rework + emitter sizes (2026-07-11)
- **Weaver's Gate now follows the full sequencing contract** (`TeleportAbility` restructured into
  `CastSequence`/`ReturnSequence` coroutines): teleport-OUT plays alone (0.25 s unscaled beat) →
  travel helix departs → arrival stops helix + landing telegraph → teleport-IN → the soul appears
  ONLY 0.2 s later (reveal masked under the burst) → then pulse/timer/cancel window begin.
  Reverse choreography on cancel/timeout: teleport-OUT at the soul's CURRENT position → helix
  travels back → teleport-IN at the casting twin → only then can the player move (both twins are
  movement-locked for the return; re-entrancy safe — a stale lock is force-released on recast).
- **Soul no longer spawns half in the ground** — new `SnapToGround` raycasts the landing point and
  stands the CharacterController on it (marker Y rode the caster's ground plane; the soul's pivot
  is mid-body).
- **Landing telegraph (`tele_castmark`) is hold-safe** — the cast plays it as a HELD handle stopped
  on arrival/End (a looping mark was held forever by FxManager = "marker not going away");
  its element is now `Follow` so the aim preview rides the marker object.
- **Travel helix (`tele_casttravel`) actually plays and travels** — the book's travel elements were
  `World`-forced while code passed Follow (the helix never left its spawn point), AND the two
  `HelixFollower` orbs (A/B, 0°/180°) were never driven. Now: elements stay World, `TeleportAbility`
  wires both followers per cast via new `FxManager.FindAllOnInstance<T>` (endpoints = this trip's
  start→end) and drives their progress from the soul's actual travel fraction (`TravelToTarget`
  gained an `onProgress` callback); `autoPlay` disabled on GateHelixOrb_A/B (it fought the wiring).
  Same wiring on the return trip — the ribbon pair flies both directions (C-RULING sky-ribbon).
- **Death helix reworked to the Evori around-character spiral (C-RULING)** — new
  `CharacterHelixDriver` (mesh-free math; replaces `OrbPathFollower` on `SoulOrbHelix.prefab`):
  two ribbon trails 180° apart orbit the body axis, ascending with an ease-in (accelerating)
  height curve and tapering radius; the second ribbon is a one-time driver-stripped clone parented
  under the pooled root (despawn nets cover it). AUTO-FIT: `EnemyDeathNotifier.OnEnemyCombatKill`
  now carries a size arg (mesh-renderer bounds height vs a 2 m humanoid, clamped 0.5–3) and
  `KillParticleSpawner` passes it as `CueContext.scale` — the driver multiplies radius/height by
  the root's lossyScale. Fixes "too small / wrong rotation" (old mesh-path import rotation is moot).
  `ManpuReactionListener.HandleAllyDown` signature updated (ignores the size arg).
- **Emitter-resize plug pass**: AccordState activation shockwave ring now stretches to the fixed
  12 m knockback radius (`emitterScale: 12/5` — ring art authored at 5 m — + `simSpeed: 2`);
  Coalesce aura footprint tracks the upgraded damage radius (`emitterScale: radius/1.5`; the
  big-node ring LAYER remains a later authoring step per the sizing ruling). Verified already
  plugged: Empower pulse (KnockbackRadius/5), SoulPulse (_pulseRadius/4), Possess_Active
  (whole-instance `scale` to range). AccordSpirit's summon burst is visual-only (no code
  knockback radius exists) — left authored.
- USER: Setsuna trails reuse the sky-ribbon language — that is trail ART on the Setsuna prefabs
  (authoring, no code seam missing). Retest gate end-to-end incl. cancel window + long marker hold.

### Changed — Editor tool consolidation (2026-07-10, playtest batch 8)
- **Validate window retired — Scene Health Dashboard is the one completeness tool.**
  `ValidatorWindow.cs` + `SceneRules.cs` deleted; their unique rules live on as dashboard
  recipes: new per-scene **References** column (R2 cross-scene serialized refs +
  `[RequiredReference]` nulls + AreaSpawnPoints left/right check — note: cross-scene detection
  still needs the target scene co-loaded), new project rows **World graph** (WorldLocationSO
  scene/Build-Settings/adjacency-symmetry/isStartLocation + AreaZoneConfig sub-SO completeness)
  and **Code lint** (`CodeLintRules` unchanged). The Validator's one-click **Fix** actions now
  render in the dashboard detail pane (create WorldLocationSO, add NavMeshSurface/AreaSpawnPoints
  — scene fixes require the scene open — fill AreaZoneConfig sub-SOs). `TestLab` added to
  `SceneClassifier.DevNames` (was being graded as an area scene).
- **POI-ecology + grading authoring folded into Scene Health as checks + fixes.** Wiring recipe
  counts `PoiEnergyEmitter` per POI ("Emitter N/M", fix = wire ecology); Enemy-prefabs recipe
  warns on brain+EnemyDarkEnergy prefabs missing `GOAPGoalSeekEnergy` (same fix); Persistent
  Volumes recipe checks the 7 grade profiles exist (fix = create); area Volumes recipe now also
  reports each identity volume's profile name (per-area gradient report). The standalone
  `Wire POI Ecology` and `Create Grade Profiles` menu items are retired (code kept, invoked by
  the fixes).
- **`Area Tools` window** replaces `New Area Scene…` + `Area Setup` (two tabs: New Area scaffold
  kit / Setup Zone config+populate — behavior unchanged, one menu entry).
  `NewAreaSceneWindow.cs` + `AreaAutoWireWindow.cs` deleted, `AreaToolsWindow.cs` added.
- **Upgrade Data Editor ↔ Cue Book linker:** `AbilityUpgradeData` gained a config-only `cueBook`
  slot (R7-clean; runtime resolution still receives the book from the ability system). The
  editor shows the linked book's ids grouped by base with the tiers each has, and a **+ _tN**
  button generates the next tier variant as a deep copy of the highest existing one
  (SerializedProperty `DuplicateCommand` — Undo-recorded, never overwrites/deletes; verified on
  a transient book: copies keep all elements, repeat clicks no-op).

### Changed — Playtest batch 9 tail (2026-07-10, docs + verifications, no code)
- **Melee slash point verified:** `ExecuteHitDetection` overlaps from `transform.position` +
  `attackRange` — the firePoint slot plays no role in melee (it's ranged-only, with the
  transform fallback + Awake auto-find from batch 1). Nothing to change.
- **Ecology wiring verified via the new dashboard recipes:** all eligible enemy prefabs carry
  `GOAPGoalSeekEnergy` (only the known deferred commander ManpuSlot warnings remain);
  L1_Park POIs Emitter 6/6; L2_Streets has no POIs yet (known content gap). TESTGUIDE §E's
  stale "ecology designed, not built" line corrected — it IS built (2026-07-09).
- **Scalable-emitter map moved to game.md §23.12 item 1** (canonical doc home); TESTGUIDE keeps
  a pointer.
- **Deliberately not done now:** P19 namespace/`package.json` leftovers (scheduled with the
  §20.4 restructure) and the load-bearing typo renames — both require isolated commits and the
  working tree currently carries the uncommitted playtest-fix batch; bundling would violate
  working-method rule 7.

### Fixed — Arrow/ranged-attack rework (2026-07-10, playtest batch 1, BUG-047)
- **Ranged damage moved to actual arrow impact.** The shared `meleeAttack.anim` hit-frame event
  was running the melee overlap-sphere with the ranged `attackRange` (7–10 m), damaging the
  player at fire time while the arrow flew as decoration. `EnemyAttackController` now sets
  `_suppressMeleeHitFrame` on every ranged attack; `ExecuteHitDetection` consumes it and bails
  (cleared on melee start and `ResetAttack` — no leak into the next melee swing).
- **Arrows can now actually hit:** `tahrArrow.prefab` (Siphon/Summoner/B_Ranged) had no collider
  and no Rigidbody; `Weapons/Arrow.prefab` (E_Ranged) had trigger colliders but no Rigidbody
  (moving triggers need one to raise events). Both got a kinematic Rigidbody; tahrArrow got a
  ~0.2 m sphere trigger. `Weapons/Arrow` `hitLayers` trimmed 192→128 (Enemy bit removed —
  would have self-hit the shooter).
- **Arrow facing fixed the pool-proof way:** `GameplayPool.Spawn` stamps the prefab ROOT rotation
  (why the user's root-rotation edits "did nothing"). tahrArrow meshes now sit under a `Model`
  child carrying the author's 180°Y correction; root stays identity; sigil flies flat, tip
  leading (screenshot-verified in-editor). `Arrow.Initialise` also self-aligns
  `LookRotation(direction)` defensively.
- **Tip anchors wired** (`Arrow._tipAnchor`): new `Tip` child on tahrArrow, existing `Head` on
  Weapons/Arrow — trail/head cues now follow the arrowhead, and impact plays at the real hit.
- **`Enemy.Awake` firePoint fallback:** unassigned muzzle slot now auto-finds a descendant named
  *firepoint*/*muzzle*/*tip* (serialized slot still wins — the per-shooter muzzle transform IS
  the industry pattern; the shared projectile prefab needs nothing).

### Fixed — Death helix + position-only cue reclaim (2026-07-10, playtest batch 2, BUG-048)
- **`OrbPathFollower` asset-path anchoring:** `SoulOrbHelix`'s path mesh is a reference INTO
  `SoulCollect.fbx` (transform at world origin) — the helix always played at (0,0,0). An
  asset-referenced path is now treated as a shape and played relative to the orb's spawn
  position (helix axis anchored at the spawn point). Scene-object paths behave as before
  (gate helix unaffected). Also: `_autoT`/`progress` reset on enable — pooled reuse used to
  restart at the top of the path.
- **`FxManager` position-only ctx fix (failure class):** a Local-sim particle under
  `attachMode: FromPrefab` resolves to Follow; played with a position-only `CueContext` its
  follow target is null and `ActiveFx.Update` reclaimed it on its FIRST frame — the cue
  silently never appeared. `SpawnParticle`/`SpawnVfx` now degrade Follow→World at spawn when
  `ctx.followTarget == null`; the late-death reclaim path is unchanged. Play-mode verified:
  full 4-beat kill sequence visible at the death spot (screenshot).

### Fixed — Direction bugs (2026-07-10, playtest batch 3)
- **Chain glow flipped:** the grab stream now flows TetherBreaker → player
  (`ChainProjectile.UpdateChainLine` arg swap; user read the old player→enemy flow as backwards).
- **GroupGrab soul-drain played BEHIND the warden:** `GrabbedSoulAbsorb.prefab`'s cone shape
  offset was local (0,0,−2) — with the cue's face-target aiming +Z at the grabbed twin, the
  suction emitted 2 m behind the warden's back. Offset flipped to (0,0,+2): particles now rise
  on the twin's side and stream into the warden (radial −10 suction unchanged).
- **Enemies snap-face their target at attack commit** (`EnemyAttackController.FaceTarget`,
  Y-only, called from both `TryAttack` and `TryRangedAttack`): swings and directional cues
  (grab drain, muzzle) now aim at the victim instead of wherever NavMesh steering left the body.

### Fixed — Stun/Possess green range circles retired (2026-07-10, playtest batch 4)
- The green discs during Stun/Possess casts were `AbilityRadiusPreview.previewObject` — a flat
  scaled primitive on each twin, activated by `ShowPrimaryPreview(range)` on every cast window.
  Same treatment as the teleport disc (2026-07-09): renderers disabled in `Awake`, API and
  radius math kept. The range read is now the scaled ability cue itself (`OnStun_Active` /
  `Possess_Active` ground circle via `CueContext.scale`). The teleport marker disc was already
  renderer-disabled — if a teleport disc is still seen it is the `tele_castmark` cue (intended).

### Fixed — Story grading invisible in Game view (2026-07-10, playtest batch 5)
- The grade slider worked; the Persistent Main Camera had **Post Processing OFF**
  (`UniversalAdditionalCameraData.renderPostProcessing`) — volumes rendered in the Scene view
  (own toggle) but never in the Game view. Enabled + Persistent saved. TESTGUIDE gained a
  "Post-processing setup" section (5-step visibility checklist + a worked Grade_MidPurpose
  example).

### Changed — Chain throw feel: Roadhog model (2026-07-10, playtest batch 6)
- `ChainProjectile` travel reworked (user spec, Overwatch Roadhog-hook reference), three
  serialized knobs on the prefab:
  - **Readiness pose** (`_windupReach`, default 0.75 m): during the marker windup the chain
    hangs partially extended toward the target instead of appearing only at the throw.
  - **Fast-out, decelerating arrival** (`_travelCurve`): normalized time → distance; default
    covers ~85% of the distance by 60% of the time, then eases into the landing spot.
  - **Launch bow** (`_launchBow`, default 1.1 m): the tip leaves the hand offset sideways
    (random side) and the offset dies as the chain straightens to full stretch — code-animated
    tip path; the multi-point `ChainBeamDriver` renders the curve, no particle/Animator needed.
- Landing position, hit check, mash/break, miss reel, cues, and pooling untouched.

### Changed — GameDebugger v2 layout + bench powers (2026-07-10, playtest batch 7)
- **Resizable window:** corner drag-grip + typed W/H fields in a footer (was: forced full-height,
  drag-only — the user couldn't work with it). Size clamps to the Game view.
- **Fixed top block** (never scrolls): spawn grid + global row — Kill ALL · STOP ALL FX ·
  Twins→pad · **God mode** (both twins `SetInvincible`, observe grabs/drains without melee
  interrupts) · **Down L/R twin** (real damage path → rescue starts, soul spawns — the
  Weaver's-Gate / ghost-bind test entry). Below it scrolls: enemy list → per-enemy menu.
- **Per-enemy additions:** **Dummy mode** (speed 0 + attacks off; brain/mood/Manpu keep reacting;
  restored from EnemyData on untoggle and never inherited across pool reuse) · **dark-energy
  bench** (absolute slider via new `EnemyDarkEnergy.DebugSetEnergy` — thresholds fire like a real
  gain; per-enemy `DebugFreezeGain` + "Freeze ALL others" so one enemy escalates in isolation) ·
  **Throw chain** (targets nearest twin → real `TryChainAttack`) + Release · **Force ghost**
  (Siphon `TestTriggerGhostSpawn`) · live ghost rows with **Pause bind timer** (new
  `SiphonGhost.DebugPauseBindTimer`, mash still works). All debug flags reset on pool despawn.
- Play-mode smoke: panel drawn with an enemy spawned, 0 console errors.

### Fixed — POI cue integration + KillParticleBook restore (2026-07-10)
- **Corruption cue id renamed `corruption` → `poi_corrupt`** to match the user-authored
  CommonCueBook id (user call): `EnemyDarkEnergy._corruptionStateCueId` default + the
  serialized value on all 13 enemy prefabs. Without this the bond-break aura silently
  never played (book had no `corruption` id anymore).
- **CommonCueBook `poi_buff`/`poi_corrupt` attach mode World → Follow** — both are held
  auras played with `CueContext.Follow(enemy)`; authored as World they would have stayed
  at the spawn point while the enemy walked away.
- **CommonCueBook `poi_feed` leak fixed** — its `EnergyInteraction` prefab loops, and the
  element had no explicit duration, so FxManager held every feed-tick instance forever
  (`PoiEnergyEmitter` discards the handle — one leaked infinite loop per enemy per 12 s).
  Explicit 1.5 s duration set (explicit lifetime deliberately auto-stops a loop).
- **`KillParticleBook.asset` REBUILT** (was accidentally deleted — most likely during the
  CueBooks folder split; never committed, so unrecoverable from git; rebuilt from the
  recorded 2026-06-27 spec). One id **`kill_seq`**, 4 elements at the locked ~1.25 s
  schedule: `helixorbs` (SoulOrbHelix, Immediate, 0.9 s) · `disintegrate`
  (DissolveParticleVfx, Immediate, 1.1 s) · `star` (KillParticleUp, WithPrevious +0.45 s,
  0.6 s) · `collect` (KillSoulAbsorb, AfterPreviousCompletion, 0.25 s). Lives at
  `Fx/CueBooks/Enemy/`. Element prefab refs verified resolving in-editor.
- **`KillParticleSpawner` re-wired**: `_cueBook` slot in Persistent (was None since the
  deletion — combat kills played no soul-release) assigned to the rebuilt book, scene
  saved; the played id fixed `"death"` → `"kill_seq"` (the book's actual id — the old
  hardcoded id predates the kill-sequence authoring).

### Added/Changed/Fixed — final pre-playtest batch (2026-07-09)
- **Fixed 6 compile errors** from the bomb-id move: `EnemyVfxLibrary` gained Weapons slots (`Arrow`/`WitnessBomb`/`SiphonBomb`, assets assigned); Witness/Siphon `ConfigureCues` rewired to them; `FxIds.cs` hand-updated to the generator's shape (regen should zero-diff).
- **Arrow cues by id**: `Arrow.cs` plays `arrow_Trail`/`arrow_Head` as held Follow cues on spawn (optional Tip Anchor) + `arrow_OnImpact` (World) at hit — sub-emitters on the mesh prefab can be stripped.
- **Chain grab glow wired live**: new `Vfx/ChainGlowDriver.cs` (stretches the stream's shape-Z to the live player↔TetherBreaker span each frame, source at the player); `ChainGlowFx` prefab added as a child of `ChainProjectile.prefab` (+driver); `ChainProjectile` drives it only while connected and clears it on despawn.
- **Old coloured markers removed**: `AttackRangeIndicator.Show` gated behind `DevConfig.Trainer`; `TeleportMarkerPreview` renderers disabled — the aim visual is now a held `tele_castmark` cue following the (invisible) marker; placement logic untouched.
- **Gate travel = the helix**: `TeleportAbility` hides the SoulPlayer's renderers during both travel legs (`tele_casttravel` represents the soul) and reveals under `tele_castin` / at return; cancel-safe.
- **POI cues via `CommonFx`** (user call — no serialized book/id): emitter plays `poi_feed`, dark-energy buff plays `poi_buff`, both hard-wired to the Common book like `on_hiteffect`. The earlier slot fields were removed (prefab/scene remnants harmless). Ids still need authoring in CommonCueBook.
- Corruption-state books batch-wired earlier today remain; `_poiBuffBook` assignments from the same batch are now dead remnants (field deleted).

### Added — POI energy-feed ecology (ritual/spawn/barrier sites feed idle enemies) (2026-07-09)
Files (new): `AI/POI/PoiEnergyProfile.cs` (SO — per-POI amounts/cadence), `AI/POI/PoiEnergyEmitter.cs` (the feed component + static nearest-site registry), `GOAP/Goals/GOAPGoalSeekEnergy.cs`, `GOAP/Actions/GOAPActionSeekEnergy.cs`, `BehaviourTree/Action/BTActionSeekEnergy.cs`, `AI/Utility/Data/SeekEnergyUtilProfile.asset`, `Editor/Authoring/PoiEcologyAuthoring.cs` (Tools ▸ Planet of Twins ▸ Authoring ▸ Wire POI Ecology). Changed: `Enemy.cs` (+`IsEngaged`), `EnemyDarkEnergy.cs` (POI Feed Buff block), `EnemyAttackController.cs` (+`SetPoiBuff` composing multiplier).
- **Feed:** a `PoiEnergyEmitter` next to any `POIBase` feeds each enemy in radius that is **below 50% HP** (profile-tunable) a small dark-energy + health tick at a **per-enemy 12 s interval**, dropping to 8 s past a dark-energy threshold; feeding **pauses while the enemy is engaging** (`Enemy.IsEngaged`: has target / stunned / possessed / feared / grabbed / brain-held — covers freeze + QTE). Feed plays a `poi_feed` cue (book+id serialized on the emitter — author it in the common enemy book) oriented POI→enemy.
- **Threshold buff (EnemyDarkEnergy):** first crossing of `_poiBuffThreshold` latches a small outgoing-damage bump via a NEW composing slot (`SetPoiBuff` — never stomps the `SetDamageMultiplier` shared by Witness/GrandSummoner/ProximityPower), plays a held `poi_buff` aura once (stopped on despawn — pool-safe), and fires a Confident mood pulse so Manpu announces it. Latch + aura reset `OnDisable`.
- **SeekEnergy AI:** utility goal + BT action — idle enemies (no target/memory, not possessed/stunned) walk to the nearest emitter and dwell inside its feed radius; **bond-broken enemies get a flat score bonus** (visit far more often), sub-half-health another. Wire with the authoring menu (adds goal+action to enemy prefabs, emitters to open-scene POIs, creates the default `PoiEnergyProfile`).

### Fixed — enemies invisible after spawn (spawn lead no longer drives a material float) (2026-07-09)
Files: `Enemy.cs`, all enemy prefabs (`_spawnRevealDelay` 1.4 → **1.2**). The spawn lead used `MaterialRevealDriver` (`_val` reveal float) — but enemies don't carry reveal materials (that path is for world objects like the Witness ritual site), so the hide/show was undefined per shader and enemies could stay invisible. Now: child **Renderers toggled off** for `_spawnRevealDelay` (1.2 s; the `on_enemyspawn` cue is 1.8 s) + brain-held, then shown. `ResetForPool` force-shows (an enemy returned mid-lead can never re-enter the pool hidden).

### Added — refcounted per-prefab warm pools in `GameplayPool` (no duplicate prewarm references) (2026-07-09)
Files: `GameplayPool.cs` (+`AddUser`/`RemoveUser`, trim-after-grace 10 s, Return destroys stragglers of trimmed pools), `Enemy.cs` (+`RegisterPooledPrefab` — released automatically in `OnDisable`), `EnemyAttackController.cs` (registers the data's projectile in `SetProjectile`), `WitnessEnemy`/`SiphonEnemy`/`TetherBreakerEnemy` (register bombs/ghost/chain at throw), `SiphonGhost` (chain via its `ISpawnPoolable` hooks). The pool key IS the prefab reference the consumer already holds (EnemyData/serialized slot) — no `SpawnPrewarmProfile` row to keep in sync; pools warm when a user spawns and are destroyed after the last user despawns + grace. The profile stays optional for boot-time warming of ability objects.

### Changed — corruption-state slots wired to `CommonCueBook` on all 13 enemy prefabs (2026-07-09)
- `EnemyDarkEnergy._corruptionStateBook` = `Fx/CueBooks/Common/CommonCueBook.asset` everywhere (id `corruption` — author the effect in that book; no dedicated book).

### Fixed — `SmartEnemySiphonGhost.prefab` was missing its `SiphonGhost` component entirely (2026-07-09)
- The prefab had the ghost brain/goals/health/Manpu but not the class that pursues + throws the binding chain — the rescue-bind path could never run. Added the component and assigned the shared `ChainProjectile` prefab to `_chainPrefab`.

### Changed — projectile config hoisted to base `EnemyData` (any enemy can shoot) (2026-07-09)
Files: `EnemyData.cs` (+`useProjectile`/`projectilePrefab`/`projectileSpeed`), `RangedEnemyData.cs` (fields removed — serialized values survive, names unchanged), `Enemy.cs` (base `ApplyData` wires `SetProjectile`; `firePoint` moved here from `RangedEnemy`; ranged cue falls back to the melee tell), `EnemyAttackController.cs` (`SetRangedMode()` now flag-only; `SetProjectile(...)` new; `TryAttack` fires the data projectile at `Enemy.Target` when configured — possession/clan-war attacks stay melee), `RangedEnemy.cs`.
- Assigning a projectile prefab + `useProjectile` on ANY enemy's data makes its basic attack shoot (melee brains included); ranged archetypes unchanged. The prefab needs an `Arrow` component (spawns through `GameplayPool.Projectiles`).

### Changed — GameDebuggerV2: story-grading bench (2026-07-09)
- New "Story grading" section: progress slider drives `StoryGradeDirector.SetStoryProgress` (window grades auto-pick) + one button per authored grade id (`PlayGrade`, event rows like `shock` included). `StoryGradeDirector` gained `GradeIds()`.

### Changed — doc-section references swept out of code (2026-07-09)
- All `§X.Y` / `game.md §…` / `instruction.md §…` / `MANPU_SYSTEM.md §…` / `GDD §…` tokens removed from comments, `[Header]`s and `[Tooltip]`s across ~85 scripts (user call: rationale lives in git/docs, not code). Phase tags (P16), BUG ids and Rulebook laws (R4/R10/E1/F1) stay.

### Added — P19 stage 3: `PoT.Fx` + `PoT.Manpu` assembly carve (the package is real) (2026-07-08)
Files: `Fx/PoT.Fx.asmdef` (new — refs Cinemachine + RP Core/URP + VFX Graph), `Manpu/PoT.Manpu.asmdef` (new — refs PoT.Fx only); GUID-preserving moves (`AssetDatabase.MoveAsset` — scene/prefab refs intact): `CameraCueDriver.cs` → `Fx/` (FxManager calls it; it is package-clean), game-glue out of the package: `ManpuDirector`/`MoodAmbient`/`ManpuReactionListener`/`ManpuAbilityListener` → new `Scripts/ManpuAdapters/`. **Compiled clean first try — the hard proof of stages 1–2**: PoT.Fx = 89 types, PoT.Manpu = 22 types, zero Assembly-CSharp references (one leak would have failed the assembly). Play-verified: all four managers boot, enemy spawn → `TransitionTo(Enraged)` → ManpuDirector glue → PoT.Manpu slot pulse, zero errors.
- Dependency shape: `PoT.Manpu → PoT.Fx` (acyclic; FxManager reaches Manpu only via `IManpuGlyphTarget`). Predefined assemblies auto-reference custom asmdefs, so every gameplay/editor consumer compiles unchanged — the §20.4 editor-inversion caveat only triggers when GAMEPLAY moves into asmdefs (later restructure stage).
- **Deliberately deferred, user to bless:** `namespace PoT.Fx` on the ~50 package files (would touch every consumer file for `using` lines — belongs with the §20.4 restructure, not mid-content) and the literal empty-URP-project compile (needs a second project; the asmdef isolation is the in-project equivalent). `FxIds/Generated` + CueBook/Library assets stay project-content per §23.13.

### Changed — P19 stage 2: Manpu core decoupled from the game's mood/perception enums (2026-07-08)
Files: `Manpu/ManpuEnums.cs` (+`ManpuMood`, +`ManpuSearchState` — mirror `EnemyMood`/`EnemySearchState` member-for-member, APPEND-ONLY contract documented), `Manpu/ManpuVocabulary.cs`, `Manpu/ManpuSlot.cs` (package core now enum-clean), `Manpu/ManpuDirector.cs` (game glue converts with int casts), `Editor/Authoring/ManpuVocabularyEditor.cs`. 0 CS errors; `ManpuVocabulary.asset` verified intact (13 mood + 5 perception rows resolve to the correct mirror values — enums serialize as ints, so identical ordering = zero asset migration).

- Package boundary clarified: **core** (ManpuSlot/ManpuGlyph/ManpuVocabulary/enums) is game-type-free; **glue** (ManpuDirector, MoodAmbient, ManpuReaction/AbilityListener) stays project-side and owns every `EnemyMoodSystem`/`PoTPerceptionMemory`/`Enemy` reference — this IS the "mood-enum adapter" §24.8 predicted, at zero runtime cost (int cast).

### Changed — P19 stage 1: the two package seams (Fx never names a game type for scene flow / Manpu slot) (2026-07-08)
Files: `Fx/Core/IFxSceneEvents.cs` (new), `Fx/Core/IManpuGlyphTarget.cs` (new), `Fx/FxManager.cs`, `Fx/MusicManager.cs`, `SceneLaoder/SceneFlowManager.cs`, `Manpu/ManpuSlot.cs`, Persistent.unity (slots wired + saved). 0 CS errors; play-verified (interfaces resolve, zero unwired warnings).

- **Seam 1 (`IFxSceneEvents`, declared in Fx):** `OnSceneWillUnload(string sceneName)` + `OnLocationAudioChanged(MusicTrackData, AmbienceData)` + current-audio getters. `SceneFlowManager` implements it, raising the mirrors alongside its existing `WorldLocationSO` events. `FxManager` (F1 reclaim) and `MusicManager` (crossfade + initial pull) now hold a `[SerializeField] MonoBehaviour → IFxSceneEvents` slot (R1 — all in Persistent, `FormerlySerializedAs` kept) — the `SceneFlowManager.Instance` fallbacks are GONE; an unwired slot LogWarnings and disables that feature loudly. **Census correction:** the 2026-07-03 "exactly two seams" claim missed MusicManager — it was seam 3, folded into the same interface.
- **Seam 2 (`IManpuGlyphTarget`, declared in Fx):** `RequestCuePulse(sprite, colorA, colorB)`; `ManpuSlot` implements; FxManager's Manpu element resolves `GetComponentInChildren<IManpuGlyphTarget>` instead of naming `ManpuSlot`.
- Remaining P19 stages: Manpu mood-enum decoupling (27 `EnemyMood` refs), then namespaces + `PoT.Fx` asmdef, then the §20.4 folder restructure. Working tree was committed (3 commits) before stage 1 per the §20.4 "never mid-content-push" law.

### Changed — GameDebuggerV2: Ctrl+1..9 spawn hotkeys (2026-07-08)
- User request: spawn the nth spawnable at the enemy pad via **Ctrl+number** (buttons now numbered "1. Melee…"); always modifier COMBOS, never bare F-keys (F3/F9 collide with Unity editor functions). Same `DevConfig.Trainer` gate as the panel toggle.

### Added — P18: Cue Book control additions — `isVariant` groups + shake custom-shape/distance-falloff; material-float element DROPPED (2026-07-08)
Files: `Fx/Data/CueElement.cs` (+`isVariant`; CameraCue +`shakeCustomShape`/`shakeRange`), `Fx/CueBookRunner.cs` (variant skip logic), `Fx/FxManager.cs` (passes `ctx.position` to the camera driver), `Camera/CameraCueDriver.cs` (custom impulse shape + Dissipating falloff), `Editor/Authoring/CueBookDataEditor.cs` (Variant toggle in the element header; Custom Shape + Range fields), `Editor/Validation/CueBookLinter.cs` (+F6/F7). 0 CS errors.

- **Variants (user-locked model, game.md §23.14):** consecutive elements marked **Is Variant** form ONE group; each `Play` picks exactly one at random (equal weights v1) and skips the rest. Skipped members are transparent to scheduling — successors (`WithPrevious`/`AfterPreviousCompletion`) resolve against the previous *playable* element, so a chain after a variant group follows whichever variant was chosen; cuts from/to skipped members are dropped. Verified via a pure-C# runner harness: 30 runs = exactly one variant each, successor fired 30/30, variant-group-at-index-0 OK, cut-at-variant never wedges the runner.
- **Shake upgrade:** `shakeShape = Custom` now honours a per-element **`shakeCustomShape`** curve (author your own impulse profile beyond the 4 presets); new **`shakeRange`** (metres): 0 = Uniform (all cameras equal — the old behaviour, unchanged default), > 0 = **Dissipating** from the cue's world position so distant cameras feel less (`GenerateImpulseAtPositionWithVelocity`). Per-camera sensitivity stays on each cam's `CinemachineImpulseListener.Gain` (authoring). Both paths play-verified exception-free.
- **Linter:** F6 = `isVariant` on a lone element (group of one always plays — Info); F7 = a cut targeting a variant member (silently dropped on plays where another variant is chosen — Warning).
- **DROPPED — material-float cue element (user call):** every object carries its own material, so a book-level property-name field is per-object fragile, mostly inapplicable, and would make the cue system project-specific (hurting the P19 shippable-package goal). `MaterialRevealDriver` (untouched) stays the one way to animate a material float — the carrying prefab is spawned by the cue instead. game.md §23.14 item 3 struck through with the reasoning.

### Added — P17: StoryGradeDirector + 6 story-grade profiles + FailureReset sting volume (2026-07-08)
Files: `Grading/StoryGradeDirector.cs` (new), `Editor/Authoring/GradeProfileAuthoring.cs` (new — `Tools ▸ Planet of Twins ▸ Authoring ▸ Create Grade Profiles (P17)`, idempotent), `TutorialSystem/FailureResetSequencer.cs` (reworked — see Changed), 7 new `Assets/Settings/Grading/*.asset` VolumeProfiles, Persistent.unity (+`StoryGradeVolume` GO w/ VolumeA/B children + director; +`FailureStingVolume` child under the sequencer, wired into `_postProcessVolume` — slot was previously unwired). 0 CS errors; play-verified in TestLab.

- **`StoryGradeDirector`** (Persistent singleton, ArtStyle.md §11.2): owns the single global priority-0 story grade via an A/B two-Volume crossfade (profiles assigned to the volumes, weights lerped over `_blendSeconds` **unscaled** — grading moves during Setsuna/pause). Rows: `act1_warm 0` · `shock (event-only, hardCut)` · `early_fear 0.15` · `mid_purpose 0.35` · `late_chaos 0.60` · `ending_losing 0.85`. API is the generic seam: `SetStoryProgress(0..1)` picks the window row; `PlayGrade(id)` for event grades (Shock = snap). CheckpointManager carries **no story flags yet** — nothing calls `SetStoryProgress` at runtime; the beat→progress mapping lands with checkpoint/story data later, never in the director. Fail-loud: missing volumes/rows disable self; unknown `PlayGrade` id LogErrors.
- **7 profiles** (starting values per ArtStyle §11 — **user tunes in Unity**): Act1_Warm (temp +10, contrast +10, vig 0.2, grain 0.15) · Shock (sat −30, contrast +30, temp −20, crushed blue-leaning lift, CA 0.4) · EarlyFear (temp −10, vig 0.35, grain 0.25) · MidPurpose (contrast +15, temp −5) · LateChaos (split-tone teal `#17909A` shadows / gold `#FFCE52` highlights, bloom 0.7) · Ending_Losing (sat −20, temp −15, lifted cold blacks) · FailureReset_Sting (sat −80, vig 0.45, CA 0.25).
- Play-verified: boot lands `act1_warm` at weight 1 with no fade; `SetStoryProgress` crossfades complete in both directions with volume role-swap; `shock` hard-cuts; unknown id LogErrors (only console entry of the run); sting sequence runs weight 0→1→0 and rests at 0. Editor-focus note: play-mode frames freeze while the Editor is unfocused — `Application.runInBackground = true` (runtime-only flag) lets MCP verification tick.

### Changed — P17: FailureResetSequencer drives the sting Volume WEIGHT (was: mutating profile saturation) (2026-07-08)
- `_postProcessVolume` is now a **dedicated** global Volume (priority 30, weight 0 at rest, profile `FailureReset_Sting`). The sequencer animates only `volume.weight` 0→1 (sting-in) and 1→0 (restore), so the full authored look (desat+vignette+CA) rides one blend, nothing fights the priority-0 story volumes, and no profile is mutated at runtime. `ColorAdjustments`/`_colourAdj` saturation-Override path removed; unwired slot now LogErrors in Awake (was silent).
- **TestLab fix:** the `Ground` plane + NavMeshSurface child of DebugLab was missing from the saved scene (twins fell on play) — recreated and saved.

### Changed — Lore/colour canon: Orveth ≠ the teal palette; Voreth ≠ Archon residue; Tahr trickery beat (docx edits, no code) (2026-07-08)
- **`Planet_of_Twins_Colour_Bible_v1.docx`**: the pure-teal palette renamed **"Pure Current"** everywhere (was "Orveth"/"ORVETH / PURE" — conflated the Archon with the substance). §1.1 rewritten: dark energy = the planet's own current, born of the clash between the two natures, pressurised by war + the Accord's seal — belongs to the planet, not any Archon. New naming-clarification note after the five-signals table (Orveth = the Archon, merely *depicted in* the pure-current palette in her one scene — so no palettes needed for the other four Archons; Voreth = the same planetary current war-distorted; crack = pure teal bottom + Voreth body + Khal-Vor veins by design so its balance shifts with story degradation). User's crack plan: ONE gradient (teal pole → violet-black pole) driven light→dark by story progression — maps to the P17 StoryGradeDirector / P18 material-float element.
- **`planet_of_twins_story_bible_v4.docx`**: Voreth origin corrected ("was not of her making — the planet's own dark current, distorted by the war"); Tahr's boon rewritten as a **worded trap** (he tries to corner Orveth into surrendering her own power; the law binds her to his words, not his scheme; his "warrior's power" phrasing is why she points to the keeperless planetary reservoir) + echo in his character section; top-of-doc note added: colour descriptions are narrative flavour only — the Colour Bible wins on any conflict; "Orveth's dark energy is cold teal" → "the planet's pure dark energy is cold teal".

### Added — P16: GameplayPool (categorized gameplay-spawn pooling) + all 9 migration sites (2026-07-04)
Files: `SpawnSystem/GameplayPool.cs` (new — pool + `PoolCategory` + `ISpawnPoolable`), `SpawnSystem/SpawnPrewarmProfile.cs` (new SO), `SpawnSystem/EnemyPool.cs` (+`SpawnReady`), poolables: `Combat/{Arrow,BombProjectile,ChainProjectile}.cs`, `Players/Ability/AccordAbility/{RadiantSeekerOrb,AccordSpiritAgent}.cs`, `Players/Ability/Systems/CoalesceAura.cs`, `EnemyAI/Types/SiphonGhost.cs`; spawners: `EnemyAI/EnemyAttackController.cs`, `EnemyAI/Types/{WitnessEnemy,SiphonEnemy,TetherBreakerEnemy}.cs`, `Players/Ability/AccordAbility/RadiantSeekerAbility.cs`, `Players/Ability/Systems/{AccordSpiritSystem,CoalesceSystem,SoulConvergenceSystem}.cs`. `GameplayPoolRoot` GO added to Persistent (scene saved). 0 CS errors; play-verified (below).

- **`GameplayPool`** (Persistent singleton, standard pair): one pool for GAMEPLAY spawns — `PoolCategory { Projectiles, AbilityObjects, Summons, Hazards }`, hierarchy `GameplayPoolRoot/<Category>/…`; instances reused, never destroyed, **reparented home on Return** (a despawning host/area can never drag a pooled instance away — F1 class; this is what makes parenting the CoalesceAura to a pooled enemy safe). Call sites use the statics: `GameplayPool.Spawn(prefab, category, pos, rot)` / `Despawn(go)` / `Despawn(go, delay)` — LogError + degrade to Instantiate/Destroy if the Persistent GO is missing (loud, never dead gameplay). **Delayed despawns are version-stamped:** a stale lifetime timer (arrow hit first, chain re-thrown) can never kill a reused instance. Return runs `ISpawnPoolable.OnDespawned` on the hierarchy, then the safety nets: `StopAllCoroutines` on every MonoBehaviour + `FxManager.StopAllOn(transform)`. `NavMeshAgent.Warp` on Get (only when the agent is enabled — self-managed agents are left alone). Cue-on-spawn stays caller-played (pool owns lifetime, caller owns presentation).
- **`ISpawnPoolable { OnSpawned(pool), OnDespawned() }`** implemented with precise resets: **Arrow** (`_hasHit`/controller; lifetime = version-stamped `Despawn(go, lifetime)`) · **BombProjectile** (fuse handle stopped, `_initialised` cleared, timer ring restored) · **ChainProjectile** (release player, stop marker/drag handles, beam `HideImmediate`, **event fields nulled — stale thrower subscribers can never fire for the next thrower**, flags cleared) · **RadiantSeekerOrb** (owner-notify **moved from OnDestroy to OnDespawned** — pooled objects never destroy) · **AccordSpiritAgent** (portal handle promoted from a coroutine local to a field so mid-portal despawn stops it; claimed-target freed) · **CoalesceAura** (host-death **named-handler unsubscribe** via new `_hostHealth` field; linger state reset) · **SiphonGhost** (rescue/health unsubscribes moved to OnDespawned, full state + colour + events reset, chain force-disconnected). SC shield pools with no script (Animation replays on spawn).
- **`SpawnPrewarmProfile`** SO (`rows {prefab, count, category}`) — optional slot on the pool, instantiated inactive at Start; counts to come from the TestLab/PSO trace pass (authoring, checklist).
- **`EnemyPool.SpawnReady(prefab, pos, rot, data=null)`** — the canonical ready-to-fight spawn (Get → Warp+enable → `SetPoolProvider` → `ITimeAffected` register → `ApplyData` → `EnemyDeathNotifier.Register`) as a shared method; the **Witness summon now routes through it** (minions get on_enemyspawn, Setsuna participation, kill cues + pooled reuse — it was a raw `Instantiate` with none of that). Dedup of the EnemySpawner/GameDebuggerV2 copies = a later isolated commit (game.md §20).
- **Latent bug fixed by necessity:** `SiphonEnemy` subscribed a **lambda** capturing its ghost to its own `OnDeath` — harmless with Destroy (Unity fake-null), but with pooling a stale subscription would have **killed a reused ghost in someone else's rescue**. Now a named handler (`KillSpawnedGhost`) + unsubscribe on rescue-resolved.
- **Play-mode verification (MCP, TestLab):** 4 category roots · spawn→active under `Projectiles` · despawn→inactive+reparented home · double-return = silent no-op · immediate respawn = **same instance reused** · **stale 0.5 s delayed despawn left the reused instance alive (version stamp)** · `EnemyPool.SpawnReady` spawned a ranged enemy through the full sequence · 0 console errors/warnings. Forcing the real `FireProjectile` path surfaced a **pre-existing authoring gap, loudly**: the `SmartEnemyRanged` prefab has no `_projectilePrefab` wired on its `EnemyAttackController` (the LogError fired) — authoring item, not a code defect.
Files: `SkillTree/UpgradeCueResolver.cs` (new), `SkillTree/AbilityUpgradeNode.cs` (doc note), plumbed callers: `Players/Ability/Systems/{StunAbility,PossessionAbility,EmpowerSystem,AccordStateSystem,SoulConvergenceSystem,SoulPulseSystem,CoalesceAura}.cs` + `Players/Ability/TeleportAbility.cs`; `Editor/Authoring/UpgradeDataEditorWindow.cs` (new — Tools ▸ Planet of Twins ▸ Upgrade Data Editor), `Editor/Authoring/CueBookDataEditor.cs` (tier-naming help box). 0 CS errors; resolver verified end-to-end in play mode (below). *(An interim per-node `cueIdOverride` field shipped earlier the same day and was replaced by this suffix model on the user's call — one override string couldn't tier multi-id/per-twin effects; the field is removed, no assets ever used it.)*

- **Mechanism (game.md §23.16, user-locked 2026-07-04 — SUFFIX, not prefix; ids stay grouped by base name and FxIds constants generate beside their base):** tier variants live in the SAME book, named **`<baseId>_t[n]`** (`stun_cast_t1`, `stun_cast_t2`). At tier N (= unlocked node count) every id the ability plays resolves through `UpgradeCueResolver.Resolve(book, data, defaultId)`: try `_tN` → `_t(N-1)` → … → `_t1` → base. **Per-sub-id opt-in:** an ability playing 3 ids can tier just one — author `id1_t2`, leave the others alone, they keep their base effect automatically. No node field, no code change per tier — tier art = author the book element.
- **Plumbed at EVERY tree cue id (zero visual change until `_t[n]` ids exist):** Stun `OnStun_Active`+`OnStun_Hit` · Possess `Possess_Active`+`Possess_Hit` · Empower `empower_buff`+`empower_pulse` · Accord `accord_ChargeUpKai/Lyra`+`accord_ActiveKai/Lyra`+`accord_ActiveBuff`+`accord_Shockwave` · SoulConv `soulcon_chargekai/lyra`+`soulcon_shieldkai/lyra`+`soulcon_buff` · Gate `pulse_fire` (SoulPulse) + `tele_castmark/castout/casttravel/castin` (TeleportAbility, incl. the return-travel replay) · Coalesce `on_aura` (via `SkillTreeManager.Instance` — spawned prefab, no store slot). NOT plumbed: HealthRegen (no cue) · AccordSpirits (system has no data store — add one first if its ids should tier).
- **Authoring messages (user request — the convention is in front of whoever creates ids):** `CueBookDataEditor` shows a help box on every book ("name an id `<baseId>_tN` … ids WITHOUT `_t[n]` serve ALL tiers"); `UpgradeDataEditorWindow` shows the same rule up top plus a per-node "ids: `<base>_t{n}`" column naming the suffix each node's unlock activates.
- **Upgrade Data Editor (game.md §23.15.3):** table per `AbilityUpgradeData` — rows = nodes; columns = label/cost + only the stat fields actually used by that tree (auto-hidden when every node holds the class default; "All columns" toggle); in-place editing through `SerializedObject.ApplyModifiedProperties` (Undo-recorded); Add-node button; asset picker + ping.
- **Play-mode verification (MCP):** transient book (`stun_cast`, `stun_cast_t1`, `stun_hit`) + transient 3-node tree purchased through the REAL `SkillTreeManager.TryPurchaseNode`: tier 0 → `stun_cast` · tier 1 → `stun_cast_t1` · tier 1 `stun_hit` → `stun_hit` (no variants = serves all tiers, the per-sub-id case) · tiers 2–3 → `stun_cast_t1` (falls back down to the best authored tier) · null book/data → default. All seven behaviors pass.

### Added — P14: Scene Health Dashboard + NewAreaSceneWindow full kit (2026-07-04)
Files: `Assets/Scripts/Editor/Validation/SceneHealthRules.cs` (new — the recipe engine), `Assets/Scripts/Editor/Validation/SceneHealthDashboardWindow.cs` (new — Tools ▸ Planet of Twins ▸ Scene Health Dashboard), `Assets/Scripts/Editor/Authoring/NewAreaSceneWindow.cs` (extended to the §23.15.4 full kit). 0 CS errors; engine exercised over real scenes via MCP (below).

- **Dashboard (game.md §23.15.2):** one row per Build-Settings scene (open-but-unlisted scenes like TestLab get rows too), one coloured cell per recipe (green/yellow/red/grey), cell click → findings pane with Select-to-ping; every finding names the offending object. *Scan All* opens each not-open scene additively, evaluates, closes without saving. Per-scene recipes: **Must-haves** (LocationEntrances + default fallback · NavMesh surface/bake · SpawnZone points + config chain · WorldLocationSO exists + adjacency bidirectional · QTE anchor where QTE content exists · R9 world-canvas resolver · SceneLoadTrigger target/collider/layer) · **Wiring** ("N placed, M wired": QTE anchors fully wired, RitualSitePOI/SpawnPointPOI `_cueBook` slots, checkpoint trigger colliders, tutorial presence) · **Counts** (info density: zones/POIs/traps/orbs/checkpoints) · **Timelines** (null track bindings = the BUG-032 class, excluding runtime-rebound Cinemachine/Animation tracks (R11 mech 1); Activation Tracks controlling ancestors of gameplay logic = R11 error) · **Volumes** (ArtStyle.md §11.1: Persistent = exactly one global prio-0 story-grade volume + FailureResetSequencer volume wired; areas = exactly one global prio-10 identity volume with profile, stray area globals = error, local prio-20 crack volumes need profiles). Project recipes: **Enemy prefabs** (missing scripts = deleted-component ghosts, error · ManpuSlot present · MaterialRevealDriver info) · **Build Settings** (Bootstrap/Persistent/Intro order · temp scenes Restore/Trees/TestLab/SampleScene never enabled).
- **First real scan results (MCP, engine invoked headlessly):** L1_Park Wiring PASS (QTE 1/1, SpawnPOI 4/4) · L1_Park **Timelines FAIL — 7 null bindings on its director (BUG-032 detected by the tool, as designed)** · L1_Park Must-haves 8 warnings (authoring gaps) · Persistent Volumes warn (no prio-0 story volume yet — P17 pending, expected) · Enemy prefabs 9/12 healthy — the 3 = commanders missing ManpuSlot (deferred roster, expected) and **zero missing-script errors — the P11 EnemyVFXController deletion left no serialized ghosts**.
- **NewAreaSceneWindow full kit (game.md §23.15.4):** now also scaffolds, per toggle — `AreaZoneConfig` + its three sub-SOs created and assigned to the SpawnZone · default `LocationEntrance` · `SceneLoadTrigger` with `targetLocation` pre-wired to the new `WorldLocationSO` (created FIRST now, so scene objects can reference it) + Player layer mask · optional empty `QTESceneAnchor` · area **identity volume** (global, priority 10, profile left for the artist). Ends by pointing at the dashboard for verification; TODO checklist logged per creation.

### Changed — P13: New Input System migration (TwinInputReader guts + all raw-Input consumers) (2026-07-04)
Files: `Assets/Settings/Input/PlanetOfTwins.inputactions` (new — 13 Gameplay + 3 UI actions, wired onto `PlayerManager` in Persistent, scene saved), `Players/TwinInputReader.cs` (rewritten), `Players/Ability/Freeze/Interface/IInputProvider.cs` (+5 methods), `TutorialSystem/TutorialInputGate.cs` (+5 passthroughs), `SettingMenu/PauseMenuController.cs`, `Camera/OverviewCamController.cs`, `UI/SkillNode/SkillTreeUI.cs`, `QuickTimeEvents/{QTEManager,QTEController,QTEDefinitionSO}.cs`, `SceneLaoder/IntroController.cs`, `TutorialSystem/TutorialDirector.cs`, `Debug/DamageDealerDebug.cs`, `Players/Ability/Systems/SoulConvergenceSystem.cs`. 0 CS errors; MCP play-mode regression PASSED (below).

- **`TwinInputReader` internals swapped** legacy `Input.*` → Input System polling (`WasPressedThisFrame`/`IsPressed`/`WasReleasedThisFrame` — closest to the old per-frame edge semantics; zero consumer diffs). `[SerializeField] InputActionAsset _actions` cached per-action in `Awake` (missing asset/action = LogError + that input dead, R4 fail-loud); whole-asset `Enable()`/`Disable()` in `OnEnable`/`OnDisable`. **The tutorial-gate seam is UNCHANGED (the P13 hard contract):** gate checks stay inside the `IInputProvider` getters — actions are never per-category enabled/disabled; `SetGate` registration, per-category filtering, and fail-open null-gate semantics are byte-identical.
- **Move** = two `2DVector(mode=1)` composites (WASD + arrows) + gamepad left stick. `mode=1` (Digital, NOT normalized) deliberately replicates legacy `GetAxisRaw` — diagonals read (±1,±1); `GetMovementDirection` still normalizes. Flip to DigitalNormalized later as a tuning call, not a migration diff.
- **Keys unchanged**, now with free gamepad seats: Attack E/LMB/West · Ability Q/RMB/North · Switch Shift/LB · Teleport+SoulBreak C/East · Interact+Rescue+Convergence+QTEMash F/South · Cancel X/RB · Empower R/LT · Overview B/dpad-up · Pause ESC/Start · SkillTree Tab/Select · intro AnySkip anyKey/mouse/South/Start. Struggle stays a **separate** E-only action (mashing escape must not also fire on LMB).
- **`IInputProvider` +5 (all ungated — these never had a gate category):** `GetOverviewDown`, `GetPauseDown`, `GetSkillTreeToggleDown`, `GetQTEMashDown`, `GetAnySkipDown`; `TutorialInputGate` passthroughs added. **The 6 gate categories are untouched.**
- **Every raw-Input gameplay consumer migrated to the provider** (raw `Input.*` outside the reader = the standing ban, now actually true): `PauseMenuController` (ESC priority chain unchanged, only the read swapped), `OverviewCamController` (B; `overviewKey` field removed), `SkillTreeUI` (Tab; `ToggleKey` field removed), `QTEManager` + obsolete `QTEController` (mash; `QTEDefinitionSO.mashKey` kept as legacy data, tooltip says so — every asset used F), `IntroController` (any-key skip; provider resolved **lazily** because Persistent is background-loaded by the intro itself — `_loadsComplete` guarantees it exists at read time).
- **Dev keys same pass:** `TutorialDirector` panic-skip F9 → **Ctrl+F9** (bare F9 = the user's profiler binding; stays editor-only + raw legacy Input, the sanctioned dev-key class); `DamageDealerDebug` now `DevConfig.Trainer`-gated + self-disables (its category keys D/S/E/W collide with WASD — extra reason it stays off; GameDebuggerV2 supersedes it); `SkillPointDebug` was already gated. Dead `SoulConvergenceSystem._activateKey` removed.
- **Player Settings stay `activeInputHandler: Both`** — debug scripts + `GameDebuggerV2`'s toggle still use legacy `Input` legally (Trainer-gated, never gameplay).
- **MCP play-mode regression (TestLab):** 0 console errors/warnings on boot (proves asset + all 16 `FindAction`s resolved); every action reports ≥1 resolved control on present devices; synthetic Input System keyboard events: W → `GetMovementInput()` = (0,1); combined E/ESC/Tab/B/F press → `GetAttackDown/GetStruggleMash/GetPauseDown/GetSkillTreeToggleDown/GetOverviewDown/GetQTEMashDown/GetRescueMash/GetConvergenceHeld/GetAnySkipDown` all TRUE (gate fail-open verified — TestLab has no gate). **Remaining human DoD (checklist item 5, instruction.md §18):** full Bootstrap tutorial run with progressive unlock, direct-area fail-open feel test, all four entry paths.

### Added — P12: GameDebugger v2 + TestLab scene (the dev bench) (2026-07-04)
Files: `Assets/Scripts/Debug/GameDebuggerV2.cs` (new), `Assets/Scripts/SpawnSystem/EnemyPool.cs` (adds the standard `Instance` singleton pair), `Assets/Scenes/Sandbox/TestLab.unity` (new — **not** in Build Settings, verified). 0 CS errors (MCP-verified).

- **`GameDebuggerV2`** — one self-contained IMGUI panel (toggle **Ctrl+`** — a serialized, rebindable COMBO; the first single-key choice F9 collided with the profiler), hard-gated on `DevConfig.Trainer` (master fail-safe + release-build hard-off → can never reach players). Capabilities per game.md §23.15.1: **spawn any enemy through the REAL pooled path** (replicates `EnemySpawner.SpawnEnemy` exactly: `pool.Get` → `NavMeshAgent.Warp` → `SetPoolProvider` (fires on_enemyspawn + reveal-delay) → `ITimeAffected` register → `ApplyData` → `deathNotifier.Register` — pooled-reuse bugs reproduce); select a spawned enemy → **Damage / Kill (DamageType.Combat → real kill-cue path) / Stun / Possess**; **mood bench** — every `EnemyMood` as a button through `TransitionTo` (watch the Manpu glyph + §24.8 aura + MoodAmbient tint react — the P11 verification rig); **force behaviours** via public APIs (Summoner `TriggerSummon`, Witness `StartRitual`/`ThrowBomb`→nearest twin, GroupGrab `StartGrab`); **perception** (selected enemy senses on/off = `PerceptionListener.enabled` — R8-paired deregistration; twins-detectable toggle = twins' `Perceivable.enabled`); **fire any cue** by book+id (buttons enumerate `CueBookData.effects`; plays on the selected enemy, else the pad); **skill-point grant**; **teleport twins to pad** (CharacterController-safe warp; TestLab sits outside the streaming graph so `NotifyTeleported` doesn't apply — commented in-source).
- **Zero-config in the editor:** `Awake` self-wires the pads + NavMeshSurface from children (R8 self-wiring); `Start` auto-fills the spawnable-prefab and cue-book lists via AssetDatabase on first run (`#if UNITY_EDITOR`); the ground NavMesh **bakes at runtime** (`NavMeshSurface.BuildNavMesh()` on a flat plane = milliseconds) — no manual bake. Context menus (`Auto-Fill Spawnables` / `Auto-Fill Cue Books`) serialize the lists for development builds. **`Start` also snaps both twins onto the TwinPad** (smoke-test finding: the twins wake at their Persistent-authored *level* position — off TestLab's plane — and fell into the void before anything ran; +1 m up-offset so the CharacterController never re-enables intersecting the ground).
- **`EnemyPool.Instance`** — the standard Persistent-singleton pair (duplicate-destroy `Awake` guard + null-on-`OnDestroy`), same Phase 5.1 treatment `EnemySpawner` got; the pool was the only R4 target the area-resident debugger had no lawful path to. In-scene consumers keep their serialized refs (R1).
- **TestLab.unity** (`Scenes/Sandbox/`): `DebugLab` root (GameDebuggerV2) → `Ground` (100×100 m plane + NavMeshSurface) + `EnemyPad` + `TwinPad`, one directional light. No camera/AudioListener/EventSystem (R9 — Persistent owns them; `PersistentSceneAutoLoader` brings Persistent + twins + managers on direct play). **Never added to Build Settings.**
- **Known v1 gaps (documented in-source):** TetherBreaker chain-throw is BT-internal (no public API) — drive it by teleporting a twin into range; Severed grief-rage = pair mechanics (pair spawning not in v1) — bench the aura via the Enraged mood button; SiphonGhost is spawned by Siphon, not the pool. Roles: P13 input-regression rig · P11 Manpu/aura bench · PSO trace source (instruction.md §19 F1) · cue-authoring preview room.
- **Smoke-test round 2 (user + MCP play run, same day):** panel converted from a fixed `BeginArea` to a **draggable `GUI.Window` re-clamped to the game view every frame** (fixed rect was half-cut on smaller/scaled Game views — user-reported; width yields to narrow views, title bar drags); **FX stop buttons** added to the cue section — `Stop FX on selected` (`FxManager.StopAllOn(transform)`) + `STOP ALL FX` (`FxManager.StopAll()`) — the escape hatch for code-ended held cues (mood auras, corruption state, looping ids) that nothing on a bench would ever stop; `ManpuSlot`'s stale-handle guard means a stopped mood aura restarts on the next transition. MCP play-mode smoke test PASSED: 0 console errors/warnings, twins snapped to TwinPad (±1 m, standing), pooled spawn verified end-to-end (`SmartEnemyGroupGrab` on the runtime-baked NavMesh, `isOnNavMesh=true`), `EnemyDeathNotifier.Instance` present (the one-off ManpuReactionListener error did NOT recur — no BUGS entry).

### Changed — P11: Manpu held mood-loop + EnemyVFXController retired (mood VFX is Manpu-only now) (2026-07-04)
Files: `Manpu/ManpuVocabulary.cs`, `Manpu/ManpuSlot.cs`, `Manpu/ManpuGlyph.cs` (capability); `EnemyAI/Enemy.cs`, `EnemyAI/Types/{SeveredEnemy,TetherBreakerEnemy,WitnessEnemy,PenitentEnemy,ChainCommander,GrandSummoner,PenitentCommander}.cs`, `SpawnSystem/EnemyPool.cs`, `AIFramework/PlanetOfTwinsAI/AI/Mood/EnemyMoodSystem.cs`, `AIFramework/PlanetOfTwinsAI/AI/Mood/MoodVfxTag.cs`, `AIFramework/PlanetOfTwinsAI/AI/Bond/EnemyDarkEnergy.cs`, `AIFramework/PlanetOfTwinsAI/BehaviourTree/Action/BTActionComboAttack.cs`, `Editor/Validation/CueIdVerifierWindow.cs` (retirement); **deleted** `UI/Enemy/EnemyVfxController.cs`. 0 CS errors (MCP-verified).

**Capability layer (§24.8 gaps 1–3):**
- **Held mood loop.** `ManpuVocabulary.MoodEntry` gains `loopPrefab` (a sustained aura ParticleSystem). `ManpuSlot` runs an aura channel (`UpdateMoodLoop`/`StopMoodLoop`): started on mood ENTER, stopped on EXIT + `Clear` (pool despawn). Pool-safe (stale-handle guard via `IsPlaying`), rides the enemy body (`CueContext.Follow`). This is the held-loop replacement for the old EnemyVFXController rage/panic loops.
- **De-gated sprite.** The aura is INDEPENDENT of the glyph sprite (plays with no sprite authored) and of R1 ability ownership (it is a body channel, not the glyph). Only the transient glyph *pulse* stays gated by `HasVisual` + R2 + R1.
- **Leak fix.** `ManpuGlyph.PlayAccents` now tracks the burst-accent handle and stops it on `Hide` (a looping `burstPrefab` no longer leaks; a one-shot is unaffected).

**Retirement (`EnemyVFXController` deleted; mood VFX is Manpu-only):** most `PlayRage/PlayBuff/PlayDarkEnergy` sites sat next to (or were covered by) a real mood transition, so retiring them just lets Manpu drive the aura autonomously.
- **Severed grief-rage** → removed the `PlayRage` (partner death already drives Grieving→Enraged via `EnemySocialBond`; the Enraged `loopPrefab` is the aura).
- **TetherBreaker chain-broken** → `TransitionTo(Enraged)` (the one site with no prior mood transition; behaviour still from `_inRage`/GOAP, mood only drives the aura).
- **Witness** → buff sites dropped (the Common `on_AlliesBuff` cue is the sole buff visual now); bomb-panic → `TransitionTo(Panicked)` with a **stomp-safe** guarded return (`if CurrentMood == Panicked`).
- **Fear** (`Enemy.FearRoutine`, twin-applied flee) → `TransitionTo(Panicked)` + guarded return (brain is paused during flee, so mood modifiers can't fight the forced routine).
- **Dark energy** → per the user, a distinct STATE not a mood ("independent, consumed entirely by corruption"): `EnemyDarkEnergy` now owns a **held corruption-state cue** (`_corruptionStateBook`/`_corruptionStateCueId`, serialized, null-safe), started on bond-break (a one-way latch) and stopped on `OnDisable`/`OnDestroy`. The existing `TransitionTo(Aggressive)` behavioural transition stays.
- **Commanders** (per the user, kept as greppable STUBS so the intent isn't lost): death-cascade soldier `PlayRage` removed (the adjacent `TransitionTo(Enraged)` drives Manpu); the deferred commander abilities (ChainStrike / DivineShaft / DarkShield) carry `// TODO(§24.8)` stub markers.
- **BTActionComboAttack** → all ~21 per-combo `PlayRage/Buff/DarkEnergy` calls removed; combo activation is not a mood, so emotional expression flows entirely through the Manpu mood layer.
- **Penitent** (dropped from roster) → rage calls replaced with `TODO(§24.8)` markers to re-add via mood during the rework. `EnemyMoodSystem.PlayMoodReaction` block deleted (the `TransitionTo` above it already fires `OnMoodChanged` → Manpu). `EnemyPool.Return` `StopAll` removed (`ManpuSlot.Clear` at the existing line + `StopAllOn` cover despawn). `MoodVFXTag`/`vfxTag` kept as dead data (dropping them = a separate GUID-safe asset pass).

**Authoring TODO (user, in Unity):** add `loopPrefab` to the ManpuVocabulary rows that should carry an aura (Enraged, Panicked, Aggressive…); assign `_corruptionStateBook` + author its cue on the EnemyDarkEnergy prefabs. All null-safe until then — no aura, no errors. `MoodAmbient` body-tint still conveys mood in the meantime.

**DoD status:** code complete, compiles clean. Player-facing verification (rage aura survives pool reuse; no leaked loops on despawn/unload; Severed loop ends with grief-rage) pending in-editor once the vocabulary auras are authored.

### Changed — P10 doc-truth + ledger sweep: architecture review written into the docs; zero code changes (2026-07-03)
Files: `CLAUDE.md`, `game.md`, `instruction.md`, `BUGS.md`, `ArtStyle.md`, `changelog.md`; deleted `CUEBOOK_AUDIT_TEMP.md` (folded into game.md §23.12 items 5–7).

The full-code architecture review (this session) found the docs materially stale against the code — the same failure class that once broke the enemy system. Every claim below was re-verified against source before writing.

- **CLAUDE.md corrections:** R10 rewritten — `TimeScaleService` is **LIVE** (all seven historical writers migrated; direct `Time.timeScale` writes are a rejected change). `MonoBehaviourSingleton<T>` footgun rewritten as **deliberate Exemption E1** (the 1.4 "fix" broke the enemy/perception stack — do not reattempt). "SoftReset restores 7 of 9 trees" → **fixed-verified** (all 9 via `SkillTreeRuntimeState.Snapshot`, `SoftResetController.cs:173-175`). `currentNodeIndex` footgun → **Phase 4 landed** (computed property, `AbilityUpgradeData.cs:54`). Read-order updated (game.md §23.11/§25/§26, instruction.md P10–P19); three keep-clean-for-co-op conventions added; §19 dead-list += AIFramework GameDebugger + the 9 stale `MANPU_SYSTEM.md` comment refs flagged.
- **game.md new sections:** §20.4 folder-migration order (staged, GUID-safe, asmdef-carve caveats) + §20.3 Phase-4 bullet corrected; **§23.13** Fx/Manpu package extraction design (2 seams: `IFxSceneEvents`, `IManpuGlyphTarget`); **§23.14** cue-control additions (shake noise/rot-pos/falloff · `isVariant` element groups · material-float track element; light + audio-randomization explicitly excluded as already-existing); **§23.15** tool-suite verdicts + specs (GameDebugger v2 + TestLab · Scene Health Dashboard with feature-wiring/count/volume recipes · upgrade-data editor · NewAreaSceneWindow full kit); **§23.16** upgrade-tier VFX (`cueIdOverride` per node, one book, plumb now with the single id); **§24.8** Manpu capability map + held-mood-loop design + `EnemyVFXController` retirement (13-file census, PlayBuff dropped for `on_AlliesBuff`); **§25** production-readiness review (indie-AA, systems-complete vertical slice; blockers; perf notes; Awake-caching = correct-keep; GameplayPool design); **§26** multiplayer feasibility (couch = weeks after P13; net = months, Setsuna hardest); §23.12 += items 6–7 (unwired enemy basic attacks; Manpu 8-glyph starter set) + item 5 extended (id typos, SiphonGhost book, enemy-book home decision).
- **instruction.md:** new **§17 Exemption Ledger** (E1 = MonoBehaviourSingleton DDOL/fabrication, locked); **§18 Phase Roadmap P10–P19** (P10 = this revision; P13 carries the HARD tutorial-gate/no-breakage contract); **§19 Future Additions** (PSO warmup design F1, couch/net/palette F2–F4); **P8.7** backlog additions (test asmdef first — EditMode tests undiscoverable; `BurnTickLoop` alloc; Find-scans → registry; log gating). R3 text + Phase 1.4 rewritten to carry the E1 cancellation (1.4c static-state hygiene stays valid).
- **ArtStyle.md:** new **§10** faction palettes + art canon (potimg codex mapping; Luminari gold / Vethara blue-violet / Khal-Vor teal hex ramps; **Voreth two-state** wild-magenta vs refined-teal; near-white-core rule; Kai=RIGHT=Vethara, Lyra=LEFT=Luminari) and **§11** post/grading spec (base look; the 4-priority volume architecture StoryGrade 0 → area 10 → crack 20 → failure 30; `StoryGradeDirector` + 6 profiles over the 10 story beats with the losing-end sequel hook). game.md §17.1 points to it.
- **BUGS.md sweep (evidence-cited per entry, no blind flips):** 26 stale Opens → **Fixed** (the ledger hadn't been swept since 2026-06-22 while Phases 1–7.6 + TimeScaleService landed); **BUG-019 → Won't-Fix (E1)**; W01/W05/W13/W16/W20 annotated; 6 remain genuinely Open (Penitent rework, SoulConv cap, debug keys, CommonStatic, entry-12, steepness) + BUG-032 In-Progress. New state `Won't-Fix` added to the legend. Counts: Open 32→6, Fixed 10→35.

### Added — Ability cue books wired: the 7 remaining player abilities that had ids but no caller now play their cues (2026-07-03)
Files: `Assets/Scripts/Players/Ability/Systems/PossessionAbility.cs`, `SetsunaSystem.cs`, `AccordStateSystem.cs`, `SoulConvergenceSystem.cs`, `CoalesceSystem`/`CoalesceAura.cs`, `Assets/Scripts/Players/Ability/AccordAbility/RadiantSeekerAbility.cs`, `RadiantSeekerOrb.cs`, `EmpowerSystem.cs`.

These books were authored + assigned in `PlayerVfxLibrary` but their systems made **zero `PlayBook` calls**. Wired each following the canonical `StunAbility` held-handle pattern (per-target held cue, stopped at the lifecycle end), lazy-resolving the book from `VfxLibraryProvider.Instance.Player.<slot>` (R4):

- **Possess** — `Possess_Active` held on the caster for the window (scaled to range), `Possess_Hit` held per possessed enemy; both stopped in `End()`. Direct mirror of Stun.
- **Setsuna** — `setsuna_chargeKai/Lyra` held per twin during the charge, `setsuna_trailKai/Lyra` held per twin during the slow-mo window (author the trail element unscaled so it animates at full speed under `timeScale 0.15`).
- **AccordState** — `accord_ChargeUpKai/Lyra` per twin during charge; `accord_ActiveKai/Lyra` **and** the single `accord_ActiveBuff` (played on each twin) held for the whole active window; `accord_Shockwave` burst at each `FireShockwave` origin.
- **RadiantSeeker** — `radorb_cast` burst at spawn; `radorb_hit` detonation burst + `radorb_hiteffect` per possessed enemy at the orb's `Detonate` (World cues survive the orb's immediate `Destroy`).
- **Empower** — `empower_buff` held, **Following** the empowered twin (Activate → EndAbility). Complements the existing `empower_pulse` knockback burst.
- **Coalesce** — `on_aura` held, Following the `CoalesceAura` (rides the host while parented, stays put on linger). **The aura prefab's embedded `ParticleSystem` is removed** — the cue is now the sole visual (`_particles` field/logic deleted; cue stopped on linger-end + `OnDestroy`). `on_burningaura` is just the upgrade's name, not a separate visual, so only `on_aura` is wired.
- **SoulConvergence** — `soulcon_shieldkai/lyra` are now the **shield visual** (the shield prefab keeps only its collider); `soulcon_buff` held on each twin. All four held for the power-state window (Activate → Deactivate/ForceDeactivate).
- **Teleport (Weaver's Gate)** — `tele_castmark` (landing telegraph @ destination), `tele_castout` (departure burst @ caster), `tele_casttravel` (held, Following the soul both out and back), `tele_castin` (arrival burst @ destination). The gate-helix set-piece rides the `tele_casttravel` element (driver authored on that prefab — see the gate-helix plan).

**Every player ability book is now wired** (grep-verified: all 13 `FxIds.Player.*` domains have callers). **Authoring notes:** remove the embedded `ParticleSystem` from the `CoalesceAura` prefab and the Animation visual from the SC shield prefab (keep its collider); author the Setsuna trail elements unscaled; author the gate-helix on the `tele_casttravel` prefab.

### Added — Environment cue wiring: ritual-site site-glow + spawn-point id cleanup; fixed the never-called `Occupy` (2026-07-03)
Files: `Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/POI/RitualSitePOI.cs`, `Assets/Scripts/AIFramework/PlanetOfTwinsAI/BehaviourTree/Action/BTActionWitnessRitualPath.cs`, `Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/POI/SpawnPointPOI.cs`, `Assets/Scripts/Fx/CueBooks/Environment/RitualSiteCueBook.asset`, `Assets/Scripts/Fx/Generated/FxIds.cs` (regenerated).

**RitualSiteCueBook** was an authored-but-orphaned Environment book (`effects: []`, no `FxIds`, no consumer). Wired it to the ritual **site** object: the book now defines `On_Occupy`, and `RitualSitePOI` plays it **held** on `Occupy` (site glow at the static site, `World`-anchored) and stops it on `Vacate` + `OnDisable` (area-unload safety; a held cue must not outlive the site). This is the *site's* own visual — complementary to the Witness *caster's* circle (`On_WitnessRitualStart` on the enemy Witness book).

Doing this surfaced a latent AI bug: **`RitualSitePOI.Occupy` was never called anywhere**, so `IsOccupied` stayed `false` forever and `SpawnZone.GetSafestRitualSite` (which skips `IsOccupied` sites) could hand the **same** site to two Witnesses. `BTActionWitnessRitualPath` now calls `_targetSite.Occupy(_poiTracker)` on ritual-arrival (released by the existing `Vacate` in `OnExit`) — this both fires the cue and makes the occupancy filter actually work.

**SpawnPointPOI**: `spawn_hit` was played via a raw string literal → now `FxIds.Unsorted.SpawnPointCueBook.spawn_hit`. (The book's other 3 ids — `spawn_rechargeparticle`/`spawn_portal`/`spawn_disable` — remain intentionally **driver-owned** state visuals via `SpawnPointVisualDriver`, not cues; left in the book as authored param notes.)

**Authoring note:** assign `RitualSiteCueBook` to `RitualSitePOI._cueBook` on each ritual-site GO, and author the `On_Occupy` particle (currently a placeholder prefab with `localScale 0`). FxIds was regenerated so `Unsorted.RitualSiteCueBook.On_Occupy` exists.

### Fixed — Player ability cues called stale raw-string ids (silently played nothing); wired to `FxIds` + per-twin (2026-07-02)
Files: `Assets/Scripts/Combat/MeleeAttackStrategy.cs`, `Assets/Scripts/Combat/PlayerAttackController.cs`, `Assets/Scripts/Players/Ability/AccordAbility/AccordMeleeAbility.cs`, `Assets/Scripts/Players/Ability/AccordAbility/VoidStrikeAbility.cs`, `Assets/Scripts/Players/Ability/AccordAbility/AccordSpiritAgent.cs`, `Assets/Scripts/Players/Ability/Systems/AccordStateSystem.cs`, `Assets/Scripts/Players/Ability/Systems/AccordSpiritSystem.cs`, `Assets/Scripts/Players/Ability/Systems/SoulConvergenceSystem.cs`, `Assets/Scripts/Players/Ability/Systems/SoulPulseSystem.cs`, `Assets/Scripts/Players/Ability/Systems/EmpowerSystem.cs`.

After the Cue Books were re-authored into the VFX Library layer, the player consumers still passed the **old literal ids** (`"swing"`, `"hit"`, `"charge"`, `"loop"`, `"ring"`, `"arrive"`, `"pulse_fire"`, `"empower_pulse"`) that no longer exist on their books — every one was a `PlayBook` miss (LogWarn + no VFX). Migrated all of them to the generated `FxIds` constants and split the ones that are **per-twin** (Kai = right/Vethara, Lyra = left/Luminari):

- **Normal melee** (`MeleeAttackStrategy` + `PlayerAttackController`): ctor gained `slashId`/`hitId`; `PlayerAttackController` gained a `[SerializeField] bool isKai` (mirrors `isSoul`; set it on Kai's controller) so the slash is `on_meleeSlashKai`/`on_meleeSlashLyra`. Slash now **Follows** the swinging twin, hit spark stays **World** at the enemy (per the attack-book "slashes Follow, hits World" rule).
- **Accord melee** (`AccordMeleeAbility`): was resolving the **empty** `PlayerVfxLibrary.AccordMelee` slot → now resolves the consolidated `.Attack` book where the 6 melee ids actually live; `Execute` gained `bool isKai` (passed by `AccordStateSystem` as `false`/`true` for left/right) → `On_AccordMeleeSlashKai`/`Lyra` (Follow) + `On_AccordMeleeHit` (World).
- **Accord Spirit charges/knockback** (`SoulConvergenceSystem`, `AccordSpiritSystem`): per-twin `soulcon_chargekai/lyra`, `on_accspiritKai/Lyra`, and `on_accspiritknocback`.
- **Accord Spirit arrival** (`AccordSpiritAgent`): removed the intermediate arrival-charge (no art yet) + the stale `"ring"` id; arrival now opens one held **per-twin portal** `on_accspiritKaiportal`/`Lyraportal` (`_isKai` plumbed via `AccordSpiritSystem.SpawnSpirit` as `twin == _rightTwin`).
- **Void Strike** (`VoidStrikeAbility`): held hazard-point void → `on_voidstrikecast`; added `on_voidstrikeTakingDamage` as a per-enemy damage reaction on each DoT tick that lands (World at chest).
- **Soul Pulse** → `pulse_fire`; **Empower** → `empower_pulse`.

**Authoring note:** designers must set the new `isKai` toggle on each twin's `PlayerAttackController` (checked on Kai, unchecked on Lyra + Soul). The `PlayerVfxLibrary.AccordMelee` slot is now dead (all ids consolidated onto `Attack`) — remove in a later GUID-safe commit.

### Added — Tether-Breaker chain: mesh-free beam visual (`ChainBeamDriver`) + fall-and-reel miss (2026-06-28)
Files: `Assets/Scripts/Combat/ChainBeamDriver.cs` (new), `Assets/Scripts/Combat/ChainProjectile.cs`, `Assets/Scripts/EnemyAI/Types/TetherBreakerEnemy.cs`, `Assets/Scripts/EnemyAI/Types/Data/TetherBreakerEnemyData.cs`, `Assets/Scripts/AIFramework/PlanetOfTwinsAI/BehaviourTree/Action/BTActionChainAttack.cs`.

The chain was a 2-point `LineRenderer` — straight, unlit, no movement, no "being pulled" feel. Added **`ChainBeamDriver`**, a mesh-free visual set-piece (HelixFollower-style, lane-3): a procedural **multi-point LineRenderer** (camera-facing strip, no mesh) with **catenary sag + perpendicular Perlin wobble**, plus an optional **billboard-quad spark system** emitted along the whole length. Wobble amplitude and spark rate scale with **tension** (high while a twin is grabbed/dragged → the "wobble when pulling" feel). Glow is the LineRenderer's HDR/additive emissive material into URP Bloom (no extra lights). Pool-safe (Perlin wobble = no carried state; `OnDisable` clears geometry). Three modes: **Taut**, **Grounded** (after a miss: ~1 m straight out of the hand then a curve lying on the ground), **Retracting** (reels the fallen chain back to the hand).

`ChainProjectile` now delegates its line to `_beamDriver` when wired (**null = legacy 2-point fallback, nothing breaks before the prefab is updated**), and on miss runs a **`MissReelRoutine`** (grounded → retract over `chainPullDuration` → despawn) instead of the old `Destroy(gameObject, 0.5f)`. New `TetherBreakerEnemyData.chainPullDuration` (default **1.2 s**) splits the **1.8 s** miss cooldown: `ChainMissCooldown` now `Movement.Stop()`s and **stands still reeling for the pull window (1.2 s)**, then nulls `_activeChain` so the BT releases him to **reposition for the remaining 0.6 s while still unable to throw** (`_chainOnCooldown`), throw-ready at 1.8 s. `BTActionChainAttack` gained a guard: when `ChainOnCooldown && !ChainActive` it returns `Failed` so the Selector falls to `ChaseTarget` during the recovery window instead of standing frozen. Chain and enemy share `chainPullDuration`, so the reel visual and the gameplay state stay in sync (also fixes the prior 0.5 s-vanish / 1.8 s-busy mismatch).

**Pending (Unity Editor, MCP was offline this session — unverified):** add `ChainBeamDriver` to the chain prefab + assign its LineRenderer (HDR/additive material, width curve) and a World-sim billboard spark system; wire `ChainProjectile._beamDriver`. Compile + play-verify a miss (fall + reel) and a successful drag (taut wobble) on both entry paths.

### Changed — Cue Book start modes redesigned: 3 clear modes + event-driven "after completion" + author lint (2026-06-25)
Files: `Assets/Scripts/Fx/Core/CueStartMode.cs`, `Assets/Scripts/Fx/Core/CueSchedule.cs`, `Assets/Scripts/Fx/CueBookRunner.cs`, `Assets/Scripts/Fx/Data/CueElement.cs`, `Assets/Scripts/Editor/Validation/CueBookLinter.cs` (new), `Assets/Scripts/Editor/Authoring/CueBookDataEditor.cs`, `Assets/Scripts/Editor/Validation/CueIdVerifierWindow.cs`, `Assets/Tests/EditMode/CueScheduleTests.cs`. Data: one-time migration of all `CueBookData` assets.

The old two-mode set was confusing: in a Cue Book `waitForCompletion` was always false, so **`AfterPrevious` chained on the previous element's START — identical to `WithPrevious`** (a lie), and there was **no way to say "truly after the previous finishes."** Replaced with **three non-overlapping modes**:
- **`Immediate`** — fire at the effect's t=0 (+ delay), ignoring the previous element (the first element always behaves this way).
- **`WithPrevious`** — fire at the previous element's START (+ delay); delay 0 = parallel, delay > 0 = staggered overlap.
- **`AfterPreviousCompletion`** — fire when the previous element actually **STOPS** (+ delay). **Event-driven** (`CueBookRunner` resolves it at runtime): "stops" = natural lifetime end **OR** a cut **OR** a gameplay `Stop` — whichever ends it. This is what makes a cut count as "completion" (cut a held element → the after-completion element fires at the cut). The separate `waitForCompletion` bool is **removed** — the mode now carries that meaning.

`CueBookRunner` rewritten from a single precompute to **incremental runtime resolution** (each element's start latches as its dependency resolves), keeping the same public API (`Begin`/`Tick`/`StopAll`/`IsFinished`/`HasLiveHeld`) so `FxManager` is unchanged. `CueSchedule` stays the pure-math oracle for finite-only timing (3 modes; unit-verified directly since the runner can't discover EditMode tests). A held element behind `AfterPreviousCompletion` with no cut/duration never fires (the held element keeps the book alive per its contract until gameplay Stops it) — this is guidance in the mode tooltip, not a flag (the linter can't tell a valid code-stopped loop from a mistake without seeing ability code).

**Author lint (`CueBookLinter`, flags only — never blocks/edits)** surfaced both per-element in the Cue Book inspector and project-wide in the Cue Id Verifier, sharing one analyzer. Each finding states the consequence + the fix: **F3** mode on a first element (ignored → set Immediate), **F4** circular cut (an `AfterPreviousCompletion` element that also cuts the predecessor it waits on → deadlock; move the cut to a parallel element or stop from code), **F5** a cut targeting an invalid/later index (cuts stop earlier elements only).

**Migration** (behavior-preserving): old `AfterPrevious` (now read as `Immediate`) on a non-first element → `WithPrevious` (its old chain-on-start behavior); first elements normalized to `Immediate` (they already start at t=0). 1 non-first element (KillParticleBook) + 6 first-elements normalized across 4 authored books; Stun/Possess behavior unchanged.

### Fixed — Boot-into-white-screen regression: FadeController now starts clear; timeline reveal is time-bounded (2026-06-24)
Files: `Assets/Scripts/UI/Fade/FadeController.cs`, `Assets/Scripts/TutorialSystem/TutorialTimelineStepSO.cs`

Non-dev boot landed on a permanent white screen. Two causes, both fixed in code:
1. **Canvas booted opaque** — the FadeCanvas `CanvasGroup` was authored/left at alpha 1 and nothing cleared it on startup, so the cover stayed up from frame 0 (the white in the screenshot). `FadeController.Awake` now forces `SetAlpha(0)` — the canvas is **clear by default** and only covers when something explicitly raises it (per user spec: alpha 0 default → snap white only at timeline end → fade to 0 over 2.3s).
2. **Reveal could hang opaque** — `FadeWhiteToGame` previously gated on the `FadeOut` **callback** (`done`); if the FadeController's coroutine ever failed to complete (null CanvasGroup, disabled GO) the callback never fired and `WaitUntil(() => done)` hung the step on white forever. Rewrote to a **time-bounded** `WaitForSecondsRealtime(whiteFadeInDuration)` that **always** ends with `SetClear()`, so the game is guaranteed visible before control returns regardless of the fade animation's fate. `FadeController.SetAlpha` now null-guards `_canvasGroup` (a null group can't throw mid-coroutine and kill the reveal) and `Awake` LogErrors if it's unassigned. The end-of-timeline white→clear is entirely code-driven; the dev-skip path stays an instant `SetClear()` (dev-only, polish not required).

### Changed — Dev "no tutorial" skip lands on the canonical tutorial-complete end-state + activates level / disables cutscene (2026-06-24)
Files: `Assets/Scripts/TutorialSystem/TutorialDirector.cs`, `Assets/Scripts/TutorialSystem/TutorialStepContext.cs`. Scene (L1_Park): `TutorialManager` context wired — `activateOnSkip=[MainLvl]`, `deactivateOnSkip=[TutorialTimelineDirector, TimelineTutorial]`.

`DevConfig.SkipTutorial` previously bypassed the tutorial but left the player with **input/abilities still gated** (the `TutorialInputGate` per-action locks default to false, and the intro timeline that normally activates the area spawn-config GO was bypassed). Reworked `SkipTutorial()` to converge on the **same end-state a completed tutorial produces** (mirrors `TutorialUnlockAllStepSO`): `inputGate.AllowAll()` **+** `TwinInputReader.SetGate(null)` (race-proof — a null gate = all input allowed, so it survives the gate's own `Start()` re-registering) **+** `TutorialContext.SetStage(Complete)`. Added two area-scene-local (R2-safe) serialized arrays on `TutorialStepContext`: **`activateOnSkip`** (the MainLvl/spawn-config root the timeline would have enabled — activated with the R11 inactive-ancestor guard, same as `TutorialCheckpoint.Activate`) and **`deactivateOnSkip`** (the cutscene timeline director + dolly-cam rig — disabled first so they can't re-grab cameras). `TutorialStepContext.Resolve()` now also falls back to `FindAnyObjectByType<TutorialInputGate>()` + LogError so an unwired gate slot can never silently leave input gated on a skip.

### Added — Player VFX Library: 8 empty player Cue Books created + wired (2026-06-24)
Files: `Assets/Scripts/Fx/CueBooks/{Teleport,Attack,SoulPulse,SoulConvergence,AccordSpirit,AccordMelee,VoidStrike,Empower}CueBook.asset` (new), `Assets/Scripts/Fx/Libraries/PlayerVfxLibrary.asset`

Filled the 8 unassigned `PlayerVfxLibrary` slots with empty `CueBookData` assets (timeMode set, empty effects list — no guessed ids/elements). All 10 player-domain slots now wired (Stun + Possess pre-existed). Authoring (effect ids, elements, prefabs) + Generate FxIds remain user content tasks.

### Added — Camera Cue: switch-proof cinematic "feel" (FOV % / impulse shake / post-proc depth) per cue element (2026-06-22)
Files: `Assets/Scripts/Fx/Data/CueElement.cs` (CameraCue block), `Assets/Scripts/Camera/CameraCueDriver.cs` (new), `Assets/Scripts/Fx/FxManager.cs`, `Assets/Scripts/Editor/Authoring/CueBookDataEditor.cs`, **removed** `Assets/Scripts/Fx/Data/CameraShakeCueData.cs`. Scene: `CameraCueDriver` + `CameraFeel` global Volume in Persistent; `CinemachineImpulseListener` on group + tutorial + QTE cams.

Replaces the thin (amplitude+duration, all-null) `CameraShakeCueData` with a richer per-element `CameraCue` block authored via a **+ Camera** button (mirrors + Sound). Three channels, all **switch-proof** and touching **NO camera transform** (so the Y-rule — Y tracks twin distance — is satisfied by construction):
- **FOV** — a PERCENTAGE of the *active* Cinemachine cam's base lens (`fovFactor`, clamped 0.5–1.5). Re-applied to whatever cam the distance-switcher makes active, so a mid-cue Close↔Top switch carries the punch instead of stranding it (the artifact the naive "modify the camera object" approach caused). Base FOV captured per cam, restored on release.
- **Shake** — Cinemachine Impulse. **Inline shape values** (`shakeShape` Recoil/Bump/Explosion/Rumble/Custom, `shakeAmplitude`, `shakeDuration`, `shakeFrequency`, `shakeDirection`) stamped onto ONE shared `CinemachineImpulseSource` on the driver, then fired — no object reference (R2-safe). Survives a cam switch because every cam has a `CinemachineImpulseListener`. (Recoil+short+sideways dir = slash; Rumble+long = earthquake.)
- **Depth** — drives the Persistent `CameraFeel` global Volume's weight 0→target→0; each cue references its OWN `VolumeProfile` (`depthProfile`) so fire/ice/etc. looks differ. Camera-independent (final render).

`CameraCueDriver` (Persistent, R3 singleton, unscaled time) holds ONE active FOV + ONE depth target — **last-writer-wins** on overlap (per spec). `FxManager.PlayElement` forwards `e.camera` → `Apply(cue, owner)`; `FxManager.Stop` forwards the stopped book's `owner` → `Release(owner)`, which blends FOV→100% / depth→0 over the cue's per-element `blendOut` (in-flight shake finishes naturally so the return isn't abrupt). `blendIn`/`blendOut` give the per-ability fast-slash-vs-smooth control. (Canonical reference now: game.md §23.9.) **Pending authoring:** per-cue `VolumeProfile`s for depth; a Stun camera block to verify (incl. a deliberate mid-stun cam switch). *(Superseded below: the real-FOV channel was REMOVED after testing — see "Camera Cue follow-ups".)*

### Changed — Camera Cue follow-ups: shake made visible, FOV blend smoothed, then real-FOV channel REMOVED (2026-06-22)
Files: `Assets/Scripts/Camera/CameraCueDriver.cs`, `Assets/Scripts/Fx/Data/CueElement.cs`, `Assets/Scripts/Editor/Authoring/CueBookDataEditor.cs`

Three fixes after live testing the Camera Cue:
1. **Shake was imperceptible** — the impulse source's signal dissipated over distance (source on the Persistent driver, far from the camera). Forced every shake to `ImpulseType = Uniform` (all listeners react equally, **no distance falloff**), so amplitude is what the cue authored. Shake confirmed visible.
2. **FOV blend was near-instant** — the blend used a hardcoded travel distance of 1.0, so a small punch (e.g. 1.0→0.8) arrived ~5× too fast. Rewrote to **time-based SmoothStep**: `blendIn`/`blendOut` now mean literal seconds to reach target, eased.
3. **Real-FOV channel REMOVED entirely** — a **group camera computes its own FOV every frame** to keep both twins framed, so the driver's `cam.Lens.FieldOfView = base × factor` write **fought the framing** — a visible zoom even at factor 1.0, every first ability use (the driver froze a stale "base" the moment it first touched the cam). Deleted `useFov`/`fovFactor` from `CameraCue`, and all FOV machinery (`_baseFov` dict, `DriveFov`, brain lookup) from the driver. **The "zoom" feel now lives in the depth channel** — author a **Lens Distortion** (`scale`) override into the cue's depth `VolumeProfile` (post-process, never touches the camera). Editor `+ Camera` block now shows Shake + Depth only. Removed the temp diagnostic log.

### Added — Camera flip fix (BUG-037): CameraRotationGuard + white→game fade at cutscene end (2026-06-22)
Files: `Assets/Scripts/Camera/CameraRotationGuard.cs` (new), `Assets/Scripts/TutorialSystem/TutorialTimelineStepSO.cs`, `Assets/Scripts/TutorialSystem/TutorialStepContext.cs`, `Assets/Scripts/TutorialSystem/TutorialDirector.cs`. Scene: `CameraRotationGuard` GO in Persistent (4 cams wired); FadeCanvas image set white.

The recurring 180° camera flip is the tutorial **timeline** leaving a transpose cam at an animated Y=180 pose with no proper revert (confirmed: no camera *code* writes rotation; the timeline animation tracks do; `.playable` not hand-editable — R11). `CameraRotationGuard` (Persistent) snapshots each gameplay cam's **authored** local rotation at `Awake` (before the timeline runs) and re-applies it on demand — no hardcoded value. At cutscene end the screen is white: `TutorialTimelineStepSO` restores the cams **behind the white** (snap invisible), then `FadeController.FadeOut` reveals the game over `whiteFadeInDuration` (2.3s). `TutorialStepContext` resolves the Persistent `FadeController`/`CameraRotationGuard` via `FindAnyObjectByType` (R2-safe, not serialized cross-scene).

### Added — Dev mode system: DevConfig (master fail-safe + independent Trainer/SkipTutorial toggles, build-safe) (2026-06-22)
Files: `Assets/Scripts/Debug/DevConfig.cs` (new), `Assets/Resources/DevConfig.asset` (new), `Assets/Scripts/Debug/SkillPointDebug.cs`, `Assets/Scripts/TutorialSystem/TutorialDirector.cs`, `Assets/Scripts/SceneLaoder/GameBootstrapper.cs`

One central dev switch (`Resources/DevConfig`, also assignable on `GameBootstrapper`). **Independent toggles** (none gates another): **Trainer** (skill-point hack keys/UI via `SkillPointDebug`, which disables its GO unless `DevConfig.Trainer`) — works even with normal scene flow; **Skip Tutorial** (`TutorialDirector` bypasses tutorial+cutscene; `GameBootstrapper` dev-boots to its **Dev Start Area**, a field renamed from `firstAreaScene`/`Location` to remove confusion with the game's real first area). Behind a **Master Enabled** fail-safe (OFF → all dev force-off regardless of toggles) + **build safety**: flags only apply in the Editor or a **Development Build**; a **release** build forces all off (`Debug.isDebugBuild`), so debug can never ship to players. The dev-skip path also clears the fade + restores cameras (else it boots into an opaque screen). `GameBootstrapper` boot path now reads `DevConfig.SkipTutorial` (dev-boot vs intro) instead of hand-editing `introScene`.

### Added — VFX Library layer + runtime cue scaling + per-element transform overrides (2026-06-22)
Files: `Assets/Scripts/Fx/Libraries/PlayerVfxLibrary.cs` + `VfxLibraryProvider.cs` (new), `Assets/Scripts/Fx/CueContext.cs`, `Assets/Scripts/Fx/Data/CueElement.cs`, `Assets/Scripts/Fx/FxManager.cs`, `Assets/Scripts/Editor/Validation/CueIdVerifierWindow.cs`, `Assets/Scripts/Players/TwinAbilitySetup.cs` + `StunAbility.cs`

A placement layer above the Cue Book (book model unchanged). `*VfxLibrary` SO per domain holds that domain's `CueBookData` slots (PascalCase); Persistent `VfxLibraryProvider` (R4) hands them out; consumers pull `provider.Player.Stun` instead of a scattered serialized book. **Generate FxIds** now nests by library/slot (`FxIds.Player.Stun.OnStun_Active`); id written once on the book. Player slice migrated (`StunAbility`). Plus **`CueContext.scale`** (sizes the spawned instance, pool-safe; `StunAbility` passes `currentRange/baseRange` so range-VFX grows with upgrades) and per-element **`localRotation`/`localScale`** transform overrides (`(0,0,0)` scale = unset → `(1,1,1)`, so pre-existing cues aren't invisible).

### Added — Per-element particle/VFX transform overrides (rotation + scale) on cue elements (2026-06-22)
Files: `Assets/Scripts/Fx/Data/CueElement.cs`, `Assets/Scripts/Fx/FxManager.cs`, `Assets/Scripts/Editor/Authoring/CueBookDataEditor.cs`

A cue element had only `localOffset` — a particle whose prefab faced the wrong way couldn't be corrected per-cue. Added **`localRotation`** (euler°) and **`localScale`** (Vector3) alongside it. Final rotation = `spawn/follow rotation × Euler(localRotation)` (re-applied each frame in follow mode, so the offset tracks the target); final scale = `prefab.localScale × localScale × CueContext.scale`. Exposed on Particle/Vfx elements in the editor (`Local Offset / Rotation / Scale`). **Migration guard:** a `localScale` of `(0,0,0)` — how elements authored before this field deserialize — is treated as `(1,1,1)` so existing cues aren't rendered invisible.

### Added — Runtime cue scaling: `CueContext.scale` so VFX radius tracks upgraded ability range (2026-06-22)
Files: `Assets/Scripts/Players/PlayerDeathRescueProxy.cs`, `Assets/Scripts/EnemyAI/Traps/SkeletonHandTrap.cs`, `Assets/Scripts/Heath/PlayerHealthComponent.cs`

**Symptom:** Enemies hit a twin every frame (`[Health] Kai TakeDamage` spam) but health never dropped. Diagnosed live: `Kai._invincible=True` while rescue was fully complete (`PlayerDeathRescueProxy._isActive=False`, `RescueEventController._state=Idle`, twin active+alive). The user observed it *eventually* self-healed "after a long time."

**Root cause:** The post-rescue grace window (`PlayerDeathRescueProxy.InvincibilityFrames(2s)` and `SkeletonTrap.TrapRescueInvincibility(1.5s)`) did `SetInvincible(true)` → `WaitForSeconds(duration)` → `SetInvincible(false)`. `WaitForSeconds` is **scaled** time — when the window opened while `Time.timeScale` was low (Setsuna 0.15, or a transition at 0 around the timescale work), the 2s grace stretched into many real seconds, so the twin stayed un-damageable far past the intended window (self-healing only once enough wall-clock elapsed). The grace ids/duration were correct; the timer domain was wrong. (Live `TimeScaleService._requests` was empty and `timeScale=1` at diagnosis — so not a stuck-low timeScale, a *stale scaled wait* issued earlier.)

**Change:** Both grace coroutines now use `WaitForSecondsRealtime` (real wall-clock grace, immune to Setsuna/pause) and wrap the release in `try/finally` so `SetInvincible(false)` runs even if the coroutine is stopped (StopCoroutine/external reset). Trap also captures the rescued `Player` up front so a `_grabbedPlayer` clear between grab and release can't skip the release. Removed the per-hit diagnostic `Debug.Log` in `PlayerHealthComponent.TakeDamage`. Verified live: clearing the stuck flag restored damage immediately; user confirmed enemies damage + rescue both work after the fix.

### Added — VFX Library layer: domain-grouped Cue Book libraries + domain-nested FxIds (2026-06-22)
Files: `Assets/Scripts/Fx/Libraries/PlayerVfxLibrary.cs` (new), `Assets/Scripts/Fx/Libraries/VfxLibraryProvider.cs` (new), `Assets/Scripts/Editor/Validation/CueIdVerifierWindow.cs`, `Assets/Scripts/Players/TwinAbilitySetup.cs`

A central placement layer ABOVE the Cue Book (the book model is unchanged — still a container of NAMED effects played by id). One `*VfxLibrary` SO per domain holds that domain's `CueBookData` slots (PascalCase: `Stun`, `Possess`, `Attack`, …); a Persistent `VfxLibraryProvider` hands the libraries to runtime systems via R4. Consumers pull their book from the relevant library instead of carrying a scattered `[SerializeField] CueBookData`. The `CueIdVerifier`'s **Generate FxIds** now nests output by library/slot — `FxIds.Player.Stun.OnStun_Active` — so the id is written ONCE on the book and the callable constant is generated per domain (books in no library fall under `FxIds.Unsorted`). **Player slice (proof) only:** `StunAbility`'s book now comes from `PlayerVfxLibrary.Stun` via the provider (old `TwinAbilitySetup.stunBook` field removed). Enemy/Spawn libraries + the remaining ~20 consumer migrations are follow-up slices. **Pending user authoring:** create the `PlayerVfxLibrary` asset, wire it on a `VfxLibraryProvider` in Persistent, run Generate FxIds; then `StunAbility`'s 2 call sites move `FxIds.StunCueBook.*` → `FxIds.Player.Stun.*`. (Done via MCP: `PlayerVfxLibrary.asset` created + wired to a `VfxLibraryProvider` GO in Persistent; book slots null pending authoring.)

### Added — Runtime cue scaling: `CueContext.scale` so VFX radius tracks upgraded ability range (2026-06-22)
Files: `Assets/Scripts/Fx/CueContext.cs`, `Assets/Scripts/Fx/FxManager.cs`, `Assets/Scripts/Players/Ability/Systems/StunAbility.cs`

A looping ability VFX must match the ability's CURRENT (upgraded) range, not a fixed prefab size — the cue had no way to receive a runtime size. `CueContext` gains an optional uniform `float scale` (default 1 = prefab size; clamped >0). `FxManager.Place` now sets the spawned instance's `localScale = prefabKey.transform.localScale * ctx.scale` — read from the PREFAB each spawn (set, not accumulated) so a pooled instance never compounds a prior cue's scale (F2); applies to both particle and VFX-graph spawns in one place. `StunAbility` passes `scale: _currentRange / data.range` on the held `OnStun_Active` cast cue, so the VFX (authored to look right at base range) grows proportionally with range upgrades. Generic — every range-based ability (empower, accord…) can now size its cue to live data with one arg; the cue authoring model is unchanged.

### Added — Enemy world-space UI (health bars) + Manpu slot rebuilt on enemy variants (2026-06-22)
Files (assets/prefabs, via MCP): 10× `Assets/Models/Prefabs/Enemies/SmartEnemy*.prefab`, `Assets/Scripts/Manpu/Data/CueBook_Reactions.asset` (new), `Persistent.unity`

The enemy world-space UI was entirely absent (stripped in the re-greybox — no Canvas, `WorldSpaceHealthUI`, `HealthBarView`, `ManpuSlot`, or `ManpuGlyph` on any enemy prefab; verified by GUID + prefab API). Rebuilt by copying the player's proven `Canvas_KaiUI` (world-space Canvas + `MainCameraProvider` + `UIBillboard` + `WorldSpaceHealthUI` + `HealthBarView`) onto the enemies as `CanvasEnemyUI`, rewired `WorldSpaceHealthUI` to the **enemy path** (`player=null`, `enemyHealth=`each variant's own `EnemyHealthComponent` — verified per-variant, no cross-refs; event-driven via `EnemyHealthComponent.OnHealthChanged`, no polling). `ManpuSlot` added to all 10 variants (`_vocabulary`→ the existing (empty) `ManpuVocabulary` asset, `_glyph`→ each variant's `ManpuGlyph` SpriteRenderer). New `CueBook_Reactions` (ids `betrayed`/`ally_down`, elements empty) wired to the Persistent `ManpuReactionListener._reactionBook`. **Placed on the 10 SmartEnemy* variants, NOT `SmartEnemyBase`** (deliberate — don't commit UI to the base until all enemies are confirmed to need it). **Manpu confirmed enemy-only** (no player Manpu; `ManpuAbilityListener` targets the enemy's slot, not the caster). **Persistent** also gained `VfxLibraryProvider` + `ManpuReactionListener`/`ManpuAbilityListener` GOs. **Pending user authoring:** glyph sprites/particles into `ManpuVocabulary` rows, the `CueBook_Reactions` elements, and repositioning the canvas/glyph above each enemy's head. (Note: prefab edits applied via `LoadPrefabContents`/`SaveAsPrefabAsset` — disk-persistent, reload-safe.)

### Fixed — Rescue success left shared `IsRescueActive` stuck true → all melee enemies gated out of combat (BUG-038) (2026-06-21)
File: `Assets/Scripts/Players/RescueEventController.cs`

**Root cause (pre-existing, predates instruction.md — verified identical at init-day `5fa951d`):** `TransitionTo(next)` fires `OnRescueStateChanged(next)` *after* `EnterState(next)`. For terminal states (`Success`/`Failed`), `EnterState` calls `CleanupRescueEvent()`, which resets `_state = Idle` and fires `OnRescueStateChanged(Idle)`. Control then returns to `TransitionTo` and fires `OnRescueStateChanged(Success)` — **stomping the Idle event with a non-Idle value as the last word subscribers see.** `PoTWorldStateWriter.OnRescueStateChanged` set the shared `IsRescueActive = (state != Idle)` → stuck `true`. `GOAPGoalAttackTwin`/`GrabTwin` hard-gate on `IsRescueActive` → every enemy that mirrors the shared flag (`PoTGOAPBrainBase` line 90) refuses to attack. Diagnosed live: `RescueEventController.IsRescueActive=False` / `_state=Idle` but `SHARED IsRescueActive=True`.

**Change:** In `TransitionTo`, only fire `OnRescueStateChanged(next)` if `_state == next` after `EnterState` (i.e. EnterState didn't already cleanup-to-Idle). Verified live: after a real rescue Success (`WasSuccessful=True`), `SHARED IsRescueActive=False` and `HasRescueTarget=False`.

### Fixed — Utility goals never scored a target: wrong blackboard key + bool-reads-GameObject (BUG-039) (2026-06-21)
Files: 9× `Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/Utility/Data/Attack*UtilProfile.asset`, `Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/Utility/UtilityFactorKeys.cs`, `Assets/Scripts/AIFramework/PlanetOfTwinsAI/AI/Utility/UtilityGOAPGoalBase.cs`

**Root cause (authoring defects baked in when the utility system was created, commit `b8accd6` — never matched the package; the package has no utility system and reads `CommonCore.Names.Awareness_BestTarget` = `"Self.Awareness.BestTarget.GameObject"` directly, e.g. `GOAPGoal_Chase`):**
1. The "has target" factor's `blackboardKey` was the short string `"Awareness.BestTarget"` (also `UtilityFactorKeys.HasTarget`) — which never matched the canonical key perception writes. Factor read an empty key → 0 → weight-50 factor contributed nothing → score capped ~34.5 < activationThreshold 35 → `DoNotRun`.
2. Even with the correct key, `ReadBlackboardValue`'s `isBool` branch read the **bool** dictionary, but the key stores a **GameObject** (per-type dicts). Bool read missed it → 0.

**Changes:**
- All 9 attack profile assets + `UtilityFactorKeys.HasTarget`: `"Awareness.BestTarget"` → `"Self.Awareness.BestTarget.GameObject"`.
- `UtilityGOAPGoalBase.ReadBlackboardValue`: `isBool` branch now falls back to a GameObject read (presence ⇒ 1) when the bool read misses — matches the package's `!= null` target check.

Verified live: Severed + some Melee enemies now select `GOAPGoalAttackTwin` and chase (NavMeshAgent vel up to 6.35). GOAP confirmed running end-to-end (patrol via `GOAPGoalWander`/`GOAPActionWander`, engage via `GOAPGoalAttackTwin`/`GOAPActionAttackTwinMelee`).

### Fixed — PerceptionManagerBootstrapper fabricated a blank singleton, killing the real manager (BUG-040) (2026-06-21)
File: `Assets/Scripts/AIFramework/CommonCore/Perception/PerceptionManager.cs`

**Root cause:** `PerceptionManagerBootstrapper.Initialize()` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) called `PerceptionManager.Instance` when Bootstrap (scene 0) loaded — before Persistent and its real `PerceptionManager` existed. `MonoBehaviourSingleton<T>.Instance` (line 41) **fabricates** `new GameObject("Singleton<PerceptionManager>")` when none is found, DDOL's it, and it wins the singleton. When the real Persistent manager later `Awake`s, `ConstructIfNeeded` destroys *it* as the duplicate. Listeners/perceivables resolved the real manager via ServiceLocator and registered there → that manager was destroyed → surviving fabricated manager had empty `ActiveSensors`/`AllDetectionData` → no detection. Diagnosed live: `Instance` was `Singleton<CommonCore.PerceptionManager>` in `DontDestroyOnLoad` with `ActiveSensors=0`.

**Change:** `Initialize()` uses `Object.FindFirstObjectByType<PerceptionManager>()` (never fabricates) instead of `.Instance`; calls `OnBootstrapped()` only if found (it's a no-op for `PerceptionManager` anyway). Verified live: `Instance` is now the real `PerceptionManager` with populated dictionaries; enemies detect twins at strength 1.0.

### Fixed — PerceptionListener double-registered sensors on pooled enemies (BUG-041) (2026-06-21)
File: `Assets/Scripts/AIFramework/CommonCore/Perception/PerceptionListener.cs`

**Root cause:** In multi-scene the Persistent `PerceptionManager` already exists when `EnemyPool` pre-warms, so `ServiceLocator.AsyncLocateService<IPerceptionManager>` in `Awake` resolved **synchronously** — `RegisterListener` ran and set `_hasEverRegistered = true` *inside the same `Instantiate()`*, then `OnEnable` (which guarded on `!_hasEverRegistered`) immediately re-registered. Result: "X is attempting to register itself multiple times for EnemyVision/Hearing/ProximitySensor" spam ×3 per pooled enemy.

**Change:** Replaced "ever registered" with "currently registered" (`_isRegistered`) routed through one idempotent `RegisterAllSensors()`. `Awake` callback and `OnEnable` both call it (no-op if already registered); `OnDisable`/`OnDestroy` clear the flag (handles pool reuse). Verified live: registration spam gone, `_isRegistered=True`, listener linked to the live manager.

### Fixed — VisionSensor deregister left stale entries in Listeners/Perceivables list (BUG-036) (2026-06-20)
File: `Assets/Scripts/AIFramework/CommonCore/Perception/Sensors/VisionSensor.cs`

`DeregisterListener` removed from `Queries` and `ListenerConfigs` but not from `Listeners`; `DeregisterPerceivable` similarly skipped `Perceivables`. A deregistered entry remaining in these lists caused `RegisterPerceivable` / `RegisterListener` to create new `VisionQuery` objects for the deregistered Listener/Perceivable, leading to `KeyNotFoundException: 'X (CommonCore.PerceptionListener)' was not present in the dictionary` in `Tick()` at `ListenerConfigs[Listener]`. Existed before BUG-035 (triggered on `OnDestroy`); BUG-035 fix made it trigger on every pool-return via `OnDisable`.

**Changes:** Added `Listeners.Remove(InListener)` to `DeregisterListener`; added `Perceivables.Remove(InPerceivable)` to `DeregisterPerceivable`.

### Removed — BUG-034 diagnostic temp logs (2026-06-20)
Files: `VisionSensor.cs`, `PerceptionListener.cs`, `PerceptionManager.cs`

Removed all temporary diagnostic instrumentation added during BUG-034/035 investigation:
- `VisionSensor`: `ZZZ_CANARY_` LogError (every-frame canary), `[VisionTick]` query-count log + `_queryCountLogged`, `[VisionStrength]` strength log + `_strengthLogCount`, `[RunQuery ENTRY]` log + `_runQueryLogCount`.
- `PerceptionListener`: `[CanDetect ENTRY]` log + `_canDetectLogCount`, `[CanDetect] relationship` log + `_relationshipLogCount`, `[Blackboard]` focus-changed log + `_focusChangedLogCount`.
- `PerceptionManager`: `[PM] OnAwake` and `[PM] RegisterPerceivable` Debug.Log lines.
- Existing runtime-useful warnings retained: `RANGE FAIL`, `CONE FAIL`, `WRONG_GO`, `MISS` in `VisionSensor`; null-faction `[CanDetect] blocked` warning in `PerceptionListener`; duplicate-perceivable `LogError` in `PerceptionManager`.

### Fixed — Enemies never detect the player after multi-scene boot — PerceptionManager registration-order race (BUG-034) (2026-06-19)
File: `Assets/Scripts/AIFramework/CommonCore/Perception/PerceptionManager.cs`

**Root cause:** `RegisterPerceivable` (called by `Perceivable.Start()` on the player twins in Persistent) only attaches to sensors already in `ActiveSensors`. `RegisterListener`'s "create new sensor" branch never backfilled perceivables that arrived earlier. In multi-scene boot, Persistent fully loads before any area scene starts, so `ActiveSensors` is empty when the player registers — enemies that load later create sensors but never see the player. `VisionSensor.Queries` is empty forever, patrol never breaks.

**Changes:**
- Added `AllPerceivables` (`List<IPerceivable>`) tracking every ever-registered perceivable.
- Extracted `TryAttachPerceivableToSensor(ISensor, IPerceivable)` — handles list creation, duplicate-check (silent on overlap — expected during backfill), and `Sensor.RegisterPerceivable`.
- `RegisterPerceivable` — guards duplicate on `AllPerceivables`, then delegates to helper per active sensor (same behaviour when sensors already exist; now also seeds `AllPerceivables` for future backfills).
- `DeregisterPerceivable` — also removes from `AllPerceivables`.
- `RegisterListener` new-sensor branch — after `NewSensor.RegisterListener(...)` loops `AllPerceivables` → `TryAttachPerceivableToSensor(NewSensor, each)`. **This is the fix**: enemies that arrive after the player now backfill immediately.

Fix is at the shared-manager level — corrects the race for every current and future Perceivable/Listener pair in the project.

**DoD:** cold boot into L1_Park → enemy in player FOV/range transitions out of patrol on first encounter. Re-run restart-loop test (1.4c) to confirm detection survives Restart.

### Fixed — Rescue tutorial soft-locks the whole game when rescue beats the prompt (BUG-033) (2026-06-19)
Files: `TutorialRescueWatchStepSO.cs`, `TutorialOverlayController.cs`

Root cause: `TutorialRescueWatchStepSO.Execute` gated the success/failure watch behind
`WaitUntil(() => promptDone)`. The overlay holds `TimeScaleService.Request(this, 0f)` until the
player clicks Continue — but the rescue mash is input-driven (not `deltaTime`) so it can complete
while `timeScale = 0`. The latched `WasSuccessful` was never observed; Continue was never clicked;
`timeScale` stayed 0 forever → twins frozen, tutorial stuck.

**Changes:**
- **`TutorialRescueWatchStepSO`** — replaced the strict sequence (show prompt → wait dismiss → watch
  outcome) with a **RACE**: `yield return new WaitUntil(() => promptDone || rescue.WasSuccessful)`.
  If success beats the prompt, calls `ctx.overlay.Continue()` to force-release the `timeScale=0` hold,
  then `yield break`. Failure watch loop now only runs after the prompt is dismissed (correct — TTK
  timer uses `Time.deltaTime`, so failure cannot expire at `timeScale=0`). Also fixed a secondary
  deadlock: `ctx.overlay?.Show(...)` with a null overlay silently skipped the callback so `promptDone`
  stayed false forever; replaced with an explicit null-check + `Debug.LogError` + `promptDone = true`.
- **`TutorialOverlayController`** — added `public void Continue() => OnContinueClicked()` (idempotent
  via existing `if (!_isOpen) return;` guard) so steps can programmatically dismiss without sharing the
  ESC-arbiter path (`TriggerContinue`, single-consumer per its doc).

DoD: play through tutorial — rescue should complete and the tutorial should advance without requiring
a Continue click; twins should move freely afterward; the confinement boundary should lift normally.

### Investigated — Rescue tutorial soft-locks the whole game after the cutscene (BUG-033, diagnosis only — fix pending) (2026-06-19)
No code changed this entry — diagnosis recorded ahead of the fix (CLAUDE.md #10; fix spec coming via instruction.md).
- **Symptom:** after the intro/park timeline, the grabbed twin can be rescued (mash F succeeds) but the tutorial never advances; both twins are frozen and "can't leave the rescue area." Reads as *"the rescue checkpoint didn't load."* Intermittent.
- **Real cause:** the stall is in **step 11 `TutorialRescueWatchStepSO`**, not the checkpoint. The step does `WaitUntil(() => promptDone)` **before** the loop that checks `WasSuccessful`/`Failed`. `promptDone` only flips on the overlay's Continue click; the player rescues instead of clicking, so the already-latched success is **never observed** → permanent stall. Compounded by [`TutorialOverlayController.Show`](Assets/Scripts/TutorialSystem/TutorialOverlayController.cs#L103) holding `TimeScaleService.Request(this, 0f)` that never releases → `timeScale` stuck at 0 → twins frozen (the "can't go out of bounds" is a time-freeze, not the boundary). The rescue mash still completes at `timeScale 0` because mash progress is input-driven, not `deltaTime`-driven.
- **How it was proven (live, MCP):** console (survives play-stop) showed `RescueEventController TransitionTo … Success` **twice** ⇒ `WasSuccessful` latched true while the step was still parked on the prompt wait. Checkpoint `CheckpointsRescueL` read `active:true` + `IsCompleted:true`, ruling out the inactive-under-Activation-Track theory and confirming step 10 finished. Two earlier hypotheses (inactive checkpoint; "checkpoint completed early so `Activate()` no-ops") were corrected after noticing `RescueCheckpoint.asset` and `RescueTrapWatch.asset` **both** use `stage:5`, so the stage log couldn't distinguish steps 10 vs 11.
- **Planned fix (#2):** make the watch observe success/failure regardless of `promptDone`; close the prompt on success to release the `timeScale 0` hold; don't judge *failure* until the prompt is dismissed; fail loud if the overlay never opened; keep an active player-facing objective visual throughout. Full trail in **BUGS.md → BUG-033**; supersedes BUG-021's root-cause guess.

### Added — Per-element camera shake (Cinemachine Impulse) in the Cue Book (2026-06-19)
`CameraShakeCueData` was a no-op stub (`FxManager.PlayShake` logged "deferred"). Now each `CueElement` has an
optional **`cameraShake`** (`CameraShakeCueData`) — when the element fires, `FxManager` triggers a Cinemachine
Impulse (`CinemachineImpulseSource.GenerateImpulseWithForce(amplitude)`, `ImpulseDefinition.ImpulseDuration =
duration`). So you set, per particle/VFX, exactly which shake plays with it. `FxManager` auto-creates the impulse
source if none is assigned (assign a configured `CinemachineImpulseSource` to tune shape/frequency). **Setup:**
the camera needs a `CinemachineImpulseListener` to react. The standalone `Play(CameraShakeCueData)` path now works
too. Inspector shows a Camera Shake slot per element.

### Added — StunCueBook integration: FxIds constants, OnStun_Hit owned by StunAbility, StunVFXSystem → ImmobiliseAuraVFX (2026-06-19)
Wires the first authored Cue Book end-to-end per instruction.md §14.1d (per-target-effect owner rule) + game.md §23.8 (FxIds).
- **`FxIds` generated constants** (`Fx/Generated/FxIds.cs`): one nested class per `CueBookData`, one const per
  effect id (`FxIds.StunCueBook.OnStun_Active` / `OnStun_Hit`). The Cue Id Verifier gained a **"Generate FxIds"**
  button (`CueIdVerifierWindow.GenerateFxIds`) that (re)writes it from the live assets. Call sites use the const,
  not a raw string — autocompleted, compiler-checked, rename-safe; raw strings stay legal for dynamic ids.
- **`OnStun_Hit` owned by `StunAbility`** (the §14.1d fork, resolved — NOT a shared VFX engine): StunAbility plays
  the held `OnStun_Hit` per stunned enemy (Follow the enemy), keeps each `CueHandle` in `_hitHandles`, and stops
  them all at window-end. `OnStun_Active` plays held on the caster (Follow the twin), stopped at End. **Durations
  are never baked in the asset** — the effects loop; the ability's own upgrade-scaled timer (3/6/4 s) drives Stop.
  Ids are the asset's real `OnStun_Active`/`OnStun_Hit` (already correct — no typo "fix"), via `FxIds` consts.
- **`StunVFXSystem` → `ImmobiliseAuraVFX`** (Banned Lazy Work #14 — a class named for one ability served three):
  it no longer handles Stun (StunAbility owns that); it keeps the shared per-enemy held aura for **Possess**
  (Coalesce if wired). File + class renamed **preserving the `.meta` GUID** (scene component bindings survive);
  stale `ManpuAbilityListener` comment updated. No code referenced the type — only comments.

### Changed — Cue Book FINAL model: string-id effects + per-element audio + verifier (2026-06-18)
Supersedes the `FxEvent` / `EntityCueBook` / two-book entries below — those were intermediate. The Cue Book
is now exactly the user-specified container: **one book per thing = a list of NAMED effects (string `id`)**;
code plays the *correct* effect by id, never the whole book.
- **Removed `FxEvent`** (the global enum) entirely. `CueBookData` = `List<CueEntry{ string id; List<CueElement> }>`;
  `FxManager.PlayBook(book, string id, ctx)` looks the effect up by id (wrong id → LogWarning). `CueBookRunner`
  unchanged (still runs a flat element list).
- **Per-element audio.** Each `CueElement` carries its own `List<CueAudio>` — each sound has **loop / one-shot**
  and **Kill-With-Visual (default ON)** so it dies with its visual; several one-shots per element; `Sound`-kind =
  pure audio (no visual). `FxManager` schedules each element's audio (per-audio start delay) and stops the
  kill-with-visual handles when the visual stops. `AudioManager.Play` gained a `bool loop` override so a shared
  `SoundCueData` can loop in one element and one-shot in another.
- **`CueIdVerifierWindow`** (Tools ▸ Planet of Twins ▸ Cue Id Verifier) — the safety net for string ids: flags a
  `PlayBook("id")` literal no book defines (typo, with file:line), duplicate ids in a book, and a book id no code
  literal references (renamed/dead). Variable-passed ids (EnemyVFXController) are coverage-checked via all literals.
- **Inspector rebuilt** (`CueBookDataEditor`): per effect a string-id header + element list; per element the kind
  fields, its audio list (loop / kill-with-visual / delay per sound), timing, default-or-explicit duration, cut list.
- **All consumers migrated to one book + string ids** (no two-book splitting, no ctor/owner changes): `swing`/`hit`
  (melee, accord melee), `cast`/`hit` (stun), `loop` (stun VFX, void strike), `impact` (bomb, shield ripple,
  empower, accord knockback), `death` (kill particles), `pulse` (soul pulse, witness), `glow` (witness),
  `charge` (soul convergence, accord spirit), `ring`/`arrive` (accord spirit agent), `down`/`spawn` (spawn point).
  `EnemyVFXController` → one book with mood-named ids (`rageReaction`/`rageLoop`/…). **Book slots are NULL pending
  authoring.** Compile-verified (0 errors).
- **Manpu folded into the Cue Book.** New `CueElementKind.Manpu` element — a glyph (sprite + 2 colors, dropped
  straight into the cue) **pulsed on the cue target's `ManpuSlot`**, timed before/with/after the effect's other
  elements by its Start Mode / Delay (the "manpu during/before/after an event" ask). Transient and **R1-respecting**
  (dropped if a held ability glyph owns the slot) via a new `ManpuSlot.RequestCuePulse`; the `ManpuVocabulary`
  stays the single source for *state* glyphs, so existing Manpu behavior is unchanged. May carry its own accent
  sound(s) via the element's audio list. Held (channel-long) cue glyphs deferred (needs slot-ownership arbitration).
  Manpu's existing direct path (`ManpuGlyph.PlayAccents` → `FxManager.PlayParticle` + `Play`) is untouched.
- **Removed (net cleanup, supersedes the dev-iteration entries below):** the cue-subtype SOs `ParticleCueData` /
  `VfxGraphCueData` / `CueSequenceData` (+ `CueSequenceRunner` + its EditMode test); the intermediate
  `EntityCueBook` holder (+ its editor); the `FxEvent` enum; `FxManager`'s old `Play(CueData)` VFX dispatch +
  prewarm machinery; `AccordStateSystem`'s dead `PlayChargeVFX`/`StopChargeVFX`/`PlayKnockbackVFX` (+ fields);
  and 5 empty orphan `Cue_*.asset` placeholders. **`FxManager` public surface is now** `PlayBook(book, id, ctx)`
  / `PlayParticle(ParticleSystem, ctx)` / `Play(SoundCueData | CameraShakeCueData)`. The three 2026-06-17 cue
  entries + the 2026-06-16 Phase-9 entry below describe the intermediate `FxEvent`/`EntityCueBook` journey and are
  **superseded by this entry** — kept only as history.

### Changed — SpawnZone POIs auto-discovered; new AmbientLoopEmitter (2026-06-17)
- **SpawnZone no longer hand-places POIs.** Its `spawnPoints`/`ritualSites`/`barriers` serialized arrays are
  removed; `GetNearestSpawnPoint` / `GetNearestRitualSite` (new) / `IsNearBarrier` / `GetSafestRitualSite` / `Has*`
  now query `POIManager` scoped to the zone's `BoxCollider` bounds (new `POIManager.GetAllInBounds`). Because POIs
  self-register to POIManager on `OnEnable` (POIBase), **Timeline-enabled and runtime-instantiated POIs are
  included automatically** — zero per-zone wiring. `EnemyPOITracker` uses the new `SpawnZone.GetNearestRitualSite`;
  the Weaver's Gate + enemy AI already resolved POIs via POIManager, so they're unaffected. The Area Auto-Wire tool
  stops writing the removed POI arrays (left/right Transform population unchanged; its POI-source UI is now vestigial —
  cleanup pending). Compile-verified (0 errors).
- **`AmbientLoopEmitter`** (`Fx/AmbientLoopEmitter.cs`) — plays a looping spatial `SoundCueData` (a crack's
  dark-energy buzz, a flame's crackle) through the pooled `AudioManager`, never a raw AudioSource; optionally
  proximity-gated to keep the 32-voice pool bounded; stops/frees the voice on disable or out-of-range, re-acquires
  a stolen voice. For dense static set-dressing, a plain AudioSource on the Ambience group is the noted alternative.

### Added — Cue Book authoring inspector + orphan cleanup (2026-06-17)
`CueBookDataEditor` — the custom inspector for `CueBookData`: per event, an ordered element list where each
element picks a kind (Particle / Vfx / Sound) from a dropdown and shows ONLY that kind's fields, then timing
(start mode + delay), default-or-explicit duration, and an optional cut list (target popup limited to EARLIER
elements + after-seconds). No separate cue SOs to create — drop prefabs/clips straight in. Also deleted the 5
empty orphaned `Cue_*.asset` placeholders in `Assets/Data/Fx/` (held no prefab; broken by the subtype removal).

### Changed — Cue Book migration: all gameplay FX consumers now play books (2026-06-17)
Every gameplay consumer that held loose cue fields (`ParticleCueData`/`VfxGraphCueData`) + called
`FxManager.Play` now references a `CueBookData` and calls `FxManager.PlayBook(book, FxEvent, ctx)` (or
`EntityCueBook` for self-effect objects). Visual/audio data is OFF gameplay logic (SRP); each consumer's
held-loop handle management is unchanged. All new refs are to a `CueBookData` **asset** → R2-safe by
construction. **The old slots are now null book slots — author one book per consumer + re-assign.**
Compile-verified (0 errors).
- **World/enemy:** `SpawnPointPOI` (EntityCueBook Death/Spawn), `BombProjectile`, `SpawnShieldRipples`,
  `KillParticleSpawner`, `StunVFXSystem` (held Loop per enemy), `WitnessAuraVFX` (Recharge pulse + held Loop glow).
- **Plain-C# abilities → one ctor-injected book** (each owner drops N cue fields for one book — the SRP fix):
  `StunAbility` (+`TwinAbilitySetup`), `MeleeAttackStrategy` (+`PlayerAttackController`), `AccordMeleeAbility`
  + `VoidStrikeAbility` (+`AccordStateSystem`).
- **Player systems:** `EmpowerSystem`, `SoulPulseSystem`, `SoulConvergenceSystem`, `AccordSpiritSystem`
  (one book passed to each spawned `AccordSpiritAgent`).
- **Enemy mood VFX:** `EnemyVFXController` → two books (one-shot reaction + held loop) keyed by new mood
  `FxEvent`s (Rage/Fear/Panic/Buff/DarkEnergy); confirmed LIVE (10+ enemy types, BT combo, mood system) —
  complements Manpu (glyph/tint), not superseded.
- **Removed dead wiring:** `AccordStateSystem`'s uncalled `PlayChargeVFX`/`StopChargeVFX`/`PlayKnockbackVFX`
  + their `_chargeCue`/`_knockbackCue`/`_fx`/charge-handle fields.
- **Subtypes deleted (cleanup complete):** `ParticleCueData`, `VfxGraphCueData`, `CueSequenceData` +
  `CueSequenceRunner` + its EditMode test removed. `FxManager`'s old `Play(CueData)` dispatch is gone — the
  public surface is now `PlayBook` (cue books), `PlayParticle(ParticleSystem, ctx)` (a single raw prefab),
  and `Play` (only `SoundCueData`/`CameraShakeCueData`); prewarm machinery removed (lazy lifetime cache stays).
  Manpu **keeps its own system** but moved its glyph burst from `ParticleCueData` to an inline
  `ParticleSystem burstPrefab` played via `FxManager.PlayParticle`. `SoundCueData` retained (Sound elements +
  Manpu sound + audio). **Orphans:** 5 empty placeholder cue `.asset`s in `Assets/Data/Fx/` (Cue_Stun/Charge/
  SoulPulse/SoulAbsorb/AccordKnockback) held no prefab and are now missing-script — safe to delete.

### Changed — Cue Book redesign, phase 1: one inline-element asset (collapses the cue-subtype SOs) (2026-06-17)
Reworks the FX authoring surface per the design dialogue: instead of authoring a separate
`ParticleCueData` + `VfxGraphCueData` + `SoundCueData` (+ a `CueSequenceData` to tie them) per effect —
"too many SOs to run one ability" — a single **`CueBookData`** asset holds the whole visual+audio data,
keyed by `FxEvent`, as an ordered list of inline elements. Gameplay data stays OFF gameplay logic (SRP):
the entity's `EntityCueBook` holds the book *reference*; `FxManager` *executes* it. **Additive — the old
cue SOs and their ~12 consumers are untouched and still compile; migrating them off their loose cue fields
and removing the subtypes is phase 3.** Compile-verified (0 errors).
- **New data model:** `CueElement` (kind dropdown Particle/Vfx/Sound → drop a prefab/clip directly; per-
  element `useDefaultDuration` vs explicit `duration`, `startDelay`, `startMode`, and a `canCut` list that
  stops earlier elements at a scripted beat — "VFX1 → VFX2, then cut VFX1"). `CueBookData` SO (one asset,
  many events; a player book carries Walk/Melee/Attack, an ability book one/few). `CueElementKind`, `FxEvent`
  (extracted to its own file).
- **Runtime:** `CueBookRunner` (schedules elements via the existing `CueSchedule`, fires authored cuts, and
  keeps the book alive while a held/looping element runs so a gameplay `Stop` can still reach it — no leaked
  loop). `FxManager.PlayBook(book, evt, ctx)` + `PlayElement` dispatch + `ActiveBook` (its `Stop` halts every
  held element — the trap-reset contract). `PlayParticle`/`PlayVfx` refactored to share `SpawnParticle`/
  `SpawnVfx` with the element path (pool/registry/`ActiveFx`/follow/held-until-stop all reused).
- **Multi one-shot audio per visual:** falls out for free — add sibling `Sound` elements with
  `WithPrevious` + their own `startDelay`. Routed through `AudioManager` (no AudioManager change).
- **Holder:** `EntityCueBook` repurposed from event→`CueData` to a list of `CueBookData` with
  `Play(FxEvent)/Stop(FxEvent)`; the stale `EntityCueBookEditor` (quick-created the subtype SOs we're
  collapsing) **deleted**.
- **Pending:** custom `CueBookData` inspector (the `+`/type-dropdown/duration/cut tick-list UI); migrate the
  phase-9 consumers + `StunAbility`/`TwinAbilitySetup` off their loose cue fields onto books; then delete the
  `ParticleCueData`/`VfxGraphCueData`/`SoundCueData`/`CueSequenceData` subtypes.

### Changed — Phase 9 VFX migration: POIs + Tier 2/3/4 sites → cue-driven (2026-06-16)
Removed the last raw `Instantiate`-of-VFX sites (Banned Lazy Work #10) — the linter's Phase-9 punch-list
is now **0**. Each migrated `GameObject` prefab field → a `CueData` field played via `FxManager.Play`
(pooled, lifetime-managed, no `Destroy`); loops/held VFX → held `CueHandle`s. **Re-wiring needed:** the
old prefab slots are now null `CueData` slots — author a cue from each old VFX prefab (Cue Book
*create-from-prefab*) and re-assign. Compile-verified (0 errors).
- **POIs:** `SpawnPointPOI` destroy/respawn → `ParticleCueData` cues; `RitualSitePOI` idle VFX **removed**
  (no need); `BarrierPOI` had none.
- **Enemy/UI:** `EnemyVFXController` (one-shots + loops → cues, held loop handles); `WitnessAuraVFX`
  (pulse → one-shot cue, per-enemy glow → held cue handle).
- **Combat:** `SpawnShieldRipples` (VFX-graph → positioned cue); `BombProjectile` (detonation cue; bomb
  stays gameplay); `MeleeAttackStrategy` slash/hit cues (+ `PlayerAttackController`).
- **Stun ability:** `StunAbility` gains a held **active** cast cue (on the caster for the window) + an
  **on-hit** cue on each stunned target (wire a 2-step `CueSequence` to run two particles together);
  wired through `TwinAbilitySetup` (`stunActiveCue` / `stunHitCue`). Complements the existing held stun
  aura (`StunVFXSystem`) and the Manpu rolling-eyes→"!" reaction (`ManpuAbilityListener`).
- **Abilities:** `EmpowerSystem` knockback (held instance → per-pulse one-shot cue); `AccordMeleeAbility`
  slash/hit (+ wired through `AccordStateSystem`, fixing dead prefab wiring); `VoidStrikeAbility` hazard
  points (held cue handles); `AccordSpiritAgent` arrival/ring (+ `AccordSpiritSystem`). The seeker orb
  and projectiles stay gameplay `Instantiate` (not VFX). Per-instance shape tweaks (ring/point radius,
  ripple centre) move to the authored prefab.

### Added — Manpu emotion-glyph system (replaces Ikari) (2026-06-16)
New enemy-readability layer (`Assets/Scripts/Manpu/`) per `MANPU_SYSTEM.md` — the chosen Hybrid (synthesis
variant): a **loud channel** (one glyph slot per enemy, transient mood/perception pulses vs. persistent
ability arcs) + a **quiet channel** (continuous body tint by mood category). Presentation only — reads the
existing Mood/Perception/Ability/Setsuna systems, owns no state. Compile-verified (0 errors).
- **Components:** `ManpuVocabulary` (SO data table), `ManpuGlyph` (display — unscaled, E1 slow-mode hold),
  `ManpuSlot` (per-enemy arbiter: **R1** ability-owns-slot, **R2** escalation+debounce, **R3** empty-sprite=
  no-glyph, pool-clear), `ManpuDirector` (per-enemy: mood/perception/Setsuna → slot), `ManpuAbilityListener`
  (central: stun/possess events → held→closing arc, shares `StunVFXSystem`'s event source so they can't
  desync), `MoodAmbient` (quiet channel; yields to the stun/possess body tint).
- **Authoring tool** (`ManpuVocabularyEditor`): reflects over the enums so every `EnemyMood`/`EnemySearchState`/
  `ManpuAbility` is a row automatically (add a mood → new row appears); drag-drop **sprite + particle + sound**
  per row. Empty sprite = suppressed (the curation). Mood/glyph **sound** plays via `FxManager`→`AudioManager`
  (no AudioManager change), gated by the same R1/R2.
- **Hooks added to existing systems** (additive): `EnemyMoodSystem.OnMoodChanged`,
  `PoTPerceptionMemory.OnSearchStateChanged` (edge-detected), `SetsunaSystem.OnActiveChanged` (static, E1),
  `EnemyPool.Return` → `ManpuSlot.Clear()`.
- **Removed (Ikari retired):** `IkariMarkVFX.cs` (deleted); `EnemyStateUIController` stripped of the
  `ShowIkari*` methods + per-emotion sprite/colour fields (rage/ritual sliders kept); the hardcoded
  mood→ShowIkari switch in `EnemyMoodSystem`; the 2 direct callers (`Enemy.cs` fear, `PenitentEnemy` rage).
  Prefabs that had `IkariMarkVFX` will show a missing-script until cleaned.
- **Docs:** game.md §24 (components, the tool, wiring checklist, rule→code map). **Content wiring**
  (vocabulary asset, glyph prefab, enemy components, listener) + the §5 acceptance trace remain to author.

### Added — Editor tooling suite "Planet of Twins Tools" + FX stop-step (2026-06-16)
The instruction.md Phase 8 **scene-lint** backlog item, delivered — a coherent Editor suite that enforces
the Reference Rulebook automatically and kills the per-area-scene wiring ritual. All editor tools live in
`Assets/Scripts/Editor/` (no asmdef → predefined `Assembly-CSharp-Editor`, which can see gameplay types;
an asmdef cannot reference Assembly-CSharp). Verified against the live project (compile-clean; rules
exercised on L1_Park + the WorldLocationSO graph; **zero false positives** after tightening).
- **Validator** (`Tools ▸ Planet of Twins ▸ Validate`) — `SceneScan` core + `ValidatorWindow`:
  - **Cross-scene serialized refs (R2)** — structural, no annotations (the SkillPointOrb-class bug, caught
    at author time). **Null required** via new runtime `[RequiredReference]` attribute + curated list
    (`SpawnZone.areaConfig`, `AreaSpawnPoints` starts). **Conditional completeness** by scene class
    (Area/Persistent/Bootstrap/Intro/Dev) — *nothing optional is ever required*: skill orbs/QTE only
    validated when present; the one hard Area rule is "a WorldLocationSO references this scene" (+a Fix).
    **WorldLocationSO graph** — adjacency symmetry, build-settings membership, exactly one start;
    AreaZoneConfig sub-SO completeness. **Fix buttons** for: create WorldLocationSO, add AreaSpawnPoints,
    add NavMeshSurface, fill AreaZoneConfig sub-SOs. NavMesh check accepts legacy baked `NavMeshData`
    (beside the scene), and the R9 canvas check skips canvases with a `WorldSpaceCanvasCamera` resolver
    (both were false positives, now fixed). **Validate Build Scenes** button sweeps every enabled
    Build-Settings scene (opens additively, scans, closes — open scenes untouched), not just the open set.
  - **Architecture linter** (code tab) — `DontDestroyOnLoad` (R3), raw `Time.timeScale=` (R10), raw
    `Input.*` (gate-relevant only), `AudioMixerSnapshot.TransitionTo` (gated on the file actually using
    it), unguarded debug keys. Allowlisted legit homes, **aggregated per file**, and it **doubles as the
    Phase-9 migration punch-list** (flagged exactly the Tier 2/3/4 sites).
- **Runtime Integrity Guard** (`SceneIntegrityChecker`, dev/editor-only) — re-checks `[RequiredReference]`
  nulls + duplicate-singleton canary (R3 Restart→Bootstrap) on each scene load, fail-loud (#4).
- **Area Auto-Wire** (`Tools ▸ Planet of Twins ▸ Area Setup`) — one-click `AreaZoneConfig` + 3 sub-SOs and
  assign; auto-populate the `SpawnZone` arrays (typed POIs by component-type-in-bounds; left/right via
  named child containers; per-field Tag/Manual override). All writes via `SerializedObject` (Undo-able).
  Collects **only same-scene** data (R2-safe) — checkpoints/orbs/entrances are validated, never written
  into an SO.
- **New-Area Generator** (`Tools ▸ Planet of Twins ▸ New Area Scene…`) — scaffolds `<name>/<name>.unity`
  with the required skeleton (AreaSpawnPoints + L/R, NavMeshSurface, Geometry/POIs roots, optional
  SpawnZone), a `WorldLocationSO`, optional Build-Settings entry, + a "still to hand-place" checklist.
- **FX choreography — sequence Stop step.** `CueSequenceData.CueStep` gains `CueStepKind {Play,Stop}` +
  `stopTargetStep` (negative = most-recent Play). `CueSequenceRunner` now retains each Play step's
  `CueHandle` and a Stop step halts the targeted cue at its scheduled beat (`FxManager.PlaySequence`
  passes play+stop callbacks). Enables "VFX1 → VFX2 parallel → cut VFX1 → VFX3" without code. Verified by
  `Assets/Tests/EditMode/CueSequenceRunnerTests.cs` (3 scenarios green via an editor self-test — note the
  project's EditMode tests aren't discovered by Test Runner today; no test asmdef exists).
- **Cue Book** (`EntityCueBook` + `EntityCueBookEditor`) — per-entity `event → CueData` table (the §14
  "feel" layer). `Play(FxEvent)` / `Stop(FxEvent)` → one `FxManager.Play`, no strings, no per-call SO
  alloc; held/looping cues remembered. Custom inspector adds rows + a quick-create that builds + binds
  the cue SO for all three kinds — `ParticleCueData` (ParticleSystem prefab), `VfxGraphCueData`
  (VisualEffect prefab), empty `CueSequenceData` — at FromPrefab defaults. Opt-in — does not refactor
  existing per-system cue fields.
- **Deferred backlog** (captured in the plan, not built): animation tooling, localization coverage,
  skill-tree validator, prefab-contract validator, build-settings check, enemy scaffolder, timeline auditor.

### Fixed/Changed — Multiscene cross-scene refs + ERB shader URP ports (2026-06-14)
- **`SkillPointOrb` cross-scene ref (R2→R4).** The orb's `_pointBankMono` was meant to be dragged to
  `SkillTreeManager`, which now lives in Persistent — a cross-scene serialized ref that won't serialize
  (orbs would award no points). Now resolves `SkillTreeManager.Instance` in `Start()` (R4); the
  serialized slot is optional same-scene only. Place orbs in area scenes with the slot **empty**.
- **`CheckpointTrigger` audited — already correct** (no change): resolves `CheckpointManager.Instance` +
  `TwinSelector.Instance` at runtime and only serializes the `WorldLocationSO` **asset** (assets cross
  scenes legally). Recorded so it isn't re-flagged.
- **ERB effect shaders → URP** (`Assets/ErbGameArt/Fantasy effects pack/builtinShaders/`):
  `Add_CenterGlow`, `Blend_CenterGlow`, `ManaWall` hand-ported from Built-in/Amplify CG to URP HLSL —
  same Properties, same blend (additive / alpha), same per-pixel math, soft-depth via `SampleSceneDepth`
  gated by `_Usedepth`, URP fog (`MixFogColor`→black for additive, `MixFog` for blend). SRP-batcher
  `CBUFFER`. Compile-verified (0 errors/warnings). `WaterOrb` ported to a hand-written **URP Lit** shader:
  surface→ForwardLit (`UniversalFragmentPBR`), GrabPass→`SampleSceneColor` (needs **Opaque Texture ON**),
  same vertex waves + normal distortion; PBR is URP's (close, not pixel-identical to Built-in Standard).
  The old `_GrabTexture` render errors are gone. The ports are re-applied at the GUIDs the materials
  already reference, so materials stay linked.
  > **Reimport caveat:** these 4 shaders live *inside* the imported pack folder, so re-importing the
  > Fantasy-effects pack reverts them to the original CG (it already happened once). Don't re-import the
  > pack; if you must, the ports need re-applying.
- **Projector decals — abandoned & removed (scope dropped).** The legacy `Projector_add/blend_FCW`
  shaders were going to be re-authored as URP **Decal Shader Graphs**. That whole path was dropped: the
  projector **prefabs were deleted** (user), so there's nothing left to drive. Deleted with it:
  `Projector_add_FCW`/`Projector_blend_FCW` shaders, both `ProjectorMaterialChanger` copies (mine +
  the pack's legacy one), `DecalMaterialChanger.cs` (0 references), and the stub `DecalCenterWave.hlsl` /
  `WaterOrbNodes.hlsl` graph-helpers (lost in the reimport; redundant now `WaterOrb` is a full HLSL shader).
  No Decal Renderer Feature is required. **Kept** (still used by surviving ERB prefabs): `ColliderTurnOff.cs`
  (disables `BoxCollider`s on a timer — Mana wall) and `ParticleCollisionInstance.cs` (spawns impact FX
  `OnParticleCollision` — Lightning strike / Spears rain / Meteor rain).

### Changed — Phase 9 P9.3 (Tier 1 complete): first VFX migrations onto cues (2026-06-14)
Migrating the ~25 raw `Instantiate` VFX sites onto `FxManager.Play`, tier by tier (§14.6). **Tier 1 (Critical) done** — all 6 sites compile clean, 19/19 EditMode tests pass:
- **FxManager fix:** a looping particle prefab is now **held until Stop()** (reads `main.loop`) instead
  of auto-expiring after one loop — required for held-handle effects like stun auras (§14.1b). An
  explicit `explicitLifetime` override still wins.
- **`SoulPulseSystem`** (Pattern C one-shot): `GameObject _pulseVFXPrefab` + Instantiate/Destroy →
  `ParticleCueData _pulseVfxCue` + `_fx.Play(.., new CueContext(soulPos))`; R4 manager resolve.
- **`StunVFXSystem`** (Pattern D loop — the stale-child bug class): per-enemy `Dictionary<GameObject,
  CueHandle>`; `Play(cue, CueContext.Follow(enemy))` on stun/possess applied, `Stop(handle)` on ended.
  **Pooled-enemy reuse is now correct** — a re-stunned enemy whose old handle was pool-reclaimed reads
  `!IsPlaying` and respawns cleanly (F2, via version-stamped handles). Head-height offset moves to the
  cue's `localOffset`.
- **`EnemyPool.Return`** now calls `FxManager.Instance?.StopAllOn(instance.transform)` — enemies
  re-enter the pool visually/audibly naked (F2, retires the stale-StunVfx-child bug).
- **`KillParticleSpawner` + `SoulParticleAttractor`** (resolved the design fork — chosen: attractor
  self-resolves): the attractor now picks the nearest twin via `TwinSelector` on its first `LateUpdate`
  (after FxManager positions it), resets on every `OnEnable`, and **no longer `Destroy`s itself**
  (pool-safe — FxManager reclaims by cue lifetime). `KillParticleSpawner` drops the nearest-twin/
  `SetTarget` plumbing and just plays `Cue_SoulAbsorb` at the death point; keeps its
  `EnemyDeathNotifier` subscription. No new cue-API mechanism added (CLAUDE.md #3).
- **`AccordStateSystem`** / **`AccordSpiritSystem`** / **`SoulConvergenceSystem`** (held charge + one-shot
  knockback): charge VFX → held `CueHandle` per twin (`Play(Follow(twin))` / `Stop(handle)`), knockback →
  one-shot cue. The three copies of `SpawnAndPlayPS`/`StopAndDestroyPS`/`PlayOneShotPS`/`SpawnChargeVFX`
  deleted. **Gameplay objects left untouched** (R-correct): the SC **shield** GO (collider), the Accord
  **spirit agent**, and the arrival/ring prefabs passed to `AccordSpiritAgent` (those are Tier 2).
- New cue assets: `Cue_SoulPulse`, `Cue_Stun`, `Cue_SoulAbsorb`, `Cue_Charge` (shared by the three
  charge systems), `Cue_AccordKnockback` — prefab/offset assigned in the scene-wiring session.
- **Tier 1 complete.** Next: Tier 2 (`AccordSpiritAgent`, `VoidStrikeAbility`, `RadiantSeekerAbility`,
  `CoalesceSystem`, `EmpowerSystem`, `WitnessAuraVfx`, the `AccordMeleeAbility` SpawnOneShot copy).

### Added — Phase 9 P9.2: audio engines (AudioManager, MusicManager, snapshot arbiter) (2026-06-14)
Second slice of the FX/audio system (instruction.md §14.2/§14.5). Engines + data only — call-site
hookups (§14.7) are P9.4, and the AudioMixer *asset* (groups/snapshots) is wired by hand (the code
references them via serialized slots). Branch `vfxsounds`.
- **`AudioManager`** (Persistent, R3-safe): 32 pooled `AudioSource` voices; `Play(SoundCueData, pos)` /
  `Play(.., Transform)` / `PlayUI` (2D + `ignoreListenerPause` so UI survives pause, F4) / `Stop` /
  `StopAllSfx` (F5) / `IsPlaying`. **Voice stealing** = lowest priority then oldest; per-cue
  `cooldown` (scaled-time anti-spam) and `maxSimultaneous` (steals this cue's oldest) — F8. **Sole**
  writer of `AudioListener.pause` via `SetPaused(owner)`/`ReleasePaused(owner)` (owner set) and **sole**
  caller of `AudioMixerSnapshot.TransitionTo` via the arbiter — Banned Lazy Work #11. Non-loop voices
  reclaim when finished (skipped while paused). Handles use the same version-stamped `CueInstanceRegistry`.
- **`SnapshotArbiter`** (FxCore, plain C#): highest-priority-wins, empty → Default, ties keep the
  incumbent — the mixer-snapshot analogue of `TimeScaleService`. `RequestSnapshot(owner, id, priority)`/
  `ReleaseSnapshot(owner)` on `AudioManager` map `AudioSnapshotId {Default,Paused,Setsuna,GameOver}` to
  a serialized `AudioMixerSnapshot[]`.
- **`MusicManager`** (Persistent, R3-safe): A/B `AudioSource` crossfade on **unscaled** time (R10);
  loops the area ambience bed + fires ambience one-shots through `AudioManager` on an **unscaled**
  scheduler (breathes during Setsuna); no-op when the incoming track/bed is unchanged.
- **`SceneFlowManager`**: new `OnActiveLocationChanged` event + `ActiveLocation` property (additive —
  `UpdateActiveScene` refactored to resolve-then-apply, no behaviour change to streaming). MusicManager
  subscribes it (R8); the skybox/NavMesh path is unaffected.
- **Data**: `MusicTrackData`, `AmbienceData` SOs; `WorldLocationSO` gains optional `musicTrack`/
  `ambience` (R7 config; null = silence, not an error).
- **`FxManager.PlaySound`** now routes to `AudioManager` (replacing the P9.1 stub) — sequence sound
  steps now actually play. Fire-and-forget through FxManager (the voice handle lives in AudioManager's
  registry; hold/stop looping sounds via AudioManager directly).
- **EditMode tests**: +7 `SnapshotArbiter` tests (priority permutations, empty→Default, release
  fallback, same-owner replace) — **19/19 total pass**. Clean compile (only the pre-existing benign
  native `Persistent` allocator warning).
- **Remaining for the DoD** (in-editor / P9.4): wire `AudioManager` + `MusicManager` into Persistent;
  extend the `GameAudioMixer` asset to Master→{Music,Ambience,SFX,UI,Voice} + the four snapshots +
  `AmbienceVolume`/`UIVolume`/`VoiceVolume` exposed params (§14.5) and assign the serialized slots;
  hook `SetsunaSystem`→Setsuna snapshot (release on both end paths, F3), `PauseMenuController`→`SetPaused`
  + Paused snapshot (F4), `GameOverController`→GameOver snapshot. BUGS.md W25/W26/W27/W30 annotated.

### Added — Phase 9 P9.1: FX/audio cue core (data + runtime + tests) (2026-06-14)
First slice of the unified FX/audio system (instruction.md §14). **Data + runtime core only** —
no migrations, no audio engine yet (P9.2), so nothing existing changes behaviour. Branch `vfxsounds`.
- **Cue SO family** (`Assets/Scripts/Fx/Data/`, R7 config-only): `CueData` (abstract, `TimeMode`
  Scaled/Unscaled), `ParticleCueData`, `VfxGraphCueData`, `SoundCueData`, `CameraShakeCueData`
  (optional/stubbed), `CueSequenceData` (steps with `AfterPrevious`/`WithPrevious` + delay +
  `waitForCompletion`; self-reference cleared in `OnValidate`, runtime nesting capped at
  `MaxNestingDepth` = 4). Each has a `[CreateAssetMenu]` under `PlanetOfTwins/Fx/`.
- **Runtime core** (`Assets/Scripts/Fx/`): `CueContext` (readonly struct), `FxManager`
  (Persistent singleton — R3-safe: duplicate-destroy Awake, `Instance` nulled in OnDestroy, **no
  DDOL**; `Play`/`Stop`/`StopAllOn`/`StopAll`/`IsPlaying`; owns auto-created `FxPoolRoot`),
  `VfxPool` (per-prefab pooling mirroring `EnemyPool`), `CueSequenceRunner` (plays a sequence by
  dispatching each step back through `FxManager.Play` — the single entry point).
- **Dependency-free testable core** in a new `PlanetOfTwins.FxCore` asmdef (auto-referenced, so
  `Assembly-CSharp` sees it; nothing inside references back): `CueHandle` (version-stamped — stale
  handles inert), `CueInstanceRegistry<T>` (slot-recycling + version bump on reclaim),
  `CueSchedule` (pure timing math), `CueStartMode`. This is the "OccupancyModel move" — timing/
  staleness logic unit-tested without a scene (asmdefs can't reference predefined `Assembly-CSharp`,
  hence the split).
- **EditMode tests** (`Assets/Tests/EditMode/`, `PlanetOfTwins.FxCore.Tests` asmdef): 12 tests, all
  passing — `CueSchedule` (the §14.3 worked example timings, `waitForCompletion`, `WithPrevious`
  concurrency, negative-clamp, empty) and `CueInstanceRegistry` (reclaim invalidates, **reused slot
  → stale handle inert**, double-reclaim safe, `None` never valid).
- **Demo assets**: `Assets/Data/Fx/Cue_Demo_Particle.asset` + `Seq_Demo.asset` (two timed beats).
- **F-class status** (BUGS.md W23–W30): F1 unload reclaim **implemented** in `FxManager`
  (`HandleLocationWillUnload`, subscribed in `Start()` per R4 — not `OnEnable`, since
  `SceneFlowManager.Instance` isn't guaranteed that early); `StopAllOn`/`StopAll` (F2/F5) and the R3
  singleton contract (F6) shipped. Call-site wiring (EnemyPool return, SoftReset teardown) and the
  audio half (F3/F4/F8) remain for P9.2/P9.3. `SoundCueData`/`CameraShakeCueData` play paths are
  **fail-loud stubs** until then (never a direct `AudioSource`/`Instantiate`).
- Verified: clean compile (only the pre-existing benign native `Persistent` allocator warning),
  12/12 EditMode tests pass. Pending: in-scene play verification (needs an `FxManager` in
  Persistent + a ParticleSystem prefab on the demo cue).
- **Correction (same session, per the newly-added §14.1b "prefab is the source of truth"):**
  `TimeMode` and `FxAttachMode` gained a **`FromPrefab` default** — the cue no longer re-states what
  the artist authored. `FromPrefab` timeMode leaves `main.useUnscaledTime` untouched; `FromPrefab`
  attach reads `main.simulationSpace` (Local→Follow, World→World) at play time. `explicitLifetime = 0`
  now derives the lifetime from the prefab **recursing sub-emitters** (new `FxLifetime.Compute`,
  cached per-prefab in `FxManager` at prewarm) instead of reading only the root system. Re-stating
  authored particle data is now treated as Banned Lazy Work #1/#6.

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
