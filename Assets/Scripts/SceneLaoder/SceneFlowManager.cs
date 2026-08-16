using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton. Owns all async scene load/unload operations.
///
/// DESIGN (Graves pattern — transition model, no exit tracking):
///   • Tracks which WorldLocationSO each actor (twin, SoulPlayer) currently occupies.
///     Assignment is a transition: _currentLocation[actor] = destination overwrites the
///     previous value implicitly — exits are never tracked. A stale value can only ever
///     keep an extra area loaded (safe); it can never unload occupied ground (unsafe).
///   • Loaded set = union of all actor locations + each location's direct adjacents.
///   • OnLocationWillUnload fires before UnloadSceneAsync so EnemySpawner / QTEManager
///     can despawn/cancel before the scene disappears (R5 despawn signal).
///   • Active scene follows the selected twin's current location (drives skybox / NavMesh).
///
/// CALL SITES for NotifyTeleported (triggers never see teleports):
///   Boot seeding (GameBootstrapper dev mode, IntroController), SoftResetController,
///   Weaver's Gate soul travel, IntroTimelinePositioner, debug warps.
/// </summary>
public class SceneFlowManager : MonoBehaviour, IFxSceneEvents
{
    public static SceneFlowManager Instance { get; private set; }

    [Header("Locations")]
    [Tooltip("Drag all WorldLocationSO assets here. Manager finds start location automatically.")]
    [SerializeField] private WorldLocationSO[] allLocations;

    [Header("Transitions")]
    [Tooltip("Real-time seconds before unloading a distant scene. Unscaled so Setsuna / pause " +
             "don't stretch/halt the grace window.")]
    [SerializeField] private float unloadDelay = 0.5f; // unscaled — R10

    // ── Events ─────────────────────────────────────────────────────────────────
    /// <summary>Fires after a location's scene finishes loading.</summary>
    public event Action<WorldLocationSO> OnLocationLoaded;
    /// <summary>
    /// Fires inside UnloadLocationAsync AFTER the re-checks pass and BEFORE UnloadSceneAsync.
    /// Subscribers (EnemySpawner, QTEManager) must despawn/cancel synchronously or via short
    /// coroutines that have already started by the time the scene disappears.
    /// </summary>
    public event Action<WorldLocationSO> OnLocationWillUnload;
    /// <summary>Fires after a location's scene has been unloaded.</summary>
    public event Action<WorldLocationSO> OnLocationUnloaded;
    /// <summary>
    /// Fires when the <b>active</b> location changes (the one the selected twin is in — drives
    /// skybox / NavMesh / music). MusicManager subscribes this to crossfade tracks (R8). May fire
    /// with null if no loaded location resolves.
    /// </summary>
    public event Action<WorldLocationSO> OnActiveLocationChanged;

    // ── IFxSceneEvents (P19 seam 1 — the Fx package's only view of scene flow) ──
    // Raised alongside the WorldLocationSO events above with package-safe payloads
    // (scene name string; music/ambience Fx types) so Fx/ never names a game type.
    public event Action<string> OnSceneWillUnload;
    public event Action<MusicTrackData, AmbienceData> OnLocationAudioChanged;
    public MusicTrackData CurrentMusicTrack => ActiveLocation != null ? ActiveLocation.musicTrack : null;
    public AmbienceData CurrentAmbience => ActiveLocation != null ? ActiveLocation.ambience : null;

