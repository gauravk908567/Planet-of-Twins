using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global registry of combo-ready enemies.
/// When enough enemies are combo-ready, they form a Pact.
/// Pact is permanent until a member dies.
/// No commit/rally/timeout — pact just exists.
///
/// ATTACH: To AIManager GO.
/// </summary>
public class ComboReadyRegistry : MonoBehaviour
{
    public static ComboReadyRegistry Instance { get; private set; }

    [Header("Pact Config")]
    [Tooltip("How close pact members stay to each other.")]
    [SerializeField] private float _pactRange = 12f;

    [Tooltip("Min dark energy both enemies need to form a pact.")]
    [SerializeField] private float _minDarkEnergyToPact = 0.6f;

    private readonly List<EnemyDarkEnergy> _comboReady = new();
    private readonly List<List<EnemyDarkEnergy>> _pacts = new();

    // Quick lookup — which pact is this enemy in
    private readonly Dictionary<EnemyDarkEnergy, List<EnemyDarkEnergy>> _pactLookup = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Registration ───────────────────────────────────────
    public void Register(EnemyDarkEnergy enemy)
    {
        if (_comboReady.Contains(enemy)) return;
        _comboReady.Add(enemy);
        Debug.Log($"[ComboRegistry] {enemy.name} registered — {_comboReady.Count} ready");
        TryFormPact(enemy);
    }

    public void Unregister(EnemyDarkEnergy enemy)
    {
        _comboReady.Remove(enemy);
        RemoveFromPact(enemy);
    }

    // ── Pact formation ─────────────────────────────────────
    private void TryFormPact(EnemyDarkEnergy newEnemy)
    {
        // Already in a pact
        if (_pactLookup.ContainsKey(newEnemy)) return;

        // Find best partner by priority
        var partner = FindPactPartner(newEnemy);
        if (partner == null) return;

        // Check if partner is already in a pact — join it
        if (_pactLookup.TryGetValue(partner, out var existingPact))
        {
            existingPact.Add(newEnemy);
            _pactLookup[newEnemy] = existingPact;
            NotifyPactMembers(existingPact);
            Debug.Log($"[ComboRegistry] {newEnemy.name} joined existing pact " +
                      $"({existingPact.Count} members)");
            return;
        }

        // Form new pact
        var pact = new List<EnemyDarkEnergy> { newEnemy, partner };
        _pacts.Add(pact);
        _pactLookup[newEnemy] = pact;
        _pactLookup[partner] = pact;
        NotifyPactMembers(pact);

        Debug.Log($"[ComboRegistry] Pact formed: {newEnemy.name} + {partner.name}");
    }

    private EnemyDarkEnergy FindPactPartner(EnemyDarkEnergy requester)
    {
        if (requester == null) return null;

        var bond = requester.GetComponent<EnemySocialBond>();

        // Priority 1 — own bond partner
        if (bond != null && bond.HasPartner)
        {
            var partnerDE = bond.BondPartner?.GetComponent<EnemyDarkEnergy>();
            if (partnerDE != null && partnerDE.ComboUnlocked &&
                _comboReady.Contains(partnerDE))
                return partnerDE;
        }

        // Priority 2 — same alignment, partnerless
        foreach (var e in _comboReady)
        {
            if (e == requester) continue;
            if (e.Alignment != requester.Alignment) continue;
            if (e.CurrentEnergy < _minDarkEnergyToPact) continue;
            var eBond = e.GetComponent<EnemySocialBond>();
            if (eBond == null || !eBond.HasPartner) return e;
        }

        // Priority 3 — same alignment any
        foreach (var e in _comboReady)
        {
            if (e == requester) continue;
            if (e.Alignment == requester.Alignment) return e;
        }

        return null;
    }

    private void RemoveFromPact(EnemyDarkEnergy enemy)
    {
        if (!_pactLookup.TryGetValue(enemy, out var pact)) return;

        pact.Remove(enemy);
        _pactLookup.Remove(enemy);

        if (pact.Count <= 1)
        {
            // Pact dissolved — notify remaining member
            foreach (var m in pact)
            {
                _pactLookup.Remove(m);
                m.GetComponent<EnemyProximityPower>()?.OnPactDissolved();
            }
            _pacts.Remove(pact);
            Debug.Log($"[ComboRegistry] Pact dissolved — {enemy.name} died");
        }
        else
        {
            NotifyPactMembers(pact);
            Debug.Log($"[ComboRegistry] {enemy.name} left pact — {pact.Count} remain");
        }
    }

    private void NotifyPactMembers(List<EnemyDarkEnergy> pact)
    {
        foreach (var m in pact)
            m.GetComponent<EnemyProximityPower>()?.OnPactUpdated(pact);
    }

    // ── Public API ─────────────────────────────────────────
    public bool IsInPact(EnemyDarkEnergy enemy)
        => _pactLookup.ContainsKey(enemy);

    public List<EnemyDarkEnergy> GetPact(EnemyDarkEnergy enemy)
        => _pactLookup.TryGetValue(enemy, out var p) ? p : null;

    public float PactRange => _pactRange;
    public int ComboReadyCount => _comboReady.Count;

    // Legacy — kept for compatibility
    public bool IsInActiveCombo(EnemyDarkEnergy enemy) => IsInPact(enemy);
    public EnemyDarkEnergy GetComboPartner(EnemyDarkEnergy enemy)
    {
        var pact = GetPact(enemy);
        if (pact == null) return null;
        foreach (var m in pact)
            if (m != enemy) return m;
        return null;
    }
}