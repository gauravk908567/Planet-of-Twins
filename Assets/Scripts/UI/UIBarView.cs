using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ONE generic fill-bar driver — used identically by twin health, enemy health, the
/// shared-health emblem, and the accord bar. Never fork a per-consumer copy of this class;
/// restyle an instance through the serialized colour/sprite dials and the public setters.
///
/// Drives a <c>UnityEngine.UI.Image</c> whose material uses the <c>PoT/UIBar</c> shader
/// (Assets/Art/Shaders/PoTUIBar.shader) and, optionally, a second plain-tint <c>Image</c> for
/// the frame/outline sprite drawn above it.
///
/// Two ORTHOGONAL channels are exposed, and this class never lets one leak into the other:
///   <b>FILL</b>  (<see cref="SetValue"/> / <see cref="SetValueB"/>) — the real value (health,
///                accord progress, ...). Moves the visible edge of the coloured region.
///   <b>DRAIN</b> (<see cref="SetDrain"/> / <see cref="SetDrainB"/>) — a separate weakness /
///                bond signal. It only re-tints the region the FILL channel already painted
///                toward the drain colour — it must never change how much of the bar is
///                filled. A bar can be reported 100% filled and 100% drained at once.
///
/// This is a pure view: it knows nothing about health pools, twins, enemies, or the accord
/// system. Callers push normalised 0..1 values in; this class owns only the shader wiring,
/// the display-value smoothing, the low-value flash, and the outline sweep flourish.
/// </summary>
public class UIBarView : MonoBehaviour
{
    private const string RequiredShaderName = "PoT/UIBar";

    [Header("Targets")]
    [Tooltip("Image whose material uses the PoT/UIBar shader — the fill mask sprite.")]
    [SerializeField] private Image _fillImage;
    [Tooltip("Optional. The outline/frame sprite drawn above the fill — tint only, no shader logic.")]
    [SerializeField] private Image _frameImage;

    [Header("Smoothing")]
    [Tooltip("How fast the displayed value chases the target value, in normalised units/sec.")]
    [SerializeField] private float _lerpSpeed = 1.5f;
    [Tooltip("false (default): SCALED time — bars are gameplay values and freeze with " +
             "Setsuna/pause via TimeScaleService (R10), same as the shader's flash pulse. " +
             "true: unscaled — only for bars that must keep animating while the game is paused.")]
    [SerializeField] private bool _useUnscaledTime = false;

    [Header("Low-State Flash")]
    [Tooltip("Displayed value below this normalised threshold starts flashing.")]
    [SerializeField, Range(0f, 1f)] private float _flashThreshold = 0.30f;
    [Tooltip("Flash amount right at value == 0. Above ~0.7 the pulse washes the clan colour " +
             "out to white — keep it moderate so the bar still reads as Kai/Lyra/enemy.")]
    [SerializeField, Range(0f, 1f)] private float _flashMaxAmount = 0.6f;
    [Tooltip("Flash amount right at value == _flashThreshold.")]
    [SerializeField, Range(0f, 1f)] private float _flashMinAmount = 0.15f;

    [Header("Outline Sweep")]
    [Tooltip("Seconds for one PlaySweep() pass to travel 0 -> 1.")]
    [SerializeField] private float _sweepSeconds = 0.6f;
    [Tooltip("While the displayed value is at 1 (full), auto-replay the sweep on an interval.")]
    [SerializeField] private bool _autoRepeatSweepWhenFull = false;
    [SerializeField] private float _sweepInterval = 2f;

    [Header("Styling (applied to the material in Awake — restyle without code)")]
    [SerializeField] private Color _fillColor = Color.white;
    [SerializeField] private Color _fillColorB = Color.white;
    [Tooltip("Plain tint on the frame Image. Only used when the frame does NOT use a " +
             "PoT/UIBar line-mode material; otherwise _lineColor/_lineFlashColor drive it.")]
    [SerializeField] private Color _frameColor = Color.white;
    [Header("Frame / symbol line colours (frame material in Line Mode)")]
    [Tooltip("Normal colour of the outline + clan symbol.")]
    [ColorUsage(true, true)] [SerializeField] private Color _lineColor = new Color(0.95f, 0.93f, 0.88f);
    [Tooltip("Colour the outline + symbol pulse to while flashing. Use an HDR value " +
             "(intensity > 1) so it blooms — this is the loud half of the low-health flash.")]
    [ColorUsage(true, true)] [SerializeField] private Color _lineFlashColor = new Color(3f, 2.4f, 1.2f);
    [SerializeField] private Color _drainColor = new Color(0.35f, 0.35f, 0.38f, 1f);
    [SerializeField] private Color _flashColor = new Color(1f, 0.92f, 0.75f, 1f);
    [SerializeField] private Color _sweepColor = Color.white;
    [Tooltip("Enables the shader's dual-colour half split (shared emblem). Leave off for a " +
             "single-value bar (twin/enemy health, accord).")]
    [SerializeField] private bool _splitHalves = false;
    [Tooltip("Optional — overrides _fillImage.sprite in Awake if assigned.")]
    [SerializeField] private Sprite _fillSpriteOverride;
    [Tooltip("Optional — overrides _frameImage.sprite in Awake if assigned.")]
    [SerializeField] private Sprite _frameSpriteOverride;

