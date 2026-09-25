using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages pact/combo power state for this enemy.
///
/// PACT MODEL:
///   Dark energy > threshold → register in ComboReadyRegistry
///   Registry forms permanent Pact with compatible enemies
///   Pact members stay within PactRange of each other (loose formation)
///   No commit/rally/timeout — pact persists until member dies
///   ComboReady = all pact members within executionRange of twin simultaneously
///   Pact breaks only on member death
///
/// ATTACH: To every enemy prefab alongside EnemyDarkEnergy.
/// </summary>
public class EnemyProximityPower : MonoBehaviour
{
    [SerializeField] private ProximityPowerProfile _profile;
    [SerializeField] private float _thirdWheelBoostMultiplier = 1.3f;

    private Enemy _enemy;
    private EnemyDarkEnergy _darkEnergy;
    private SpawnZone _homeZone;
    private List<EnemyDarkEnergy> _pactMembers;
    private string _activePowerID;
    private bool _isThirdWheel;

    // ── Public ─────────────────────────────────────────────
    public bool InPact => _pactMembers != null && _pactMembers.Count > 1;
    public bool IsThirdWheel => _isThirdWheel;
    public string ActivePowerID => _activePowerID;
    public ProximityPowerProfile Profile => _profile;

    // Nearest pact member position — for StayInPact goal
    public Vector3 NearestPactMemberPosition
    {
        get
        {
            if (_pactMembers == null) return transform.position;
            Vector3 nearest = transform.position;
            float minDist = float.MaxValue;
            foreach (var m in _pactMembers)
            {
                if (m == _darkEnergy || m == null) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < minDist) { minDist = d; nearest = m.transform.position; }
            }
            return nearest;
        }
    }

    public float DistanceToNearestPactMember
    {
        get
        {
            if (_pactMembers == null) return 0f;
            float minDist = float.MaxValue;
            foreach (var m in _pactMembers)
            {
                if (m == _darkEnergy || m == null) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < minDist) minDist = d;
            }
            return minDist == float.MaxValue ? 0f : minDist;
        }
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _darkEnergy = GetComponent<EnemyDarkEnergy>();
    }

    private void Start()
    {
        var tracker = GetComponent<ZoneEnemyTracker>();
        if (tracker != null) _homeZone = tracker.HomeZone;
    }

    private void Update()
    {
        if (_enemy == null || _enemy.Health.IsDead) return;

        if (_homeZone == null)
        {
            var tracker = GetComponent<ZoneEnemyTracker>();
            if (tracker != null) _homeZone = tracker.HomeZone;
        }

        if (InPact)
            UpdateActivePower();

        WriteToBlackboard();
    }

    // ── Pact callbacks from registry ───────────────────────
    public void OnPactUpdated(List<EnemyDarkEnergy> pact)
    {
        _pactMembers = pact;

        // Check if joining as third wheel (pact had 2, now 3)
        if (pact.Count >= 3 && !_isThirdWheel)
        {
            bool wasPairMember = pact.IndexOf(_darkEnergy) < 2;
            if (!wasPairMember)
            {
                _isThirdWheel = true;
                GetComponent<EnemyAttackController>()
                    ?.SetDamageMultiplier(_thirdWheelBoostMultiplier);
                Debug.Log($"[ProximityPower] {_enemy.name} joined pact as third wheel x{_thirdWheelBoostMultiplier}");
            }
        }

        GetComponent<EnemyMoodSystem>()
            ?.TransitionTo(EnemyMood.Confident, 3f, EnemyMood.Normal);

        Debug.Log($"[ProximityPower] {_enemy.name} pact updated — {pact.Count} members");
    }

    public void OnPactDissolved()
    {
        _pactMembers = null;
        _activePowerID = string.Empty;

        if (_isThirdWheel)
        {
            _isThirdWheel = false;
            GetComponent<EnemyAttackController>()?.ClearDamageMultiplier();
        }

        GetComponent<EnemyMoodSystem>()
            ?.TransitionTo(EnemyMood.Frustrated, 3f, EnemyMood.Normal);

        Debug.Log($"[ProximityPower] {_enemy.name} pact dissolved");
    }

    // ── Power selection ────────────────────────────────────
    private void UpdateActivePower()
    {
        if (_profile?.comboPowers == null) return;

        // Find nearest pact partner
        EnemyDarkEnergy partner = null;
        float minDist = float.MaxValue;
        foreach (var m in _pactMembers)
        {
            if (m == _darkEnergy || m == null) continue;
            float d = Vector3.Distance(transform.position, m.transform.position);
            if (d < minDist) { minDist = d; partner = m; }
        }
        if (partner == null) return;

        var twin = FindNearestTwin();
        float twinHP = 1f;
        if (twin != null)
        {
            var ph = twin.GetComponent<PlayerHealthComponent>();
            if (ph != null && ph.MaxHealth > 0f)
                twinHP = ph.CombatHealth / ph.MaxHealth;
        }
        float distToTwin = twin != null
            ? Vector3.Distance(transform.position, twin.transform.position) : 999f;
        float distToPartner = minDist;

        string newPowerID = SelectPower(partner, twinHP, distToTwin, distToPartner);
        if (newPowerID != _activePowerID)
        {
            _activePowerID = newPowerID ?? string.Empty;
            if (!string.IsNullOrEmpty(newPowerID))
                Debug.Log($"[ProximityPower] {_enemy.name} power: '{newPowerID}'");
        }
    }

    private string SelectPower(EnemyDarkEnergy partner, float twinHPNorm,
                                float distToTwin, float distToPartner)
    {
        if (_profile?.comboPowers == null) return null;

        var partnerMood = partner.GetComponent<EnemyMoodSystem>();
        bool partnerRaging = partnerMood?.CurrentMood == EnemyMood.Enraged ||
                             partnerMood?.CurrentMood == EnemyMood.Aggressive;

        foreach (var power in _profile.comboPowers)
        {
            if (power.requiredPartnerClan != AlliedClanType.Any &&
    partner.ClanType != power.requiredPartnerClan) continue;
            if (_darkEnergy.CurrentEnergy < power.minDarkEnergyBoth) continue;
            if (partner.CurrentEnergy < power.minDarkEnergyBoth) continue;
            if (distToPartner > power.executionRange) continue;
            if (twinHPNorm > power.maxTwinHPNorm) continue;
            if (twinHPNorm < power.minTwinHPNorm) continue;
            if (distToTwin > power.maxRangeToTwin) continue;
            if (distToTwin < power.minRangeToTwin) continue;
            if (power.requiresPartnerRaging && !partnerRaging) continue;
            return power.powerID;
        }
        return null;
    }

    // ── Helpers ────────────────────────────────────────────
    private GameObject FindNearestTwin()
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        GameObject nearest = null;
        float minDist = float.MaxValue;
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < minDist) { minDist = d; nearest = p.gameObject; }
        }
        return nearest;
    }

    private void WriteToBlackboard()
    {
        var brain = GetComponent<PoTGOAPBrainBase>();
        if (brain?.LinkedBlackboard == null) return;

        bool comboReady = InPact && !string.IsNullOrEmpty(_activePowerID);
        bool tooFar = InPact && DistanceToNearestPactMember >
                          (ComboReadyRegistry.Instance?.PactRange ?? 12f);

        brain.LinkedBlackboard.Set(PoTNames.ComboReady, comboReady);
        brain.LinkedBlackboard.Set(PoTNames.NearCompatibleAlly, InPact);
        brain.LinkedBlackboard.Set(PoTNames.IsRallying, tooFar);  // reuse — means "needs to regroup"
        brain.LinkedBlackboard.Set(PoTNames.IsCommitting, comboReady);
    }

    private void OnDestroy()
    {
        if (_darkEnergy != null)
            ComboReadyRegistry.Instance?.Unregister(_darkEnergy);
    }

    private void OnDrawGizmosSelected()
    {
        if (!InPact) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position,
            ComboReadyRegistry.Instance?.PactRange ?? 12f);

        if (_pactMembers == null) return;
        foreach (var m in _pactMembers)
        {
            if (m == _darkEnergy || m == null) continue;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawLine(transform.position, m.transform.position);
        }
    }
}