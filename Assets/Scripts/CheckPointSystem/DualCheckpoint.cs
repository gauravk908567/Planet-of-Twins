using UnityEngine;
using TMPro;

/// <summary>
/// Dual-node co-op checkpoint (game.md §11.2). The save fires only when BOTH twins are present — one on each
/// node (any twin, any node) — and both HOLD X together. While ANY twin stands on a node the checkpoint CLAIMS
/// X (via <see cref="CheckpointManager"/>) so Accord can't charge on the same hold; on the nodes X means
/// "save" (or, solo, the come-here flash). Guarded: both alive, neither in rescue, no active ability
/// (Coalesce EXEMPT). Firing calls <see cref="CheckpointManager.SaveCheckpoint"/> (full §11.1 capture).
///
/// <para>R1: nodes + prompt are same-scene children. R4: resolves CheckpointManager / rescue by singleton at
/// play time. Reuses <see cref="PlayerInputRouter.For"/> + <see cref="JointHoldSync"/> (the Accord/SC idiom).</para>
///
/// <para>Prompt text is driven here; the burst VFX + the solo-attempt flash (with its interval-reset timer)
/// are the deferred visual pass — this exposes <see cref="CurrentState"/>, <see cref="HoldProgress"/> and
/// <see cref="OnSoloSaveAttempt"/> for it to consume.</para>
/// </summary>
public class DualCheckpoint : MonoBehaviour
{
    [Header("Nodes (same-scene children — R1)")]
    [SerializeField] private CheckpointNode _nodeA;
    [SerializeField] private CheckpointNode _nodeB;

    [Header("Save")]
    [Tooltip("This area's WorldLocationSO — REQUIRED so Continue/respawn streams the right chunk (§11.1). " +
             "Surfaced by the dashboard checkpoint inspector; a save with no location can't resolve its area.")]
    [SerializeField] private WorldLocationSO _location;
    [Tooltip("Save once, then this checkpoint stays inert for the session. Off = re-saves each fresh visit.")]
    [SerializeField] private bool _saveOnce = true;

    [Header("Activation — both twins HOLD X (§11.2)")]
    [Tooltip("Seconds both players must hold X together while both stand on the nodes. UNSCALED.")]
    [SerializeField, Min(0.05f)] private float _holdSeconds = 0.9f;
    [Tooltip("Sync leniency (s) between the two X-holds — same idiom as the Accord/SC joint powers.")]
    [SerializeField, Range(0f, 1.5f)] private float _jointLeniency = 0.5f;

    [Header("Prompt (world-space text — R1; optional)")]
    [Tooltip("World-space TMP text near the checkpoint: BRIGHT 'both' line when both twins are present, GREY 'one' " +
             "line when only one is, hidden otherwise (§11.2).")]
    [SerializeField] private TMP_Text _promptText;
    [Tooltip("Both twins present. {Cancel} = the save button: ONE glyph if both players use the same device kind, " +
             "else each player's own (keyboard key + pad button), P1 first.")]
    [SerializeField] private string _bothTemplate = "Hold {Cancel} to save checkpoint";
    [Tooltip("One twin present. {Cancel} = THAT player's own device glyph.")]
    [SerializeField] private string _oneTemplate  = "Hold {Cancel} to save";
    [SerializeField] private Color _brightColor = Color.white;
    [SerializeField] private Color _greyColor   = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    // Read-only views for the Scene Health "Checkpoints" recipe + GameDebuggerV2 live section.
    public WorldLocationSO Location => _location;
    public CheckpointNode NodeA => _nodeA;
    public CheckpointNode NodeB => _nodeB;
    public TMP_Text PromptText => _promptText;
    public bool IsSpent => _saveOnce && _saved;

    public enum State { BothVacant, OneOccupied, BothOccupied }

    /// <summary>Occupancy state for the visual burst state machine (§11.2).</summary>
    public State CurrentState { get; private set; } = State.BothVacant;
    /// <summary>0..1 progress of the joint X-hold (for the visual pass).</summary>
    public float HoldProgress { get; private set; }
    /// <summary>Fired (rising edge) when a lone twin on a node presses X — the vacant node to flash "come here"
    /// (§11.2). The visual pass owns the flash + its interval-reset timer; this just signals the attempt.</summary>
    public event System.Action<CheckpointNode> OnSoloSaveAttempt;

