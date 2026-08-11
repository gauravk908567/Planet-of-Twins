using UnityEngine;

/// <summary>
/// Plays a shield-ripple VFX at the bullet impact point — cue-driven (Banned Lazy Work #10).
/// The cue's VFX-graph instance is positioned at the contact point (centre the ripple on its own
/// transform in the graph instead of the old SetVector3("SphereCenter", …)). No Instantiate/Destroy.
/// </summary>
public class SpawnShieldRipples : MonoBehaviour
{
    [Tooltip("Cue Book — plays the \"impact\" effect at the bullet contact point.")]
    [SerializeField] private CueBookData _cueBook;

    private void OnCollisionEnter(Collision co)
    {
        if (_cueBook == null || !co.gameObject.CompareTag("Bullet")) return;
        FxManager.Instance?.PlayBook(_cueBook, "impact", new CueContext(co.contacts[0].point));
    }
}
