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

    // Couch M5 — clan owner-frame colours (ArtStyle §10, locked): Lyra/Luminari LEFT = antique gold #FFCE52,
    // Kai/Vethara RIGHT = royal violet #A874F0. Serialized so the designer can match final art; these are the
    // authored defaults. A JOINT slot gets left=lyra, right=kai (a split frame reads "both twins").
    [Header("Clan owner-frame colours (couch M5 — ArtStyle §10)")]
    [SerializeField] private Color lyraColour = new Color(1f, 0.808f, 0.322f, 1f);   // #FFCE52
    [SerializeField] private Color kaiColour  = new Color(0.659f, 0.455f, 0.941f, 1f); // #A874F0

    [Header("Stagger timing")]
    [SerializeField] private float staggerInterval = 0.05f;

    private ISkillUnlockState _unlockState;

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
    }

    private void OnDisable()
    {
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

        BindNormalSources();
        BindAccordSources();
        ApplyOwnerTints();
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

        // Locked until purchased — bind source now, visibility controlled by _startLocked
        slotCoalesce?.BindNormal(new PassiveHUDSource());
        slotSC?.BindNormal(new PassiveHUDSource());

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

        slotSC?.BindAccord(new EnhancedHUDSource(), null);
        slotGate?.BindAccord(new EnhancedHUDSource(), null);
        slotKaiGate?.BindAccord(new EnhancedHUDSource(), null);   // couch M5 — Kai's gate mirrors Lyra's in Accord
        slotCoalesce?.BindAccord(new EnhancedHUDSource(), null);
        // Empower accord slot = Accord Spirits (same R button, different mode)
        // If not wired or not unlocked, slot stays hidden — no locked text shown
        if (accordSystem != null)
            slotEmpower?.BindAccord(accordSystem.AccordSpiritHUDSource, null);
    }

    // ── Owner tints (couch M5) ────────────────────────────────
    // Paint each slot's clan-owner frame once, from its FIXED owner (the slot's identity is the same in normal
    // and accord — Lyra's Possess→RadiantSeeker, Kai's Stun→VoidStrike). Twin-owned slots = a solid clan frame;
    // the three joint powers = a gold↔violet split (owned by neither). Owner is per-slot-ROLE (structural), not a
    // behavior fork on twin identity, so this stays keep-clean-for-co-op: a UI colour lookup, no `if (isKai)`.
    private void ApplyOwnerTints()
    {
        // Lyra (LEFT = gold)
        slotGate?.SetOwnerTint(lyraColour, lyraColour);
        slotPossess?.SetOwnerTint(lyraColour, lyraColour);

        // Joint powers (split: Lyra gold ↔ Kai violet)
        slotSC?.SetOwnerTint(lyraColour, kaiColour);
        slotCoalesce?.SetOwnerTint(lyraColour, kaiColour);
        slotEmpower?.SetOwnerTint(lyraColour, kaiColour);

        // Kai (RIGHT = violet)
        slotKaiGate?.SetOwnerTint(kaiColour, kaiColour);
        slotStun?.SetOwnerTint(kaiColour, kaiColour);
    }

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
    }

    private class EnhancedHUDSource : IAbilityHUDSource
    {
        public string AbilityName => "";
        public float CooldownProgress => 1f;
        public int CurrentCharges => 1;
        public int MaxCharges => 1;
        public bool IsActive => false;
    }


}