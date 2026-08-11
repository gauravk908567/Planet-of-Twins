# Multi-Scene Wiring Guide

**Generated from source after Phases 0–7. Previous version was stale — wrong field names,
obsolete components, and a phantom QTE canvas section. Trust this file, not the old one.**

---

## 1. ScriptableObjects to Create

### WorldLocationSO assets

`Right-click Assets/Data > Create > PlanetOfTwins > World Location`

| Asset name | `scene` | `adjacentLocations` | `isStartLocation` |
|---|---|---|---|
| `Location_L1_Park.asset` | pick `L1_Park/L1_Park.unity` | drag `Location_L2_Streets.asset` | ✓ |
| `Location_L2_Streets.asset` | pick `L2_Streets/L2_Streets.unity` | drag `Location_L1_Park.asset` | — |

The `scene` field is a `SceneReference` — use the scene picker (click the field, select from
Build Settings). Both assets must reference scenes that appear in Build Settings.

---

## 2. Persistent.unity — Inspector Wiring

### SceneFlowManager GO

| Field | Wire to |
|---|---|
| `allLocations[]` | Both WorldLocationSO assets (Location_L1_Park, Location_L2_Streets) |

### QTEManager GO

QTEManager has **no UI fields** — UI is owned by each `QTESceneAnchor` in the area scene
(world-space canvas, co-located with the QTE trigger). Only wire services:

| Field | Wire to |
|---|---|
| `enemyFreezeServiceMono` | `EnemyFreezeService` component on the QTEManager GO |
| `cameraControllerMono` | `CameraManager` component on CameraManager GO |
| `cameraSwitcher` | `CameraSwitcher` component on CameraManager GO |

### SoftResetController GO

| Field | Wire to |
|---|---|
| `_leftTwin` | Lyra (left twin Player component) |
| `_rightTwin` | Kai (right twin Player component) |
| `fadeImage` | FadeCanvas > FadeImage (Image component) |
| `enemySpawner` | EnemySpawner component on EnemySpawnner GO |
| `sharedHealthPool` | SharedHealthPool component (TwinBondManager GO) |
| `skillTreeManager` | SkillTreeManager component |
| `rescueController` | RescueEventController component on PlayerManager GO |

### SettingsMenuController

| Field | Wire to |
|---|---|
| `_audioMixer` | `Assets/Audio/GameAudioMixer.mixer` ✓ **already wired** — Master/Music/SFX groups + MasterVolume/MusicVolume/SFXVolume exposed params set up via script |

---

## 3. L1_Park.unity — Inspector Wiring

### AreaSpawnPoints GO

Wire the two child Transforms to `leftStart` and `rightStart` on the `AreaSpawnPoints`
component, then move them to the actual gameplay start positions for each twin.

### LocationEntrance GO(s)

Each `LocationEntrance` marks a spawn point for twins arriving from a specific direction.

| Field | Value |
|---|---|
| `comesFrom` | WorldLocationSO of the area the twins are arriving **FROM** (e.g. `Location_L2_Streets.asset` for the entrance from Streets). Leave **null** for the default/fallback entrance. |

Create at least one default entrance (null `comesFrom`) at the general start position.
Create a directional entrance (`comesFrom = Location_L2_Streets`) at the park boundary
for twins returning from Streets.

### SceneLoadTrigger GO(s) — Park ↔ Streets boundary

Add a trigger volume at the exit to Streets. Wire the `SceneLoadTrigger` component:

| Field | Value |
|---|---|
| `targetLocation` | `Location_L2_Streets.asset` |

### CheckpointTrigger GO(s)

Each checkpoint trigger already has `[SerializeField] WorldLocationSO location` — wire it:

| Field | Value |
|---|---|
| `location` | `Location_L1_Park.asset` |

### ParkGateQTEAnchor GO (`QTESceneAnchor` component)

The QTE canvas hierarchy lives **here** (world-space, area-local) — **not** in Persistent.

| Field | Wire to |
|---|---|
| `definition` | `QTE_ParkGate.asset` |
| `triggerPoints[]` | Left + Right `QTETriggerPoint` GOs near the gate |
| `qteCamera` | Cinemachine VCam aimed at the gate |
| `zoneTrigger` | `QTEZoneTrigger` on the approach volume |
| `activatableMono[]` | Gate/door GOs implementing `IActivatable` |
| `rootPanel` | Root panel of the world-space QTE canvas (disabled by default) |
| `fillBar` | `Image` (horizontal fill) for mash progress |
| `timerRing` | `Image` (radial 360 fill) for countdown |
| `instructionLabel` | `TMP_Text` ("Press F!") |
| `countdownLabel` | `TMP_Text` (approach countdown number) |

### TutorialDirector GO — `TutorialStepContext` block

| Field | Wire to | Notes |
|---|---|---|
| `inputGate` | `TutorialInputGate` GO in L1_Park | |
| `resetSequencer` | `FailureResetSequencer` GO | in Persistent |
| `failureNotice` | `FailureNotice` GO | in Persistent |
| `overlay` | Leave blank | Auto-resolves to Persistent singleton |
| `hintDisplay` | Leave blank | Auto-resolves to Persistent singleton |
| `twinSelectorMono` | Leave blank | Auto-resolves via TwinSelector.Instance |
| `checkpoints[]` | TutorialCheckpoint GOs | match indexA/indexB in step SOs |
| `timeline` | `PlayableDirector` on TutorialTimelineDirector GO | |
| `rescueProviderMono` | `RescueEventController` on PlayerManager (Persistent) | wire if used |
| `qteAnchor` | `ParkGateQTEAnchor` GO | |

### TutorialTimelineDirector GO — `IntroTimelinePositioner` component

