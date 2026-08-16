using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry-point bootstrapper. Lives in Bootstrap.unity (build index 0 — the only
/// scene present when the game starts).
///
/// TWO MODES (set in Inspector):
///
///   WITH INTRO (recommended for shipped build):
///     Bootstrap → loads Intro.unity additively → IntroController loads Persistent +
///     first area in the background while the intro video plays.
///     Set introScene to your Intro.unity; leave devStartArea blank.
///
///   WITHOUT INTRO (dev / quick-start):
///     Bootstrap → loads Persistent + devStartArea directly, fires OnBootstrapComplete.
///     Leave introScene blank; set devStartArea.
///
/// SETUP:
///   1. Create Bootstrap.unity. Add empty GameObject "Bootstrapper" with this component.
///   2. Pick scenes from the dropdowns (all must be in File > Build Settings).
///   3. Build Settings order: Bootstrap (0), Intro, Persistent, L1Park, …
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    [Header("Scenes (must all be in Build Settings)")]
    [Tooltip("The persistent scene: PlayerRig + all singletons. Never unloaded.")]
    [SerializeField] private SceneReference persistentScene;

    [Tooltip("Intro / loading screen scene. If valid, Bootstrap hands off to IntroController " +
             "which loads Persistent + the first area in the background.")]
    [SerializeField] private SceneReference introScene;

    [Tooltip("DEV start area — where dev-boot (DevConfig.Skip Tutorial) jumps for TESTING. This is NOT the " +
             "game's real first area (that's reached via the intro → IntroController). Set it to whatever scene " +
             "you want to test in (L1_Park, L2_Streets, …). Ignored unless Skip Tutorial is on.")]
    [SerializeField] private SceneReference devStartArea;

    [Tooltip("WorldLocationSO for the DEV start area (above). Seeds SceneFlowManager occupancy so triggers " +
             "aren't needed for the initial load. Leave null to skip seeding. Dev-boot only.")]
    [SerializeField] private WorldLocationSO devStartLocation;

    [Header("Dev")]
    [Tooltip("The central dev/debug config (also at Resources/DevConfig). Assign it here so its toggles " +
             "(Master / Trainer / Skip Tutorial) are visible right next to the boot fields. When Skip Tutorial " +
             "is on, boot skips the intro and dev-loads the Dev Start Area above.")]
    [SerializeField] private DevConfig devConfig;

    [Header("Options")]
    [Tooltip("Unload the Bootstrap scene once handoff is complete.")]
    [SerializeField] private bool unloadBootstrapWhenDone = true;

    /// <summary>
    /// Fires after Persistent + first area are up (dev mode only).
    /// In intro mode, IntroController.OnIntroFinished is the equivalent hook.
    /// </summary>
    public event System.Action OnBootstrapComplete;

    // Register the inspector-assigned config as the active one BEFORE anything reads DevConfig (Awake < Start).
    // If left null, DevConfig falls back to Resources/DevConfig automatically.
    private void Awake() => DevConfig.SetActive(devConfig);

    private void Start() => StartCoroutine(BootSequence());

    private IEnumerator BootSequence()
    {
        // Persistent isn't loaded yet — direct write; TimeScaleService takes over after.
        Time.timeScale = 1f;

        // DevConfig.SkipTutorial ON → dev-boot (skip intro, load devStartArea straight). OFF (or release
        // build) → normal intro flow. One flag instead of hand-editing introScene. Needs devStartArea set.
        bool useIntro = introScene.IsValid && !(DevConfig.SkipTutorial && devStartArea.IsValid);

        if (useIntro)
        {
            // ── Intro mode: hand off to IntroController ──────────────────────
            // IntroController will load Persistent + first area in background.
            yield return LoadAdditive(introScene.Name);
            Debug.Log("[GameBootstrapper] Intro scene loaded — handing off to IntroController.");
        }
        else
        {
            // ── Dev mode: load Persistent + first area directly ───────────────
            if (!IsLoaded(persistentScene.Name))
                yield return LoadAdditive(persistentScene.Name);

            // Persistent has no floor — lock twin movement so they don't fall
            // during the area-scene load. Gravity only applies inside ExecuteCommand,
            // so SetMovementLocked(true) is sufficient (no CC disable needed yet).
            SetTwinsMovementLocked(true);

            if (devStartArea.IsValid && !IsLoaded(devStartArea.Name))
                yield return LoadAdditive(devStartArea.Name);

            // Place twins at the area's designated per-twin gameplay-start positions.
            // AreaSpawnPoints (leftStart / rightStart) is the authoritative source for
            // "where twins stand when gameplay begins" — not LocationEntrance, which is
            // for streaming transitions between areas.
            PlaceTwinsAtAreaSpawn();

            // Seed SceneFlowManager occupancy — triggers never see teleports (3.7b)
            if (devStartLocation != null)
            {
                var roster = PlayerRoster.Instance;
                SceneFlowManager.Instance?.NotifyTeleported(roster?.TwinA, devStartLocation);
                SceneFlowManager.Instance?.NotifyTeleported(roster?.TwinB, devStartLocation);
            }

            // Make the area the active scene (drives ambient/skybox/NavMesh)
            // SceneFlowManager.UpdateActiveScene() will take over once occupancy is seeded above.
            var area = SceneManager.GetSceneByName(devStartArea.Name);
            if (area.IsValid() && area.isLoaded)
                SceneManager.SetActiveScene(area);

            OnBootstrapComplete?.Invoke();
            Debug.Log($"[GameBootstrapper] Boot complete — Persistent + '{devStartArea.Name}' loaded.");
        }

        // Unload Bootstrap (this GameObject lives here, so do it last)
        if (unloadBootstrapWhenDone)
        {
            var boot = gameObject.scene;
            if (boot.name != persistentScene.Name &&
                boot.name != devStartArea.Name &&
                boot.name != introScene.Name)
            {
                yield return SceneManager.UnloadSceneAsync(boot);
            }
        }
    }

    private void SetTwinsMovementLocked(bool locked)
    {
        var roster = PlayerRoster.Instance;
        if (roster == null) return;
        roster.TwinA?.GetComponent<PlayerMovementController>()?.SetMovementLocked(locked);
        roster.TwinB?.GetComponent<PlayerMovementController>()?.SetMovementLocked(locked);
    }

    private void PlaceTwinsAtAreaSpawn()
    {
        var spawnPoints = Object.FindFirstObjectByType<AreaSpawnPoints>();
        if (spawnPoints == null)
        {
            Debug.LogWarning("[GameBootstrapper] No AreaSpawnPoints found — twins not repositioned.");
            SetTwinsMovementLocked(false);
            return;
        }
        var roster = PlayerRoster.Instance;
        if (roster == null) { SetTwinsMovementLocked(false); return; }

        PlaceTwin(roster.TwinA,  spawnPoints.leftStart  != null ? spawnPoints.leftStart.position  : Vector3.zero);
        PlaceTwin(roster.TwinB, spawnPoints.rightStart != null ? spawnPoints.rightStart.position : Vector3.zero);
        SetTwinsMovementLocked(false);
        Debug.Log($"[GameBootstrapper] Twins placed — L={spawnPoints.leftStart?.position} R={spawnPoints.rightStart?.position}");
    }

    private static void PlaceTwin(Player twin, Vector3 pos)
    {
        if (twin == null) return;
        var cc = twin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        twin.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    private static IEnumerator LoadAdditive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) yield break;

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[GameBootstrapper] Could not load '{sceneName}'. " +
                           "Is it added to Build Settings?");
            yield break;
        }
        while (!op.isDone) yield return null;
    }

    private static bool IsLoaded(string sceneName)
    {
        var s = SceneManager.GetSceneByName(sceneName);
        return s.IsValid() && s.isLoaded;
    }
}
