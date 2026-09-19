using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Apply + persist for the VIDEO tab quality/display block (quality preset, V-Sync, FPS cap, texture,
/// shadow, anti-aliasing, ambient occlusion, volumetric fog, sun shafts, terrain, render scale, and
/// the restart-to-apply graphics API). Ported from the old GraphicsSettingsController: every value is
/// read from / written to the same "gfx_*" PlayerPrefs keys, so saved settings and their defaults are
/// honoured even before any UI exists. Holds no UI reference — the SettingsScreenController pushes
/// values in and reads them back to refresh controls (e.g. a preset change updating the other rows).
///
/// EDITOR SAFETY: mutating the shared URP asset / renderer features / fog material dirties those
/// ASSETS in the editor and would persist across sessions. <see cref="SnapshotAssets"/> records the
/// values and <see cref="RestoreAssets"/> puts them back; the controller calls these on Awake/OnDestroy
/// under UNITY_EDITOR only (a build wants the last-applied values to stick for the session).
/// </summary>
public sealed class GraphicsSettingsHandler : ISettingHandler
{
    private UniversalRenderPipelineAsset _urpAsset;
    private ScriptableRendererData _rendererData;
    private Material _fogMaterial;
    private Camera _mainCamera;

    private ScriptableRendererFeature _ssaoFeature;
    private ScriptableRendererFeature _fogFeature;
    private ScriptableRendererFeature _shaftsFeature;

    // Renderer-feature names (must match PC_Renderer.asset). NOTE: "PoTVolumetricFog" is not currently
    // present on PC_Renderer — the fog control no-ops until that name is reconciled (tracked separately).
    private const string SsaoFeatureName   = "ScreenSpaceAmbientOcclusion";
    private const string FogFeatureName    = "PoTVolumetricFog";
    private const string ShaftsFeatureName = "CoexistenceShafts";
    private static readonly int StepsID = Shader.PropertyToID("_Steps");

    private const string K_Preset      = "gfx_preset";
    private const string K_VSync       = "gfx_vsync";
    private const string K_FpsCap      = "gfx_fpscap";
    private const string K_Texture     = "gfx_texture";
    private const string K_Shadow      = "gfx_shadow";
    private const string K_AA          = "gfx_aa";
    private const string K_AO          = "gfx_ao";
    private const string K_Fog         = "gfx_fog";
    private const string K_Shafts      = "gfx_shafts";
    private const string K_Terrain     = "gfx_terrain";
    private const string K_RenderScale = "gfx_renderscale";

    private static readonly int[] FpsCapValues = { -1, 60, 120, 144 };

    public void Initialize(SettingsBackendConfig config)
    {
        _urpAsset     = config?.UrpAsset;
        _rendererData = config?.RendererData;
        _fogMaterial  = config?.FogMaterial;
        _mainCamera   = config?.MainCamera;

        _ssaoFeature   = FindFeature(SsaoFeatureName);
        _fogFeature    = FindFeature(FogFeatureName);
        _shaftsFeature = FindFeature(ShaftsFeatureName);
    }

    public bool Owns(string id) =>
        id == SettingsCatalog.QualityPreset || id == SettingsCatalog.VSync ||
        id == SettingsCatalog.FpsCap        || id == SettingsCatalog.Texture ||
        id == SettingsCatalog.Shadow        || id == SettingsCatalog.AntiAliasing ||
        id == SettingsCatalog.AmbientOcclusion || id == SettingsCatalog.Fog ||
        id == SettingsCatalog.SunShafts     || id == SettingsCatalog.Terrain ||
        id == SettingsCatalog.RenderScale   || id == SettingsCatalog.GraphicsApi;

    public IReadOnlyList<string> BuildDynamicOptions(string id) => null;

    public string GetReadout(string id)
    {
        if (id == SettingsCatalog.RenderScale)
            return PlayerPrefs.GetFloat(K_RenderScale, 1f).ToString("0.00");
        if (id == SettingsCatalog.GraphicsApi)
            return $"Current: {GraphicsApiPreference.CurrentApiLabel()}";
        return null;
    }

    // ── Reads (effective value from prefs) ────────────────────────────
    public int GetInt(string id)
    {
        if (id == SettingsCatalog.QualityPreset) return PlayerPrefs.GetInt(K_Preset, 3);
        if (id == SettingsCatalog.FpsCap)        return PlayerPrefs.GetInt(K_FpsCap, 0);
        if (id == SettingsCatalog.Texture)       return PlayerPrefs.GetInt(K_Texture, 0);
        if (id == SettingsCatalog.Shadow)        return PlayerPrefs.GetInt(K_Shadow, 2);
        if (id == SettingsCatalog.AntiAliasing)  return PlayerPrefs.GetInt(K_AA, 1);
        if (id == SettingsCatalog.Fog)           return PlayerPrefs.GetInt(K_Fog, 2);
        if (id == SettingsCatalog.Terrain)       return PlayerPrefs.GetInt(K_Terrain, 2);
        if (id == SettingsCatalog.GraphicsApi)   return (int)GraphicsApiPreference.Saved;
        return 0;
    }

    public bool GetBool(string id)
    {
        if (id == SettingsCatalog.VSync)           return PlayerPrefs.GetInt(K_VSync, 1) == 1;
        if (id == SettingsCatalog.AmbientOcclusion) return PlayerPrefs.GetInt(K_AO, 1) == 1;
        if (id == SettingsCatalog.SunShafts)        return PlayerPrefs.GetInt(K_Shafts, 1) == 1;
        return false;
    }

    public float GetFloat(string id) =>
        id == SettingsCatalog.RenderScale ? PlayerPrefs.GetFloat(K_RenderScale, 1f) : 0f;

    // ── Writes (apply + persist; any per-control change flips the preset to Custom) ──
    public void SetInt(string id, int index)
    {
        if (id == SettingsCatalog.QualityPreset) { ApplyPreset(index); return; }
        if (id == SettingsCatalog.GraphicsApi)
        {
            // Deferred: the API is fixed at launch; persist the choice — the relaunch applies it.
            // Deliberately NOT MarkCustom (the API is orthogonal to Low/Medium/High).
            PlayerPrefs.SetInt(GraphicsApiPreference.PrefKey, Mathf.Clamp(index, 0, 2));
            PlayerPrefs.Save();
            return;
        }

        if (id == SettingsCatalog.FpsCap)       { PlayerPrefs.SetInt(K_FpsCap, index);  ApplyFpsCap(); }
        else if (id == SettingsCatalog.Texture) { PlayerPrefs.SetInt(K_Texture, index); ApplyTexture(); }
        else if (id == SettingsCatalog.Shadow)  { PlayerPrefs.SetInt(K_Shadow, index);  ApplyShadow(); }
        else if (id == SettingsCatalog.AntiAliasing) { PlayerPrefs.SetInt(K_AA, index); ApplyAntiAliasing(); }
        else if (id == SettingsCatalog.Fog)     { PlayerPrefs.SetInt(K_Fog, index);     ApplyVolumetricFog(); }
        else if (id == SettingsCatalog.Terrain) { PlayerPrefs.SetInt(K_Terrain, index); ApplyTerrain(); }
        else return;

        MarkCustom();
        PlayerPrefs.Save();
    }

    public void SetBool(string id, bool on)
    {
        if (id == SettingsCatalog.VSync)                { PlayerPrefs.SetInt(K_VSync, on ? 1 : 0);  ApplyVSync(); }
        else if (id == SettingsCatalog.AmbientOcclusion) { PlayerPrefs.SetInt(K_AO, on ? 1 : 0);    ApplyAmbientOcclusion(); }
        else if (id == SettingsCatalog.SunShafts)        { PlayerPrefs.SetInt(K_Shafts, on ? 1 : 0); ApplySunShafts(); }
        else return;

        MarkCustom();
        PlayerPrefs.Save();
    }

    public void SetFloat(string id, float value)
    {
        if (id != SettingsCatalog.RenderScale) return;
        PlayerPrefs.SetFloat(K_RenderScale, Mathf.Clamp(value, 0.7f, 1f));
        ApplyRenderScale();
        MarkCustom();
        PlayerPrefs.Save();
    }

    public void Invoke(string id) { }

    private static void MarkCustom() => PlayerPrefs.SetInt(K_Preset, 3);

    // ── Boot / preset apply-all ───────────────────────────────────────
    /// <summary>Apply every graphics value from prefs. Called at boot so saved settings take effect
    /// before the screen is opened (mirrors the old controller's Start).</summary>
    public void ApplyAll()
    {
        ApplyVSync(); ApplyFpsCap(); ApplyTexture(); ApplyShadow(); ApplyAntiAliasing();
        ApplyAmbientOcclusion(); ApplyVolumetricFog(); ApplySunShafts(); ApplyTerrain(); ApplyRenderScale();
    }

    private void ApplyPreset(int index)
    {
        if (index >= 3) { PlayerPrefs.SetInt(K_Preset, 3); PlayerPrefs.Save(); return; } // Custom: leave rows

        bool vsync; int fps, tex, shadow, aa, fog, terrain; bool ao, shafts; float rs;
        switch (index)
        {
            case 0:  vsync = false; fps = 1; tex = 2; shadow = 0; aa = 0; ao = false; fog = 0; shafts = false; terrain = 0; rs = 0.70f; break;
            case 1:  vsync = true;  fps = 0; tex = 1; shadow = 1; aa = 1; ao = true;  fog = 1; shafts = true;  terrain = 1; rs = 0.85f; break;
            default: vsync = true;  fps = 0; tex = 0; shadow = 2; aa = 1; ao = true;  fog = 2; shafts = true;  terrain = 2; rs = 1.00f; break;
        }

        PlayerPrefs.SetInt(K_VSync, vsync ? 1 : 0);
        PlayerPrefs.SetInt(K_FpsCap, fps);
        PlayerPrefs.SetInt(K_Texture, tex);
        PlayerPrefs.SetInt(K_Shadow, shadow);
        PlayerPrefs.SetInt(K_AA, aa);
        PlayerPrefs.SetInt(K_AO, ao ? 1 : 0);
        PlayerPrefs.SetInt(K_Fog, fog);
        PlayerPrefs.SetInt(K_Shafts, shafts ? 1 : 0);
        PlayerPrefs.SetInt(K_Terrain, terrain);
        PlayerPrefs.SetFloat(K_RenderScale, rs);
        PlayerPrefs.SetInt(K_Preset, index);
        PlayerPrefs.Save();

        ApplyAll();
    }

    // ── Feature lookup ────────────────────────────────────────────────
    private ScriptableRendererFeature FindFeature(string featureName)
    {
        if (_rendererData == null || _rendererData.rendererFeatures == null) return null;
        foreach (var f in _rendererData.rendererFeatures)
            if (f != null && f.name == featureName) return f;
        Debug.LogWarning($"[GraphicsSettingsHandler] Renderer feature '{featureName}' not found on the renderer data.", _rendererData);
        return null;
    }

    // ── Individual apply methods (all read from prefs) ────────────────
    private void ApplyVSync() => QualitySettings.vSyncCount = PlayerPrefs.GetInt(K_VSync, 1) == 1 ? 1 : 0;

    private void ApplyFpsCap()
    {
        int idx = PlayerPrefs.GetInt(K_FpsCap, 0);
        Application.targetFrameRate = FpsCapValues[Mathf.Clamp(idx, 0, FpsCapValues.Length - 1)];
    }

    private void ApplyTexture() =>
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(PlayerPrefs.GetInt(K_Texture, 0), 0, 2);

    private void ApplyShadow()
    {
        if (_urpAsset == null) return;
        switch (PlayerPrefs.GetInt(K_Shadow, 2))
        {
            case 0:  _urpAsset.shadowDistance = 20f; SetSoftShadows(false, 1); break;
            case 1:  _urpAsset.shadowDistance = 35f; SetSoftShadows(true, 2); break;
            default: _urpAsset.shadowDistance = 50f; SetSoftShadows(true, 3); break;
        }
    }

    // supportsSoftShadows / softShadowQuality have no public setters; set the serialized fields by
    // reflection (best-effort — a URP rename leaves shadowDistance still applied).
    private void SetSoftShadows(bool enabled, int tier)
    {
        SetPrivateField(_urpAsset, "m_SoftShadowsSupported", enabled);
        SetPrivateField(_urpAsset, "m_SoftShadowQuality", tier);
    }

    private void ApplyAntiAliasing()
    {
        var cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;
        data.antialiasing = PlayerPrefs.GetInt(K_AA, 1) == 1
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : AntialiasingMode.None;
    }

    private void ApplyAmbientOcclusion() => _ssaoFeature?.SetActive(PlayerPrefs.GetInt(K_AO, 1) == 1);

    private void ApplyVolumetricFog()
    {
        int idx = PlayerPrefs.GetInt(K_Fog, 2);   // Off / Low / High
        if (_fogFeature != null) _fogFeature.SetActive(idx > 0);
        if (_fogMaterial != null && idx > 0) _fogMaterial.SetFloat(StepsID, idx == 1 ? 12f : 24f);
    }

    private void ApplySunShafts() => _shaftsFeature?.SetActive(PlayerPrefs.GetInt(K_Shafts, 1) == 1);

    private void ApplyTerrain() =>
        TerrainQualityService.Instance?.SetTier(Mathf.Clamp(PlayerPrefs.GetInt(K_Terrain, 2), 0, 2));

    private void ApplyRenderScale()
    {
        if (_urpAsset == null) return;
        _urpAsset.renderScale = Mathf.Clamp(PlayerPrefs.GetFloat(K_RenderScale, 1f), 0.7f, 1f);
    }

    // ── Reflection helpers ────────────────────────────────────────────
    private static void SetPrivateField(object target, string field, object value)
    {
        if (target == null) return;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return;
        try { f.SetValue(target, value); } catch { /* type shape changed — non-fatal */ }
    }

#if UNITY_EDITOR
    private static int GetPrivateInt(object target, string field, int fallback)
    {
        if (target == null) return fallback;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (int)f.GetValue(target); } catch { return fallback; }
    }

    private static bool GetPrivateBool(object target, string field, bool fallback)
    {
        if (target == null) return fallback;
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (bool)f.GetValue(target); } catch { return fallback; }
    }

    private struct AssetSnapshot
    {
        public float renderScale, shadowDistance, fogSteps;
        public bool softShadows, ssao, fog, shafts;
        public int softShadowQuality;   // -1 = field not found
        public int vSyncCount, mipmapLimit, targetFps;
        public bool valid;
    }
    private AssetSnapshot _snap;
