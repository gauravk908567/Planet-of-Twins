using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Execute combo attack based on ActivePowerID.
/// All cases are STUBS — prints powerID and plays VFX.
/// Replace each case with real attack logic when animations/data ready.
/// Uses ComboPowerIDs constants — no string literals.
/// </summary>
public class BTActionComboAttack : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "ComboAttack";

    private EnemyProximityPower _proximityPower;
    private string _currentPowerID;
    private float _attackTimer;
    private const float StubDuration = 1.5f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _proximityPower = _enemy?.GetComponent<EnemyProximityPower>();
        _currentPowerID = _proximityPower?.ActivePowerID;
        _attackTimer = 0f;

        if (string.IsNullOrEmpty(_currentPowerID))
        {
            Debug.LogWarning($"[ComboAttack] {_enemy?.name} entered with no powerID");
            return;
        }

        ExecuteComboStart(_currentPowerID);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_proximityPower == null || string.IsNullOrEmpty(_currentPowerID))
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (!_proximityPower.InPact)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        _attackTimer += InDeltaTime;
        if (_attackTimer >= StubDuration)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _attackTimer = 0f;
    }

    private void ExecuteComboStart(string id)
    {
        var vfx = _enemy?.GetComponentInChildren<EnemyVFXController>();

        switch (id)
        {
            // ── TetherBreaker ──────────────────────────────
            case ComboPowerIDs.TB_PinHold:
                Debug.Log($"[Combo] {_enemy?.name} TB_PinHold — chain thrown, holding pin, not dragging");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.TB_DragToAlly:
                Debug.Log($"[Combo] {_enemy?.name} TB_DragToAlly — dragging twin toward ally position");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.TB_Standard:
                Debug.Log($"[Combo] {_enemy?.name} TB_Standard — normal chain, ally coordinates");
                vfx?.PlayRage();
                break;

            // ── BasicMelee ─────────────────────────────────
            case ComboPowerIDs.Melee_ChargeOnPin:
                Debug.Log($"[Combo] {_enemy?.name} Melee_ChargeOnPin — charging pinned twin, fast attacks");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.Melee_PrePosition:
                Debug.Log($"[Combo] {_enemy?.name} Melee_PrePosition — moving to drag destination");
                vfx?.PlayBuff();
                break;

            case ComboPowerIDs.Melee_CornerAssist:
                Debug.Log($"[Combo] {_enemy?.name} Melee_CornerAssist — cornering twin for chain");
                vfx?.PlayBuff();
                break;

            // ── Severed ────────────────────────────────────
            case ComboPowerIDs.Sev_EarlyRage:
                Debug.Log($"[Combo] {_enemy?.name} Sev_EarlyRage — grief rage on combo activation");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.Sev_FlankAttack:
                Debug.Log($"[Combo] {_enemy?.name} Sev_FlankAttack — attacking from opposite side");
                vfx?.PlayRage();
                break;

            // ── GroupGrab ──────────────────────────────────
            case ComboPowerIDs.GG_GrabAndHold:
                Debug.Log($"[Combo] {_enemy?.name} GG_GrabAndHold — grabbing twin, partner piles at max priority");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.GG_GrabAndDistract:
                Debug.Log($"[Combo] {_enemy?.name} GG_GrabAndDistract — grabbing one twin, partner attacks other");
                vfx?.PlayRage();
                break;

            // ── Siphon ─────────────────────────────────────
            case ComboPowerIDs.Siph_EarlyGhost:
                Debug.Log($"[Combo] {_enemy?.name} Siph_EarlyGhost — spawning ghost on combo not rescue");
                vfx?.PlayDarkEnergy();
                break;

            case ComboPowerIDs.Siph_ExtendedBomb:
                Debug.Log($"[Combo] {_enemy?.name} Siph_ExtendedBomb — bomb trigger range extended");
                vfx?.PlayDarkEnergy();
                break;

            // ── Ranged ─────────────────────────────────────
            case ComboPowerIDs.Range_SuppressiveFire:
                Debug.Log($"[Combo] {_enemy?.name} Range_SuppressiveFire — pinning twin with arrows, ally closes in");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.Range_ExecutionShot:
                Debug.Log($"[Combo] {_enemy?.name} Range_ExecutionShot — high damage shot on grabbed twin");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.Range_Standard:
                Debug.Log($"[Combo] {_enemy?.name} Range_Standard — normal ranged with combo awareness");
                vfx?.PlayBuff();
                break;

            // ── Witness ────────────────────────────────────
            case ComboPowerIDs.Wit_IntenseBuff:
                Debug.Log($"[Combo] {_enemy?.name} Wit_IntenseBuff — double buff strength active");
                vfx?.PlayBuff();
                break;

            case ComboPowerIDs.Wit_Standard:
                Debug.Log($"[Combo] {_enemy?.name} Wit_Standard — normal shadow and buff");
                vfx?.PlayBuff();
                break;

            // ── Summoner ───────────────────────────────────
            case ComboPowerIDs.Sum_EmergencySummon:
                Debug.Log($"[Combo] {_enemy?.name} Sum_EmergencySummon — summoning immediately");
                vfx?.PlayBuff();
                break;

            case ComboPowerIDs.Sum_Standard:
                Debug.Log($"[Combo] {_enemy?.name} Sum_Standard — normal summon timing");
                vfx?.PlayBuff();
                break;

            // ── Penitent ───────────────────────────────────
            case ComboPowerIDs.Pen_ReflectAmplify:
                Debug.Log($"[Combo] {_enemy?.name} Pen_ReflectAmplify — reflection damage boosted");
                vfx?.PlayRage();
                break;

            case ComboPowerIDs.Pen_Standard:
                Debug.Log($"[Combo] {_enemy?.name} Pen_Standard — normal approach with reflection");
                vfx?.PlayBuff();
                break;

            // ── Generic ────────────────────────────────────
            case ComboPowerIDs.ThirdWheelBoost:
                Debug.Log($"[Combo] {_enemy?.name} ThirdWheelBoost — boosted attack as third pact member");
                vfx?.PlayBuff();
                break;

            default:
            case ComboPowerIDs.FallbackCombo:
                Debug.Log($"[Combo] {_enemy?.name} FallbackCombo — generic enhanced attack");
                break;
        }
    }
}