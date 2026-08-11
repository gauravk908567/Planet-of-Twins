using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Skippable intro cutscene with background scene loading.
///
/// HOW IT WORKS:
///   1. Immediately starts loading Persistent + the first game area in the background
///      (additive, while the intro plays — zero wasted time).
///   2. Plays a video clip via VideoPlayer (optional — works without a clip assigned).
///   3. Shows a "Press any key to skip" hint. The hint is active from the start,
///      but the skip is only processed AFTER minSkipDelay seconds AND after all
///      background loads are complete (so we never skip into a not-yet-ready game).
///   4. When skip fires (or video ends + loads done): fires OnIntroFinished,
///      then unloads this intro scene.
///
/// SETUP:
///   • Add to an empty GameObject in Intro.unity.
///   • Assign persistentScene + firstAreaLocation (WorldLocationSO for Level 1).
///   • Optionally assign videoPlayer (pointing at your intro clip).
///   • Assign skipHintText (a UI Text/TMP to show "Press any key to skip").
///   • Assign fadeImage (a full-screen black Image for fade in/out).
///   • Build Settings: Bootstrap (0) → Intro (1) → Persistent → L1Park → …
/// </summary>
public class IntroController : MonoBehaviour
{
    [Header("Scenes to load in background")]
    [SerializeField] private SceneReference persistentScene;
    [SerializeField] private WorldLocationSO firstAreaLocation;

