using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Lives in **Persistent**. Holds same-scene (R1) references to the Persistent objects an
/// area-scene Timeline needs to drive across scenes. The area-scene <see cref="TimelineBindingResolver"/>
/// finds this by type (R4) and pulls each target by role — which disambiguates targets that share a
/// type (e.g. the two transpose cameras) without any GameObject-name strings. (R11 / BUG-032.)
///
/// SETUP: put this on one GO in Persistent.unity (e.g. a `TimelineTargets` empty, or an existing
/// manager) and drag each field from the Persistent hierarchy. All refs are same-scene → legal (R1).
/// Leave a field null if that timeline doesn't use it.
///
/// NOTE: the twins are NOT here — the intro already locks/unlocks twin movement in code
/// (GameBootstrapper + IntroController at the start, IntroTimelinePositioner at the end), so the
/// timeline must never toggle the Persistent twin GOs (R11/BUG-W15).
/// </summary>
public class TimelineTargetRegistry : MonoBehaviour
{
    public static TimelineTargetRegistry Instance { get; private set; }

    [Header("Camera")]
    [Tooltip("CinemachineBrain on Main Camera (the lone Brain is also auto-found by type — optional).")]
    public CinemachineBrain brain;
    [Tooltip("CameraManager GO — carries the cinematic SignalReceiver the Signal Track drives.")]
    public GameObject cameraManager;
    [Tooltip("TutorialGroupTransposeClose camera (toggled off by the timeline to avoid conflict).")]
    public GameObject transposeClose;
    [Tooltip("TutorialGroupTransposeTop camera (toggled off by the timeline to avoid conflict).")]
    public GameObject transposeTop;

    [Header("UI / Fade")]
    [Tooltip("FadeCanvas GO (FadeController). Animation Track 7 drives its Animator; Activation 8 toggles it.")]
    public GameObject fadeCanvas;
    [Tooltip("HUD_Canvas GO (Activation 22 toggles it).")]
    public GameObject hudCanvas;

    [Header("World")]
    [Tooltip("SkyboxChanger GO (Activation 9). Moved to Persistent — skybox persists across all levels.")]
    public GameObject skyboxChanger;

    private void Awake()
    {
        // R3-safe singleton: no DDOL (we live in Persistent), null Instance in OnDestroy.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
