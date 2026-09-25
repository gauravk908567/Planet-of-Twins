using UnityEngine;

/// <summary>
/// Debug tool — combo key system.
/// Hold CATEGORY key + press ACTION key.
///
/// CATEGORIES:
///   D = Damage     M = Mood       E = Energy
///   G = GroupGrab  S = Severed    T = Tether
///   W = Witness    P = Penitent   X = Siphon
///   R = Rescue     N = Perception B = Bond/Dark Energy
/// </summary>
public class DamageDealerDebug : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 25f;
    [SerializeField] private DamageType _damageType = DamageType.Combat;
    [SerializeField] private float _effectDuration = 3f;

    [Header("Phase 4 — POI")]
    [SerializeField] private SpawnPointPOI _spawnPointTarget;

    [Header("Targets")]
    [SerializeField] private Enemy _specificTarget;
    [SerializeField] private Enemy _moodTarget;
    [SerializeField] private Enemy _perceptionTarget;
    [SerializeField] private Enemy _bondEnemy1;
    [SerializeField] private Enemy _bondEnemy2;
    [SerializeField] private GroupGrabEnemy _grabTarget;
    [SerializeField] private SeveredEnemy _severedTarget;
    [SerializeField] private TetherBreakerEnemy _tetherTarget;
    [SerializeField] private SiphonEnemy _siphonTarget;
    [SerializeField] private WitnessEnemy _witnessTarget;
    [SerializeField] private PenitentEnemy _penitentTarget;
    [SerializeField] private SoulPlayer _soulPlayer;
    [SerializeField] private RescueEventController _rescueController;

    [Header("Commander")]
    [SerializeField] private ChainCommander _chainCommander;
    [SerializeField] private GrandSummoner _grandSummoner;
    [SerializeField] private PenitentCommander _penitentCommander;

    [Header("Sound Settings")]
    [SerializeField] private float _soundRadius = 10f;

    private bool Cat(KeyCode k) => Input.GetKey(k);

    private void Start()
    {
        // P13 ship-safety: Trainer-gated like every other debug surface. Extra reason to stay
        // OFF even in dev: the category keys (D/S/E/W...) collide with WASD movement — holding
        // D to walk right arms the Damage category. GameDebuggerV2 (TestLab) supersedes this.
        if (!DevConfig.Trainer) { enabled = false; return; }
    }

    private void Update()
    {
        // ── D: Damage ─────────────────────────────────────
        if (Cat(KeyCode.D))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) DamageAll(_damageAmount);
            if (Input.GetKeyDown(KeyCode.Alpha2)) DamageAll(9999f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) DamageTarget(_specificTarget, _damageAmount);
            if (Input.GetKeyDown(KeyCode.Alpha4)) StunTarget(_specificTarget);
            if (Input.GetKeyDown(KeyCode.Alpha5)) PossessTarget(_specificTarget);
            if (Input.GetKeyDown(KeyCode.Alpha6)) PrintTarget(_specificTarget);
        }

        // ── M: Mood ───────────────────────────────────────
        if (Cat(KeyCode.M))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForceMood(EnemyMood.Opportunistic, 8f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ForceMood(EnemyMood.Aggressive, 10f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ForceMood(EnemyMood.Panicked, 2f, EnemyMood.Aggressive);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ForceMood(EnemyMood.Frustrated, 3f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha5)) ForceMood(EnemyMood.Grieving, 4f, EnemyMood.Enraged);
            if (Input.GetKeyDown(KeyCode.Alpha6)) ForceMood(EnemyMood.Contemptuous, 0f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha7)) ForceMood(EnemyMood.Enraged, 0f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha8)) ForceMood(EnemyMood.Wounded, 0f, EnemyMood.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha9)) ResetMood();
            if (Input.GetKeyDown(KeyCode.Alpha0)) PrintAllMoods();
        }

        // ── E: Faction Energy ─────────────────────────────
        if (Cat(KeyCode.E))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) FactionEnergySystem.Instance?.AddEnergy(10f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) FactionEnergySystem.Instance?.DrainEnergy(10f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) FactionEnergySystem.Instance?.AddEnergy(25f);
            if (Input.GetKeyDown(KeyCode.Alpha4)) FactionEnergySystem.Instance?.DrainEnergy(25f);
            if (Input.GetKeyDown(KeyCode.Alpha5)) ForceFactionBurst();
            if (Input.GetKeyDown(KeyCode.Alpha6)) ForceFactionDepleted();
            if (Input.GetKeyDown(KeyCode.Alpha7)) PrintEnergyState();
        }

        // ── G: GroupGrab ──────────────────────────────────
        if (Cat(KeyCode.G))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForceGrab();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ForceRelease();
            if (Input.GetKeyDown(KeyCode.Alpha3)) ForceKillGrabbed();
            if (Input.GetKeyDown(KeyCode.Alpha4)) PrintGrabState();
        }

        // ── S: Severed ────────────────────────────────────
        if (Cat(KeyCode.S))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForcePartnerDie();
            if (Input.GetKeyDown(KeyCode.Alpha2)) PrintSeveredState();
        }

        // ── T: TetherBreaker ──────────────────────────────
        if (Cat(KeyCode.T))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForceChainAttack();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ForceRage();
            if (Input.GetKeyDown(KeyCode.Alpha3)) ForceBreakChain();
            if (Input.GetKeyDown(KeyCode.Alpha4)) PrintTetherState();
        }

        // ── W: Witness ────────────────────────────────────
        if (Cat(KeyCode.W))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) KillWitnessAlly();
            if (Input.GetKeyDown(KeyCode.Alpha2)) PrintWitnessState();
            if (Input.GetKeyDown(KeyCode.Alpha3)) PrintWitnessPOIState(); // ← new
        }

        // ── P: Penitent ───────────────────────────────────
        if (Cat(KeyCode.P))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForcePenitentCrush();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ForcePenitentReflection();
            if (Input.GetKeyDown(KeyCode.Alpha3)) PrintPenitentState();
            if (Input.GetKeyDown(KeyCode.Alpha4)) PrintPenitentMood();
        }

        // ── X: Siphon ─────────────────────────────────────
        if (Cat(KeyCode.X))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForcePanicBomb();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ForceGhostSpawn();
            if (Input.GetKeyDown(KeyCode.Alpha3)) KillGhost();
        }

        // ── R: Rescue ─────────────────────────────────────
        if (Cat(KeyCode.R))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) FakeRescueActive();
            if (Input.GetKeyDown(KeyCode.Alpha2)) FakeSoulArrived();
        }

        // ── N: Perception / Sound ─────────────────────────
        if (Cat(KeyCode.N))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) FireSoundAtTarget();
            if (Input.GetKeyDown(KeyCode.Alpha2)) FireSoundFar();
            if (Input.GetKeyDown(KeyCode.Alpha3)) FireSoundAtEnemy();
            if (Input.GetKeyDown(KeyCode.Alpha4)) PrintPerceptionState();
            if (Input.GetKeyDown(KeyCode.Alpha5)) ForceGiveUpSearch();
            if (Input.GetKeyDown(KeyCode.Alpha6)) PrintAllPerceptionStates();
        }

        // ── B: Bond / Dark Energy ─────────────────────────
        if (Cat(KeyCode.B))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) BondEnemies();
            if (Input.GetKeyDown(KeyCode.Alpha2)) PrintDarkEnergyState();
            if (Input.GetKeyDown(KeyCode.Alpha3)) AddDarkEnergy();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ForceBreakBond();
            if (Input.GetKeyDown(KeyCode.Alpha5)) PrintComboRegistry();
            if (Input.GetKeyDown(KeyCode.Alpha6)) ForceComboForm();
            if (Input.GetKeyDown(KeyCode.Alpha7)) BreakActiveCombo();
            if (Input.GetKeyDown(KeyCode.Alpha8)) DamageNearestSpawn();
        }

        if (Cat(KeyCode.C))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) PrintCommanderState();
            if (Input.GetKeyDown(KeyCode.Alpha2)) KillCommander();
            if (Input.GetKeyDown(KeyCode.Alpha3)) ForceCommanderGroup();
            if (Input.GetKeyDown(KeyCode.Alpha4)) DamageCommanderSoldier();
        }
    }

    // ═══════════════════════════════════════════════════════
    // DAMAGE / GENERAL
    // ═══════════════════════════════════════════════════════
    private void DamageAll(float amount)
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e.Health.IsDead) continue;
            e.Health.TakeDamage(new DamageData(amount, _damageType));
            Debug.Log($"[Debug] Dealt {amount} to {e.name} HP:{e.Health.CurrentHealth:F0}/{e.Health.MaxHealth:F0}");
        }
    }

    private void DamageTarget(Enemy t, float amount)
    {
        if (t == null) { Debug.LogWarning("[Debug] No target."); return; }
        t.Health.TakeDamage(new DamageData(amount, _damageType));
        Debug.Log($"[Debug] Dealt {amount} to {t.name} HP:{t.Health.CurrentHealth:F0}");
    }

    private void StunTarget(Enemy t)
    {
        if (t == null) { Debug.LogWarning("[Debug] No target."); return; }
        t.ApplyStun(_effectDuration);
        Debug.Log($"[Debug] Stunned {t.name}");
    }

    private void PossessTarget(Enemy t)
    {
        if (t == null) { Debug.LogWarning("[Debug] No target."); return; }
        t.ApplyPossession(_effectDuration, 1f);
        Debug.Log($"[Debug] Possessed {t.name}");
    }

    private void PrintTarget(Enemy t)
    {
        if (t == null) return;
        var ms = t.GetComponent<EnemyMoodSystem>();
        Debug.Log($"[Debug] {t.name} HP:{t.Health.CurrentHealth:F0}/{t.Health.MaxHealth:F0} " +
                  $"Mood:{ms?.CurrentMood ?? EnemyMood.Normal} Stunned:{t.IsStunned}");
    }

    // ═══════════════════════════════════════════════════════
    // MOOD
    // ═══════════════════════════════════════════════════════
    private void ForceMood(EnemyMood mood, float duration, EnemyMood decayTo)
    {
        var target = _moodTarget ?? _specificTarget;
        if (target == null) { Debug.LogWarning("[Debug] Wire _moodTarget."); return; }
        var ms = target.GetComponent<EnemyMoodSystem>();
        if (ms == null) { Debug.LogWarning($"[Debug] No EnemyMoodSystem on {target.name}."); return; }
        ms.TransitionTo(mood, duration, decayTo);
        Debug.Log($"[Debug] {target.name} → {mood} (dur={duration}s → {decayTo})");
    }

    private void ResetMood()
    {
        var target = _moodTarget ?? _specificTarget;
        if (target == null) return;
        target.GetComponent<EnemyMoodSystem>()?.TransitionTo(EnemyMood.Normal, 0f, EnemyMood.Normal);
        Debug.Log($"[Debug] {target.name} mood reset");
    }

    private void PrintAllMoods()
    {
        Debug.Log("[Debug] === ALL MOODS ===");
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            var ms = e.GetComponent<EnemyMoodSystem>();
            if (ms != null)
                Debug.Log($"  {e.name}: {ms.CurrentMood} HP:{e.Health.CurrentHealth:F0}/{e.Health.MaxHealth:F0}");
        }
    }

    // ═══════════════════════════════════════════════════════
    // FACTION ENERGY
    // ═══════════════════════════════════════════════════════
    private void ForceFactionBurst()
    {
        var fe = FactionEnergySystem.Instance;
        if (fe == null) { Debug.LogWarning("[Debug] No FactionEnergySystem."); return; }
        fe.AddEnergy(95f - fe.CurrentEnergy);
        Debug.Log($"[Debug] Energy → {fe.CurrentEnergy:F0}");
    }

    private void ForceFactionDepleted()
    {
        var fe = FactionEnergySystem.Instance;
        if (fe == null) return;
        fe.DrainEnergy(fe.CurrentEnergy - 10f);
        MoodEventBus.Fire(EnemySocialEvent.EnergyDepleted);
        Debug.Log($"[Debug] Energy depleted → {fe.CurrentEnergy:F0}");
    }

    private void PrintEnergyState()
    {
        var fe = FactionEnergySystem.Instance;
        if (fe == null) return;
        string t = fe.CurrentEnergy >= 90 ? "BURST" :
                   fe.CurrentEnergy >= 75 ? "Surging" :
                   fe.CurrentEnergy >= 50 ? "Empowered" :
                   fe.CurrentEnergy >= 25 ? "Normal" : "Weakened";
        Debug.Log($"[Debug] Energy:{fe.CurrentEnergy:F0} [{t}] Burst={fe.BurstActive}");
    }

    // ═══════════════════════════════════════════════════════
    // GROUP GRAB
    // ═══════════════════════════════════════════════════════
    private void ForceGrab()
    {
        if (_grabTarget == null) { Debug.LogWarning("[Debug] No grabTarget."); return; }
        Debug.Log($"[Debug] Grab: Grabbing={_grabTarget.IsGrabbing} Cooldown={_grabTarget.GrabOnCooldown}");
        Debug.Log($"[Debug] ForceGrab={_grabTarget.StartGrab()}");
    }

    private void ForceRelease()
    {
        if (_grabTarget == null || !_grabTarget.IsGrabbing) { Debug.LogWarning("[Debug] Not grabbing."); return; }
        _grabTarget.ReleasePlayer(_grabTarget.PartialHealAmount);
    }

    private void ForceKillGrabbed()
    {
        if (_grabTarget == null || !_grabTarget.IsGrabbing) { Debug.LogWarning("[Debug] Not grabbing."); return; }
        _grabTarget.KillPlayer();
    }

    private void PrintGrabState()
    {
        if (_grabTarget == null) return;
        Debug.Log($"[Debug] Grab: Grabbing={_grabTarget.IsGrabbing} TTK={_grabTarget.NormalisedTTK:P0}");
    }

    // ═══════════════════════════════════════════════════════
    // SEVERED
    // ═══════════════════════════════════════════════════════
    private void ForcePartnerDie()
    {
        if (_severedTarget == null) { Debug.LogWarning("[Debug] No severedTarget."); return; }
        _severedTarget.OnPartnerDied();
        Debug.Log("[Debug] OnPartnerDied — grace window starting");
    }

    private void PrintSeveredState()
    {
        if (_severedTarget == null) return;
        Debug.Log($"[Debug] Severed: Dead={_severedTarget.PartnerDead} Rage={_severedTarget.IsInGriefRage}");
    }

    // ═══════════════════════════════════════════════════════
    // TETHER BREAKER
    // ═══════════════════════════════════════════════════════
    private void ForceChainAttack()
    {
        if (_tetherTarget == null) { Debug.LogWarning("[Debug] No tetherTarget."); return; }
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in players) { if (p is SoulPlayer) continue; _tetherTarget.SetTarget(p.transform); break; }
        _tetherTarget.TryChainAttack();
        Debug.Log("[Debug] TryChainAttack");
    }

    private void ForceRage()
    {
        if (_tetherTarget == null) return;
        _tetherTarget.NotifyChainMash();
    }

    private void ForceBreakChain()
    {
        if (_tetherTarget == null) return;
        var tbData = _tetherTarget.Data as TetherBreakerEnemyData;
        int n = tbData?.chainMashThreshold ?? 8;
        for (int i = 0; i < n; i++) _tetherTarget.NotifyChainMash();
    }

    private void PrintTetherState()
    {
        if (_tetherTarget == null) return;
        Debug.Log($"[Debug] Tether: Rage={_tetherTarget.IsInRage} Chain={_tetherTarget.ChainActive} " +
                  $"Throwing={_tetherTarget.IsThrowing}");
    }

    // ═══════════════════════════════════════════════════════
    // WITNESS
    // ═══════════════════════════════════════════════════════
    private void KillWitnessAlly()
    {
        if (_witnessTarget == null) return;
        var ally = _witnessTarget.FollowTarget;
        if (ally == null || ally.Health.IsDead) { Debug.LogWarning("[Debug] No alive ally."); return; }
        ally.Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }

    private void PrintWitnessState()
    {
        if (_witnessTarget == null) return;
        Debug.Log($"[Debug] Witness: AllyAlive={_witnessTarget.AllyIsAlive} " +
                  $"Ritual={_witnessTarget.IsRitualing}");
    }

    // ═══════════════════════════════════════════════════════
    // PENITENT
    // ═══════════════════════════════════════════════════════
    private void ForcePenitentCrush()
    {
        if (_penitentTarget == null) return;
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            _penitentTarget.SetTarget(p.transform);
            _penitentTarget.StartCrush(p);
            Debug.Log($"[Debug] Crush on {p.name}"); break;
        }
    }

    private void ForcePenitentReflection()
    {
        if (_penitentTarget == null) return;
        var pd = _penitentTarget.PenitentData;
        if (pd?.reflectionThresholds == null || pd.reflectionThresholds.Length == 0) return;
        float dmg = _penitentTarget.Health.CurrentHealth -
                    (_penitentTarget.Health.MaxHealth * (pd.reflectionThresholds[0] - 0.01f));
        if (dmg <= 0f) return;
        _penitentTarget.Health.TakeDamage(new DamageData(dmg, DamageType.Combat));
    }

    private void PrintPenitentState()
    {
        if (_penitentTarget == null) return;
        Debug.Log($"[Debug] Penitent: Crush={_penitentTarget.IsCrushing} " +
                  $"Reflect={_penitentTarget.ReflectionActive} Rage={_penitentTarget.IsInRage}");
    }
    private void PrintPenitentMood()
    {
        if (_penitentTarget == null) return;
        var ms = _penitentTarget.GetComponent<EnemyMoodSystem>();
        Debug.Log($"[Penitent] Mood={ms?.CurrentMood} " +
                  $"Crushing={_penitentTarget.IsCrushing} " +
                  $"Reflection={_penitentTarget.ReflectionActive} " +
                  $"Rage={_penitentTarget.IsInRage} " +
                  $"Cooldown={_penitentTarget.IsInCooldown}");
    }

    // ═══════════════════════════════════════════════════════
    // SIPHON
    // ═══════════════════════════════════════════════════════
    private void ForcePanicBomb()
    {
        if (_siphonTarget == null) return;
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in players) { if (p is SoulPlayer) continue; _siphonTarget.SetTarget(p.transform); break; }
        _siphonTarget.SpawnPanicBomb();
    }

    private void ForceGhostSpawn()
    {
        if (_siphonTarget == null || _soulPlayer == null || _rescueController == null) return;
        _siphonTarget.Initialise(FindLeftTwin(), FindRightTwin(), _soulPlayer, _rescueController);
        _siphonTarget.TestTriggerGhostSpawn();
    }

    private void KillGhost()
    {
        var ghost = FindAnyObjectByType<SiphonGhost>();
        if (ghost == null) return;
        ghost.Health?.TakeDamage(new DamageData(9999f, DamageType.Ability));
    }

    // ═══════════════════════════════════════════════════════
    // RESCUE
    // ═══════════════════════════════════════════════════════
    private void FakeRescueActive()
    {
        var shared = CommonCore.BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        shared?.Set(PoTNames.IsRescueActive, true);
        Debug.Log("[Debug] Rescue faked");
    }

    private void FakeSoulArrived()
    {
        if (_siphonTarget == null || _soulPlayer == null || _rescueController == null) return;
        _siphonTarget.Initialise(FindLeftTwin(), FindRightTwin(), _soulPlayer, _rescueController);
        _siphonTarget.TestTriggerGhostSpawn();
    }

    // ═══════════════════════════════════════════════════════
    // PERCEPTION / SOUND
    // ═══════════════════════════════════════════════════════
    private void FireSoundAtTarget()
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            SoundEventSystem.Fire(p.transform.position, _soundRadius, SoundType.ThrowableItem);
            Debug.Log($"[Debug] Sound at player {p.name} radius={_soundRadius}");
            break;
        }
    }

    private void FireSoundFar()
    {
        Vector3 farPos = Camera.main != null
            ? Camera.main.transform.position + Vector3.forward * 30f
            : Vector3.forward * 30f;
        SoundEventSystem.Fire(farPos, 5f, SoundType.ThrowableItem, 0.4f);
        Debug.Log($"[Debug] Sound far at {farPos}");
    }

    private void FireSoundAtEnemy()
    {
        var target = _perceptionTarget ?? _specificTarget;
        if (target == null) { Debug.LogWarning("[Debug] No perceptionTarget."); return; }
        SoundEventSystem.Fire(target.transform.position, _soundRadius, SoundType.ThrowableItem);
        Debug.Log($"[Debug] Sound AT {target.name}");
    }

    private void PrintPerceptionState()
    {
        var target = _perceptionTarget ?? _specificTarget;
        if (target == null) { Debug.LogWarning("[Debug] No perceptionTarget."); return; }
        var mem = target.GetComponent<PoTPerceptionMemory>();
        if (mem == null) { Debug.LogWarning($"[Debug] No PoTPerceptionMemory on {target.name}."); return; }
        Debug.Log($"[Debug] Perception {target.name}: Conf={mem.Confidence:F2} " +
                  $"State={mem.SearchState} HasMemory={mem.HasMemory} " +
                  $"SpeedMult={mem.SpeedMultiplier:F2}");
    }

    private void ForceGiveUpSearch()
    {
        var target = _perceptionTarget ?? _specificTarget;
        if (target == null) return;
        var mem = target.GetComponent<PoTPerceptionMemory>();
        if (mem == null) return;
        mem.OnSearchedLastKnownPosition();
        mem.OnSearchedLastKnownPosition();
        mem.OnSearchedLastKnownPosition();
        Debug.Log($"[Debug] Forced confidence drop on {target.name}");
    }

    private void PrintAllPerceptionStates()
    {
        Debug.Log("[Debug] === ALL PERCEPTION STATES ===");
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            var mem = e.GetComponent<PoTPerceptionMemory>();
            if (mem != null)
                Debug.Log($"  {e.name}: Conf={mem.Confidence:F2} State={mem.SearchState} " +
                          $"HasMemory={mem.HasMemory} SpeedMult={mem.SpeedMultiplier:F2}");
        }
    }

    // ═══════════════════════════════════════════════════════
    // BOND / DARK ENERGY (Phase 3)
    // ═══════════════════════════════════════════════════════
    private void BondEnemies()
    {
        if (_bondEnemy1 == null || _bondEnemy2 == null)
        { Debug.LogWarning("[Debug] Wire _bondEnemy1 and _bondEnemy2."); return; }
        var bond1 = _bondEnemy1.GetComponent<EnemySocialBond>();
        var bond2 = _bondEnemy2.GetComponent<EnemySocialBond>();
        bond1?.SetBondPartner(_bondEnemy2, EnemySocialBond.BondType.ComboPartner);
        bond2?.SetBondPartner(_bondEnemy1, EnemySocialBond.BondType.ComboPartner);
        Debug.Log($"[Debug] Bonded {_bondEnemy1.name} ↔ {_bondEnemy2.name}");
    }

    private void PrintDarkEnergyState()
    {
        foreach (var e in new[] { _bondEnemy1, _bondEnemy2 })
        {
            if (e == null) continue;
            var de = e.GetComponent<EnemyDarkEnergy>();
            var bond = e.GetComponent<EnemySocialBond>();
            var prox = e.GetComponent<EnemyProximityPower>();
            Debug.Log($"[Debug] {e.name}: DE={de?.CurrentEnergy:F2} " +
                      $"BondBroken={de?.BondBroken} ComboUnlocked={de?.ComboUnlocked} " +
                      $"InPact={prox?.InPact} Power={prox?.ActivePowerID} PartnerAlive={bond?.HasPartner}");
        }
    }

    private void AddDarkEnergy()
    {
        var target = _moodTarget ?? _specificTarget;
        if (target == null) { Debug.LogWarning("[Debug] No target for dark energy."); return; }
        target.GetComponent<EnemyDarkEnergy>()?.AddEnergy(0.1f);
        var de = target.GetComponent<EnemyDarkEnergy>();
        Debug.Log($"[Debug] +0.1 dark energy → {target.name} now {de?.CurrentEnergy:F2}");
    }

    private void ForceBreakBond()
    {
        if (_bondEnemy1 == null) { Debug.LogWarning("[Debug] Wire _bondEnemy1."); return; }
        var de = _bondEnemy1.GetComponent<EnemyDarkEnergy>();
        if (de != null) de.AddEnergy(1f);
        Debug.Log($"[Debug] Forced max energy on {_bondEnemy1.name} — bond should break");
    }

    private void PrintComboRegistry()
    {
        var registry = ComboReadyRegistry.Instance;
        if (registry == null) { Debug.Log("[Debug] No ComboReadyRegistry in scene."); return; }
        Debug.Log($"[Debug] ComboRegistry: {registry.ComboReadyCount} enemies ready");
        foreach (var e in FindObjectsByType<EnemyDarkEnergy>(FindObjectsSortMode.None))
        {
            var prox = e.GetComponent<EnemyProximityPower>();
            Debug.Log($"  {e.name}: DE={e.CurrentEnergy:F2} Unlocked={e.ComboUnlocked} " +
                      $"Active={prox?.InPact} InPact={prox?.InPact} Power={prox?.ActivePowerID}");
        }
    }

    private void ForceComboForm()
    {
        if (_bondEnemy1 == null || _bondEnemy2 == null)
        { Debug.LogWarning("[Debug] Wire _bondEnemy1 and _bondEnemy2."); return; }
        var de1 = _bondEnemy1.GetComponent<EnemyDarkEnergy>();
        var de2 = _bondEnemy2.GetComponent<EnemyDarkEnergy>();
        de1?.AddEnergy(1f);
        de2?.AddEnergy(1f);
        Debug.Log("[Debug] Forced max energy on both — pact should form next frame");
    }

    private void BreakActiveCombo()
    {
        var target = _moodTarget ?? _specificTarget;
        if (target == null) { Debug.LogWarning("[Debug] No target."); return; }
        var de = target.GetComponent<EnemyDarkEnergy>();
        if (de == null) { Debug.LogWarning("[Debug] No EnemyDarkEnergy."); return; }
        ComboReadyRegistry.Instance?.Unregister(de);
        Debug.Log($"[Debug] Unregistered {target.name} from pact");
    }

    private void DamageNearestSpawn()
    {
        var spawn = FindAnyObjectByType<SpawnPointPOI>();
        if (spawn == null) { Debug.LogWarning("[Debug] No SpawnPointPOI in scene."); return; }
        spawn.TakeDamage(50f);
        Debug.Log($"[Debug] Dealt 50 damage to spawn point {spawn.name}");
    }

    private void PrintWitnessPOIState()
    {
        if (_witnessTarget == null) { Debug.LogWarning("[Debug] No witnessTarget."); return; }
        var tracker = _witnessTarget.GetComponent<EnemyPOITracker>();
        Debug.Log($"[Debug] Witness POI: " +
                  $"HasRitualSites={tracker?.HasRitualSites} " +
                  $"NearestSite={tracker?.NearestRitualSite?.name ?? "none"} " +
                  $"NearBarrier={tracker?.NearBarrier} " +
                  $"NearestSpawn={tracker?.NearestSpawnPoint?.name ?? "none"}");
    }

    private void PrintCommanderState()
    {
        ICommander[] commanders = new ICommander[]
        {
        _chainCommander, _grandSummoner, _penitentCommander
        };
        foreach (var c in commanders)
        {
            if (c == null) continue;
            var e = (c as Enemy);
            Debug.Log($"[Commander] {e?.name} Alive={c.IsAlive} " +
                      $"Soldiers={c.Soldiers.Count} " +
                      $"HP={e?.Health.CurrentHealth:F0}");
            foreach (var s in c.Soldiers)
            {
                var formGoal = s.GetComponent<GOAPGoalHoldFormation>();
                var mood = s.GetComponent<EnemyMoodSystem>();
                Debug.Log($"  Soldier: {s.name} Mood={mood?.CurrentMood} " +
                          $"FormationPos={formGoal?.GetFormationPosition()}");
            }
        }
    }

    private void KillCommander()
    {
        ICommander[] commanders = new ICommander[]
        {
        _chainCommander, _grandSummoner, _penitentCommander
        };
        foreach (var c in commanders)
        {
            var e = c as Enemy;
            if (e == null || e.Health.IsDead) continue;
            e.Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
            Debug.Log($"[Commander] Killed {e.name} — death cascade should fire");
            break; // kill one at a time
        }
    }

    private void ForceCommanderGroup()
    {
        var spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) { Debug.LogWarning("[Commander] No EnemySpawner."); return; }
        // Trigger via zone activation — re-enter active zone
        Debug.Log("[Commander] Use zone trigger to force group spawn. " +
                  "Set GroupSpawnConfig.groupSpawnProbability=1 for guaranteed spawn.");
    }

    private void DamageCommanderSoldier()
    {
        // Triggers PenitentCommander dark shield — deal 30% HP to first soldier
        if (_penitentCommander == null) return;
        var soldiers = _penitentCommander.Soldiers;
        if (soldiers.Count == 0) { Debug.Log("[Commander] No soldiers registered."); return; }
        var s = soldiers[0];
        float dmg = s.Health.MaxHealth * 0.3f;
        s.Health.TakeDamage(new DamageData(dmg, DamageType.Combat));
        Debug.Log($"[Commander] Dealt {dmg:F0} to {s.name} — DarkShield should trigger");
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════
    private Player FindLeftTwin()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (!(p is SoulPlayer)) return p;
        return null;
    }

    private Player FindRightTwin()
    {
        Player first = null;
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p is SoulPlayer) continue;
            if (first == null) first = p; else return p;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════
    // ON GUI
    // ═══════════════════════════════════════════════════════
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 420, 700));
        GUILayout.Label("=== DEBUG (Hold category + number) ===");

        // Faction energy
        var fe = FactionEnergySystem.Instance;
        if (fe != null)
        {
            string t = fe.CurrentEnergy >= 90 ? "BURST!" :
                       fe.CurrentEnergy >= 75 ? "Surging" :
                       fe.CurrentEnergy >= 50 ? "Empowered" :
                       fe.CurrentEnergy >= 25 ? "Normal" : "Weakened";
            GUILayout.Label($"FactionEnergy: {fe.CurrentEnergy:F0}/100 [{t}]{(fe.BurstActive ? " ***" : "")}");
        }

        // Combo registry
        var registry = ComboReadyRegistry.Instance;
        if (registry != null)
            GUILayout.Label($"ComboRegistry: {registry.ComboReadyCount} ready");

        // All enemy states
        GUILayout.Label("--- Enemies ---");
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            var ms = e.GetComponent<EnemyMoodSystem>();
            var mem = e.GetComponent<PoTPerceptionMemory>();
            var de = e.GetComponent<EnemyDarkEnergy>();
            var prox = e.GetComponent<EnemyProximityPower>();

            string moodStr = ms != null ? $" {ms.CurrentMood}" : "";
            string confStr = mem != null ? $" Conf:{mem.Confidence:F1}" : "";
            string deStr = de != null ? $" DE:{de.CurrentEnergy:F2}" : "";
            string comboStr = prox != null && prox.InPact
                ? $" [{(prox.InPact ? " [PACT]" : "")}]" : "";

            GUILayout.Label($"{e.name}: HP:{e.Health.CurrentHealth:F0}/{e.Health.MaxHealth:F0}" +
                            $"{moodStr}{confStr}{deStr}{comboStr}");
        }

        // Bond targets
        GUILayout.Label("--- Bond ---");
        if (_bondEnemy1 != null)
        {
            var de = _bondEnemy1.GetComponent<EnemyDarkEnergy>();
            var prox = _bondEnemy1.GetComponent<EnemyProximityPower>();
            var bond = _bondEnemy1.GetComponent<EnemySocialBond>();
            GUILayout.Label($"Bond1: DE={de?.CurrentEnergy:F2} Broken={de?.BondBroken} " +
                            $"InPact={prox?.InPact} Power={prox?.ActivePowerID} Partner={bond?.HasPartner}");
        }
        if (_bondEnemy2 != null)
        {
            var de = _bondEnemy2.GetComponent<EnemyDarkEnergy>();
            var prox = _bondEnemy2.GetComponent<EnemyProximityPower>();
            var bond = _bondEnemy2.GetComponent<EnemySocialBond>();
            GUILayout.Label($"Bond2: DE={de?.CurrentEnergy:F2} Broken={de?.BondBroken} " +
                            $"InPact={prox?.InPact} Power={prox?.ActivePowerID} Partner={bond?.HasPartner}");
        }

        GUILayout.Label("--- Commanders ---");
        ICommander[] cmds = new ICommander[] { _chainCommander, _grandSummoner, _penitentCommander };
        foreach (var c in cmds)
        {
            if (c == null) continue;
            var e = c as Enemy;
            GUILayout.Label($"{e?.name}: Alive={c.IsAlive} Soldiers={c.Soldiers.Count} " +
                            $"HP={e?.Health.CurrentHealth:F0}");
        }

        GUILayout.Label("--- Keys ---");
        GUILayout.Label("D+1=DmgAll D+2=KillAll D+3=DmgTarget D+4=Stun D+5=Possess");
        GUILayout.Label("M+1=Opportunistic M+2=Aggressive M+3=Panic M+4=Frustrated");
        GUILayout.Label("M+5=Grieving M+6=Contempt M+7=Enraged M+8=Wounded M+9=Reset");
        GUILayout.Label("E+1/2=±10 E+3/4=±25 E+5=Burst E+6=Deplete");
        GUILayout.Label("G+1=Grab G+2=Release G+3=Kill S+1=PartnerDie");
        GUILayout.Label("T+1=Chain T+2=Rage T+3=Break W+1=KillAlly");
        GUILayout.Label("P+1=Crush P+2=Reflect X+1=Bomb X+2=Ghost X+3=KillGhost");
        GUILayout.Label("N+1=SoundAtPlayer N+2=SoundFar N+3=SoundAtEnemy N+4=PrintPerc");
        GUILayout.Label("B+1=Bond B+2=PrintDE B+3=+Energy B+4=BreakBond");
        GUILayout.Label("B+5=PrintRegistry B+6=ForcePact B+7=LeavePact");
        GUILayout.Label("R+1=FakeRescue R+2=SoulArrived");
        GUILayout.Label("W+1=KillAlly  W+2=Print  W+3=PrintPOI");
        GUILayout.Label("B+8=DamageSpawn");

        GUILayout.EndArea();
    }
}