    // ── Shader property IDs (cached once — never look up by string per-frame) ──────────
    private static readonly int PropFillColor = Shader.PropertyToID("_FillColor");
    private static readonly int PropFillColorB = Shader.PropertyToID("_FillColorB");
    private static readonly int PropFill = Shader.PropertyToID("_Fill");
    private static readonly int PropFillB = Shader.PropertyToID("_FillB");
    private static readonly int PropSplitHalves = Shader.PropertyToID("_SplitHalves");
    private static readonly int PropDrain = Shader.PropertyToID("_Drain");
    private static readonly int PropDrainB = Shader.PropertyToID("_DrainB");
    private static readonly int PropDrainColor = Shader.PropertyToID("_DrainColor");
    private static readonly int PropFlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int PropFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int PropSweepActive = Shader.PropertyToID("_SweepActive");
    private static readonly int PropSweepAmount = Shader.PropertyToID("_SweepAmount");
    private static readonly int PropSweepColor = Shader.PropertyToID("_SweepColor");
    private static readonly int PropLineColor = Shader.PropertyToID("_LineColor");
    private static readonly int PropLineFlashColor = Shader.PropertyToID("_LineFlashColor");

    // ── Runtime state ────────────────────────────────────────────────────────────────
    // Image can't take a MaterialPropertyBlock (UGUI batches Graphics by material, not
    // per-renderer MPB) so each view needs its OWN material instance. Note the trap:
    // unlike Renderer.material, UnityEngine.UI.Graphic.material does NOT auto-instance —
    // it hands back the shared asset. Writing to that would make every bar in the game
    // share one fill value AND dirty the material asset on disk. So we explicitly clone it
    // at runtime and destroy the clone in OnDestroy.
    private Material _material;
    // The frame layer's own clone when it uses a PoT/UIBar line-mode material. It receives
    // the SAME _FlashAmount as the fill, so outline+symbol and interior pulse together.
    private Material _frameMaterial;
    private float _lastWrittenFrameFlash = float.NaN;

    private float _targetValue = 1f;
    private float _displayValue = 1f;
    private float _targetValueB = 1f;
    private float _displayValueB = 1f;
    // Held in fields, not written straight to the material: a caller may set drain from its
    // own Awake, before ours has run and made the material instance. Applied in styling.
    private float _drain;
    private float _drainB;

    private float _lastWrittenFill = float.NaN;
    private float _lastWrittenFillB = float.NaN;
    private float _lastWrittenDrain = float.NaN;
    private float _lastWrittenDrainB = float.NaN;
    private float _lastWrittenFlash = float.NaN;

    private bool _sweepPlaying;
    private float _sweepTimer;
    private float _autoRepeatTimer;

    private void Awake()
    {
        if (_fillImage == null)
        {
            Debug.LogError($"[UIBarView] {name}: _fillImage is not assigned.", this);
            enabled = false;
            return;
        }

        if (!Application.isPlaying)
        {
            // Edit-mode Awake (prefab stage / thumbnailing) — never touch Image.material here.
            return;
        }

        if (_fillSpriteOverride != null) _fillImage.sprite = _fillSpriteOverride;
        if (_frameImage != null && _frameSpriteOverride != null) _frameImage.sprite = _frameSpriteOverride;

        Material source = _fillImage.material;   // the SHARED asset — never written to
        if (source == null || source.shader == null || source.shader.name != RequiredShaderName)
        {
            string foundShader = source != null && source.shader != null ? source.shader.name : "null";
            Debug.LogError($"[UIBarView] {name}: _fillImage's material must use shader " +
                            $"'{RequiredShaderName}' (found '{foundShader}').", this);
            enabled = false;
            return;
        }

        // Explicit clone (Graphic.material does not auto-instance — see the field comment).
        _material = new Material(source) { name = source.name + " (UIBarView instance)" };
        _fillImage.material = _material;

        // Same treatment for the frame layer when it runs the shader in line mode.
        if (_frameImage != null)
        {
            Material frameSource = _frameImage.material;
            if (frameSource != null && frameSource.shader != null &&
                frameSource.shader.name == RequiredShaderName)
            {
                _frameMaterial = new Material(frameSource) { name = frameSource.name + " (UIBarView instance)" };
                _frameImage.material = _frameMaterial;
            }
        }

        ApplyInitialStyling();
    }

