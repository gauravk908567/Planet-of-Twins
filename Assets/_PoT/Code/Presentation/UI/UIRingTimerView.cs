using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one PoT/UIRingTimer Image — the shared timer/cooldown widget (game.md §17.5:
/// "one ring language" for ability cooldowns, the QTE timer, and any radial rundown).
///
/// Owns two things the shader cannot do alone:
///   1. Material instancing — Graphic.material does NOT auto-instance; assigning through it
///      writes to the shared .mat asset (round-1 footgun). The clone here is explicit.
///   2. The ready flash — a one-shot 0→1 sweep of _FlashT on recharge complete (Overwatch
///      ability-ready). The shader only renders the band; this animates it.
///
/// API: SetProgress(0..1) each frame from the consumer (cooldown fraction, QTE time left);
/// PlayReadyFlash() once when the thing becomes available.
/// Timers: flash runs on UNSCALED time — UI feedback must not freeze under Setsuna/pause (R10).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class UIRingTimerView : MonoBehaviour
{
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int FlashTID = Shader.PropertyToID("_FlashT");
    private static readonly int FillColorAID = Shader.PropertyToID("_FillColorA");

    [Tooltip("Seconds the ready flash takes to sweep top to bottom. Unscaled time.")]
    [SerializeField] private float _flashDuration = 0.35f;

    [Header("Editor preview only")]
    [Tooltip("Editor-only: animates progress in a loop so the ring can be judged without play mode.")]
    [SerializeField] private bool _editorDemo;
    [SerializeField, Range(0.05f, 2f)] private float _editorDemoSpeed = 0.35f;

    private Image _image;
    private Material _runtimeMat;   // explicit clone — never the shared asset
    private float _flashStart = -1f; // unscaled start time of the running flash; <0 = idle

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (!Application.isPlaying) return; // edit mode renders the shared mat untouched
        if (_image.material == null)
        {
            Debug.LogError($"[UIRingTimerView] {name} has no ring material assigned.", this);
            enabled = false;
            return;
        }
        _runtimeMat = new Material(_image.material);
        _image.material = _runtimeMat;
    }

    private void OnDestroy()
    {
        if (_runtimeMat != null) Destroy(_runtimeMat);
    }

    /// <summary>Cooldown/timer fraction, 0 = empty, 1 = full. Call every frame while running.</summary>
    public void SetProgress(float normalised)
    {
        var m = TargetMat();
        if (m != null) m.SetFloat(ProgressID, Mathf.Clamp01(normalised));
    }

    /// <summary>One-shot ready flash (top→bottom sweep). Call when the ability becomes available.</summary>
    public void PlayReadyFlash()
    {
        _flashStart = Time.unscaledTime; // unscaled — must play through pause/Setsuna
    }

    /// <summary>Override this instance's fill colour (`_FillColorA`). At RUNTIME this writes the cloned
    /// material (never the shared .mat), so one ring material serves differently-coloured rings (rescue
    /// TTK red / mash green / struggle gold) driven from code. Runtime-only: in edit mode there is no
    /// clone yet, so this would write the shared asset — the consumers only call it in play.
    /// NOTE: the ring's `Image.color` (vertex tint) multiplies this in the shader, so leaving a stray
    /// non-white Image.color silently corrupts the result — keep the ring Image.color white.</summary>
    public void SetFillColor(Color c)
    {
        var m = TargetMat();
        if (m != null) m.SetColor(FillColorAID, c);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (_editorDemo && _image != null && _image.material != null)
            {
                float t = (float)UnityEditor.EditorApplication.timeSinceStartup * _editorDemoSpeed;
                _image.material.SetFloat(ProgressID, Mathf.PingPong(t, 1f));
                UnityEditor.SceneView.RepaintAll();
            }
            return;
        }
#endif
        if (_flashStart < 0f || _runtimeMat == null) return;
        float ft = (Time.unscaledTime - _flashStart) / Mathf.Max(_flashDuration, 0.01f);
        if (ft >= 1f) { _runtimeMat.SetFloat(FlashTID, 0f); _flashStart = -1f; return; }
        _runtimeMat.SetFloat(FlashTID, ft);
    }

    private Material TargetMat()
    {
        if (Application.isPlaying) return _runtimeMat;
        return _image != null ? _image.material : null;
    }
}
