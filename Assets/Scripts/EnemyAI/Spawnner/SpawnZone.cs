using System.Collections.Generic;
using UnityEngine;

public class SpawnZone : MonoBehaviour
{
    [Header("Config")]
    public AreaZoneConfig areaConfig;

    [Header("Spawn Points — handplace in this area")]
    public Transform[] leftSpawnPoints;
    public Transform[] rightSpawnPoints;

    [Header("Detection")]
    [Tooltip("Layer mask for players — zone activates when any player enters")]
    [SerializeField] private LayerMask playerLayer;

    // Notifies EnemySpawner
    public event System.Action<SpawnZone> OnZoneEntered;
    public event System.Action<SpawnZone> OnZoneExited;

    private readonly HashSet<GameObject> _playersInZone = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
      //  Debug.Log($"[SpawnZone] {gameObject.name} TriggerEnter by {other.gameObject.name} layer={other.gameObject.layer}");
        if (((1 << other.gameObject.layer) & playerLayer) == 0)
        {
           // Debug.Log($"[SpawnZone] {gameObject.name} — layer {other.gameObject.layer} failed playerLayer mask {playerLayer.value}");
            return;
        }
       // Debug.Log($"[SpawnZone] {gameObject.name} — passed layer check, subscribers={OnZoneEntered?.GetInvocationList()?.Length ?? 0}");
        if (_playersInZone.Add(other.gameObject) && _playersInZone.Count == 1)
        {
           // Debug.Log($"[SpawnZone] {gameObject.name} — firing OnZoneEntered");
            OnZoneEntered?.Invoke(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        if (_playersInZone.Remove(other.gameObject) && _playersInZone.Count == 0)
            OnZoneExited?.Invoke(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        var col = GetComponent<BoxCollider>();
        if (col != null)
            Gizmos.DrawCube(
                transform.position + col.center,
                Vector3.Scale(col.size, transform.lossyScale));

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.7f);
        if (leftSpawnPoints != null)
            foreach (var p in leftSpawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.7f);
        if (rightSpawnPoints != null)
            foreach (var p in rightSpawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);
    }
}