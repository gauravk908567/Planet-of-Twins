using UnityEngine;

/// <summary>
/// Central dev toggles (Resources/DevConfig.asset). The flags are INDEPENDENT — none gates another, so you can
/// e.g. enable the Trainer while the game still follows the normal scene flow. Check none → full shipping
/// version (normal flow, no trainer, no debug). Check both → trainer + skip tutorial. Etc.
///
///   • Trainer        — skill-point hack (keys + UI) and other debug UI/overlays.
///   • SkipTutorial   — skip the tutorial + cutscene (and dev-boot straight to the start area).
///
/// SHIP SAFETY (per-build, no code change): a flag only takes effect where debug is ALLOWED — the Editor, or a
/// **Development Build** (the Build Settings checkbox). A **release** build (checkbox off) forces every flag
/// FALSE, so debug can never reach players regardless of the asset (CLAUDE.md "never ship debug"). So: tick
/// Trainer + make a Development Build → trainer ships in that build; a normal release build ignores it. CONFIG ONLY (R7).
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Dev/Dev Config", fileName = "DevConfig")]
public class DevConfig : ScriptableObject
{
    [Header("MASTER FAIL-SAFE")]
    [Tooltip("Hard kill switch. OFF = ALL dev/debug is force-disabled no matter what the toggles below say " +
             "(catches a flag left on by mistake / stray debug code). ON = the independent toggles below apply. " +
             "A release build ignores this entirely and forces everything off regardless.")]
    [SerializeField] private bool masterEnabled = true;

    [Header("Independent toggles (none gates another — both need Master ON)")]
    [Tooltip("Enable the Trainer: skill-point hack keys (L/O/P/I/K) + its on-screen UI + other debug UI. " +
             "Works even while the game follows the NORMAL scene flow.")]
    [SerializeField] private bool trainer = false;

    [Tooltip("Skip the tutorial + cutscene and dev-boot straight into the start area (GameBootstrapper). " +
             "OFF = normal intro/tutorial flow.")]
    [SerializeField] private bool skipTutorial = false;

    // ── runtime accessor ────────────────────────────────────────────────────────
    // Resolved from Resources/DevConfig by default, but a scene component (e.g. GameBootstrapper) can call
    // SetActive() to make ITS assigned asset the source — so you can see/assign the config in an inspector
    // AND any consumer (SkillPointDebug, TutorialDirector) still reads the same one statically.
    private static DevConfig _instance;
    private static bool _loaded;

    private static DevConfig Instance
    {
        get
        {
            if (!_loaded)
            {
                _instance = Resources.Load<DevConfig>("DevConfig");
                _loaded = true;
                if (_instance == null)
                    Debug.LogWarning("[DevConfig] No Resources/DevConfig.asset — all dev flags default OFF (shipping flow).");
            }
            return _instance;
        }
    }

    /// <summary>Let a scene component supply the active config explicitly (e.g. the one assigned on
    /// GameBootstrapper's inspector slot). Null is ignored so the Resources fallback still wins.</summary>
    public static void SetActive(DevConfig config)
    {
        if (config == null) return;
        _instance = config;
        _loaded = true;
    }

    /// <summary>Is ANY dev/debug allowed to run? Requires: an asset exists, its master fail-safe is ON, AND the
    /// build permits debug (Editor or Development Build; a release build always returns false). Every flag is
    /// ANDed with this, so master OFF or a release build hard-kills all dev/debug regardless of the toggles.</summary>
    private static bool MasterOn
    {
        get
        {
            if (Instance == null || !Instance.masterEnabled) return false;
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild;   // release build → no debug, ever
#endif
        }
    }

    /// <summary>Trainer (skill-point hack + debug UI). Independent of SkipTutorial; both need Master ON.</summary>
    public static bool Trainer      => MasterOn && Instance.trainer;
    /// <summary>Skip tutorial + dev-boot to the start area. Independent of Trainer; both need Master ON.</summary>
    public static bool SkipTutorial => MasterOn && Instance.skipTutorial;
}
