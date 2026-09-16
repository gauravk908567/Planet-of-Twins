using UnityEngine;

/// <summary>
/// Couch M2 — the boot load cover. A full-screen opaque overlay shown across the risky boot transitions
/// (FrontEnd → Persistent/Intro, and Continue area-streaming) so no camera-handoff or over-the-void frame is
/// ever visible, regardless of how the async loads interleave.
///
/// <para>Lives in <b>Bootstrap.unity</b> — the one scene alive for the entire boot — so it survives while the
/// FrontEnd scene unloads and Persistent loads underneath. <see cref="GameBootstrapper"/> (same scene, R1) holds
/// a serialized reference and drives <see cref="Show"/>/<see cref="Hide"/> around each load. Not a singleton and
/// not persistent: it is intentionally scoped to the boot and goes away when Bootstrap unloads at the end.</para>
///
/// <para>Its Canvas must be Screen-Space Overlay with a very high sort order so it covers every other canvas, and
/// it needs no camera (overlay), no EventSystem (non-interactive), and no AudioListener (a brief listener gap
/// during the black cover is harmless).</para>
/// </summary>
[DisallowMultipleComponent]
public class LoadScreenController : MonoBehaviour
{
    [Tooltip("The opaque overlay root toggled on/off. Usually this GameObject's Canvas child.")]
    [SerializeField] private GameObject panel;

    /// <summary>Cover the screen (call BEFORE starting a risky load).</summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>Reveal the loaded content (call once the target scene is up and safe to show).</summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
