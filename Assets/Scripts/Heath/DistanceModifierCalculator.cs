// SOLID compliance:
//   S — only converts distance + upgrade node → modifier float. Nothing else.
//   O — zone bands are injected as data (DistanceZone[]); adding a new band
//       requires zero modification to this class.
//   L — implements IDistanceModifierCalculator fully and correctly.
//   I — only IDistanceModifierCalculator; no irrelevant members.
//   D — consumers depend on IDistanceModifierCalculator, not this class.
//
// BUG FIXES from original DistanceHealthSystem:
// 1. Upgrade bonus now guarded by d <= upgradeCapDistance (16u per).
//   2. Does NOT return 0 for d>18 — that is Over18DrainCalculator's job.
//   3. Not a MonoBehaviour — no Unity lifecycle overhead.
// ═══════════════════════════════════════════════════════════════════════
using UnityEngine;

public class DistanceModifierCalculator : IDistanceModifierCalculator
{
 //: upgrade bonus only applied when d ≤ this value.
    // The 16–18u danger zone is upgrade-immune.
    private const float UpgradeCapDistance = 16f;

 // Injected zone data — default matches exactly.
    // Consumers (or Unity Inspector via a ScriptableObject wrapper) supply these.
    private readonly DistanceZone[] _zones;

    /// <summary>
 /// Default constructor uses the exact piecewise bands.
    /// Pass custom zones to override (OCP).
    /// </summary>
    public DistanceModifierCalculator(DistanceZone[] zones = null)
    {
        _zones = zones ?? DefaultZones();
    }

    public float CalculateModifier(float distance, int upgradeNode)
    {
        // Safe zone — full health, no calculation needed
        if (distance <= _zones[0].DistanceStart)
            return 1f;

        float basePercent = EvaluateZones(distance);

 //: bonus ONLY when distance is within the upgradeable range.
        // FIX from original script: original had no d<=16 guard, allowing
        // Node 3 to give 31% health at 17u instead of the correct 13%.
        float bonus = (distance <= UpgradeCapDistance)
            ? (9f * upgradeNode) - (upgradeNode * upgradeNode)
            : 0f;

        float finalPercent = Mathf.Clamp(basePercent + bonus, 0f, 100f);
        return finalPercent / 100f;
    }

    private float EvaluateZones(float distance)
    {
        foreach (var zone in _zones)
        {
            if (distance > zone.DistanceStart && distance <= zone.DistanceEnd)
            {
                float t = Mathf.InverseLerp(zone.DistanceStart, zone.DistanceEnd, distance);
                return Mathf.Lerp(zone.HealthPercentAtStart, zone.HealthPercentAtEnd, t);
            }
        }
        // Beyond the last zone — return the terminal health of the last zone.
        // NOTE: This does NOT return 0. Returning 0 here would be snap-kill.
        // The >18u system is handled exclusively by Over18DrainCalculator.
        return _zones[_zones.Length - 1].HealthPercentAtEnd;
    }

    private static DistanceZone[] DefaultZones() => new[]
    {
 // exact specification
            new DistanceZone { DistanceStart =  0f, DistanceEnd =  6f, HealthPercentAtStart = 100f, HealthPercentAtEnd = 100f },
            new DistanceZone { DistanceStart =  6f, DistanceEnd = 12f, HealthPercentAtStart = 100f, HealthPercentAtEnd =  50f },
            new DistanceZone { DistanceStart = 12f, DistanceEnd = 16f, HealthPercentAtStart =  50f, HealthPercentAtEnd =  25f },
            new DistanceZone { DistanceStart = 16f, DistanceEnd = 18f, HealthPercentAtStart =  25f, HealthPercentAtEnd =   1f },
    };
}