#endif

    // Exposed unconditionally so the controller can call them without #if fences; body is editor-only.
    public void SnapshotAssets()
    {
#if UNITY_EDITOR
        _snap = new AssetSnapshot { valid = true };
        if (_urpAsset != null)
        {
            _snap.renderScale = _urpAsset.renderScale;
            _snap.shadowDistance = _urpAsset.shadowDistance;
            _snap.softShadows = GetPrivateBool(_urpAsset, "m_SoftShadowsSupported", _urpAsset.supportsSoftShadows);
            _snap.softShadowQuality = GetPrivateInt(_urpAsset, "m_SoftShadowQuality", -1);
        }
        _snap.ssao   = _ssaoFeature != null && _ssaoFeature.isActive;
        _snap.fog    = _fogFeature != null && _fogFeature.isActive;
        _snap.shafts = _shaftsFeature != null && _shaftsFeature.isActive;
        if (_fogMaterial != null) _snap.fogSteps = _fogMaterial.GetFloat(StepsID);
        _snap.vSyncCount = QualitySettings.vSyncCount;
        _snap.mipmapLimit = QualitySettings.globalTextureMipmapLimit;
        _snap.targetFps = Application.targetFrameRate;
#endif
    }

    public void RestoreAssets()
    {
#if UNITY_EDITOR
        if (!_snap.valid) return;
        if (_urpAsset != null)
        {
            _urpAsset.renderScale = _snap.renderScale;
            _urpAsset.shadowDistance = _snap.shadowDistance;
            SetPrivateField(_urpAsset, "m_SoftShadowsSupported", _snap.softShadows);
            if (_snap.softShadowQuality >= 0)
                SetPrivateField(_urpAsset, "m_SoftShadowQuality", _snap.softShadowQuality);
        }
        _ssaoFeature?.SetActive(_snap.ssao);
        _fogFeature?.SetActive(_snap.fog);
        _shaftsFeature?.SetActive(_snap.shafts);
        if (_fogMaterial != null) _fogMaterial.SetFloat(StepsID, _snap.fogSteps);
        QualitySettings.vSyncCount = _snap.vSyncCount;
        QualitySettings.globalTextureMipmapLimit = _snap.mipmapLimit;
        Application.targetFrameRate = _snap.targetFps;
#endif
    }
}
