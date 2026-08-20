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

    [Tooltip("Couch M2 — MENU-FIRST boot: run the pre-game front-end (Start Menu → Character Select) after " +
             "Persistent loads and before the area, in BOTH dev and shipped. When on, the intro cutscene is " +
             "bypassed as the ENTRY (it is re-introduced as a New Game step inside the flow — C2). Requires a " +
             "FrontEndFlowController in Persistent; fail-open (skips to gameplay if absent/unwired). Off by " +
             "default so the legacy intro / dev quick-boot paths are unaffected.")]
    [SerializeField] private bool useFrontEnd = false;

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

        // Boot has three shapes:
        //   • DEV-DIRECT (DevConfig.SkipTutorial + devStartArea): skip the intro/cutscene and jump straight to
        //     a chosen area for fast iteration. LocalBoot() owns it (optionally showing the front-end first).
        //   • SHIPPED (intro configured): the intro cutscene owns WHERE the game actually starts (its own
        //     firstAreaLocation — the tutorial zone) and the tutorial start signal. We do NOT pick the start
        //     area here. Menu-first (couch M2): when useFrontEnd is on we run the pre-game front-end (Main
        //     Menu → Character Select) FIRST, then hand off to the untouched intro. Full order:
        //     boot → menu → new game → character select → cutscene → game starts exactly where it always has.
        //   • FALLBACK (no intro, no dev area): LocalBoot().
        bool devDirect = DevConfig.SkipTutorial && devStartArea.IsValid;
        bool useIntro  = introScene.IsValid && !devDirect;

        if (devDirect)
        {
            yield return LocalBoot();
        }
        else if (useIntro)
        {
            // Menu-first: the menu lives in Persistent, so load Persistent in the foreground BEFORE the menu,
            // run the front-end, THEN hand off to the intro. IntroController re-guards the Persistent load
            // (won't double-load) and owns the real start-area load + cutscene + tutorial start — untouched.
            if (useFrontEnd)
            {
                if (!IsLoaded(persistentScene.Name))
                    yield return LoadAdditive(persistentScene.Name);
                SetTwinsMovementLocked(true);   // twins sit in Persistent over the void while the menu shows
                yield return RunFrontEnd();
            }

            yield return LoadAdditive(introScene.Name);
            Debug.Log("[GameBootstrapper] Intro scene loaded — handing off to IntroController.");
        }
        else
        {
            yield return LocalBoot();
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

    /// <summary>Local boot with NO intro/cutscene: load Persistent, (optionally) run the front-end, then load
    /// <see cref="devStartArea"/> directly and place the twins. This is the DEV-DIRECT fast-iteration path
    /// (DevConfig.SkipTutorial) and the no-intro fallback — it is the ONLY path that picks a start area itself;
    /// the shipped path defers that to the intro.</summary>
    private IEnumerator LocalBoot()
    {
        if (!IsLoaded(persistentScene.Name))
            yield return LoadAdditive(persistentScene.Name);

        // Persistent has no floor — lock twin movement so they don't fall during the area-scene load. Gravity
        // only applies inside ExecuteCommand, so SetMovementLocked(true) is sufficient (no CC disable needed).
        SetTwinsMovementLocked(true);

        // Front-end (couch M2): Main Menu → Character Select (writes PlayerRoster). Persistent is up so the
        // roster exists; the area isn't loaded yet (the full-screen menu covers the void). Twins stay locked.
        if (useFrontEnd)
            yield return RunFrontEnd();

        if (devStartArea.IsValid && !IsLoaded(devStartArea.Name))
            yield return LoadAdditive(devStartArea.Name);

        // Place twins at the area's designated per-twin gameplay-start positions. AreaSpawnPoints
        // (leftStart / rightStart) is the authoritative source for "where twins stand when gameplay begins" —
        // not LocationEntrance, which is for streaming transitions between areas.
        PlaceTwinsAtAreaSpawn();

        // Seed SceneFlowManager occupancy — triggers never see teleports (3.7b)
        if (devStartLocation != null)
        {
            var roster = PlayerRoster.Instance;
            SceneFlowManager.Instance?.NotifyTeleported(roster?.TwinA, devStartLocation);
            SceneFlowManager.Instance?.NotifyTeleported(roster?.TwinB, devStartLocation);
        }

        // Make the area the active scene (drives ambient/skybox/NavMesh). SceneFlowManager.UpdateActiveScene()
        // takes over once occupancy is seeded above.
        var area = SceneManager.GetSceneByName(devStartArea.Name);
        if (area.IsValid() && area.isLoaded)
            SceneManager.SetActiveScene(area);

        OnBootstrapComplete?.Invoke();
        Debug.Log($"[GameBootstrapper] Boot complete — Persistent + '{devStartArea.Name}' loaded.");
    }

    /// <summary>Run the pre-game front-end (Main Menu → Character Select) and wait for it to finish. Fail-open:
    /// if the <see cref="FrontEndFlowController"/> is absent/unwired, log and proceed so boot still reaches the
    /// game. The controller writes the ownership into PlayerRoster; it never loads any area.</summary>
    private IEnumerator RunFrontEnd()
    {
        var frontEnd = FrontEndFlowController.Instance;
        if (frontEnd != null)
        {
            frontEnd.Begin();
            yield return new WaitUntil(() => FrontEndFlowController.Instance == null
                                             || FrontEndFlowController.Instance.IsFrontEndComplete);
        }
        else
        {
            Debug.LogWarning("[GameBootstrapper] useFrontEnd is on but no FrontEndFlowController in " +
                             "Persistent — skipping the front-end.");
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
