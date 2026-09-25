using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// GroupGrab enemy — gets behind twin, holds them while allies pile on.
/// All config centralised in GroupGrabEnemyData SO.
/// Base EnemyData fields used for TTK, mash, heal values.
/// </summary>
public class GroupGrabEnemy : Enemy, IRescueTarget
{
    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.GroupGrab;
    protected override string MeleeAttackCueId => FxIds.Enemy.GroupGrab.On_wardenAttack;
    // While holding a twin the warden is absorbing, not slashing — mute the slash + hit spark
    // so the soul-drain cue is the only attack visual (playtest round 2).
    public override bool SuppressBasicAttackCues => _isGrabbing;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask _enemyLayer;

    private GroupGrabEnemyData _grabData;

    // Grab state
    private bool _isResolved;
    private bool _grabOnCooldown;
    private bool _isGrabbing;
    private bool _ttkPaused;
    private float _ttkRemaining;
    private Player _grabbedPlayer;
    private Coroutine _strugglePauseCoroutine;
    private CueHandle _soulConsumeHandle;   // HELD warden→twin drain (GroupGrab book); stopped at every grab resolution

    private readonly Collider[] _pileBuffer = new Collider[8];

    // ── Public accessors for BT actions and debug ──────────────
    public bool IsGrabbing => _isGrabbing;
    public bool GrabOnCooldown => _grabOnCooldown;
    public GroupGrabEnemyData GrabData => _grabData;
    public LayerMask EnemyLayer => _enemyLayer;

    // ── IRescueTarget ──────────────────────────────────────────
    public Transform GrabbedPlayerTransform => _grabbedPlayer?.transform;
    public Player GrabbedPlayer => _grabbedPlayer;
    public float NormalisedTTK => Data != null && Data.timeToKill > 0f
                                                ? _ttkRemaining / Data.timeToKill : 0f;
    public float MashFrequency => Data?.mashFrequency ?? 4f;
    public float MashWindowDuration => Data?.mashWindowDuration ?? 3f;
    public float MashCooldown => Data?.mashCooldown ?? 0.75f;
    public float PartialHealAmount => Data?.partialHealAmount ?? 20f;
    public bool CanGrabbedPlayerStruggle => (_grabData?.trapTier ?? 2) == 1;
    public float StrugglePauseDuration => _grabData?.strugglePauseDuration ?? 0.4f;
    public float RescueProximityRadius => 1.5f;