    [Header("Video (optional)")]
    [Tooltip("Leave unassigned to skip straight to a fade-in title card.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("UI")]
    [SerializeField] private Graphic skipHintText;
    [SerializeField] private Image fadeImage;

    [Header("Timing")]
    [Tooltip("Minimum seconds before skip input is accepted.")]
    [SerializeField] private float minSkipDelay = 3f;
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("Ground-ready gate (2026-07-23 fall bug)")]
    [Tooltip("Terrain GameObject names that must ALL be present, enabled, with a valid " +
             "TerrainCollider before twins are unfrozen. AsyncOperation.isDone on the scene " +
             "load only proves the scene loaded, not that these are ready yet. No timeout — " +
             "keeps polling until they're actually ready. Empty = gate disabled.")]
    [SerializeField] private string[] requiredTerrainNames = { "BeforeTerrainPark", "BeforeTerrainStreet" };

    /// <summary>Fires just before the intro scene unloads itself.</summary>
    public static event System.Action OnIntroFinished;

    private bool _loadsComplete;
    private bool _skipping;
    private float _elapsed;
    private IInputProvider _input;   // P13: lazy — see Update (Persistent loads DURING the intro)
    private bool _noInputLogged;

    private void Start()
    {
        if (fadeImage != null) fadeImage.color = Color.black;
        if (skipHintText != null) skipHintText.color = new Color(1, 1, 1, 0);

        StartCoroutine(BackgroundLoad());
        StartCoroutine(PlayIntro());
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;

        // Enable skip hint after minSkipDelay
        if (skipHintText != null && _elapsed >= minSkipDelay)
        {
            float alpha = Mathf.Clamp01((_elapsed - minSkipDelay) / 0.5f);
            skipHintText.color = new Color(1, 1, 1, alpha);
        }

        // Accept skip only after delay + loads complete
        if (!_skipping && _loadsComplete && _elapsed >= minSkipDelay)
        {
            // P13: any-key via IInputProvider (Input System). Resolved LAZILY — Persistent
            // (and thus TwinInputReader) is background-loaded by THIS controller, so it does
            // not exist at our Start; _loadsComplete guarantees it exists here.
            _input ??= TwinInputReader.Instance;
            if (_input == null && !_noInputLogged)
            {
                _noInputLogged = true;
                Debug.LogError("[IntroController] TwinInputReader.Instance unresolved after loads — intro skip dead.", this);
            }
            if (_input != null && _input.GetAnySkipDown())
                StartCoroutine(FinishIntro());
        }
    }

    // ── Background loading ────────────────────────────────────────────────────

    private IEnumerator BackgroundLoad()
    {
        // Load Persistent first (contains all systems)
        if (!IsSceneLoaded(persistentScene.Name))
        {
            var op = SceneManager.LoadSceneAsync(persistentScene.Name, LoadSceneMode.Additive);
            if (op != null)
            {
                op.allowSceneActivation = true;
                while (!op.isDone) yield return null;
            }
            else
            {
                Debug.LogError($"[IntroController] Could not load '{persistentScene.Name}'. Check Build Settings.");
            }
        }

        // Persistent has no floor — lock twin movement so gravity doesn't send them underground
        // during the area load. IntroTimelinePositioner calls SetMovementLocked(false) + repositions
        // when the level timeline stops. PlaceTwinsAtAreaSpawn() is the fallback when no positioner exists.
        LockTwinMovement(true);

        // Load first game area
        if (firstAreaLocation != null && firstAreaLocation.IsValid)
        {
            string areaName = firstAreaLocation.scene.Name;
            if (!IsSceneLoaded(areaName))
            {
                var op = SceneManager.LoadSceneAsync(areaName, LoadSceneMode.Additive);
                if (op != null)
                {
                    op.allowSceneActivation = true;
                    while (!op.isDone) yield return null;
                }
                else
                {
                    Debug.LogError($"[IntroController] Could not load '{areaName}'. Check Build Settings.");
                }
            }

            // Set the area as the active scene for lighting / NavMesh
            var areaScene = SceneManager.GetSceneByName(areaName);
            if (areaScene.IsValid() && areaScene.isLoaded)
                SceneManager.SetActiveScene(areaScene);

            // Seed SceneFlowManager occupancy — triggers never see teleports (3.7b)
            var sel = TwinSelector.Instance;
            SceneFlowManager.Instance?.NotifyTeleported(sel?.LeftTwin,  firstAreaLocation);
            SceneFlowManager.Instance?.NotifyTeleported(sel?.RightTwin, firstAreaLocation);
        }

        // Ground-ready gate: wait for every required terrain (both AND'd), then unfreeze the
        // twins immediately — see WaitForGroundReady. NotifyTeleported above is what triggers
        // SceneFlowManager to stream in the adjacent area (e.g. Streets next to Park) — its
        // terrain may not exist yet even though THIS scene's own load already finished, which
        // is exactly the gap that let the twins fall through nothing.
        yield return WaitForGroundReady();

        // Fallback: if no IntroTimelinePositioner is present the lock would never lift.
        // IntroTimelinePositioner (explicitly allowed by FindAnyObjectByType whitelist) takes
        // priority when present — it handles CC-disable + rotation too.
        if (FindAnyObjectByType<IntroTimelinePositioner>() == null)
            PlaceTwinsAtAreaSpawn();

        _loadsComplete = true;
        Debug.Log("[IntroController] Background loads complete.");
    }

    // Waits until EVERY name in requiredTerrainNames resolves to an active GameObject with an
    // enabled Terrain + TerrainCollider carrying real data — the AND is deliberate (user call:
    // Park's terrain alone isn't enough if the twins can straddle the Streets boundary too).
    // No timeout: keeps polling for as long as it takes on whatever machine this runs on. The
    // instant it passes, twins are unfrozen right here — not handed off to anything else.
    private IEnumerator WaitForGroundReady()
    {
        if (requiredTerrainNames == null || requiredTerrainNames.Length == 0) yield break;

        while (true)
        {
            bool allReady = true;
            foreach (var name in requiredTerrainNames)
            {
                if (!TerrainReady(name)) { allReady = false; break; }
            }
            if (allReady)
            {
                Debug.Log("[IntroController] Ground-ready gate passed — " +
                          string.Join(", ", requiredTerrainNames));
                LockTwinMovement(false);
                yield break;
            }
            yield return null;
        }
    }

    // Terrain.activeTerrains is Unity's own live list of currently enabled terrains — cheaper
    // and more correct than a scene-wide FindObjectsByType sweep for this check.
    private static bool TerrainReady(string terrainName)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain == null) continue;
            if (terrain.gameObject.name != terrainName) continue;
            if (!terrain.gameObject.activeInHierarchy) continue;
            if (!terrain.enabled) continue;

            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null || !collider.enabled || collider.terrainData == null) continue;

            return true;
        }
        return false;
    }

    private static void LockTwinMovement(bool locked)
    {
        var selector = TwinSelector.Instance;
        if (selector == null) return;
        selector.LeftTwin?.Movement?.SetMovementLocked(locked);
        selector.RightTwin?.Movement?.SetMovementLocked(locked);
    }

    private static void PlaceTwinsAtAreaSpawn()
    {
        var spawnPoints = FindFirstObjectByType<AreaSpawnPoints>();
        if (spawnPoints == null)
        {
            Debug.LogWarning("[IntroController] No AreaSpawnPoints found — twins not repositioned.");
            LockTwinMovement(false);
            return;
        }
        var selector = TwinSelector.Instance;
        if (selector == null) { LockTwinMovement(false); return; }

        PlaceTwin(selector.LeftTwin,  spawnPoints.leftStart  != null ? spawnPoints.leftStart.position  : Vector3.zero);
        PlaceTwin(selector.RightTwin, spawnPoints.rightStart != null ? spawnPoints.rightStart.position : Vector3.zero);
        LockTwinMovement(false);
        Debug.Log($"[IntroController] Twins placed (fallback) — L={spawnPoints.leftStart?.position} R={spawnPoints.rightStart?.position}");
    }

    private static void PlaceTwin(Player twin, Vector3 pos)
    {
        if (twin == null) return;
        var cc = twin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        twin.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    // ── Intro playback ────────────────────────────────────────────────────────

    private IEnumerator PlayIntro()
    {
        // Fade in from black
        yield return Fade(1f, 0f, fadeInDuration);

        if (videoPlayer != null && videoPlayer.clip != null)
        {
            videoPlayer.Play();
            // Wait for video to finish (or for skip to fire first)
            while (videoPlayer.isPlaying && !_skipping)
                yield return null;
        }
        else
        {
            // No video: wait until loads are done + skip delay has passed
            yield return new WaitUntil(() => _loadsComplete && _elapsed >= minSkipDelay);
        }

        if (!_skipping)
            StartCoroutine(FinishIntro());
    }

    private IEnumerator FinishIntro()
    {
        if (_skipping) yield break;
        _skipping = true;

        // Ensure loads are done before we vanish
        yield return new WaitUntil(() => _loadsComplete);

        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        // Fade out
        yield return Fade(0f, 1f, fadeOutDuration);

        OnIntroFinished?.Invoke();

        // Unload this intro scene
        var introScene = gameObject.scene;
        yield return SceneManager.UnloadSceneAsync(introScene);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, to);
    }

    private static bool IsSceneLoaded(string name)
    {
        var s = SceneManager.GetSceneByName(name);
        return s.IsValid() && s.isLoaded;
    }
}