    private void OnDestroy()
    {
        // The clones are ours alone; without this every spawned bar leaks materials.
        if (_material != null) Destroy(_material);
        if (_frameMaterial != null) Destroy(_frameMaterial);
    }

    private void ApplyInitialStyling()
    {
        _material.SetColor(PropFillColor, _fillColor);
        _material.SetColor(PropFillColorB, _fillColorB);
        _material.SetColor(PropDrainColor, _drainColor);
        _material.SetColor(PropFlashColor, _flashColor);
        _material.SetColor(PropSweepColor, _sweepColor);
        _material.SetFloat(PropSplitHalves, _splitHalves ? 1f : 0f);

        if (_frameMaterial != null)
        {
            _frameMaterial.SetColor(PropLineColor, _lineColor);
            _frameMaterial.SetColor(PropLineFlashColor, _lineFlashColor);
        }
        else if (_frameImage != null)
        {
            _frameImage.color = _frameColor;   // plain-tint frame (no line-mode material)
        }

        // Force the first Update() to push the initial fill/drain values even though they
        // match the shader's own Properties-block defaults (NaN never equals anything).
        WriteFloatIfChanged(PropFill, _displayValue, ref _lastWrittenFill);
        WriteFloatIfChanged(PropFillB, _displayValueB, ref _lastWrittenFillB);
        WriteFloatIfChanged(PropDrain, _drain, ref _lastWrittenDrain);
        WriteFloatIfChanged(PropDrainB, _drainB, ref _lastWrittenDrainB);
        WriteFloatIfChanged(PropFlashAmount, 0f, ref _lastWrittenFlash);
    }

    private void Update()
    {
        if (_material == null) return;

        // R10: scaled by default (bars are gameplay reads and should freeze with Setsuna/
        // pause); _useUnscaledTime opts a specific bar out. See field tooltip.
        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float step = _lerpSpeed * dt;

        _displayValue = Mathf.MoveTowards(_displayValue, _targetValue, step);
        _displayValueB = Mathf.MoveTowards(_displayValueB, _targetValueB, step);

        WriteFloatIfChanged(PropFill, _displayValue, ref _lastWrittenFill);
        WriteFloatIfChanged(PropFillB, _displayValueB, ref _lastWrittenFillB);

        float flash = ComputeFlashAmount(_displayValue);
        WriteFloatIfChanged(PropFlashAmount, flash, ref _lastWrittenFlash);
        // Frame/symbol carries the LOUD half of the flash — same amount, same clock.
        if (_frameMaterial != null &&
            (float.IsNaN(_lastWrittenFrameFlash) || !Mathf.Approximately(flash, _lastWrittenFrameFlash)))
        {
            _frameMaterial.SetFloat(PropFlashAmount, flash);
            _lastWrittenFrameFlash = flash;
        }

        UpdateSweep(dt);
        UpdateAutoRepeat(dt);
    }

    /// <summary>Flash ramps from _flashMinAmount at the threshold to _flashMaxAmount at 0;
    /// exactly 0 above the threshold. The threshold/curve live here, NOT in the shader —
    /// the shader only ever renders whatever amount it's given.</summary>
    private float ComputeFlashAmount(float value)
    {
        if (_flashThreshold <= 0f || value >= _flashThreshold) return 0f;

        float t = 1f - (value / _flashThreshold); // 0 at threshold, 1 at zero
        return Mathf.Lerp(_flashMinAmount, _flashMaxAmount, t);
    }

    private void UpdateSweep(float dt)
    {
        if (!_sweepPlaying) return;

        _sweepTimer += dt;
        float t = _sweepSeconds > 0f ? Mathf.Clamp01(_sweepTimer / _sweepSeconds) : 1f;
        _material.SetFloat(PropSweepAmount, t);

        if (t >= 1f)
        {
            _sweepPlaying = false;
            _material.SetFloat(PropSweepActive, 0f);
        }
    }