    public event Action<Player> OnPlayerGrabbed;
    public event Action OnPlayerReleased;
    public event Action OnPlayerKilled;

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is GroupGrabEnemyData grabData)
            _grabData = grabData;
        else
            Debug.LogWarning($"[GroupGrabEnemy] Expected GroupGrabEnemyData, got {data?.GetType().Name}", this);
    }

    private void Update()
    {
        if (_isGrabbing && !_ttkPaused)
        {
            _ttkRemaining -= Time.deltaTime;
            if (_ttkRemaining <= 0f)
                KillPlayer();
        }
    }

    // ── Called by BTActionGrab ─────────────────────────────────
    public bool StartGrab()
    {
        if (Target == null || _grabOnCooldown) return false;

        var player = Target.GetComponent<Player>();
        if (player == null || player.IsGrabbed) return false;

        _isResolved = false;
        _grabbedPlayer = player;
        _grabbedPlayer.SetGrabbed(true);
        _ttkRemaining = Data?.timeToKill ?? 5f;
        _ttkPaused = false;
        _isGrabbing = true;

        if ((_grabData?.trapTier ?? 2) >= 2)
            (_grabbedPlayer.Movement as IMovementFreezable)?.SetFrozen(true);

        OnPlayerGrabbed?.Invoke(player);
        // Grab-catch burst on the grabbed twin (GroupGrab book) — the snap moment; rides the player.
        PlayCue(FxIds.Enemy.GroupGrab.On_wardenGrab, CueContext.Follow(player.transform));
        // Sustained soul-drain: originates on the warden, AIMS at the grabbed twin (face-target), held for the
        // whole grab — stopped at every resolution (release / kill / death) via StopSoulConsume().
        _soulConsumeHandle = PlayCue(FxIds.Enemy.GroupGrab.On_wardenGrabSoulConsume,
                                     CueContext.FollowFacing(transform, player.transform, owner: this));
        MoodEventBus.AllyGrabbedTwin(player.gameObject);
        FactionEnergySystem.Instance?.OnGrabPileActive();
        AlertNearbyEnemies(player.transform, isGrab: true);
        PoTWorldStateWriter.Instance?.NotifyTargetEngaged(player.gameObject, true);

        return true;
    }

    public void EndGrab()
    {
        StopSoulConsume();
        _grabbedPlayer?.SetGrabbed(false);
        _isGrabbing = false;
        _grabbedPlayer = null;
        PoTWorldStateWriter.Instance?.NotifyTargetEngaged(null, false);
    }

    // Stop the held warden→twin drain (idempotent; a stale handle is inert, so double-stop is safe).
    private void StopSoulConsume()
    {
        if (_soulConsumeHandle.IsNone) return;
        FxManager.Instance?.Stop(_soulConsumeHandle);
        _soulConsumeHandle = CueHandle.None;
    }

    // ── Alert nearby enemies ───────────────────────────────────
    public void AlertNearbyEnemies(Transform playerTransform, bool isGrab)
    {
        float range = isGrab
            ? (_grabData?.grabAlertRange ?? 14f)
            : (_grabData?.chaseAlertRange ?? 6f);

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, range, _pileBuffer, _enemyLayer);

        for (int i = 0; i < count; i++)
        {
            var receiver = _pileBuffer[i].GetComponent<IAlertReceiver>();
            if (receiver == null || (object)receiver == this) continue;

            if (isGrab) receiver.OnGrabAlert(playerTransform);
            else receiver.OnChaseAlert(playerTransform);
        }
    }

    // ── IRescueTarget ──────────────────────────────────────────
    public void PauseTTK() => _ttkPaused = true;
    public void ResumeTTK() => _ttkPaused = false;

    public void ReleasePlayer(float healAmount)
    {
        if (!_isGrabbing || _isResolved) return;
        _isResolved = true;
        StopSoulConsume();

        if ((_grabData?.trapTier ?? 2) >= 2)
            (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(false);

        _grabbedPlayer?.Health?.Heal(healAmount);
        _grabbedPlayer?.SetGrabbed(false);
        _isGrabbing = false;

        StartCoroutine(GrabCooldownRoutine());
        _grabbedPlayer = null;
        OnPlayerReleased?.Invoke();
        MoodEventBus.TwinEscaped(_grabbedPlayer?.gameObject);
        PoTWorldStateWriter.Instance?.NotifyTargetEngaged(null, false);
    }

    public void KillPlayer()
    {
        if (!_isGrabbing || _isResolved) return;
        _isResolved = true;
        StopSoulConsume();

        if ((_grabData?.trapTier ?? 2) >= 2)
            (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(false);

        _grabbedPlayer?.SetGrabbed(false);

        var proxy = _grabbedPlayer?.GetComponent<PlayerDeathRescueProxy>();
        _isGrabbing = false;

        if (proxy != null && proxy.IsActive)
            proxy.KillPlayer();
        else
        {
            _grabbedPlayer?.Health?.TakeDamage(
                new DamageData(9999f, DamageType.Environmental));
            OnPlayerKilled?.Invoke();
            MoodEventBus.TwinDowned(_grabbedPlayer?.gameObject);
        }

        _grabbedPlayer = null;
        PoTWorldStateWriter.Instance?.NotifyTargetEngaged(null, false);
    }

    public void OnStruggle()
    {
        if (!CanGrabbedPlayerStruggle || !_isGrabbing) return;
        // Struggle feedback on the grabbed twin (GroupGrab book) — fires each mash beat; rides the player.
        PlayCue(FxIds.Enemy.GroupGrab.On_wardenGrabStruggle, CueContext.Follow(_grabbedPlayer.transform));
        if (_strugglePauseCoroutine != null)
            StopCoroutine(_strugglePauseCoroutine);
        _strugglePauseCoroutine = StartCoroutine(StrugglePauseRoutine());
    }

    private IEnumerator StrugglePauseRoutine()
    {
        PauseTTK();
        yield return new WaitForSeconds(_grabData?.strugglePauseDuration ?? 0.4f);
        ResumeTTK();
        _strugglePauseCoroutine = null;
    }

    private IEnumerator GrabCooldownRoutine()
    {
        _grabOnCooldown = true;
        yield return new WaitForSeconds(_grabData?.grabCooldownAfterRescue ?? 3f);
        _grabOnCooldown = false;
    }

    public override void ReceiveKnockback(KnockbackData data)
    {
        if (_isGrabbing) return;
        base.ReceiveKnockback(data);
    }

    protected override void HandleDeath()
    {
        if (_isGrabbing)
        {
            _grabbedPlayer?.SetGrabbed(false);
            ReleasePlayer(0f);
        }
        base.HandleDeath();
    }
}