    private readonly JointHoldSync _jointSync = new JointHoldSync();
    private float _holdTimer;          // unscaled
    private bool _saved;               // saveOnce latch
    private bool _firedThisVisit;      // re-arm guard: one save per both-present visit
    private bool _claimingInput;       // whether we currently hold the Accord-X claim
    private bool _soloHoldPrev;        // rising-edge tracker for the solo-attempt signal
    private int _promptKey = int.MinValue;   // prompt rebuilt only when state/occupant/device kinds change (no per-frame TMP alloc)
    private IRescueActive _rescue;

    private void Start()
    {
        if (_nodeA == null || _nodeB == null)
        {
            Debug.LogError("[DualCheckpoint] Node A/B unassigned — checkpoint disabled.", this);
            enabled = false;
            return;
        }
        if (_location == null)
            Debug.LogWarning("[DualCheckpoint] No WorldLocationSO wired — a save here won't resolve its area " +
                             "on Continue/respawn (§11.2). Wire this area's WorldLocationSO.", this);
        _rescue = RescueEventController.Instance as IRescueActive;   // R4
        if (_promptText != null) _promptText.gameObject.SetActive(false);
    }

    private void OnDisable() => ReleaseClaim();   // area unload — never leak the Accord suppression

    private void Update()
    {
        var a = _nodeA.Occupant;
        var b = _nodeB.Occupant;
        int occupied = (a != null ? 1 : 0) + (b != null ? 1 : 0);
        CurrentState = occupied == 0 ? State.BothVacant : occupied == 1 ? State.OneOccupied : State.BothOccupied;

        bool spent = _saveOnce && _saved;
        UpdatePrompt(spent, a, b);

        // Claim X for the whole checkpoint zone whenever anyone stands on a node (§11.2): on the nodes X means
        // "save" (or the solo come-here flash), never "charge Accord". A spent checkpoint releases its claim.
        if (occupied >= 1 && !spent) Claim(); else ReleaseClaim();

        // Solo attempt → signal the vacant node (visual flash consumes this, §11.2 visual pass).
        HandleSoloAttempt(a, b, occupied);

        bool bothPresent = a != null && b != null && a != b;   // two DISTINCT twins, one per node
        if (!bothPresent) { _firedThisVisit = false; ResetHoldTimer(); return; }
        if (spent) { ResetHoldTimer(); return; }
        if (_firedThisVisit) { ResetHoldTimer(); return; }     // already saved this visit; wait until they leave
        if (!GuardsPass(a, b)) { ResetHoldTimer(); return; }   // rescued / dead / ability active → no save

        var ia = PlayerInputRouter.For(a);
        var ib = PlayerInputRouter.For(b);
        bool bothHold = ia != null && ib != null &&
                        _jointSync.Tick(ia.GetCancelHeld(), ib.GetCancelHeld(), _jointLeniency, Time.unscaledDeltaTime);

        if (bothHold)
        {
            _holdTimer += Time.unscaledDeltaTime;
            HoldProgress = Mathf.Clamp01(_holdTimer / _holdSeconds);
            if (_holdTimer >= _holdSeconds) FireSave();
        }
        else ResetHoldTimer();
    }

