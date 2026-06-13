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
///     Set introScene to your Intro.unity; leave firstAreaScene blank.
///
///   WITHOUT INTRO (dev / quick-start):
///     Bootstrap → loads Persistent + firstAreaScene directly, fires OnBootstrapComplete.
///     Leave introScene blank; set firstAreaScene.
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

    [Tooltip("First playable area — used only when introScene is blank (dev mode).")]
    [SerializeField] private SceneReference firstAreaScene;

    [Tooltip("WorldLocationSO for the first area — dev mode only. Seeds SceneFlowManager " +
             "occupancy so triggers aren't needed for the initial load. Leave null to skip seeding.")]
    [SerializeField] private WorldLocationSO firstAreaLocation;

    [Header("Options")]
    [Tooltip("Unload the Bootstrap scene once handoff is complete.")]
    [SerializeField] private bool unloadBootstrapWhenDone = true;

    /// <summary>
    /// Fires after Persistent + first area are up (dev mode only).
    /// In intro mode, IntroController.OnIntroFinished is the equivalent hook.
    /// </summary>
    public event System.Action OnBootstrapComplete;

    private void Start() => StartCoroutine(BootSequence());

    private IEnumerator BootSequence()
    {
        // Persistent isn't loaded yet — direct write; TimeScaleService takes over after.
        Time.timeScale = 1f;

        if (introScene.IsValid)
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

            if (firstAreaScene.IsValid && !IsLoaded(firstAreaScene.Name))
                yield return LoadAdditive(firstAreaScene.Name);

            // Place twins at the area's designated per-twin gameplay-start positions.
            // AreaSpawnPoints (leftStart / rightStart) is the authoritative source for
            // "where twins stand when gameplay begins" — not LocationEntrance, which is
            // for streaming transitions between areas.
            PlaceTwinsAtAreaSpawn();

            // Seed SceneFlowManager occupancy — triggers never see teleports (3.7b)
            if (firstAreaLocation != null)
            {
                var sel = TwinSelector.Instance;
                SceneFlowManager.Instance?.NotifyTeleported(sel?.LeftTwin,  firstAreaLocation);
                SceneFlowManager.Instance?.NotifyTeleported(sel?.RightTwin, firstAreaLocation);
            }

            // Make the area the active scene (drives ambient/skybox/NavMesh)
            // SceneFlowManager.UpdateActiveScene() will take over once occupancy is seeded above.
            var area = SceneManager.GetSceneByName(firstAreaScene.Name);
            if (area.IsValid() && area.isLoaded)
                SceneManager.SetActiveScene(area);

            OnBootstrapComplete?.Invoke();
            Debug.Log($"[GameBootstrapper] Boot complete — Persistent + '{firstAreaScene.Name}' loaded.");
        }

        // Unload Bootstrap (this GameObject lives here, so do it last)
        if (unloadBootstrapWhenDone)
        {
            var boot = gameObject.scene;
            if (boot.name != persistentScene.Name &&
                boot.name != firstAreaScene.Name &&
                boot.name != introScene.Name)
            {
                yield return SceneManager.UnloadSceneAsync(boot);
            }
        }
    }

    private void SetTwinsMovementLocked(bool locked)
    {
        var selector = TwinSelector.Instance;
        if (selector == null) return;
        selector.LeftTwin?.GetComponent<PlayerMovementController>()?.SetMovementLocked(locked);
        selector.RightTwin?.GetComponent<PlayerMovementController>()?.SetMovementLocked(locked);
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
        var selector = TwinSelector.Instance;
        if (selector == null) { SetTwinsMovementLocked(false); return; }

        PlaceTwin(selector.LeftTwin,  spawnPoints.leftStart  != null ? spawnPoints.leftStart.position  : Vector3.zero);
        PlaceTwin(selector.RightTwin, spawnPoints.rightStart != null ? spawnPoints.rightStart.position : Vector3.zero);
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
