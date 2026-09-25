using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// DEV AUTHORING TOOL (edit-mode, generic) — place the SUN or the MOON by azimuth/elevation and
/// bake the result into a <see cref="SkyStateData"/> asset. This is the generic version of the old
/// play-only SunTestRotator: it drives EITHER body, updates LIVE in edit mode, and saves to the SO.
///
/// WHY it exists: the moon is a skybox-disc DIRECTION (`_MoonDir`) and the sun is the real light —
/// editing `moonDir`/`sunDir` on the SO does nothing until a driver applies that state, so authoring
/// by eye was impossible. This tool applies the direction to the LIVE sky the instant you scrub a
/// slider (OnValidate), so you see the body move in Scene/Game view without entering play.
///
/// HOW to use:
///   1. Drop this on any GameObject. Pick Body = Sun or Moon.
///   2. For Sun: assign the directional Light (or leave empty → RenderSettings.sun).
///      For Moon: assign the skybox Material (or leave empty → RenderSettings.skybox); leave
///      "Force Moon Visible" on so the disc shows while you place it (it's off in day states).
///   3. Scrub Azimuth (compass 0–360) + Elevation (above horizon, −20…90). The body moves live.
///   4. Assign Target State (the SkyState_* asset) → right-click ▸ "Save position to SO".
///      "Load position from SO" pulls the saved value back into the sliders.
///
/// FOOTGUN (by design — you're authoring): applying writes the LIVE sun light transform (dirties
/// its scene) and the LIVE skybox material (dirties that asset). That's how you preview. Save to the
/// SO, then the SkyStateDriver reproduces it at runtime; the material/light are just the canvas.
/// Remove/disable this component before building.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("PoT/Dev/Sky Body Placer (sun & moon)")]
public class SkyBodyPlacer : MonoBehaviour
{
    public enum Body { Sun, Moon }

    [Header("What to place")]
    [SerializeField] private Body _body = Body.Moon;

    [Header("Position — rotation, live in edit mode")]
    [Tooltip("Compass angle around the world Y axis (0 = +Z / north).")]
    [Range(0f, 360f)][SerializeField] private float _azimuth = 200f;
    [Tooltip("Height above the horizon in degrees (0 = on the skyline, 90 = straight up).")]
    [Range(-20f, 90f)][SerializeField] private float _elevation = 14f;

    [Header("Targets")]
    [Tooltip("SUN mode: directional Light to rotate (moves the skybox disc AND shadows). Empty = RenderSettings.sun.")]
    [SerializeField] private Light _sunLight;
    [Tooltip("MOON mode: skybox material carrying _MoonDir / _MoonIntensity. Empty = RenderSettings.skybox.")]
    [SerializeField] private Material _skyboxMaterial;

    [Header("Moon preview")]
    [Tooltip("Force the moon visible while placing it (day/golden states ship it at intensity 0 = invisible).")]
    [SerializeField] private bool _forceMoonVisible = true;
    [Range(0f, 10f)][SerializeField] private float _previewMoonIntensity = 1.6f;

    [Header("Save target")]
    [Tooltip("The SkyState_* asset that 'Save position to SO' writes into.")]
    [SerializeField] private SkyStateData _targetState;

    private static readonly int MoonDirID = Shader.PropertyToID("_MoonDir");
    private static readonly int MoonIntensityID = Shader.PropertyToID("_MoonIntensity");

    // Direction TOWARD the body from azimuth/elevation.
    //   dir = (cos(el)·sin(az), sin(el), cos(el)·cos(az))  — elevation positive = up.
    private Vector3 SkyDir() => Quaternion.Euler(-_elevation, _azimuth, 0f) * Vector3.forward;

    // NOTE: intentionally NOT applied on OnEnable/domain-reload — that re-stamps the placer's values
    // over whatever the SkyStateDriver applied (they'd fight over the same material). It applies ONLY
    // when you actively scrub a slider, so the driver stays the owner until you touch this tool.
    private void OnValidate() => Apply();   // fires on every inspector scrub → live edit-mode preview

