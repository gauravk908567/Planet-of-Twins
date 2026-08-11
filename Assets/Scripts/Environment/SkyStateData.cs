using UnityEngine;

/// <summary>
/// SkyStateData — one authored time-of-day/story state for the PoT/CoexistenceSkybox
/// material (R7: config only; the driver owns all runtime application).
/// States this project ships: day · golden_hour (Accord festival) · dusk (post-cinematic
/// wake-up, moon on). The story arc canon lives in game.md §1.1.
/// </summary>
[CreateAssetMenu(menuName = "PoT/Sky State", fileName = "SkyState_")]
public class SkyStateData : ScriptableObject
{
    [Tooltip("Id used by SkyStateDriver.ApplyState/BlendTo.")]
    public string id = "day";

    [Header("Sky Gradient")]
    public Color skyTop = new Color(0.35f, 0.52f, 0.85f);
    public Color skyHorizon = new Color(0.95f, 0.85f, 0.72f);
    public Color skyBottom = new Color(0.55f, 0.50f, 0.48f);

    [Header("Clouds")]
    public Color cloudColor = Color.white;
    public Color cloudShadow = new Color(0.72f, 0.74f, 0.80f);

    [Header("Sun DISC (skybox _SunColor/_SunIntensity — the disc in the sky, NOT scene light)")]
    public Color sunColor = new Color(1f, 0.96f, 0.88f);
    [Tooltip("Brightness of the sun DISC in the skybox. LOWER this for dusk or the disc blows out (that " +
             "white blob). Does NOT dim the world — use Scene light below for that.")]
    [Range(0f, 10f)] public float sunIntensity = 3f;
    [Tooltip("Sky direction the sun sits — rotates the real sun LIGHT so the disc AND shadows move " +
             "together. (0,0,0) = leave the sun light untouched. Low/below-horizon = dusk.")]
    public Vector3 sunDir = new Vector3(0.3f, 0.5f, 0.8f);

    [Header("Scene light (the REAL directional light — dims the WORLD, not just the disc)")]
    [Tooltip("If true, the directional light follows the moon's position (moonDir) instead of the sun's (sunDir). Use for dusk/night states.")]
    public bool lightFollowsMoon = false;
    [Tooltip("If ≥ 0, sets the sun light's intensity for this state — THIS is what actually darkens the " +
             "scene. < 0 = leave the light's intensity untouched (default; backwards-compatible).")]
    public float lightIntensity = -1f;
    [Tooltip("If alpha > 0, tints the sun light's colour for this state (warm amber at dusk, etc.). " +
             "Alpha 0 = leave the light's colour untouched (default).")]
    public Color lightColor = new Color(1f, 1f, 1f, 0f);

    [Header("Volumetric fog (main-light TINT — alpha 0 = leave fog untouched)")]
    [Tooltip("If alpha > 0, drives the ACTIVE volumetric fog's main-light tint for this state (cool blue at " +
             "dusk). Fog density/scattering are NOT touched, so the environment stays as visible as before — " +
             "only the fog's hue shifts. Keep the colour bright (high value) so it doesn't darken. Alpha 0 = " +
             "leave the fog tint alone (default; backwards-compatible). Needs SkyStateDriver._fogProfile assigned.")]
    public Color fogTint = new Color(1f, 1f, 1f, 0f);

    [Header("Moon (0 intensity = off)")]
    [Range(0f, 10f)] public float moonIntensity = 0f;
    [Range(0.001f, 0.6f)] public float moonSize = 0.14f;
    public Vector3 moonDir = new Vector3(-1f, 0.2f, 0.05f);
    [Range(0f, 3f)] public float moonHalo = 1.4f;
}