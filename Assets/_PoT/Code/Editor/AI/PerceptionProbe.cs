using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CommonCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Play-mode check that enemies can SEE the twins (the perception stack from Ian's framework, which broke once after the
/// multi-scene split: BUG-034/040/041, and whose June debug leftovers flooded the logs: BUG-140). Read-only: it never
/// changes the framework, and never touches <c>PerceptionManager.Instance</c> (that would fabricate a blank singleton).
///
///   • <b>Perception Probe</b>: exactly one PerceptionManager, in Persistent (not a fabricated
///     <c>Singleton&lt;…&gt;</c>); the vision sensor exists and holds BOTH twins; for each twin, the nearest enemies
///     with distance, vision range and detection strength. PASS needs an enemy in range to detect the twin, so
///     bring one first.
///   • <b>Bring Nearest Enemy To Twin</b>: warps the nearest enemy onto the NavMesh 6 m in front of the first twin,
///     facing it. Run the probe about a second later.
///
/// Run: start Play from Bootstrap (dev-direct is fine), then Planet of Twins Tools ▸ AI ▸ ….
/// </summary>
public static class PerceptionProbe
{
    private const float BringDistance = 6f;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Planet of Twins Tools/AI/Perception Probe (Play mode)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[Perception probe] Start Play from Bootstrap first."); return; }

        var sb = new StringBuilder("[Perception probe]\n");
        var problems = new List<string>();

        // ── 1. One real manager (BUG-040: a fabricated blank singleton made every enemy blind) ──
        var managers = Object.FindObjectsByType<PerceptionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in managers)
            sb.Append($"  manager '{m.name}' in scene '{m.gameObject.scene.name}' (id {m.GetInstanceID()})\n");
        if (managers.Length != 1) problems.Add($"{managers.Length} PerceptionManagers (expected exactly 1)");
        // The real one moves itself to DontDestroyOnLoad (Ian's MonoBehaviourSingleton, CLAUDE.md exemption E1), so the
        // scene is no clue; a fabricated one is a blank GameObject named "Singleton<…>".
        var pm = managers.FirstOrDefault();
        if (pm != null && pm.name.StartsWith("Singleton<"))
            problems.Add($"the manager is '{pm.name}': a fabricated blank singleton (BUG-040), not Persistent's");
        if (pm == null) { Report(sb, problems); return; }

        // ── 2. Sensors and who they hold (BUG-034: twins never attached to the vision sensor) ──
        var sensors = Field<Dictionary<ISensor, List<ListenerEntry>>>(pm, "ActiveSensors");
        var perceivables = Field<Dictionary<ISensor, List<IPerceivable>>>(pm, "ActivePerceivables");
        if (sensors == null || perceivables == null) { problems.Add("can't read the manager's sensor tables"); Report(sb, problems); return; }

        var twins = Object.FindObjectsByType<Perceivable>(FindObjectsSortMode.None)
                          .Where(p => p.GetComponent<PerceptionListener>() == null).ToList();   // enemies carry both
        sb.Append($"  twins (perceivables that aren't listeners): {string.Join(", ", twins.Select(t => t.name))}\n");
        if (twins.Count == 0) problems.Add("no twin Perceivables found");

        ISensor vision = null;
        foreach (var kv in sensors)
        {
            perceivables.TryGetValue(kv.Key, out var held);
            sb.Append($"  sensor {kv.Key.GetType().Name}: {kv.Value.Count} listeners, {held?.Count ?? 0} perceivables\n");
            if (kv.Key is VisionSensor) vision = kv.Key;
        }
        if (vision == null) problems.Add("no VisionSensor (no enemy has registered a vision config)");
        else
        {
            perceivables.TryGetValue(vision, out var seen);
            foreach (var twin in twins)
                if (seen == null || !seen.Contains(twin))
                    problems.Add($"{twin.name} is NOT registered with the vision sensor, so no enemy can see them");
        }

        // ── 3. Per twin: nearest enemies, range, detection ──
        bool anyInRange = false, anyDetected = false;
        if (vision != null)
            foreach (var twin in twins)
            {
                sb.Append($"  {twin.name} at {twin.Position:F1}:\n");
                var nearest = sensors[vision]
                    .Where(e => e.Listener is Component c && c != null && c.gameObject.activeInHierarchy)
                    .Select(e => (entry: e, dist: Vector3.Distance(e.Listener.SensorLocation, twin.Position)))
                    .OrderBy(x => x.dist).Take(3);
                foreach (var (entry, dist) in nearest)
                {
                    float range = (entry.Configuration as VisionSensorConfig)?.VisionConeRange ?? 0f;
                    float strength = pm.GetDetectionStrength(entry.Listener, twin);
                    bool inRange = dist <= range;
                    anyInRange |= inRange;
                    anyDetected |= strength > 0f;
                    sb.Append($"    {entry.Listener.Owner.name}: {dist:F1} m (range {range:F0}){(inRange ? " IN RANGE" : "")}, " +
                              $"detection {(strength > float.MinValue ? strength.ToString("F2") : "none")}\n");
                    if (inRange) sb.Append($"      {DescribeBrain(entry.Listener.Owner)}\n");
                }
            }

        if (!anyInRange) sb.Append("  No enemy is within vision range of a twin: run AI ▸ Bring Nearest Enemy To Twin, " +
                                   "wait a second, then probe again.\n");
        else if (!anyDetected) problems.Add("an enemy is in range but detects nobody (check its facing and line of sight)");
        Report(sb, problems, anyDetected);
    }

    [MenuItem("Planet of Twins Tools/AI/Bring Nearest Enemy To Twin (Play mode)")]
    public static void BringNearestEnemy()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[Perception probe] Start Play from Bootstrap first."); return; }
        var twin = Object.FindObjectsByType<Perceivable>(FindObjectsSortMode.None)
                         .FirstOrDefault(p => p.GetComponent<PerceptionListener>() == null);
        if (twin == null) { Debug.LogError("[Perception probe] No twin found."); return; }

        var enemy = Object.FindObjectsByType<PerceptionListener>(FindObjectsSortMode.None)
                          .Where(l => l.GetComponent<NavMeshAgent>() != null)
                          .OrderBy(l => Vector3.Distance(l.transform.position, twin.transform.position))
                          .FirstOrDefault();
        if (enemy == null) { Debug.LogError("[Perception probe] No active enemy with a NavMeshAgent found."); return; }

        Vector3 forward = twin.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 target = twin.transform.position + forward.normalized * BringDistance;
        if (!NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas))
        {
            Debug.LogError($"[Perception probe] No NavMesh near {target:F1} in front of {twin.name}; turn the twin and retry.");
            return;
        }
        var agent = enemy.GetComponent<NavMeshAgent>();
        agent.Warp(hit.position);
        Vector3 look = twin.transform.position - hit.position; look.y = 0f;
        if (look.sqrMagnitude > 0.01f) enemy.transform.rotation = Quaternion.LookRotation(look);
        Debug.Log($"[Perception probe] Moved {enemy.name} to {hit.position:F1}, {BringDistance} m in front of {twin.name}, " +
                  "facing them. Run AI ▸ Perception Probe in a second.", enemy);
    }

    private static void Report(StringBuilder sb, List<string> problems, bool detected = false)
    {
        if (problems.Count > 0) Debug.LogError($"{sb}FAIL: {string.Join("; ", problems)}");
        else if (detected) Debug.Log($"{sb}PASS: one manager in Persistent, both twins on the vision sensor, and an enemy in range detects a twin.");
        else Debug.Log($"{sb}STRUCTURE OK (one manager, twins on the vision sensor); detection not tested yet.");
    }

    // What the enemy DOES with what it sees: its blackboard target and its GOAP plan (goal → action), read by reflection
    // because the plan types are internal to PoT.Framework.
    private static string DescribeBrain(GameObject enemy)
    {
        var brain = enemy.GetComponent<HybridGOAP.GOAPBrainBase>();
        if (brain == null) return "no GOAP brain";
        string target = "?";
        if (brain.LinkedBlackboard != null)
        {
            brain.LinkedBlackboard.TryGetGeneric<GameObject>(CommonCore.Names.Awareness_BestTarget, out var go, null);
            target = go != null ? go.name : "none";
        }
        var plan = typeof(HybridGOAP.GOAPBrainBase).GetField("ActivePlan", Private)?.GetValue(brain);
        var elements = plan?.GetType().GetProperty("Elements")?.GetValue(plan) as IEnumerable;
        var steps = new List<string>();
        if (elements != null)
            foreach (var el in elements)
            {
                var goal = el.GetType().GetProperty("Goal")?.GetValue(el);
                var action = el.GetType().GetProperty("Action")?.GetValue(el);
                steps.Add($"{goal?.GetType().Name ?? "-"} → {action?.GetType().Name ?? "-"}");
            }
        var agent = enemy.GetComponent<NavMeshAgent>();
        string moving = agent != null && agent.isOnNavMesh ? $", speed {agent.velocity.magnitude:F1}, stopped={agent.isStopped}" : "";
        return $"target {target}; plan: {(steps.Count > 0 ? string.Join(" | ", steps) : "none")}{moving}";
    }

    private static T Field<T>(object target, string name) where T : class =>
        target.GetType().GetField(name, Private)?.GetValue(target) as T
        ?? typeof(PerceptionManager).GetField(name, Private)?.GetValue(target) as T;
}