    private void Apply()
    {
        Vector3 dir = SkyDir();
        if (_body == Body.Sun)
        {
            var sun = _sunLight != null ? _sunLight : RenderSettings.sun;
            if (sun == null) return;
            // Light shines FROM the sun toward the scene, so its forward = -dir (disc sits opposite forward).
            sun.transform.rotation = Quaternion.LookRotation(-dir);
        }
        else
        {
            var m = _skyboxMaterial != null ? _skyboxMaterial : RenderSettings.skybox;
            if (m == null || !m.HasProperty(MoonDirID)) return;
            m.SetVector(MoonDirID, dir.normalized);
            if (_forceMoonVisible && m.HasProperty(MoonIntensityID) && m.GetFloat(MoonIntensityID) <= 0.001f)
                m.SetFloat(MoonIntensityID, _previewMoonIntensity);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Save position to SO")]
    private void SaveToSO()
    {
        if (_targetState == null) { Debug.LogWarning("[SkyBodyPlacer] No Target State assigned — nothing to save.", this); return; }
        Vector3 dir = SkyDir();
        Undo.RecordObject(_targetState, "Save sky body position");
        if (_body == Body.Sun)
        {
            _targetState.sunDir = dir;   // "sky direction toward the sun" — matches SkyStateDriver's convention
        }
        else
        {
            _targetState.moonDir = dir.normalized;
            if (_forceMoonVisible && _targetState.moonIntensity <= 0.001f)
            {
                _targetState.moonIntensity = _previewMoonIntensity;   // a placed moon at intensity 0 would still be invisible in that state
                Debug.Log($"[SkyBodyPlacer] '{_targetState.name}' had moonIntensity 0 → set to {_previewMoonIntensity} so the moon shows.", _targetState);
            }
        }
        EditorUtility.SetDirty(_targetState);
        Debug.Log($"[SkyBodyPlacer] Saved {_body} dir {dir.normalized} (az {_azimuth:0}°, el {_elevation:0}°) → '{_targetState.name}'. Save the asset (Ctrl+S) to keep it.", _targetState);
    }

    [ContextMenu("Load position from SO")]
    private void LoadFromSO()
    {
        if (_targetState == null) return;
        Vector3 dir = _body == Body.Sun ? _targetState.sunDir : _targetState.moonDir;
        if (dir.sqrMagnitude < 1e-5f) { Debug.LogWarning($"[SkyBodyPlacer] '{_targetState.name}' {_body} dir is zero — nothing to load.", this); return; }
        dir.Normalize();
        _elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        _azimuth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        if (_azimuth < 0f) _azimuth += 360f;
        Apply();
    }

    // Reads the LIVE body's current rotation back INTO the sliders (the inverse of Apply), so a
    // rotation set by hand on the transform or by another tool (e.g. Sun Test Rotator) can be
    // captured here, then written out with "Save position to SO".
    [ContextMenu("Capture current rotation → sliders")]
    private void CaptureFromLive()
    {
        Vector3 dir;
        if (_body == Body.Sun)
        {
            var sun = _sunLight != null ? _sunLight : RenderSettings.sun;
            if (sun == null) { Debug.LogWarning("[SkyBodyPlacer] No sun light to capture from.", this); return; }
            dir = -sun.transform.forward;   // inverse of Apply's LookRotation(-dir): sky dir TOWARD the sun
        }
        else
        {
            var m = _skyboxMaterial != null ? _skyboxMaterial : RenderSettings.skybox;
            if (m == null || !m.HasProperty(MoonDirID)) { Debug.LogWarning("[SkyBodyPlacer] No skybox _MoonDir to capture from.", this); return; }
            dir = m.GetVector(MoonDirID);
        }

        if (dir.sqrMagnitude < 1e-5f) { Debug.LogWarning("[SkyBodyPlacer] Captured direction is zero — nothing to sync.", this); return; }
        dir.Normalize();
        _elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        _azimuth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        if (_azimuth < 0f) _azimuth += 360f;
        Apply();   // re-stamp so the live body matches the now-synced sliders exactly
        Debug.Log($"[SkyBodyPlacer] Captured {_body} rotation → az {_azimuth:0}°, el {_elevation:0}°. Now right-click ▸ 'Save position to SO'.", this);
    }
#endif


#if UNITY_EDITOR
    [ContextMenu("Capture & Save to SO")]
    private void CaptureAndSave()
    {
        CaptureFromLive();
        SaveToSO();
    }
#endif
}
