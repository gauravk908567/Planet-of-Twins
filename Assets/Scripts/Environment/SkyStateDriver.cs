using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;   // VolumeProfile — the volumetric fog whose tint sky states drive

/// <summary>
/// SkyStateDriver — Persistent singleton that applies/blends authored SkyStateData sets
/// onto the shared PoT/CoexistenceSkybox material. The sky-side twin of StoryGradeDirector:
/// same seam pattern (no runtime caller yet — story wiring calls ApplyState/BlendTo when
/// checkpoint story flags exist; the boot state applies in Start).
///
/// Story arc (game.md §1.1): "golden_hour" = Accord festival before the crack cinematic;
/// "dusk" = the wake-up after it (moon on); "day" = neutral/dev.
///
/// Blends run on UNSCALED time (sky must keep moving through Setsuna/pause, mirroring
/// StoryGradeDirector). The skybox material is a SHARED ASSET: in-editor we snapshot its
/// state on Awake and restore it in OnDestroy so play mode never dirties the asset
/// (same editor-safety pattern as GraphicsSettingsController).
/// </summary>
[ExecuteInEditMode]
public class SkyStateDriver : MonoBehaviour
{
    public static SkyStateDriver Instance { get; private set; }

    [SerializeField] private Material _skyboxMaterial;   // M_CoexistenceSkybox
    [Tooltip("The Persistent sun (directional) light. Assigned → sky states move the sun's DIRECTION " +
             "(the skybox disc reads the light, so disc + shadows move together). Empty → sun dir untouched.")]
    [SerializeField] private Light _sunLight;
    [SerializeField] private SkyStateData[] _states;
    [SerializeField] private string _bootStateId = "day";
    [Tooltip("Seconds between DynamicGI ambient refreshes while a blend is running.")]
    [SerializeField] private float _ambientRefreshInterval = 0.5f; // unscaled
    [Tooltip("The volumetric-fog VolumeProfile (the Persistent 'FogVolume Profile') whose main-light TINT " +
             "sky states drive. Empty → fog tint left untouched; only states with fogTint alpha > 0 change it.")]
    [SerializeField] private VolumeProfile _fogProfile;

    private static readonly int SkyTopID = Shader.PropertyToID("_SkyTop");
    private static readonly int SkyHorizonID = Shader.PropertyToID("_SkyHorizon");
    private static readonly int SkyBottomID = Shader.PropertyToID("_SkyBottom");
    private static readonly int CloudColorID = Shader.PropertyToID("_CloudColor");
    private static readonly int CloudShadowID = Shader.PropertyToID("_CloudShadow");
    private static readonly int SunColorID = Shader.PropertyToID("_SunColor");
    private static readonly int SunIntensityID = Shader.PropertyToID("_SunIntensity");
    private static readonly int MoonIntensityID = Shader.PropertyToID("_MoonIntensity");
    private static readonly int MoonSizeID = Shader.PropertyToID("_MoonSize");
    private static readonly int MoonDirID = Shader.PropertyToID("_MoonDir");
    private static readonly int MoonHaloID = Shader.PropertyToID("_MoonHalo");

    private Coroutine _blend;
    private VolumetricFogVolumeComponent _fogComp;   // resolved from _fogProfile in Awake (null = fog untouched)

#if UNITY_EDITOR
    private Material _editorSnapshot;   // exact-value restore so play mode never dirties the asset
    private Color _editorFogTint;       // fog-tint restore so play mode never dirties the shared fog profile
    private bool _editorFogTintOverride;
    private bool _hadFogComp;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { SafeDestroy(gameObject); return; }
        Instance = this;

        if (_skyboxMaterial == null)
        {
            Debug.LogError("[SkyStateDriver] Skybox material not assigned.", this);
            enabled = false;
            return;
        }

