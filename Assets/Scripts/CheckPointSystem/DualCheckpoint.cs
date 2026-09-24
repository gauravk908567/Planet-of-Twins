using UnityEngine;
using UnityEngine.Serialization;
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
/// <para>After a save the checkpoint is CONSUMED (prompts hidden, no saving) and re-arms after
/// <c>_rearmSeconds</c> — big games keep save points reusable, and ours heals / refills nothing, so there is nothing
/// to exploit (§11.2 "Re-use rule"). Saving again also needs both twins to step off and back on.</para>
///
/// <para>Prompt text is driven here. The node visuals (<see cref="CheckpointNodeVisual"/>) READ this: they poll
/// <see cref="CurrentState"/> / <see cref="HoldProgress"/> / <see cref="IsConsumed"/> and listen to
/// <see cref="Saved"/>, <see cref="Rearmed"/> and <see cref="OnSoloSaveAttempt"/>.</para>
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
    [Tooltip("After a save the checkpoint looks consumed, then comes back after this many seconds (§11.2 — long " +
             "enough that players think it's gone). 0 = it stays consumed while this area is loaded. SCALED time.")]
    [SerializeField, Min(0f)] private float _rearmSeconds = 13f;

    [Header("Activation — both twins HOLD X (§11.2)")]
    [Tooltip("Seconds both players must hold X together while both stand on the nodes. UNSCALED.")]
    [SerializeField, Min(0.05f)] private float _holdSeconds = 0.9f;
    [Tooltip("Sync leniency (s) between the two X-holds — same idiom as the Accord/SC joint powers.")]
    [SerializeField, Range(0f, 1.5f)] private float _jointLeniency = 0.5f;

    [Header("Prompts — one per node (each node's CheckpointNodePrompt child, wired on the node)")]
    [Tooltip("{Cancel} = the save button of the twin ON that node — or, on the empty node, of the partner who still " +
             "has to get there. BRIGHT when both twins are on their nodes, GREY while one waits, hidden when nobody is " +
             "on a node or the checkpoint is consumed (§11.2).")]
    [FormerlySerializedAs("_oneTemplate")]
    [SerializeField] private string _nodeTemplate = "Hold {Cancel} to save";
    [SerializeField] private Color _brightColor = Color.white;
    [SerializeField] private Color _greyColor   = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    // Read-only views for the Scene Health "Checkpoints" recipe + GameDebuggerV2 live section.
    public WorldLocationSO Location => _location;
    public CheckpointNode NodeA => _nodeA;
    public CheckpointNode NodeB => _nodeB;
    /// <summary>True from a save until the checkpoint re-arms (§11.2 "Re-use rule").</summary>
    public bool IsConsumed => _consumed;

    public enum State { BothVacant, OneOccupied, BothOccupied }

    /// <summary>Occupancy state for the node visuals (§11.2).</summary>
    public State CurrentState { get; private set; } = State.BothVacant;
    /// <summary>0..1 progress of the joint X-hold (drives the hold ring).</summary>
    public float HoldProgress { get; private set; }
    /// <summary>Fired (rising edge) when a lone twin on a node presses X while the checkpoint is live — both node
    /// visuals fire a trail at once ("come here", §11.2). The argument is the vacant node.</summary>
    public event System.Action<CheckpointNode> OnSoloSaveAttempt;
    /// <summary>Fired once when a save goes through — the checkpoint is now consumed.</summary>
    public event System.Action Saved;
    /// <summary>Fired when a consumed checkpoint comes back (<c>_rearmSeconds</c> after the save).</summary>
    public event System.Action Rearmed;

    private readonly JointHoldSync _jointSync = new JointHoldSync();
    private float _holdTimer;          // unscaled
    private bool _consumed;            // saved, waiting to re-arm
    private float _rearmTimer;         // SCALED countdown: pause freezes it, Setsuna slows it (a world beat)
    private bool _firedThisVisit;      // one save per both-present visit — step off and back on to save again
    private bool _claimingInput;       // whether we currently hold the Accord-X claim
    private bool _soloHoldPrev;        // rising-edge tracker for the solo-attempt signal
    private int _promptKeyA = int.MinValue;  // each rebuilt only when brightness/player/device kind change (no per-frame TMP alloc)
    private int _promptKeyB = int.MinValue;
    private IRescueActive _rescue;

    // R8: own children only — start with both node prompts hidden.
    private void Awake()
    {
        HidePrompt(_nodeA);
        HidePrompt(_nodeB);
    }

    private static void HidePrompt(CheckpointNode node)
    {
        if (node != null && node.Prompt != null) node.Prompt.gameObject.SetActive(false);
    }

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
        if (_nodeA.Prompt == null || _nodeB.Prompt == null)
            Debug.LogWarning("[DualCheckpoint] A node has no prompt — place a CheckpointNodePrompt under each node and " +
                             "wire it on the node, or that player won't see which button saves (§11.2).", this);
        _rescue = RescueEventController.Instance as IRescueActive;   // R4
    }

    private void OnDisable() => ReleaseClaim();   // area unload — never leak the Accord suppression

    private void Update()
    {
        var a = _nodeA.Occupant;
        var b = _nodeB.Occupant;
        int occupied = (a != null ? 1 : 0) + (b != null ? 1 : 0);
        CurrentState = occupied == 0 ? State.BothVacant : occupied == 1 ? State.OneOccupied : State.BothOccupied;

        TickRearm();
        UpdatePrompt(_consumed, a, b);

        // Claim X whenever anyone stands on a node (§11.2): on the nodes X means "save" (or the solo come-here call),
        // never "charge Accord". Held through the consumed window too — both players are still holding X on the
        // frame the save fires, and releasing then let a full-bar Accord start (BUG-124).
        if (occupied >= 1) Claim(); else ReleaseClaim();

        // Solo attempt → both node visuals fire a trail (only while the checkpoint is live).
        if (_consumed) _soloHoldPrev = false;
        else HandleSoloAttempt(a, b, occupied);

        bool bothPresent = a != null && b != null && a != b;   // two DISTINCT twins, one per node
        if (!bothPresent) { _firedThisVisit = false; ResetHoldTimer(); return; }
        if (_consumed) { ResetHoldTimer(); return; }
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

    private void UpdatePrompt(bool consumed, Player a, Player b)
    {
        bool show = !consumed && CurrentState != State.BothVacant;
        bool bright = CurrentState == State.BothOccupied;

        // Each node names ITS player: the twin standing on it, or — on the empty node — the partner who still has to
        // get there (the twin that isn't on the other node). Grey while waiting, bright once both are in place.
        UpdateNodePrompt(_nodeA.Prompt, ref _promptKeyA, show, bright, a != null ? a : PartnerOf(b));
        UpdateNodePrompt(_nodeB.Prompt, ref _promptKeyB, show, bright, b != null ? b : PartnerOf(a));
    }

    private void UpdateNodePrompt(TMP_Text prompt, ref int cacheKey, bool show, bool bright, Player player)
    {
        if (prompt == null) return;
        var input = player != null ? PlayerInputRouter.For(player) : null;
        if (!show || input == null)
        {
            if (prompt.gameObject.activeSelf) prompt.gameObject.SetActive(false);
            cacheKey = int.MinValue;
            return;
        }
        if (!prompt.gameObject.activeSelf) prompt.gameObject.SetActive(true);

        // Device kind is in the key so the glyph follows a keyboard↔pad switch (solo last-used) while shown.
        int key = System.HashCode.Combine(bright, input, InputGlyphResolver.ResolveKind(input));
        if (key == cacheKey) return;
        cacheKey = key;

        InputGlyphText.Apply(prompt, _nodeTemplate, input);
        prompt.color = bright ? _brightColor : _greyColor;
    }

    // The twin that is NOT `present`. Null when `present` is null, isn't a roster twin, or the roster isn't up.
    private static Player PartnerOf(Player present) =>
        present != null && PlayerRoster.Instance != null ? PlayerRoster.Instance.Other(present) : null;

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

        _consumed = true;
        _rearmTimer = _rearmSeconds;
        _firedThisVisit = true;
        ResetHoldTimer();
        Saved?.Invoke();
    }

    // Consumed → live again after _rearmSeconds (0 = stays consumed while the area is loaded).
    private void TickRearm()
    {
        if (!_consumed || _rearmSeconds <= 0f) return;
        _rearmTimer -= Time.deltaTime;   // SCALED — see _rearmTimer
        if (_rearmTimer > 0f) return;
        _consumed = false;
        Rearmed?.Invoke();
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
