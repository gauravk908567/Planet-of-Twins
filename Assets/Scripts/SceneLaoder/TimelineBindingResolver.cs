using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

/// <summary>
/// Rebinds cross-scene Timeline tracks at runtime, before the first Play()
/// (instruction.md §16.1, R11 / BUG-032).
///
/// WHY: a track binding is a scene-local fileID and cannot reach a Persistent object (R2).
/// We defer the link to runtime, where the area scene can look the target up (R4).
///
/// HOW (no name strings):
///   • The lone CinemachineTrack auto-binds to the one CinemachineBrain **by type** — no row.
///   • Every other cross-scene track gets a row: drag the track (dropdown) + pick a
///     <see cref="BindingRole"/>. The role is resolved through the Persistent-resident
///     <see cref="TimelineTargetRegistry"/>, whose same-scene refs disambiguate the targets that
///     share a type (two nameplates, two transpose cameras, the HUD canvas).
///
/// Target type follows the track kind: Animation→Animator, Cinemachine→Brain, Activation/Signal→GameObject.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TimelineBindingResolver : MonoBehaviour
{
    /// <summary>Persistent targets, resolved through <see cref="TimelineTargetRegistry"/>.</summary>
    public enum BindingRole
    {
        CameraManager,    // Signal Track → CameraManager's SignalReceiver GO
        FadeCanvas,       // Animation 7 → its Animator; Activation 8 → the GO
        HudCanvas,        // Activation 22 → the GO
        TransposeClose,   // Activation 1 → TutorialGroupTransposeClose camera GO (toggled off by timeline)
        TransposeTop,     // Activation 2 → TutorialGroupTransposeTop camera GO (toggled off by timeline)
        SkyboxChanger,    // Activation 9 → SkyboxChanger GO (now Persistent — skybox persists across levels)
    }

    [Serializable]
    public struct TrackRoleBinding
    {
        [Tooltip("Drag the cross-scene track from this director's Timeline (use the dropdown).")]
        public TrackAsset track;     // direct asset reference — survives renames, no string
        [Tooltip("Which Persistent target this track drives (resolved via TimelineTargetRegistry).")]
        public BindingRole role;
    }

    [Tooltip("One row per cross-scene track. The lone CinemachineTrack is auto-bound by type " +
             "and needs no row. See instruction.md §16.")]
    [SerializeField] private TrackRoleBinding[] _trackBindings = Array.Empty<TrackRoleBinding>();

    private PlayableDirector _director;

    private void Awake() => _director = GetComponent<PlayableDirector>();

    // R4: Persistent singletons/registry exist by Start. Director is m_InitialState:0 (no
    // play-on-awake), so binding here lands before the tutorial flow calls Play().
    private void Start() => ApplyBindings();

    /// <summary>Resolve and apply every cross-scene binding. Public for re-apply after soft reset.</summary>
    public void ApplyBindings()
    {
        if (_director == null) _director = GetComponent<PlayableDirector>();
        if (_director == null || _director.playableAsset == null)
        {
            Debug.LogError("[TimelineBindingResolver] No PlayableDirector/asset to bind.", this);
            return;
        }

        var brain = FindAnyObjectByType<CinemachineBrain>();   // by TYPE — the only Brain (Persistent)

        // 1. Auto: the lone CinemachineTrack → Brain, zero authoring.
        foreach (var output in _director.playableAsset.outputs)
        {
            if (output.sourceObject is CinemachineTrack ct)
            {
                if (brain != null) _director.SetGenericBinding(ct, brain);
                else Debug.LogError("[TimelineBindingResolver] CinemachineTrack present but no " +
                                    "CinemachineBrain found — is Persistent loaded?", this);
            }
        }

        if (_trackBindings.Length == 0) return;

        var reg = TimelineTargetRegistry.Instance != null
            ? TimelineTargetRegistry.Instance
            : FindAnyObjectByType<TimelineTargetRegistry>();
        if (reg == null)
        {
            Debug.LogError("[TimelineBindingResolver] No TimelineTargetRegistry in Persistent — " +
                           "cross-scene rows cannot resolve. Add one and wire its fields.", this);
            return;
        }

        // 2. Explicit role map → resolve each role through the Persistent registry.
        foreach (var b in _trackBindings)
        {
            if (b.track == null) { Debug.LogError("[TimelineBindingResolver] empty track row.", this); continue; }

            var go = ResolveRoleObject(b.role, reg);
            if (go == null)
            {
                Debug.LogError($"[TimelineBindingResolver] role {b.role} (track '{b.track.name}') " +
                               "did not resolve — is its registry field / TwinSelector wired?", this);
                continue;
            }

            UnityEngine.Object target;
            if (b.track is AnimationTrack)
            {
                target = go.GetComponentInChildren<Animator>();
                if (target == null)
                {
                    Debug.LogError($"[TimelineBindingResolver] no Animator on {b.role} for animation " +
                                   $"track '{b.track.name}'.", this);
                    continue;
                }
            }
            else
            {
                target = go;   // Activation / Signal / other → the GameObject
            }

            _director.SetGenericBinding(b.track, target);
        }
    }

    /// <summary>Resolve a role to its Persistent GameObject via the registry.</summary>
    private GameObject ResolveRoleObject(BindingRole role, TimelineTargetRegistry reg)
    {
        switch (role)
        {
            case BindingRole.CameraManager:  return reg.cameraManager;
            case BindingRole.FadeCanvas:     return reg.fadeCanvas;
            case BindingRole.HudCanvas:      return reg.hudCanvas;
            case BindingRole.TransposeClose: return reg.transposeClose;
            case BindingRole.TransposeTop:   return reg.transposeTop;
            case BindingRole.SkyboxChanger:  return reg.skyboxChanger;
            default: return null;
        }
    }
}
