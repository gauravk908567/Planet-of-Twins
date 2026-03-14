using UnityEngine;

/// <summary>
/// Wires ability sources to their icon slots and reacts to skill unlocks.
///
/// SCENE SETUP:
///   On your screen-space Canvas, place this component on a manager object.
///   Layout: Left column = Lyra (Possess), Right column = Kai (Stun),
///           Middle row = Gate + unlockable shared abilities.
///
///   Drag each pre-placed AbilityIconUI into the corresponding slot below.
///   Drag both AbilityControllers (Lyra = left, Kai = right).
///   Drag SkillTreeManager into both ISkillUnlockState and IAbilityDataStore slots.
///
/// NOTE: AbilityBase must implement IAbilityHUDSource for cooldown rings to work.
///       See IAbilityHUDSource.cs for the required properties.
/// </summary>
public class AbilityHUDController : MonoBehaviour
{
    [Header("Twin Controllers")]
    [SerializeField] private AbilityController lyraController;   // left twin (Possess)
    [SerializeField] private AbilityController kaiController;    // right twin (Stun)

    [Header("Left column — Lyra")]
    [SerializeField] private AbilityIconUI possessIcon;

    [Header("Right column — Kai")]
    [SerializeField] private AbilityIconUI stunIcon;

    [Header("Middle row — Shared (always visible)")]
    [SerializeField] private AbilityIconUI gateIcon;             // Weaver's Gate / teleport

    [Header("Middle row — Shared (appear on unlock)")]
    [SerializeField] private AbilityIconUI dualCastIcon;
    [SerializeField] private AbilityIconUI coalesceIcon;
    [SerializeField] private AbilityIconUI soulConvIcon;
    [SerializeField] private AbilityIconUI healthRegenIcon;

    [Header("Skill tree references")]
    [SerializeField] private MonoBehaviour skillUnlockStateMono; // → ISkillUnlockState

    private ISkillUnlockState _unlockState;

    // ── Unity ─────────────────────────────────────────────────
    private void Awake()
    {
        _unlockState = skillUnlockStateMono as ISkillUnlockState;

        // Hide unlockable icons until purchased
        dualCastIcon?.SetUnlocked(false);
        coalesceIcon?.SetUnlocked(false);
        soulConvIcon?.SetUnlocked(false);
        healthRegenIcon?.SetUnlocked(false);
    }

    private void Start()
    {
        BindAlwaysVisible();
        BindUnlockEvents();
    }

    private void OnEnable()
    {
        if (_unlockState == null) return;
        _unlockState.OnDualCastUnlocked += () => dualCastIcon?.SetUnlocked(true);
        _unlockState.OnCoalesceUnlocked += () => coalesceIcon?.SetUnlocked(true);
        _unlockState.OnSoulConvergenceUnlocked += () => soulConvIcon?.SetUnlocked(true);
        // HealthRegen uses ISkillUnlockState.OnHealthRegenUnlocked if it exists;
        // if not, wire it manually or add the event to ISkillUnlockState.
    }

    private void OnDisable()
    {
        if (_unlockState == null) return;
        _unlockState.OnDualCastUnlocked -= () => dualCastIcon?.SetUnlocked(true);
        _unlockState.OnCoalesceUnlocked -= () => coalesceIcon?.SetUnlocked(true);
        _unlockState.OnSoulConvergenceUnlocked -= () => soulConvIcon?.SetUnlocked(true);
    }

    // ── Wiring ─────────────────────────────────────────────────
    private void BindAlwaysVisible()
    {
        // Possess (Lyra primary) — always unlocked
        if (lyraController != null && possessIcon != null)
        {
            var src = lyraController.GetPrimaryHUDSource();
            if (src != null) possessIcon.Bind(src);
            else Debug.LogWarning("[AbilityHUDController] Lyra primary does not implement IAbilityHUDSource.");
        }

        // Stun (Kai primary) — always unlocked
        if (kaiController != null && stunIcon != null)
        {
            var src = kaiController.GetPrimaryHUDSource();
            if (src != null) stunIcon.Bind(src);
            else Debug.LogWarning("[AbilityHUDController] Kai primary does not implement IAbilityHUDSource.");
        }

        // Gate (teleport on either twin — use Lyra's; both share same cooldown data)
        if (lyraController != null && gateIcon != null)
        {
            var src = lyraController.GetTeleportHUDSource();
            if (src != null) gateIcon.Bind(src);
            else Debug.LogWarning("[AbilityHUDController] Teleport does not implement IAbilityHUDSource.");
        }
    }

    private void BindUnlockEvents()
    {
        // Unlockable passives have no runtime cooldown — they show as "active" once unlocked.
        // Bind a NullHUDSource that always returns CooldownProgress=1, IsActive=true.
        if (dualCastIcon != null) dualCastIcon.Bind(new PassiveHUDSource("Dual Cast"));
        if (coalesceIcon != null) coalesceIcon.Bind(new PassiveHUDSource("Coalesce"));
        if (soulConvIcon != null) soulConvIcon.Bind(new PassiveHUDSource("Soul Convergence"));
        if (healthRegenIcon != null) healthRegenIcon.Bind(new PassiveHUDSource("HP Regen"));
    }

    // ── Passive source (no cooldown) ──────────────────────────
    private class PassiveHUDSource : IAbilityHUDSource
    {
        public string AbilityName { get; }
        public float CooldownProgress => 1f;
        public int CurrentCharges => 1;
        public int MaxCharges => 1;
        public bool IsActive => true;

        public PassiveHUDSource(string name) { AbilityName = name; }
    }
}