        // Optional: the volumetric fog whose main-light tint sky states drive (null = fog untouched).
        if (_fogProfile != null) _fogProfile.TryGet(out _fogComp);

#if UNITY_EDITOR
        _editorSnapshot = new Material(_skyboxMaterial);
        if (_fogComp != null)
        {
            _editorFogTint = _fogComp.tint.value;
            _editorFogTintOverride = _fogComp.tint.overrideState;
            _hadFogComp = true;
        }
#endif
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        if (!string.IsNullOrEmpty(_bootStateId))
            ApplyState(_bootStateId);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
#if UNITY_EDITOR
        if (_skyboxMaterial != null && _editorSnapshot != null)
        {
            _skyboxMaterial.CopyPropertiesFromMaterial(_editorSnapshot);
            SafeDestroy(_editorSnapshot);
            _editorSnapshot = null;
        }
        if (_hadFogComp && _fogComp != null)   // restore the shared fog profile's tint so play mode never dirties it
        {
            _fogComp.tint.value = _editorFogTint;
            _fogComp.tint.overrideState = _editorFogTintOverride;
        }
#endif
    }

    /// <summary>Instant cut to a state (use for boot / hard story cuts).</summary>
    public void ApplyState(string id)
    {
        var s = Find(id);
        if (s == null) return;
        if (_blend != null) { StopCoroutine(_blend); _blend = null; }
        Write(s, s, 1f);
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>Blend from the CURRENT material values to a state over unscaled seconds.</summary>
    public void BlendTo(string id, float seconds)
    {
        var target = Find(id);
        if (target == null) return;
        if (_blend != null) StopCoroutine(_blend);
        _blend = StartCoroutine(BlendRoutine(target, Mathf.Max(0.01f, seconds)));
    }

    /// <summary>Authored state ids (for dev benches / editors). Allocates — cache the result.</summary>
    public string[] StateIds()
    {
        if (_states == null) return System.Array.Empty<string>();
        var ids = new string[_states.Length];
        for (int i = 0; i < _states.Length; i++) ids[i] = _states[i] != null ? _states[i].id : "-";
        return ids;
    }

    private SkyStateData Find(string id)
    {
        foreach (var s in _states)
            if (s != null && s.id == id) return s;
        Debug.LogError($"[SkyStateDriver] Unknown sky state '{id}'.", this);
        return null;
    }

    private IEnumerator BlendRoutine(SkyStateData target, float seconds)
    {
        // Capture the CURRENT material values as the from-pole so mid-blend retargeting is
        // seamless (mirrors StoryGradeDirector's role-swap behaviour).
        var from = ScriptableObject.CreateInstance<SkyStateData>();
        ReadInto(from);

        float elapsed = 0f;              // unscaled — sky keeps moving through Setsuna/pause
        float nextAmbient = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            Write(from, target, t);
            if (elapsed >= nextAmbient)
            {
                DynamicGI.UpdateEnvironment();
                nextAmbient = elapsed + _ambientRefreshInterval;
            }
            yield return null;
        }
        Write(target, target, 1f);
        DynamicGI.UpdateEnvironment();
        SafeDestroy(from);
        _blend = null;
    }

    private void ReadInto(SkyStateData s)
    {
        var m = _skyboxMaterial;
        s.skyTop = m.GetColor(SkyTopID);
        s.skyHorizon = m.GetColor(SkyHorizonID);
        s.skyBottom = m.GetColor(SkyBottomID);
        s.cloudColor = m.GetColor(CloudColorID);
        s.cloudShadow = m.GetColor(CloudShadowID);
        s.sunColor = m.GetColor(SunColorID);
        s.sunIntensity = m.GetFloat(SunIntensityID);
        s.moonIntensity = m.GetFloat(MoonIntensityID);
        s.moonSize = m.GetFloat(MoonSizeID);
        s.moonDir = m.GetVector(MoonDirID);
        s.moonHalo = m.GetFloat(MoonHaloID);
        if (_sunLight != null)
        {
            s.sunDir = -_sunLight.transform.forward;   // sky direction toward the sun
            s.lightIntensity = _sunLight.intensity;    // capture so a blend starts from the real light
            s.lightColor = new Color(_sunLight.color.r, _sunLight.color.g, _sunLight.color.b, 1f); // alpha 1 = drive
        }
        if (_fogComp != null)   // capture live fog tint (alpha 1 = drive) so a blend starts from the real value
        {
            var f = _fogComp.tint.value;
            s.fogTint = new Color(f.r, f.g, f.b, 1f);
        }
    }

    private void Write(SkyStateData a, SkyStateData b, float t)
    {
        var m = _skyboxMaterial;
        m.SetColor(SkyTopID, Color.Lerp(a.skyTop, b.skyTop, t));
        m.SetColor(SkyHorizonID, Color.Lerp(a.skyHorizon, b.skyHorizon, t));
        m.SetColor(SkyBottomID, Color.Lerp(a.skyBottom, b.skyBottom, t));
        m.SetColor(CloudColorID, Color.Lerp(a.cloudColor, b.cloudColor, t));
        m.SetColor(CloudShadowID, Color.Lerp(a.cloudShadow, b.cloudShadow, t));
        m.SetColor(SunColorID, Color.Lerp(a.sunColor, b.sunColor, t));
        m.SetFloat(SunIntensityID, Mathf.Lerp(a.sunIntensity, b.sunIntensity, t));
        m.SetFloat(MoonIntensityID, Mathf.Lerp(a.moonIntensity, b.moonIntensity, t));
        m.SetFloat(MoonSizeID, Mathf.Lerp(a.moonSize, b.moonSize, t));
        m.SetVector(MoonDirID, Vector3.Slerp(a.moonDir.normalized, b.moonDir.normalized, t));
        m.SetFloat(MoonHaloID, Mathf.Lerp(a.moonHalo, b.moonHalo, t));

        // Sun is the real LIGHT (the skybox disc reads _MainLightPosition), so "sun direction" rotates
        // the light — disc + shadows stay in sync. A zero target sunDir = a state that doesn't move the sun.
        if (_sunLight != null)
        {
            // NEW: pick which body drives the directional light for this state
            Vector3 targetDir = b.lightFollowsMoon ? b.moonDir : b.sunDir;
            Vector3 fromDir = a.lightFollowsMoon ? a.moonDir : a.sunDir;

            if (targetDir.sqrMagnitude > 1e-4f)
            {
                Vector3 effectiveFromDir = fromDir.sqrMagnitude > 1e-4f ? fromDir.normalized : -_sunLight.transform.forward;
                _sunLight.transform.rotation = Quaternion.LookRotation(-Vector3.Slerp(effectiveFromDir, targetDir.normalized, t));
            }
            // The REAL light's intensity/colour is what actually darkens the WORLD (skybox _SunIntensity only
            // dims the disc). Sentinels: intensity < 0 or colour alpha 0 = "this state leaves the light alone".
            if (b.lightIntensity >= 0f)
            {
                float fromI = a.lightIntensity >= 0f ? a.lightIntensity : _sunLight.intensity;
                _sunLight.intensity = Mathf.Lerp(fromI, b.lightIntensity, t);
            }
            if (b.lightColor.a > 0f)
            {
                Color fromC = a.lightColor.a > 0f ? a.lightColor : _sunLight.color;
                _sunLight.color = Color.Lerp(fromC, b.lightColor, t);
            }
        }

        // Volumetric fog main-light TINT — same alpha sentinel as the scene light colour (alpha 0 = leave alone).
        // Only the fog's HUE is touched (density/scattering untouched), so the environment stays as visible as
        // before while the fog shifts cool for dusk. Writes the shared profile's override; the Volume stack picks
        // it up next frame. Editor snapshot in OnDestroy keeps the asset from being dirtied by play mode.
        if (_fogComp != null && b.fogTint.a > 0f)
        {
            Color fromF = a.fogTint.a > 0f ? a.fogTint : _fogComp.tint.value;
            _fogComp.tint.value = Color.Lerp(fromF, b.fogTint, t);
            _fogComp.tint.overrideState = true;
        }
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("TEST: Apply golden_hour")] private void TestGolden() => ApplyState("golden_hour");
    [ContextMenu("TEST: Apply dusk (instant)")] private void TestApplyDusk() => ApplyState("dusk");
    [ContextMenu("TEST: Blend to dusk (5s)")] private void TestDusk() => BlendTo("dusk", 5f);
    [ContextMenu("TEST: Apply day")] private void TestDay() => ApplyState("day");
#endif
}