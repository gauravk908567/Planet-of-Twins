using UnityEngine;

/// <summary>
/// Marks the two gameplay start positions inside an area scene.
/// One per area — place in the scene where twins should stand when gameplay begins.
///
/// Used by IntroTimelinePositioner after the intro Timeline stops.
/// No strings, no IDs — just wire two Transform children in Inspector.
///
/// SETUP:
///   1. Create empty GO in your area scene, name it "AreaSpawnPoints".
///   2. Add two child empty GOs: "SpawnLeft" and "SpawnRight".
///   3. Position them where each twin starts gameplay.
///   4. Wire leftStart and rightStart in this component's Inspector.
/// </summary>
public class AreaSpawnPoints : MonoBehaviour
{
    [SerializeField] public Transform leftStart;
    [SerializeField] public Transform rightStart;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Draw(leftStart,  new Color(0.2f, 0.6f, 1f, 0.9f), "L");
        Draw(rightStart, new Color(1f,   0.4f, 0.2f, 0.9f), "R");
    }

    private static void Draw(Transform t, Color c, string label)
    {
        if (t == null) return;
        Gizmos.color = c;
        Gizmos.DrawWireSphere(t.position, 0.35f);
        Gizmos.DrawRay(t.position, t.forward * 1f);
        UnityEditor.Handles.Label(t.position + Vector3.up * 0.7f, $"Spawn {label}");
    }
#endif
}