    /// <summary>The current active location (selected twin's), or null. Set by UpdateActiveScene.</summary>
    public WorldLocationSO ActiveLocation { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────
    // Per-actor current location (transition model). Key = any Player subtype incl. SoulPlayer.
    private readonly Dictionary<Player, WorldLocationSO> _currentLocation = new();
    private readonly HashSet<WorldLocationSO> _loadedLocations = new();
    private readonly HashSet<WorldLocationSO> _loadingInProgress = new();
    private readonly HashSet<WorldLocationSO> _unloadingInProgress = new();

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Adopt scenes already loaded at boot (Bootstrap loads the first area before this Start).
        if (allLocations == null) return;
        foreach (var loc in allLocations)
        {
            if (!loc.IsValid) continue;
            var scene = SceneManager.GetSceneByName(loc.scene.Name);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            _loadedLocations.Add(loc);
#if UNITY_EDITOR
            // Editor direct-play: seed both twins into the pre-opened area so
            // RecalculateLoadedSet() never unloads it before either twin crosses a trigger.
            // PlayerRoster.Awake() guaranteed to have run (same-scene, Start() fires after all Awakes).
            var roster = PlayerRoster.Instance;
            if (roster?.TwinA != null) _currentLocation.TryAdd(roster.TwinA, loc);
            if (roster?.TwinB != null) _currentLocation.TryAdd(roster.TwinB, loc);
#endif
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A twin walked into this location's trigger volume (SceneLoadTrigger.OnTriggerEnter).
    /// Assigns actor's current location; recalculates loaded set.
    /// </summary>
    public void NotifyTwinEntered(WorldLocationSO location, Player actor)
    {
        if (location == null || actor == null) return;
        _currentLocation[actor] = location;
        RecalculateLoadedSet();
    }

    /// <summary>
    /// Scripted teleport — triggers never see teleports, so callers must invoke this.
    /// Assigns actor's current location and recalculates.
    /// </summary>
    public void NotifyTeleported(Player actor, WorldLocationSO destination)
    {
        if (actor == null || destination == null) return;
        _currentLocation[actor] = destination;
        RecalculateLoadedSet();
    }

    /// <summary>Phase 3 hook: soft-reset in place (implementation pending).</summary>
    public void RequestSoftReset(WorldLocationSO[] locationsToReset)
    {
        Debug.Log("[SceneFlowManager] SoftReset requested (Phase 3 implementation pending).");
    }

    // ── Core streaming ──────────────────────────────────────────────────────────

    private void RecalculateLoadedSet()
    {
        var shouldBeLoaded = BuildDesiredSet();

        foreach (var loc in shouldBeLoaded)
        {
            if (!_loadedLocations.Contains(loc) && !_loadingInProgress.Contains(loc))
                StartCoroutine(LoadLocationAsync(loc));
        }

        foreach (var loc in _loadedLocations.ToList())
        {
            if (!shouldBeLoaded.Contains(loc) && !IsOccupied(loc) && !_unloadingInProgress.Contains(loc))
                StartCoroutine(UnloadLocationAsync(loc));
        }

        UpdateActiveScene();
    }

    private HashSet<WorldLocationSO> BuildDesiredSet()
    {
        var desired = new HashSet<WorldLocationSO>();
        foreach (var loc in _currentLocation.Values)
        {
            if (loc == null) continue;
            desired.Add(loc);
            if (loc.adjacentLocations == null) continue;
            foreach (var adj in loc.adjacentLocations)
                if (adj != null && adj.IsValid) desired.Add(adj);
        }
        return desired;
    }

    private IEnumerator LoadLocationAsync(WorldLocationSO location)
    {
        if (!location.IsValid) yield break;
        _loadingInProgress.Add(location);

        string sceneName = location.scene.Name;
        var existing = SceneManager.GetSceneByName(sceneName);
        if (!existing.IsValid() || !existing.isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneFlowManager] Cannot load '{sceneName}'. Is it in Build Settings?");
                _loadingInProgress.Remove(location);
                yield break;
            }
            while (!op.isDone) yield return null;
        }

        _loadedLocations.Add(location);
        _loadingInProgress.Remove(location);

        Debug.Log($"[SceneFlowManager] Loaded: {sceneName}");
        OnLocationLoaded?.Invoke(location);
        UpdateActiveScene(); // re-assert after the newly loaded scene is available
    }

