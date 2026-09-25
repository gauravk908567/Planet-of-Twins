using UnityEngine;

/// <summary>
/// Single HUD controller for all ability slots.
/// Handles:
///   1. Normal ability unlock visibility (SC, Coalesce, Empower)
///   2. Normal ability HUD source binding (Possess, Stun, Gate)
///   3. Accord State slot animation on activate/deactivate
/// </summary>
public class AccordHUDController : MonoBehaviour
{
    [Header("Twin Controllers — for HUD source binding")]
    [SerializeField] private AbilityController lyraController;
    [SerializeField] private AbilityController kaiController;
    [SerializeField] private EmpowerSystem empowerSystem;
    [Tooltip("Soul Convergence — drives its own border (souls→charge→window). Optional; falls back to " +
             "SoulConvergenceSystem.Instance, then to a passive dummy.")]
    [SerializeField] private SoulConvergenceSystem soulConvergenceSystem;
    [Tooltip("Coalesce — passive; the border pulses while an aura is live. Optional; falls back to a passive dummy.")]
    [SerializeField] private CoalesceSystem coalesceSystem;
    [Tooltip("Setsuna — the SC slot's ACCORD form. Drives the SC card while Accord is active " +
             "(souls→charge→7s slow-window). Optional; falls back to SetsunaSystem.Instance, then a passive dummy.")]
    [SerializeField] private SetsunaSystem setsunaSystem;

    [Header("Accord System")]
    [SerializeField] private AccordStateSystem accordSystem;

    [Header("Skill tree unlock events")]
    [SerializeField] private MonoBehaviour skillUnlockStateMono;

    // Couch M5 — bar is regrouped into per-twin [Gate → Primary] pairs with the joint powers between them:
    //   [slotGate (Lyra)] [slotPossess (Lyra)]  [slotSC] [slotCoalesce] [slotEmpower]  [slotKaiGate] [slotStun (Kai)]
    // Field names are LOGICAL (what each binds), not positional — the left→right order lives in the scene
    // RectTransforms and in GetOrderedSlots() (stagger ripple). slotKaiGate surfaces Kai's own teleport, which
    // exists (TwinAbilitySetup builds a Weaver's Gate for BOTH twins) but was never shown before.
    [Header("Slots — logical bindings (visual order set in scene)")]
    [SerializeField] private AccordIconSlot slotPossess;
    [SerializeField] private AccordIconSlot slotGate;
    [SerializeField] private AccordIconSlot slotSC;
    [SerializeField] private AccordIconSlot slotCoalesce;
    [SerializeField] private AccordIconSlot slotEmpower;
    [SerializeField] private AccordIconSlot slotKaiGate;   // couch M5 — Kai's Weaver's Gate (was off-HUD)
    [SerializeField] private AccordIconSlot slotStun;

    [Header("Stagger timing")]
    [SerializeField] private float staggerInterval = 0.05f;

    private ISkillUnlockState _unlockState;

    // Button-glyph system (P2b) — input action names per slot. A structural per-slot-role assignment,
    // NOT a twin-identity fork: same "Ability"/"Teleport" for both twins, resolved through each twin's OWN
    // provider so the glyph reflects that player's device. Coalesce is a PASSIVE (auto on Stun/Possess) → no key.
    private const string ActionAbility     = "Ability";      // Possess (Lyra) / Stun (Kai) — GetAbilityDown
    private const string ActionTeleport    = "Teleport";     // Weaver's Gate (both twins) — GetTeleportHeld
    private const string ActionConvergence = "Convergence";  // Soul Convergence (joint hold) — GetConvergenceHeld
    private const string ActionEmpower     = "Empower";      // Empower (joint, single-caster) — GetEmpowerHeld