Add the `IntroTimelinePositioner` component. It has **one** field:

| Field | Value |
|---|---|
| `unfreezeOnPlace` | ✓ (default true — unfreezes twins when the timeline stops) |

No transform refs needed — it finds `AreaSpawnPoints` at runtime via `FindAnyObjectByType`.
Requires that the scene playing back the intro has an `AreaSpawnPoints` component with
`leftStart`/`rightStart` positioned for gameplay start.

### BarrierPOI on the barrier GO

Ensure the barrier object has a `BarrierPOI` component. It self-registers on Awake with
`POIManager`, enabling dark-energy gain, teleport range-checks, and spawn-side detection —
all auto-wired.

### AreaManager component — delete

L1_Park still has a missing-script component (old `AreaManager`, now deleted). In the
Hierarchy, find the GO showing "Missing Script" and remove that component.

---

## 4. L2_Streets.unity — Inspector Wiring

Mirror L1_Park setup:

- **AreaSpawnPoints** GO — wire `leftStart`/`rightStart`, position for Streets start.
- **LocationEntrance** GO(s) — one default (null `comesFrom`), one directional
  (`comesFrom = Location_L1_Park.asset`) at the Park-side boundary.
- **SceneLoadTrigger** at Streets → Park boundary — `targetLocation = Location_L1_Park.asset`.
- **CheckpointTrigger** GO(s) — `location = Location_L2_Streets.asset`.
- **TutorialDirector** (if tutorial steps exist in Streets) — same wiring pattern as L1_Park.

---

## 5. Intro.unity — Inspector Wiring

### IntroController GO

| Field | Wire to |
|---|---|
| `firstAreaLocation` | `Location_L1_Park.asset` |
| `videoPlayer` | `VideoPlayer` component in scene |
| `skipHintText` | TMP_Text showing "Press any key to skip" |
| `fadeImage` | FadeImage in Intro canvas |

---

## 6. Bootstrap.unity — GameBootstrapper GO

| Field | Notes |
|---|---|
| Dev mode toggle | Enable for direct-to-gameplay without Intro |
| Scene refs | Bootstrap loads Persistent additively; GameBootstrapper drives the rest |

---

## 7. Build Settings Order

`File > Build Settings > Scenes In Build`:

| Index | Scene |
|---|---|
| 0 | `Assets/Scenes/Bootstrap.unity` |
| 1 | `Assets/Scenes/Persistent.unity` |
| 2 | `Assets/Scenes/Intro.unity` |
| 3 | `Assets/Scenes/L1_Park/L1_Park.unity` |
| 4 | `Assets/Scenes/L2_Streets/L2_Streets.unity` |
| 5 | `Assets/Scenes/SampleScene.unity` (dev only — omit from release) |

`Restore.unity` and `Trees.unity` are temporary merge scenes — **never** in Build Settings.

---

## 8. Render Settings (per-scene)

### Persistent.unity (set while Persistent is the active scene)

`Window > Rendering > Lighting`:
- Skybox Material: `None`
- Fog: disabled
- Remove or disable the Directional Light GO (no geometry — each area scene owns lighting)

### Area scenes (L1_Park, L2_Streets)

Each area owns its skybox, fog, and directional light. `SceneFlowManager.SetActiveScene()`
calls `SceneManager.SetActiveScene(areaScene)` — whichever area the player occupies drives
`RenderSettings`.

`SkyboxChanger` stays in L1_Park (area-local Timeline Signal → receiver wiring stays local).

### Occlusion Culling

Bake independently per area scene: `Window > Rendering > Occlusion Culling > Bake`.
Persistent does not need a bake (no static geometry).

---

## 9. What Is Already Done (no action needed)

- Bootstrap.unity: GameBootstrapper + Persistent additive load + dev-mode path
- Persistent.unity: all manager GOs present (CameraManager, PlayerManager, SkillTreeManager,
  QTEManager, EnemyFreezeService, PauseMenuCanvas fully wired, HUD_Canvas, TutorialOverlay,
  TutorialHintDisplay, FailureResetSequencer, FailureNotice, SoftResetController, etc.)
- QTE_ParkGate.asset: created
- PauseMenuCanvas / SettingsPanel: hierarchy built and wired
- `AbilityController`: barrier resolved from BarrierPOI — no cross-scene ref needed
- `TutorialStepContext.Resolve()`: overlay/hintDisplay fall back to Persistent singletons automatically
- `IntroTimelinePositioner`: no transform wiring needed; finds AreaSpawnPoints at runtime
- `SoftResetController`: full streaming-aware restore — all 9 skill trees, checkpoint location load, power-state teardown
- `SpawnZone`: subscribes `OnSoftReset` → `ClearOccupants()` in `OnEnable`/`OnDisable`
- `SoulConvergenceSystem._inputProviderMono`: wired to TwinInputReader
- `AccordStateSystem._inputProviderMono`: wired to TwinInputReader
- `SoftResetController._leftTwin/_rightTwin`: wired (Lyra/Kai)

---

## 10. Known Remaining Issues

| Issue | Notes |
|---|---|
| Penitent rework | Enemy behavior incomplete — not a blocker |
| Debug skill keys (L/O/P/I/K) | Active in builds — wrap in `#if UNITY_EDITOR` before release |
| `TwinInputReader` | Uses legacy `Input.GetKey` — full migration to Input System is a planned backlog item; never half-migrate |
| `TutorialCheckpoint` wiring in Timeline | Checkpoint GOs must be outside any Activation Track hierarchy (R11) — verify in TutorialTimelineDirector |
| Audio Mixer | ~~Needs to be created and wired~~ **Done** — `Assets/Audio/GameAudioMixer.mixer` created; wired in Persistent |