    private IEnumerator UnloadLocationAsync(WorldLocationSO location)
    {
        if (!location.IsValid) yield break;
        _unloadingInProgress.Add(location);

        // unscaled: Setsuna (0.15×) would stretch 0.5 s to ~3.3 s; pause would halt it forever
        yield return new WaitForSecondsRealtime(unloadDelay); // R10

        // Re-check: actor may have re-entered during the delay
        if (IsOccupied(location)) { _unloadingInProgress.Remove(location); yield break; }
        var desired = BuildDesiredSet();
        if (desired.Contains(location)) { _unloadingInProgress.Remove(location); yield break; }

        // Signal EnemySpawner / QTEManager to despawn/cancel before the scene vanishes
        OnLocationWillUnload?.Invoke(location);
        OnSceneWillUnload?.Invoke(location.scene.Name);   // IFxSceneEvents mirror (FxManager F1 reclaim)

        string sceneName = location.scene.Name;
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            var op = SceneManager.UnloadSceneAsync(scene);
            if (op != null) while (!op.isDone) yield return null;
        }

        _loadedLocations.Remove(location);
        _unloadingInProgress.Remove(location);

        Debug.Log($"[SceneFlowManager] Unloaded: {sceneName}");
        OnLocationUnloaded?.Invoke(location);
    }

    // ── Active scene (3.7d) ─────────────────────────────────────────────────────

    private void UpdateActiveScene()
    {
        var loc = ResolveActiveLocation();
        if (loc != null)
        {
            var s = SceneManager.GetSceneByName(loc.scene.Name);
            if (s.IsValid() && s.isLoaded) SceneManager.SetActiveScene(s);
        }
        SetActiveLocation(loc);
    }

    // Couch D4: no "selected" twin — the active location follows the HOST twin (TwinA), deterministically
    // (no music-crossfade flicker; online-ready host authority). Falls through to the first actor with a
    // loaded scene, else null. Bond + adjacency make a non-adjacent twin split unreachable in practice.
    private WorldLocationSO ResolveActiveLocation()
    {
        var hostTwin = PlayerRoster.Instance?.TwinA;
        if (hostTwin != null && _currentLocation.TryGetValue(hostTwin, out var pref) && pref != null)
        {
            var s = SceneManager.GetSceneByName(pref.scene.Name);
            if (s.IsValid() && s.isLoaded) return pref;
        }

        foreach (var kv in _currentLocation)
        {
            if (kv.Value == null) continue;
            var s = SceneManager.GetSceneByName(kv.Value.scene.Name);
            if (s.IsValid() && s.isLoaded) return kv.Value;
        }
        return null;
    }

    private void SetActiveLocation(WorldLocationSO loc)
    {
        if (loc == ActiveLocation) return;
        ActiveLocation = loc;
        OnActiveLocationChanged?.Invoke(loc);
        // IFxSceneEvents mirror — MusicManager crossfades on this (package-safe payload).
        OnLocationAudioChanged?.Invoke(loc != null ? loc.musicTrack : null,
                                       loc != null ? loc.ambience : null);
    }

    // ── Queries ─────────────────────────────────────────────────────────────────

    public bool IsLoaded(WorldLocationSO location) => _loadedLocations.Contains(location);

    public bool IsOccupied(WorldLocationSO location)
        => _currentLocation.Values.Any(v => v == location);

    public IReadOnlyCollection<WorldLocationSO> LoadedLocations => _loadedLocations;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = false;

    private void OnGUI()
    {
        if (!Application.isPlaying || !showDebugOverlay) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 10, alignment = TextAnchor.UpperLeft };
        float y = 10;
        int rowCount = _loadedLocations.Count + _currentLocation.Count + 1;
        GUI.Box(new Rect(10, y, 260, 18 + rowCount * 16), "", style);
        GUI.Label(new Rect(14, y + 2, 250, 16), "[SceneFlowManager]", style);
        y += 20;
        foreach (var loc in _loadedLocations)
        {
            bool occ = IsOccupied(loc);
            GUI.Label(new Rect(14, y, 250, 16),
                $"  ● {loc.scene.Name}{(occ ? " [OCCUPIED]" : "")}", style);
            y += 16;
        }
        foreach (var kv in _currentLocation)
        {
            GUI.Label(new Rect(14, y, 250, 16),
                $"  ↳ {kv.Key?.name} → {kv.Value?.scene.Name ?? "??"}", style);
            y += 16;
        }
    }
#endif
}
