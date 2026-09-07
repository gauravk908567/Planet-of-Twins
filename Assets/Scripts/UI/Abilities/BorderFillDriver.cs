using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch ability-HUD "border-as-timer" driver (Track D / Option B). Sits on (or beside) the
/// card Image that uses the <c>PoT/UIAbilityCard</c> shader and pushes the live 0..1 values every
/// frame from an <see cref="IAbilityHUDSource"/>:
///   • Cooldown  → border sweep fills 0→1 and the liquid tank RISES 0→1 (dim glow).
///   • Ready     → border + tank full, glow at the "available" level, shine on.
///   • Active    → border runs BACKWARD and the tank DRAINS 1→0 (glow at the "active" level).
///   • Holding   → tank shows the charge fraction while glow ramps down (charge abilities).
///
/// UI CanvasRenderer ignores MaterialPropertyBlock, so this clones the card material per instance
/// (≈10 slots — negligible) and sets floats on the clone. The static "look" dials (widths, colours,
/// runner glow, shine cadence) stay authored on the material; only the live progress/state comes
/// from here. The source is fed at runtime via <see cref="SetSource"/> (abilities are plain C#
/// objects constructed by TwinAbilitySetup — never serialized scene refs, R2), typically forwarded
/// by <see cref="AbilityIconUI"/> when it binds.
/// </summary>
[DisallowMultipleComponent]
public class BorderFillDriver : MonoBehaviour
{
    public enum TankDir { BottomUp = 0, TopDown = 1, LeftRight = 2, RightLeft = 3 }

    [Header("Target")]
    [Tooltip("The card Image using PoT/UIAbilityCard. Auto-resolved from this GameObject when empty.")]
    [SerializeField] private Image targetImage;

