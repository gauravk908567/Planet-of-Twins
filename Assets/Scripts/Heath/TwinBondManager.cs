using UnityEngine;

public class TwinBondManager : MonoBehaviour
{
    // ── Injected dependencies (DIP) ────────────────────────────
    // Assign via Inspector — Unity serialises the concrete MonoBehaviour
    // but TwinBondManager only ever calls the interface methods.
    [SerializeField] private TetherDistanceCalculator distanceProvider;
    [SerializeField] private PlayerHealthComponent leftHealth;
    [SerializeField] private PlayerHealthComponent rightHealth;

    // Upgrade node is set externally by UpgradeManager (SRP)
    private int _upgradeNode = 0;

    // Plain C# collaborators — created here; could also be injected
    // via a factory if a DI container is added later (OCP).
    private readonly IDistanceModifierCalculator _modifierCalc =
        new DistanceModifierCalculator();
    private readonly IOverMaxDistanceDrainCalculator _leftDrain =
        new Over18DrainCalculator();
    private readonly IOverMaxDistanceDrainCalculator _rightDrain =
        new Over18DrainCalculator();

    // ── Unity lifecycle ────────────────────────────────────────
    private void Update()
    {
        float distance = distanceProvider.GetDistance();

        UpdateModifier(distance, leftHealth, _leftDrain);
        UpdateModifier(distance, rightHealth, _rightDrain);
    }

    // ── Public API for UpgradeManager ─────────────────────────
    public void SetUpgradeNode(int node)
    {
        _upgradeNode = node;
    }

    // ── Private pipeline ───────────────────────────────────────
    private void UpdateModifier(float distance,
                                PlayerHealthComponent player,
                                IOverMaxDistanceDrainCalculator drainCalc)
    {
        if (player == null) return;

        // Step 1: base modifier from distance zones (handles 0–18u)
        float modifier = _modifierCalc.CalculateModifier(distance, _upgradeNode);
        player.SetDistanceModifier(modifier);

 // Step 2: >18u lerp-to-death drain
        // The drain calculator accumulates independently; recovery begins
        // the moment distance drops back to ≤18u.
        float drainFraction = distance > 18f
            ? drainCalc.UpdateDrain(Time.deltaTime)
            : drainCalc.UpdateRecovery(Time.deltaTime);

        player.SetOverMaxDistanceCalculator(drainFraction);
    }
}