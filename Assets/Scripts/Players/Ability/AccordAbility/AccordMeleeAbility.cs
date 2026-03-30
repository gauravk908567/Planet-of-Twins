using System.Collections;
using UnityEngine;

public class AccordMeleeAbility
{
    private readonly float _range;
    private readonly float _arc;
    private readonly float _aggroLossDuration;
    private readonly float _slowDuration;
    private readonly float _speedMultiplier;
    private readonly float _damage;
    private readonly LayerMask _enemyLayer;
    private readonly MonoBehaviour _runner;
    private readonly GameObject _slashPrefab;
    private readonly GameObject _hitPrefab;

    public AccordMeleeAbility(
        MonoBehaviour runner,
        LayerMask enemyLayer,
        float range = 5f,
        float arcDegrees = 120f,
        float aggroLossDuration = 0.75f,
        float slowDuration = 1.25f,
        float speedMultiplier = 0.4f,
        float baseMeleeDamage = 20f,
        GameObject slashPrefab = null,
        GameObject hitPrefab = null)
    {
        _runner = runner;
        _enemyLayer = enemyLayer;
        _range = range;
        _arc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
        _aggroLossDuration = aggroLossDuration;
        _slowDuration = slowDuration;
        _speedMultiplier = speedMultiplier;
        _damage = baseMeleeDamage * 1.25f;
        _slashPrefab = slashPrefab;
        _hitPrefab = hitPrefab;
    }

    public void Execute(Transform origin)
    {
        Collider[] hits = Physics.OverlapSphere(origin.position, _range, _enemyLayer);
        Debug.Log($"[AccordMelee] OverlapSphere found {hits.Length} colliders — layer={_enemyLayer.value}");
        bool hitAnything = false;

        foreach (Collider hit in hits)
        {
            Vector3 dir = (hit.transform.position - origin.position).normalized;
            float dot = Vector3.Dot(origin.forward, dir);
            Debug.Log($"[AccordMelee] Hit={hit.name} dot={dot:F2} arcThreshold={_arc:F2} pass={dot >= _arc}");

            if (dot < _arc) continue;

            var enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            hitAnything = true;
            _runner.StartCoroutine(ApplyEffects(enemy));

            // Deal damage — 25% above base melee
            enemy.GetComponent<EnemyHealthComponent>()?.TakeDamage(
                new DamageData(_damage, DamageType.Ability));

            // Hit VFX on enemy — offset upward to chest height so it is visible
            Vector3 hitPos = hit.transform.position + Vector3.up * 1f;
            SpawnOneShot(_hitPrefab, hitPos, hit.transform.rotation);
        }

        // Slash VFX on the swinging twin
        SpawnOneShot(_slashPrefab, origin.position, origin.rotation);

        Debug.Log($"[AccordMelee] Execute from {origin.name} — hit {(hitAnything ? "enemies" : "nothing")} range={_range} arc={_arc:F2}");
    }

    private static void SpawnOneShot(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        var go = Object.Instantiate(prefab, pos, rot);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
            Object.Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax + 0.1f);
        else
            Object.Destroy(go, 0.5f);
    }

    private IEnumerator ApplyEffects(Enemy enemy)
    {
        enemy.ClearTarget();

        float originalSpeed = enemy.Data?.moveSpeed ?? 3f;
        enemy.Movement?.SetSpeed(originalSpeed * _speedMultiplier);

        float elapsed = 0f;
        while (elapsed < _slowDuration)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (enemy != null && enemy.gameObject.activeInHierarchy)
            enemy.Movement?.SetSpeed(originalSpeed);
    }
}