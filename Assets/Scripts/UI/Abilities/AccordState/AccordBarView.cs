using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Accord State (X) power bar — the border-as-timer redesign (Track D).
///
/// Rides the ONE generic clan bar (<c>UIBarView</c> + <c>PoT/UIBar</c>), styled with the ornamental
/// UI_AccordBar art: the single bar value (accord points) is shown as TWO clan halves converging on the
/// centre node — Luminari gold from the left end, Vethara violet from the right end, meeting when full
/// (the "clans unite" climax). A baked-SDF corona layer (<c>PoT/UISDFGlow</c>) haloes the bar outward when
/// it's ready/charging/active — the same "solar corona" the ability keycaps use.
///
/// STATES (read from <see cref="AccordStateSystem"/>, no per-frame allocation):
///   Filling  — both halves fill 0→1 by BarProgress; shine sweeps; no corona.
///   Full     — bar at 1; a gentle "ready" corona invites activation.
///   Charging — holding X: the corona blooms with ChargeProgress.
///   Active   — bar DRAINS 1→0 over the accord window; full corona.
///
/// UNITY SETUP (children of the top-centre AccordBarPanel, back→front):
///   AccordBarCorona  Image (PoT/UISDFGlow, UI_AccordBar_SDF sprite)      → <see cref="coronaImage"/>
///   AccordBarFill    Image (PoT/UIBar, UI_AccordBar_Fill sprite) + UIBarView → <see cref="barView"/>
///   AccordBarFrame   Image (PoT/UIBar line-mode, UI_AccordBar sprite)
/// The UIBarView must have Split Halves on with gold/violet fill colours; the fill material carries the
/// symmetric-fill dials (_FillDirection=LeftToRight, _SplitMirror=1).
/// </summary>
public class AccordBarView : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private AccordStateSystem accordSystem;
    [SerializeField] private MonoBehaviour unlockStateMono;

    [Header("Root — hidden until unlocked")]
    [SerializeField] private GameObject panel;

    [Header("Bar — the generic clan bar (fill + frame)")]
    [Tooltip("UIBarView on the fill Image (PoT/UIBar). Both halves are driven with the one accord value.")]
    [SerializeField] private UIBarView barView;

    [Header("Corona — baked-SDF outward halo (PoT/UISDFGlow)")]
    [Tooltip("The corona Image behind/over the bar. Its material _Glow is driven 0..1 by this view.")]
    [SerializeField] private Image coronaImage;
    [Tooltip("Corona level while the bar is FULL and ready to activate (a gentle invite).")]
    [SerializeField, Range(0f, 1f)] private float coronaReady = 0.45f;
    [Tooltip("How fast the corona glow chases its target (per second).")]
    [SerializeField, Range(1f, 12f)] private float coronaLerp = 6f;

    [Header("Shine — clan sweep while READY to trigger (bar full)")]
    [Tooltip("Seconds between shine sweeps while the bar is FULL and ready to activate (not while filling).")]
    [SerializeField, Range(0.5f, 6f)] private float shineInterval = 2.2f;

    [Header("Flash — pulse on point-gain and while Accord is active")]
    [Tooltip("How fast a point-gain flash fades out.")]
    [SerializeField, Range(0.5f, 8f)] private float flashDecay = 3f;
    [Tooltip("Pulse speed of the sustained flash while Accord is active.")]
    [SerializeField, Range(1f, 12f)] private float activeFlashSpeed = 5f;
    [Tooltip("Peak of the sustained active flash (0 = none).")]
    [SerializeField, Range(0f, 1f)] private float activeFlashAmount = 0.55f;

    [Header("Hold-X prompt — optional (pulses when full)")]
    [SerializeField] private TMP_Text holdXText;
    [SerializeField, Range(0.5f, 8f)] private float promptPulseSpeed = 3f;

    // ── Runtime ───────────────────────────────────────────────
    private ISkillUnlockState _unlockState;
    private bool _isUnlocked;
    private Material _coronaMat;          // instance clone — we drive _Glow on it, never the shared asset
    private float _coronaGlow;            // smoothed current glow
    private float _shineTimer;
    private float _flash;                 // decaying point-gain flash
    private float _lastBar = -1f;         // previous BarProgress, for gain detection
    private static readonly int ID_Glow = Shader.PropertyToID("_Glow");

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        // Clone the corona material so per-frame _Glow writes don't dirty the shared asset (UI Graphic.material
        // hands back the shared asset, not an instance — same trap UIBarView/BorderFillDriver guard against).
        if (coronaImage != null && coronaImage.material != null)
        {
            _coronaMat = new Material(coronaImage.material) { name = coronaImage.material.name + " (AccordBar instance)" };
            coronaImage.material = _coronaMat;
            _coronaMat.SetFloat(ID_Glow, 0f);
        }
    }

    private void Start()
    {
        _unlockState = unlockStateMono as ISkillUnlockState;
        _unlockState ??= SkillTreeManager.Instance;
        if (accordSystem == null) accordSystem = AccordStateSystem.Instance;

        if (_unlockState != null)
            _unlockState.OnAccordStateUnlocked += HandleUnlocked;

        SetUnlocked(_unlockState != null && _unlockState.IsAccordStateUnlocked);
    }

    private void OnDestroy()
    {
        if (_unlockState != null)
            _unlockState.OnAccordStateUnlocked -= HandleUnlocked;
        if (_coronaMat != null) Destroy(_coronaMat);
    }

    private void HandleUnlocked() => SetUnlocked(true);

    private void SetUnlocked(bool unlocked)
    {
        _isUnlocked = unlocked;
        if (panel != null) panel.SetActive(unlocked);
    }

    // ── Update ────────────────────────────────────────────────
    private void Update()
    {
        if (!_isUnlocked || accordSystem == null) return;

        bool active = accordSystem.IsAccordActive;
        bool full = accordSystem.BarIsFull;
        float charge = accordSystem.ChargeProgress;

        RefreshBar(active);
        RefreshCorona(active, full, charge);
        RefreshShine(active, full);
        RefreshFlash(active);
        RefreshPrompt(active, full);
    }

    // FLASH — a pulse each time a point lands (bar value increases) and a sustained pulse while Accord is active.
    // Fed to UIBarView.SetFlash, which lights both the interior and the line-mode frame. The bar's own low-value
    // warning flash is disabled in-scene (_flashThreshold = 0) — it's wrong for a bar that's normally empty.
    private void RefreshFlash(bool active)
    {
        if (barView == null) return;
        float bar = accordSystem.BarProgress;
        if (!active && _lastBar >= 0f && bar > _lastBar + 0.004f) _flash = 1f;   // gained a point → flash
        _lastBar = bar;
        _flash = Mathf.MoveTowards(_flash, 0f, flashDecay * Time.deltaTime);
        float outFlash = active
            ? (0.5f + 0.5f * Mathf.Sin(Time.time * activeFlashSpeed)) * activeFlashAmount
            : _flash;
        barView.SetFlash(outFlash);
    }

    // FILL — both clan halves show the one accord value; drains over the active window.
    private void RefreshBar(bool active)
    {
        if (barView == null) return;
        float v = active ? 1f - accordSystem.ActiveProgress : accordSystem.BarProgress;
        barView.SetValue(v);
        barView.SetValueB(v);   // no-op unless Split Halves is on (it is) — mirrors the value on the violet half
    }

    // CORONA — off while filling, a gentle invite when full, blooms with the X-hold charge, full while active.
    private void RefreshCorona(bool active, bool full, float charge)
    {
        if (_coronaMat == null) return;
        float target = active ? 1f
                     : charge > 0f ? Mathf.Max(coronaReady, charge)   // holding X → corona blooms with charge
                     : full ? coronaReady                             // ready to activate → gentle invite
                     : 0f;                                            // still filling → no halo
        _coronaGlow = Mathf.MoveTowards(_coronaGlow, target, coronaLerp * Time.deltaTime);
        _coronaMat.SetFloat(ID_Glow, _coronaGlow);
    }

    // SHINE — a clan sweep travels the bar on an interval ONLY while it's FULL and ready to trigger (the "go!"
    // signal), not while filling and not while active/draining.
    private void RefreshShine(bool active, bool full)
    {
        if (barView == null) return;
        if (active || !full)
        {
            _shineTimer = 0f;
            return;
        }
        _shineTimer += Time.deltaTime;
        if (_shineTimer >= shineInterval)
        {
            _shineTimer = 0f;
            barView.PlaySweep();
        }
    }

    // Optional "hold X" prompt — pulses while the bar is full/ready (and not yet active).
    private void RefreshPrompt(bool active, bool full)
    {
        if (holdXText == null) return;
        bool show = full && !active;
        if (holdXText.gameObject.activeSelf != show) holdXText.gameObject.SetActive(show);
        if (show)
            holdXText.alpha = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(Time.time * promptPulseSpeed) + 1f) * 0.5f);
    }
}
