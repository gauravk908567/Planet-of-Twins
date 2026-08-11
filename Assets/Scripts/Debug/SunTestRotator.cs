using UnityEngine;

/// <summary>
/// DEV TEST TOOL (not for shipping) — rotate the sun (main directional light) with
/// azimuth/elevation sliders during Play mode to verify the SunShaftsDriver visibility
/// theory: the screen-space shafts should appear when the sun's projected position is
/// inside the camera frustum and fade as it leaves the frame.
///
/// Play-mode only by design — it writes the light's transform.rotation, and doing that in
/// edit mode would dirty the saved Persistent scene. Play-mode transform edits are discarded
/// on stop, so nothing is ever saved. Rotating this one light updates EVERYTHING that reads
/// it: URP's _MainLightPosition (skybox sun disc), scene lighting, and the SunShaftsDriver's
/// screen projection — so scrubbing the sliders is a live, all-in-one test.
///
/// Attach to any object (or the Directional Light itself). Leave _sun empty to drive
/// RenderSettings.sun. Remove/disable the component before building.
/// </summary>
[AddComponentMenu("PoT/Dev/Sun Test Rotator")]
public class SunTestRotator : MonoBehaviour
{
    [Tooltip("Explicit light to rotate. Empty = RenderSettings.sun (the scene's sun).")]
    [SerializeField] private Light _sun;

    [Tooltip("Master switch — off leaves the light exactly where it is.")]
    [SerializeField] private bool _drive = true;

    [Tooltip("Compass rotation around Y. Scrub until the sun disc enters the Game view.")]
    [SerializeField, Range(0f, 360f)] private float _azimuth = 30f;

    [Tooltip("Height above the horizon in degrees (low = near-horizon 'golden hour', high = overhead).")]
    [SerializeField, Range(-15f, 90f)] private float _elevation = 25f;

    // Explicit _sun first, then RenderSettings.sun, then a Light on this same object — so
    // dropping it straight onto the Directional Light needs no wiring (RenderSettings.sun is
    // unset in this project, so that fallback alone wouldn't find anything).
    private Light Resolve()
    {
        if (_sun != null) return _sun;
        if (RenderSettings.sun != null) return RenderSettings.sun;
        return GetComponent<Light>();
    }

    private void Update()
    {
        // Play-mode only: never write the light transform in edit mode (would dirty Persistent).
        if (!_drive || !Application.isPlaying) return;
        var sun = Resolve();
        if (sun == null) return;
        sun.transform.rotation = Quaternion.Euler(_elevation, _azimuth, 0f);
    }

#if UNITY_EDITOR
    // Quick presets — the sun disc appears OPPOSITE the light's forward, so these aim it to
    // put the disc roughly in front of a forward-facing camera. Fine-tune with the sliders.
    [ContextMenu("Sun: front, high noon")] private void FrontHigh() { _azimuth = 0f;   _elevation = 55f; }
    [ContextMenu("Sun: front, low (golden)")] private void FrontLow() { _azimuth = 0f;  _elevation = 6f;  }
    [ContextMenu("Sun: side")]              private void Side()      { _azimuth = 90f;  _elevation = 20f; }
    [ContextMenu("Sun: behind camera")]     private void Behind()    { _azimuth = 180f; _elevation = 30f; }
#endif
}