    private void UpdateAutoRepeat(float dt)
    {
        if (!_autoRepeatSweepWhenFull || _sweepPlaying)
        {
            _autoRepeatTimer = 0f;
            return;
        }

        if (_displayValue < 1f)
        {
            _autoRepeatTimer = 0f;
            return;
        }

        _autoRepeatTimer += dt;
        if (_autoRepeatTimer >= _sweepInterval)
        {
            _autoRepeatTimer = 0f;
            PlaySweep();
        }
    }

    private void WriteFloatIfChanged(int propId, float value, ref float lastWritten)
    {
        if (!float.IsNaN(lastWritten) && Mathf.Approximately(value, lastWritten)) return;
        _material.SetFloat(propId, value);
        lastWritten = value;
    }

    // ── Public API ───────────────────────────────────────────────────────────────────

    /// <summary>Sets the primary FILL target (real value — health, progress, ...).
    /// Displayed value smoothly chases it at _lerpSpeed unless <paramref name="instant"/>.</summary>
    public void SetValue(float normalized01, bool instant = false)
    {
        _targetValue = Mathf.Clamp01(normalized01);
        if (instant) _displayValue = _targetValue;
    }

    /// <summary>Sets the second-half FILL target (split-halves bars only, e.g. the shared
    /// emblem's Lyra side). No-op on the visuals unless Split Halves is enabled on the material.</summary>
    public void SetValueB(float normalized01, bool instant = false)
    {
        _targetValueB = Mathf.Clamp01(normalized01);
        if (instant) _displayValueB = _targetValueB;
    }

    /// <summary>Sets the DRAIN/weakness amount for the primary half. Orthogonal to fill —
    /// never moves the filled edge, only re-tints the already-filled region.</summary>
    public void SetDrain(float weakness01)
    {
        _drain = Mathf.Clamp01(weakness01);
        if (_material == null) return;   // pre-Awake call — ApplyInitialStyling pushes it
        WriteFloatIfChanged(PropDrain, _drain, ref _lastWrittenDrain);
    }

    /// <summary>Sets the DRAIN/weakness amount for the second half (split-halves bars only).</summary>
    public void SetDrainB(float weakness01)
    {
        _drainB = Mathf.Clamp01(weakness01);
        if (_material == null) return;   // pre-Awake call — ApplyInitialStyling pushes it
        WriteFloatIfChanged(PropDrainB, _drainB, ref _lastWrittenDrainB);
    }

    public void SetFillColor(Color color)
    {
        _fillColor = color;
        if (_material != null) _material.SetColor(PropFillColor, color);
    }

    public void SetFillColorB(Color color)
    {
        _fillColorB = color;
        if (_material != null) _material.SetColor(PropFillColorB, color);
    }

    /// <summary>Normal colour of the outline + clan symbol (line-mode frame material).
    /// Falls back to a plain Image tint when the frame isn't running the shader.</summary>
    public void SetLineColor(Color color)
    {
        _lineColor = color;
        if (_frameMaterial != null) _frameMaterial.SetColor(PropLineColor, color);
        else if (_frameImage != null) _frameImage.color = color;
    }

    /// <summary>Colour the outline + symbol pulse to while flashing (use HDR to bloom).</summary>
    public void SetLineFlashColor(Color color)
    {
        _lineFlashColor = color;
        if (_frameMaterial != null) _frameMaterial.SetColor(PropLineFlashColor, color);
    }

    public void SetFrameColor(Color color)
    {
        _frameColor = color;
        if (_frameMaterial == null && _frameImage != null) _frameImage.color = color;
    }

    /// <summary>Runs one outline-sweep pass, 0 -> 1 over _sweepSeconds, then turns itself off.</summary>
    public void PlaySweep()
    {
        if (_material == null) return;
        _sweepPlaying = true;
        _sweepTimer = 0f;
        _material.SetFloat(PropSweepActive, 1f);
        _material.SetFloat(PropSweepAmount, 0f);
    }

    /// <summary>Sets the interval (seconds) between auto-repeated sweeps while the bar is
    /// full and _autoRepeatSweepWhenFull is enabled.</summary>
    public void SetSweepInterval(float seconds)
    {
        _sweepInterval = Mathf.Max(0f, seconds);
    }

    // ── Editor test hooks (Play Mode only — the material instance is created in Awake) ─

    [ContextMenu("TEST: set 100%")]
    private void TestSet100() => SetValue(1f);

    [ContextMenu("TEST: set 25%")]
    private void TestSet25() => SetValue(0.25f);

    [ContextMenu("TEST: set 0%")]
    private void TestSet0() => SetValue(0f);

    [ContextMenu("TEST: drain 100%")]
    private void TestDrain100() => SetDrain(1f);

    [ContextMenu("TEST: play sweep")]
    private void TestPlaySweep() => PlaySweep();
}