    [Header("Clan identity")]
    [Tooltip("Single-owner clan colour (also the LEFT half when dual). EMISSIVE (HDR >1) so it blooms — a glowy " +
             "clan colour, not the flat exact hue. Lyra gold.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color clanA = new Color(1.5f, 1.12f, 0.42f, 1f);   // emissive gold (Luminari)
    [Tooltip("RIGHT-half clan colour for shared (dual) slots. EMISSIVE (HDR >1). Kai violet.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color clanB = new Color(1.0f, 0.66f, 1.5f, 1f);    // emissive violet (Vethara)
    [Tooltip("Shared ability — split the border/tank into both clan colours.")]
    [SerializeField] private bool dualClan = false;

    [Header("Directions")]
    [Tooltip("Border sweep direction. Convention: Lyra abilities un-inverted (L→R), Kai inverted (R→L).")]
    [SerializeField] private bool borderInvert = false;
    [Tooltip("Liquid tank fill direction. Default BottomUp per the reference: cooldown rises, active drains.")]
    [SerializeField] private TankDir tankDir = TankDir.BottomUp;

    [Header("Glow levels (state)")]
    [SerializeField, Range(0f, 2f)] private float glowCooldown = 0.18f;
    [SerializeField, Range(0f, 2f)] private float glowAvailable = 0.7f;
    [SerializeField, Range(0f, 2f)] private float glowActive = 1.0f;
    [Tooltip("Glow at the START of a charge hold (ramps down to 0 as the hold completes).")]
    [SerializeField, Range(0f, 2f)] private float glowHoldStart = 0.6f;
    [Tooltip("Active corona (the 'solar eclipse' ring the keycap draws while the ability is firing): how fast it " +
             "fades out after the ability stops being active.")]
    [SerializeField, Range(0.5f, 8f)] private float activeGlowDecay = 3f;

    [Header("Channels")]
    [SerializeField] private bool driveBorder = true;
    [SerializeField] private bool driveTank = true;
    [SerializeField] private bool shineWhenReady = true;
    [Tooltip("Flash the whole card when readiness jumps a discrete step (e.g. a Soul Convergence soul gained). " +
             "Auto-detected from an upward jump in CooldownProgress while accumulating — generic, no per-ability wiring.")]
    [SerializeField] private bool flashOnStep = true;
    [SerializeField, Range(0.005f, 0.5f)] private float flashThreshold = 0.03f;
    [SerializeField, Range(0.5f, 10f)] private float flashDecay = 3f;
    [Tooltip("Passive-pulse mode (e.g. Coalesce): while the source IsActive, PULSE the whole card instead of the " +
             "cooldown/drain readout; dim static frame otherwise. The tank is unused.")]
    [SerializeField] private bool passivePulse = false;
    [SerializeField, Range(0.5f, 10f)] private float pulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.6f;
    [Tooltip("Charge abilities (Empower): how fast the release-explosion pulse decays.")]
    [SerializeField, Range(0.5f, 8f)] private float releasePulseDecay = 2.5f;

    private IAbilityHUDSource _source;
    private Material _mat;
    private float _lastCd = -1f;   // previous CooldownProgress, for step-flash detection
    private float _flash;          // 0..1 current flash, decays
    private float _pulse;          // 0..1 release-explosion pulse, decays
    private bool _wasHolding;      // for hold→active release detection
    private float _activeGlow;     // 0..1 corona envelope: 1 while active, decays after (drives the keycap corona)

    // Cached property IDs.
    private static readonly int ID_ClanA = Shader.PropertyToID("_ClanA");
    private static readonly int ID_ClanB = Shader.PropertyToID("_ClanB");
    private static readonly int ID_DualClan = Shader.PropertyToID("_DualClan");
    private static readonly int ID_BorderOn = Shader.PropertyToID("_BorderOn");
    private static readonly int ID_BorderProgress = Shader.PropertyToID("_BorderProgress");
    private static readonly int ID_BorderInvert = Shader.PropertyToID("_BorderInvert");
    private static readonly int ID_TankOn = Shader.PropertyToID("_TankOn");
    private static readonly int ID_TankProgress = Shader.PropertyToID("_TankProgress");
    private static readonly int ID_TankDir = Shader.PropertyToID("_TankDir");
    private static readonly int ID_Glow = Shader.PropertyToID("_GlowIntensity");
    private static readonly int ID_ShineOn = Shader.PropertyToID("_ShineOn");
    private static readonly int ID_Aspect = Shader.PropertyToID("_Aspect");
    private static readonly int ID_HoldProgress = Shader.PropertyToID("_HoldProgress");
    private static readonly int ID_Flash = Shader.PropertyToID("_Flash");
    private static readonly int ID_Pulse = Shader.PropertyToID("_Pulse");
    private static readonly int ID_CasterClan = Shader.PropertyToID("_CasterClan");
    private static readonly int ID_UseCaster = Shader.PropertyToID("_UseCaster");

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage == null)
        {
            Debug.LogError("[BorderFillDriver] No target Image.", this);
            enabled = false;
            return;
        }

        // Clone the shared card material so per-slot values don't cross-talk (UI ignores MPB).
        if (targetImage.material != null)
        {
            _mat = new Material(targetImage.material);
            targetImage.material = _mat;
        }
        else
        {
            Debug.LogError("[BorderFillDriver] Target Image has no material (assign PoT/UIAbilityCard).", this);
            enabled = false;
            return;
        }

        // The clan colour now comes from the shader (_ClanA/_ClanB), NOT the Image tint. Force the tint white
        // so the retired M5 owner-tint (AccordHUDController.ApplyOwnerTints writes image.color) can't multiply/
        // dim the card. Update() re-asserts this after Start-phase tinting.
        targetImage.color = Color.white;

        // CRITICAL: the ability panels have a Mask, which would render this card through a STENCIL-VARIANT
        // material — our per-frame float writes to the base material never reach the drawn pixels, freezing the
        // timer (static config shows, animation doesn't). The card is a static full-slot frame that doesn't need
        // the panel's clipping, so opt it out of masking → it renders with our own material and animates.
        targetImage.maskable = false;

        // Static per-slot config pushed once.
        _mat.SetColor(ID_ClanA, clanA);
        _mat.SetColor(ID_ClanB, clanB);
        _mat.SetFloat(ID_DualClan, dualClan ? 1f : 0f);
        _mat.SetFloat(ID_BorderInvert, borderInvert ? 1f : 0f);
        _mat.SetFloat(ID_TankDir, (float)(int)tankDir);
        _mat.SetFloat(ID_BorderOn, driveBorder ? 1f : 0f);
        _mat.SetFloat(ID_TankOn, driveTank ? 1f : 0f);
        ApplyAspect();
    }

    /// <summary>Feed the runtime ability source (forwarded by AbilityIconUI.Bind). Null clears it.</summary>
    public void SetSource(IAbilityHUDSource source) => _source = source;

    // Clan identity — read by AccordIconSlot to recolour the input glyph to match the border.
    public Color ClanA => clanA;
    public Color ClanB => clanB;
    public bool IsDual => dualClan;

    /// <summary>0..1 "solar-eclipse" corona envelope — 1 while the ability is firing, decays after. AccordIconSlot
    /// pushes this to the keycap material's <c>_ActiveGlow</c> so the corona rings the glyph while active.</summary>
    public float ActiveGlow => _activeGlow;

    /// <summary>Readable LDR clan colour for the input glyph — the emissive clan (HDR &gt;1) normalized so the hue
    /// is preserved but it stays legible as a solid glyph tint (emissive violet would otherwise clamp to pink).</summary>
    public Color GlyphColor => NormalizeGlyph(clanA);
    public Color GlyphColorRight => NormalizeGlyph(clanB);

    private static Color NormalizeGlyph(Color c)
    {
        float m = Mathf.Max(Mathf.Max(c.r, c.g), Mathf.Max(c.b, 1f));
        return new Color(c.r / m, c.g / m, c.b / m, 1f);
    }

    /// <summary>Runtime clan re-config (AccordHUDController may drive this per slot role).</summary>
    public void SetClan(Color a, Color b, bool dual)
    {
        clanA = a; clanB = b; dualClan = dual;
        if (_mat == null) return;
        _mat.SetColor(ID_ClanA, clanA);
        _mat.SetColor(ID_ClanB, clanB);
        _mat.SetFloat(ID_DualClan, dualClan ? 1f : 0f);
    }

    private void ApplyAspect()
    {
        if (_mat == null || targetImage == null) return;
        Rect r = targetImage.rectTransform.rect;
        float aspect = r.height > 0.0001f ? r.width / r.height : 1f;
        _mat.SetFloat(ID_Aspect, aspect);
    }

    private void Update()
    {
        if (_mat == null) return;
        // Re-assert white (only when it drifted) so the retired M5 owner-tint can't dim the card.
        if (targetImage != null && targetImage.color != Color.white) targetImage.color = Color.white;
        ApplyAspect();   // cheap; keeps the rounded corners correct if the layout resizes

        if (_flash > 0f) _flash = Mathf.MoveTowards(_flash, 0f, flashDecay * Time.deltaTime);
        if (_pulse > 0f) _pulse = Mathf.MoveTowards(_pulse, 0f, releasePulseDecay * Time.deltaTime);

        // Passive-pulse mode (Coalesce): pulse while active, dim static frame otherwise. Skips the timer readout.
        if (passivePulse)
        {
            bool on = _source != null && _source.IsActive;
            if (on) _flash = (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed)) * pulseAmplitude;
            Push(border: 1f, tank: 0f, glow: on ? glowActive : glowCooldown, shine: false, hold: 0f);
            return;
        }

        if (_source == null)
        {
            // No source yet — show an empty, quiet card.
            Push(border: 0f, tank: 0f, glow: glowCooldown, shine: false, hold: 0f);
            return;
        }

        bool holding = _source.IsHolding;
        bool active = _source.IsActive;
        // Active-corona envelope: snap to 1 while firing, fade out after (the keycap reads ActiveGlow each frame).
        _activeGlow = active ? 1f : Mathf.MoveTowards(_activeGlow, 0f, activeGlowDecay * Time.deltaTime);
        float cd = Mathf.Clamp01(_source.CooldownProgress);   // 0 = on cooldown, 1 = ready

        // Flash on a discrete UPWARD jump in readiness while accumulating (e.g. SC soul gained). Smooth cooldowns
        // move in tiny per-frame steps (< threshold), so they don't flash; a soul (+1/cap) does.
        if (flashOnStep && _lastCd >= 0f && !holding && !active && (cd - _lastCd) > flashThreshold)
            _flash = 1f;
        _lastCd = cd;

        // Hold completed into the active window → fire the release-explosion pulse (charge abilities).
        if (_wasHolding && !holding && active) _pulse = 1f;
        _wasHolding = holding;

        // Joint single-caster override (Empower): while charging/active, the glow/hold/pulse take the CASTER's
        // single clan (the border keeps its dual split). -1 = no caster → dual behaviour.
        int casterSide = (holding || active) && _source is ICasterClanSource cc ? cc.CasterSide : -1;
        if (casterSide >= 0)
        {
            _mat.SetFloat(ID_UseCaster, 1f);
            _mat.SetColor(ID_CasterClan, casterSide == 0 ? clanA : clanB);
        }
        else
        {
            _mat.SetFloat(ID_UseCaster, 0f);
        }

        if (holding)
        {
            // Charge hold: tank shows the charge fraction; glow ramps glowHoldStart → 0.
            float h = Mathf.Clamp01(_source.HoldProgress);
            Push(border: h, tank: h, glow: Mathf.Lerp(glowHoldStart, 0f, h), shine: false, hold: h);
        }
        else if (active)
        {
            // Active window: border runs backward, tank drains full → empty.
            float remain = 1f - Mathf.Clamp01(_source.ActiveProgress);
            Push(border: remain, tank: remain, glow: glowActive, shine: false, hold: 0f);
        }
        else if (cd < 0.999f)
        {
            // Cooldown / recharge: border sweeps and tank rises 0 → 1.
            Push(border: cd, tank: cd, glow: glowCooldown, shine: false, hold: 0f);
        }
        else
        {
            // Ready.
            Push(border: 1f, tank: 1f, glow: glowAvailable, shine: shineWhenReady, hold: 0f);
        }
    }

    private void Push(float border, float tank, float glow, bool shine, float hold)
    {
        _mat.SetFloat(ID_BorderProgress, border);
        _mat.SetFloat(ID_TankProgress, tank);
        _mat.SetFloat(ID_Glow, glow);
        _mat.SetFloat(ID_ShineOn, shine ? 1f : 0f);
        _mat.SetFloat(ID_HoldProgress, hold);
        _mat.SetFloat(ID_Flash, _flash);
        _mat.SetFloat(ID_Pulse, _pulse);
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
