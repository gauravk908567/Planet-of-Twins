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
    // Accord melee shares the consolidated Attack book (6 ids): On_AccordMeleeSlashKai/Lyra = per-twin slash,
    // On_AccordMeleeHit = per enemy struck. Pulled from PlayerVfxLibrary.Attack (R4, lazy) — the AccordMelee slot
    // is a dead leftover (no ids authored on it); do not resolve it.
    private CueBookData _cueBook;

    public AccordMeleeAbility(
        MonoBehaviour runner,
        LayerMask enemyLayer,
        float range = 5f,
        float arcDegrees = 120f,
        float aggroLossDuration = 0.75f,
        float slowDuration = 1.25f,
        float speedMultiplier = 0.4f,
        float baseMeleeDamage = 20f)
    {
        _runner = runner;
        _enemyLayer = enemyLayer;
        _range = range;
        _arc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
        _aggroLossDuration = aggroLossDuration;
        _slowDuration = slowDuration;
        _speedMultiplier = speedMultiplier;
        _damage = baseMeleeDamage * 1.25f;
    }

    // isKai selects the per-twin slash id (Kai=right/Vethara, Lyra=left/Luminari). Called once per twin.
    public void Execute(Transform origin, bool isKai)
    {
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Attack;  // R4 — consolidated Attack book from PlayerVfxLibrary
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

            // Accord hit cue on enemy — World-anchored at chest height so it is visible.
            if (_cueBook != null)
            {
                Vector3 hitPos = hit.transform.position + Vector3.up * 1f;
                FxManager.Instance?.PlayBook(_cueBook, FxIds.Player.Attack.On_AccordMeleeHit,
                    new CueContext(hitPos, hit.transform.rotation));
            }
        }

        // Per-twin accord slash on the swinging twin — Follows the swing.
        if (_cueBook != null)
        {
            string slashId = isKai ? FxIds.Player.Attack.On_AccordMeleeSlashKai
                                   : FxIds.Player.Attack.On_AccordMeleeSlashLyra;
            FxManager.Instance?.PlayBook(_cueBook, slashId, CueContext.Follow(origin));
        }

        Debug.Log($"[AccordMelee] Execute from {origin.name} — hit {(hitAnything ? "enemies" : "nothing")} range={_range} arc={_arc:F2}");
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