    private void UpdatePrompt(bool spent, Player a, Player b)
    {
        if (_promptText == null) return;
        if (spent || CurrentState == State.BothVacant)
        {
            if (_promptText.gameObject.activeSelf) _promptText.gameObject.SetActive(false);
            _promptKey = int.MinValue;
            return;
        }
        if (!_promptText.gameObject.activeSelf) _promptText.gameObject.SetActive(true);

        // One twin → that player's own glyph. Both → both players' glyphs (slot order; one icon if same device kind).
        bool both = CurrentState == State.BothOccupied;
        IInputProvider first  = both ? PlayerInputRouter.ForSlot(PlayerSlot.One) : PlayerInputRouter.For(a != null ? a : b);
        IInputProvider second = both ? PlayerInputRouter.ForSlot(PlayerSlot.Two) : null;

        // Device kind is in the key so the glyph follows a keyboard↔pad switch (solo last-used) while shown.
        int key = System.HashCode.Combine(CurrentState, first, InputGlyphResolver.ResolveKind(first),
                                          second, second != null ? InputGlyphResolver.ResolveKind(second) : InputDeviceKind.KeyboardMouse);
        if (key == _promptKey) return;
        _promptKey = key;

        if (both) { InputGlyphText.ApplyJointFormat(_promptText, _bothTemplate, first, second); _promptText.color = _brightColor; }
        else      { InputGlyphText.Apply(_promptText, _oneTemplate, first);                    _promptText.color = _greyColor;  }
    }

    // Rising-edge "a lone twin on a node pressed X" → the OTHER (vacant) node should flash (§11.2 visual pass).
    private void HandleSoloAttempt(Player a, Player b, int occupied)
    {
        if (occupied != 1) { _soloHoldPrev = false; return; }
        Player occupant = a != null ? a : b;
        CheckpointNode vacant = a != null ? _nodeB : _nodeA;
        var inp = PlayerInputRouter.For(occupant);
        bool held = inp != null && inp.GetCancelHeld();
        if (held && !_soloHoldPrev) OnSoloSaveAttempt?.Invoke(vacant);
        _soloHoldPrev = held;
    }

    private bool GuardsPass(Player a, Player b)
    {
        if (a.Health == null || a.Health.IsDead || b.Health == null || b.Health.IsDead) return false;   // both alive
        if (_rescue != null && (_rescue.IsRescueActive || _rescue.HasActiveRescueTarget)) return false;  // no rescue
        if (AnyAbilityActive()) return false;                                                            // no active ability
        return true;
    }

    // Mirrors SaveService.WarnIfAbilityActiveAtSave, but HARD-blocks the save here (§11.2). Coalesce is EXEMPT.
    private static bool AnyAbilityActive()
    {
        if (SoulConvergenceSystem.Instance != null && SoulConvergenceSystem.Instance.IsAbilityActive) return true;
        var acc = AccordStateSystem.Instance;
        if (acc != null && (acc.IsAccordActive
            || (acc.VoidStrikeActiveState?.IsAbilityActive ?? false)
            || (acc.RadiantSeekerActiveState?.IsAbilityActive ?? false))) return true;
        if (SetsunaSystem.Instance != null && SetsunaSystem.Instance.IsAbilityActive) return true;
        if (EmpowerSystem.Instance != null && EmpowerSystem.Instance.IsAbilityActive) return true;
        return false;
    }

    private void FireSave()
    {
        var cpm = CheckpointManager.Instance;
        if (cpm == null) { Debug.LogError("[DualCheckpoint] CheckpointManager.Instance null — cannot save.", this); return; }

        // Payload is per-twin (left = TwinA, right = TwinB), NOT node order — nodes are any-twin-any-node, but
        // the save/restore contract is keyed to roster identity. Read the roster's live positions.
        var roster = PlayerRoster.Instance;
        Vector3 left  = roster?.TwinA != null ? roster.TwinA.transform.position : transform.position;
        Vector3 right = roster?.TwinB != null ? roster.TwinB.transform.position : transform.position;
        cpm.SaveCheckpoint(left, right, _location);

        _saved = true;
        _firedThisVisit = true;
        ResetHoldTimer();
    }

    private void ResetHoldTimer()
    {
        _holdTimer = 0f;
        HoldProgress = 0f;
        _jointSync.Reset();
    }

    // ── Accord-X claim (§11.2): while a twin stands on a node, Accord is suppressed so X means "save". ──
    private void Claim()
    {
        if (_claimingInput) return;
        CheckpointManager.Instance?.RequestInputClaim();
        _claimingInput = true;
    }

    private void ReleaseClaim()
    {
        if (!_claimingInput) return;
        CheckpointManager.Instance?.ReleaseInputClaim();
        _claimingInput = false;
    }
}
