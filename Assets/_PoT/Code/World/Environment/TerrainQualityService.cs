using UnityEngine;

/// <summary>
/// Terrain quality tiers (2026-07-16) — the options-menu seam for PoT/TerrainLit.
/// The shader compiles every feature as a runtime keyword (multi_compile), so a tier
/// switch is instant: enable/disable _POT_HEX / _POT_PARALLAX on the terrain material +
/// set the per-terrain LOD knobs (pixel error, detail density/distance, tree distance,
/// basemap distance). Applies to every loaded terrain and re-applies to terrains that
/// stream in later (SceneFlowManager loads area scenes additively — call Apply again or
/// rely on the OnTerrainChanged hook below).
///
/// Wire-up: lives in Persistent. The settings menu calls <see cref="SetTier"/> (0..2).
/// Until the menu has a control, the serialized default tier applies on Start.
/// </summary>
public class TerrainQualityService : MonoBehaviour
{
    public static TerrainQualityService Instance { get; private set; }

    [System.Serializable]
    public class Tier
    {
        public string name = "High";
        public bool hexBreakup = true;        // _POT_HEX
        public bool parallax = true;          // _POT_PARALLAX
        public float pixelError = 5f;
        public float detailDensity = 1f;      // detailObjectDensity (0..1)
        public float detailDistance = 120f;
        public float treeDistance = 150f;
        public float basemapDistance = 500f;  // keep >= zone size so PoT shader covers it
    }

    [Tooltip("0 = Low, 1 = Medium, 2 = High. Applied on Start and via SetTier().")]
    [SerializeField] private int _defaultTier = 2;

    [SerializeField] private Tier[] _tiers = new Tier[]
    {
        new Tier { name = "Low",    hexBreakup = false, parallax = false, pixelError = 12f, detailDensity = 0.5f, detailDistance = 60f,  treeDistance = 90f,  basemapDistance = 300f },
        new Tier { name = "Medium", hexBreakup = true,  parallax = false, pixelError = 8f,  detailDensity = 0.8f, detailDistance = 90f,  treeDistance = 120f, basemapDistance = 500f },
        new Tier { name = "High",   hexBreakup = true,  parallax = true,  pixelError = 5f,  detailDensity = 1f,   detailDistance = 120f, treeDistance = 150f, basemapDistance = 500f },
    };

    public int CurrentTier { get; private set; } = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;   // named handler (R8)
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Area scenes stream in additively — their terrains need the current tier applied.
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ApplyToLoadedTerrains();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        SetTier(_defaultTier);
    }

    /// <summary>Options menu entry point. Applies to all currently loaded terrains.</summary>
    public void SetTier(int tier)
    {
        if (_tiers == null || _tiers.Length == 0)
        {
            Debug.LogError("[TerrainQualityService] No tiers configured.", this);
            return;
        }
        CurrentTier = Mathf.Clamp(tier, 0, _tiers.Length - 1);
        ApplyToLoadedTerrains();
    }

    /// <summary>Re-apply the current tier — call after an area scene streams in.</summary>
    public void ApplyToLoadedTerrains()
    {
        if (CurrentTier < 0) return;
        Tier t = _tiers[CurrentTier];
        Terrain[] terrains = Terrain.activeTerrains;   // loaded/enabled terrains only
        for (int i = 0; i < terrains.Length; i++)
            ApplyTier(terrains[i], t);
    }

    private static void ApplyTier(Terrain terrain, Tier t)
    {
        if (terrain == null) return;

        Material m = terrain.materialTemplate;
        // keywords only apply to our shader; foreign materials just get the LOD knobs
        if (m != null && m.shader != null && m.shader.name == "PoT/TerrainLit")
        {
            if (t.hexBreakup) m.EnableKeyword("_POT_HEX"); else m.DisableKeyword("_POT_HEX");
            if (t.parallax) m.EnableKeyword("_POT_PARALLAX"); else m.DisableKeyword("_POT_PARALLAX");

            // GLOBAL mirror for the hidden auto-generated add-pass material (layers 5-8):
            // "Hidden/PoT/TerrainLit (Add Pass)" declares these as global-scope keywords
            // because per-material state can't reach a terrain-engine-internal material.
            if (t.hexBreakup) Shader.EnableKeyword("_POT_HEX"); else Shader.DisableKeyword("_POT_HEX");
            if (t.parallax) Shader.EnableKeyword("_POT_PARALLAX"); else Shader.DisableKeyword("_POT_PARALLAX");
        }

        terrain.heightmapPixelError = t.pixelError;
        terrain.detailObjectDensity = t.detailDensity;
        terrain.detailObjectDistance = t.detailDistance;
        terrain.treeDistance = t.treeDistance;
        terrain.basemapDistance = t.basemapDistance;
    }
}