    // Resolved in Start from the controllers (AbilityController shares the twin's GameObject) so per-twin glyphs
    // route via PlayerInputRouter.For(twin). Null → ApplyKeyGlyphs falls back to SharedInput (still shows a glyph).
    private Player _lyraTwin;
    private Player _kaiTwin;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        _unlockState = skillUnlockStateMono as ISkillUnlockState;
    }

    private void OnEnable()
    {
        if (_unlockState != null)
        {
            _unlockState.OnCoalesceUnlocked += OnCoalesceUnlocked;
            _unlockState.OnSoulConvergenceUnlocked += OnSCUnlocked;
            _unlockState.OnEmpowerUnlocked += OnEmpowerUnlocked;
        }

        if (accordSystem != null)
        {
            accordSystem.OnAccordActivated += HandleAccordActivated;
            accordSystem.OnAccordDeactivated += HandleAccordDeactivated;
        }

        // Button-glyph system (P2b) — re-paint key-caps when a player swaps keyboard↔pad (Overwatch-style).
        // Named handler, unsubscribed in OnDisable (R8); the tracker's event spans scene loads.
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;
    }

    private void OnDisable()
    {
        LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;

        if (_unlockState != null)
        {
            _unlockState.OnCoalesceUnlocked -= OnCoalesceUnlocked;
            _unlockState.OnSoulConvergenceUnlocked -= OnSCUnlocked;
            _unlockState.OnEmpowerUnlocked -= OnEmpowerUnlocked;
        }

        if (accordSystem != null)
        {
            accordSystem.OnAccordActivated -= HandleAccordActivated;
            accordSystem.OnAccordDeactivated -= HandleAccordDeactivated;
        }
    }

    private void Start()
    {
        _unlockState ??= SkillTreeManager.Instance;
        if (accordSystem == null) accordSystem = AccordStateSystem.Instance;

        if (_unlockState == null)
        {
            Debug.LogError("[AccordHUDController] ISkillUnlockState unresolved — is Persistent loaded?", this);
            enabled = false;
            return;
        }

        // OnEnable fires before Start, so re-subscribe events in case _unlockState was null then.
        _unlockState.OnCoalesceUnlocked -= OnCoalesceUnlocked;
        _unlockState.OnCoalesceUnlocked += OnCoalesceUnlocked;
        _unlockState.OnSoulConvergenceUnlocked -= OnSCUnlocked;
        _unlockState.OnSoulConvergenceUnlocked += OnSCUnlocked;
        _unlockState.OnEmpowerUnlocked -= OnEmpowerUnlocked;
        _unlockState.OnEmpowerUnlocked += OnEmpowerUnlocked;

        if (accordSystem != null)
        {
            accordSystem.OnAccordActivated -= HandleAccordActivated;
            accordSystem.OnAccordActivated += HandleAccordActivated;
            accordSystem.OnAccordDeactivated -= HandleAccordDeactivated;
            accordSystem.OnAccordDeactivated += HandleAccordDeactivated;
        }

        // Button-glyph system (P2b) — the twin behind each controller (AbilityController shares the twin's
        // GameObject), so per-twin key-caps can route via PlayerInputRouter.For(twin).
        if (lyraController != null) _lyraTwin = lyraController.GetComponent<Player>();
        if (kaiController  != null) _kaiTwin  = kaiController.GetComponent<Player>();

        BindNormalSources();
        BindAccordSources();
        ApplyKeyGlyphs();
    }

    // ── Unlock events — show slot when purchased ──────────────
    private void OnCoalesceUnlocked() => slotCoalesce?.SetNormalUnlocked(true);
    private void OnSCUnlocked()
    {
        slotSC?.SetNormalUnlocked(true);
        // Setsuna accord icon unlocks together with SC
        slotSC?.SetAccordUnlocked(true);
    }
    private void OnEmpowerUnlocked() => slotEmpower?.SetNormalUnlocked(true);

    // ── Normal source binding ─────────────────────────────────
    private void BindNormalSources()
    {
        // Always visible
        if (lyraController != null)
        {
            slotPossess?.BindNormal(lyraController.GetPrimaryHUDSource());
            slotGate?.BindNormal(lyraController.GetTeleportHUDSource());
        }
        if (kaiController != null)
        {
            slotStun?.BindNormal(kaiController.GetPrimaryHUDSource());
            // Couch M5 — surface Kai's own teleport (both twins have one; Kai's was never on the bar).
            slotKaiGate?.BindNormal(kaiController.GetTeleportHUDSource());
        }

        // Locked until purchased — bind source now, visibility controlled by _startLocked.
        // Coalesce is passive — bind the real system so the border can pulse while an aura is live.
        slotCoalesce?.BindNormal(coalesceSystem != null
            ? (IAbilityHUDSource)coalesceSystem
            : new PassiveHUDSource());

        // Soul Convergence drives its OWN border (souls meter → charge → 7s window). Bind the real system so the
        // card shows its state; fall back to the passive dummy if it's somehow unresolved.
        soulConvergenceSystem ??= SoulConvergenceSystem.Instance;
        slotSC?.BindNormal(soulConvergenceSystem != null
            ? (IAbilityHUDSource)soulConvergenceSystem
            : new PassiveHUDSource());

        if (empowerSystem != null)
            slotEmpower?.BindNormal(empowerSystem);
    }

    // ── Accord source binding ─────────────────────────────────
    private void BindAccordSources()
    {
        if (accordSystem == null)
        {
            Debug.LogWarning("[AccordHUDController] AccordStateSystem not assigned — accord icons won't bind.", this);
            return;
        }

        slotPossess?.BindAccord(accordSystem.RadiantSeekerHUDSource, accordSystem.RadiantSeekerActiveState);
        slotStun?.BindAccord(accordSystem.VoidStrikeHUDSource, accordSystem.VoidStrikeActiveState);

        // SC's accord form = Setsuna (souls→charge→7s slow-window). Bind the real system so the SC card reads
        // Setsuna while Accord is up; pass it as the active-state so the slot defers returning to the normal SC
        // view until Setsuna's window (and rewind) finishes. Falls back to a passive dummy if unresolved.
        setsunaSystem ??= SetsunaSystem.Instance;
        slotSC?.BindAccord(
            setsunaSystem != null ? (IAbilityHUDSource)setsunaSystem : new EnhancedHUDSource(),
            setsunaSystem as IAbilityActiveState);
        slotGate?.BindAccord(new EnhancedHUDSource(), null);
        slotKaiGate?.BindAccord(new EnhancedHUDSource(), null);   // couch M5 — Kai's gate mirrors Lyra's in Accord
        slotCoalesce?.BindAccord(new EnhancedHUDSource(), null);
        // Empower accord slot = Accord Spirits (same R button, different mode)
        // If not wired or not unlocked, slot stays hidden — no locked text shown
        if (accordSystem != null)
            slotEmpower?.BindAccord(accordSystem.AccordSpiritHUDSource, null);
    }

    // ── Button-glyph key-caps (P2b) ───────────────────────────
    // Replace each ability icon's static keybind letter with a device-aware input glyph. Single-owner slots use
    // that twin's provider; joint slots (SC/Empower) resolve BOTH twins' providers → one glyph if the players
    // share a device kind, or both glyphs (kb + pad) when they differ. Coalesce is a passive → no key-cap.
    private void ApplyKeyGlyphs()
    {
        var lyra = _lyraTwin != null ? PlayerInputRouter.For(_lyraTwin) : PlayerInputRouter.SharedInput;
        var kai  = _kaiTwin  != null ? PlayerInputRouter.For(_kaiTwin)  : PlayerInputRouter.SharedInput;

        // Single-owner → that twin's device glyph.
        slotPossess?.ApplyKeyGlyph(lyra, ActionAbility);
        slotGate?.ApplyKeyGlyph(lyra, ActionTeleport);
        slotStun?.ApplyKeyGlyph(kai, ActionAbility);
        slotKaiGate?.ApplyKeyGlyph(kai, ActionTeleport);

        // Joint → one glyph if both share a device kind, else half-half (kb + pad).
        slotSC?.ApplyKeyGlyphJoint(lyra, kai, ActionConvergence);
        slotEmpower?.ApplyKeyGlyphJoint(lyra, kai, ActionEmpower);

        // Passive (no button) → clear.
        slotCoalesce?.ClearKeyGlyph();
    }

    private void OnDeviceSwitched(InputDeviceKind kind) => ApplyKeyGlyphs();

    // ── Accord slot animation ─────────────────────────────────
    private void HandleAccordActivated()
    {
        var slots = GetOrderedSlots();
        for (int i = 0; i < slots.Length; i++)
            slots[i]?.AnimateToAccord(i * staggerInterval);
    }

    private void HandleAccordDeactivated()
    {
        var slots = GetOrderedSlots();
        for (int i = slots.Length - 1; i >= 0; i--)
            slots[i]?.AnimateToNormal();
    }

    // Couch M5 — visual left→right order for the accord stagger ripple: Lyra pair, joints, Kai pair.
    private AccordIconSlot[] GetOrderedSlots() => new[]
    {
        slotGate, slotPossess,
        slotSC, slotCoalesce, slotEmpower,
        slotKaiGate, slotStun
    };

    // ── HUD source helpers ────────────────────────────────────
    // Names always empty — designer sets TMP text directly in scene
    private class PassiveHUDSource : IAbilityHUDSource
    {
        public string AbilityName => "";
        public float CooldownProgress => 1f;
        public int CurrentCharges => 1;
        public int MaxCharges => 1;
        public bool IsActive => true;
        public float ActiveProgress => 1f;   // passive/always-on — no drain
        public bool IsHolding => false;
        public float HoldProgress => 0f;
    }

    private class EnhancedHUDSource : IAbilityHUDSource
    {
        public string AbilityName => "";
        public float CooldownProgress => 1f;
        public int CurrentCharges => 1;
        public int MaxCharges => 1;
        public bool IsActive => false;
        public float ActiveProgress => 1f;
        public bool IsHolding => false;
        public float HoldProgress => 0f;
    }


}