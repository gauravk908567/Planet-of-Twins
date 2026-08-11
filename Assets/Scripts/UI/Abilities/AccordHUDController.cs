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

    [Header("Slots — in order left to right")]
    [SerializeField] private AccordIconSlot slotPossess;
    [SerializeField] private AccordIconSlot slotGate;
    [SerializeField] private AccordIconSlot slotSC;
    [SerializeField] private AccordIconSlot slotCoalesce;
    [SerializeField] private AccordIconSlot slotEmpower;
    [SerializeField] private AccordIconSlot slotStun;

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
            slotStun?.BindNormal(kaiController.GetPrimaryHUDSource());

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
        slotCoalesce?.BindAccord(new EnhancedHUDSource(), null);
        // Empower accord slot = Accord Spirits (same R button, different mode)
        // If not wired or not unlocked, slot stays hidden — no locked text shown
        if (accordSystem != null)
            slotEmpower?.BindAccord(accordSystem.AccordSpiritHUDSource, null);
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

    private AccordIconSlot[] GetOrderedSlots() => new[]
    {
        slotPossess, slotGate, slotSC,
        slotCoalesce, slotEmpower, slotStun
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