using UnityEngine;

/// <summary>
/// The Manpu QUIET channel (MANPU_SYSTEM.md synthesis) — a continuous, subtle body tint by mood
/// CATEGORY, so the mood ecology is *felt* even between glyph pulses (the loud channel). It reads mood,
/// owns no mood state. It deliberately YIELDS while an ability tint owns the body (stun = blue,
/// possess = purple, set in <c>Enemy.cs</c>) and resumes after — one body, never two writers fighting.
///
/// Colours + strength are tunable here (the deferred "visual" decision); the logic is the contract.
/// Uses <c>material.color</c> to match how Enemy.cs already tints (consistent main-colour assumption).
/// </summary>
public class MoodAmbient : MonoBehaviour
{
    [SerializeField] private EnemyMoodSystem _moodSystem;
    [SerializeField] private Enemy _enemy;
    [Tooltip("Body renderers to tint. Auto-filled from children if empty.")]
    [SerializeField] private Renderer[] _bodyRenderers;

    [Header("Tuning")]
    [Range(0f, 1f)] [SerializeField] private float _tintStrength = 0.22f;
    [SerializeField] private float _lerpSpeed = 3f;

    [Header("Category colours")]
    [SerializeField] private Color _neutral = Color.white;
    [SerializeField] private Color _aggressive = new Color(1f, 0.55f, 0.45f);
    [SerializeField] private Color _defensive = new Color(0.55f, 0.7f, 1f);
    [SerializeField] private Color _distressed = new Color(1f, 0.9f, 0.5f);
    [SerializeField] private Color _story = new Color(0.75f, 0.65f, 0.9f);

    private Color[] _baseColors;
    private Color _targetCategory = Color.white;

    private void Awake()
    {
        if (_moodSystem == null) _moodSystem = GetComponent<EnemyMoodSystem>();
        if (_enemy == null) _enemy = GetComponent<Enemy>();
        if (_bodyRenderers == null || _bodyRenderers.Length == 0)
            _bodyRenderers = GetComponentsInChildren<Renderer>(true);

        CaptureBase();
        _targetCategory = CategoryColor(_moodSystem != null ? _moodSystem.CurrentMood : EnemyMood.Normal);
    }

    private void OnEnable()
    {
        if (_moodSystem != null) _moodSystem.OnMoodChanged += HandleMood;
    }

    private void OnDisable()
    {
        if (_moodSystem != null) _moodSystem.OnMoodChanged -= HandleMood;
    }

    private void HandleMood(EnemyMood oldMood, EnemyMood newMood) => _targetCategory = CategoryColor(newMood);

    private void Update()
    {
        if (_bodyRenderers == null) return;
        // R1-style yield: while an ability owns the body tint, leave it alone (no two writers).
        if (_enemy != null && (_enemy.IsStunned || _enemy.IsPossessed)) return;

        float k = _lerpSpeed * Time.deltaTime;
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            var r = _bodyRenderers[i];
            if (r == null) continue;
            Color want = Color.Lerp(_baseColors[i], _targetCategory, _tintStrength);
            MaterialTint.SetColor(r.material, Color.Lerp(MaterialTint.GetColor(r.material), want, k));
        }
    }

    private void CaptureBase()
    {
        if (_bodyRenderers == null) { _baseColors = new Color[0]; return; }
        _baseColors = new Color[_bodyRenderers.Length];
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            var r = _bodyRenderers[i];
            _baseColors[i] = (r != null && r.sharedMaterial != null) ? MaterialTint.GetColor(r.sharedMaterial) : Color.white;
        }
    }

    private Color CategoryColor(EnemyMood mood)
    {
        switch (mood)
        {
            case EnemyMood.Confident:
            case EnemyMood.Opportunistic:
            case EnemyMood.Aggressive:
            case EnemyMood.Enraged: return _aggressive;

            case EnemyMood.Cautious:
            case EnemyMood.Wounded: return _defensive;

            case EnemyMood.Frustrated:
            case EnemyMood.Panicked: return _distressed;

            case EnemyMood.Grieving:
            case EnemyMood.Contemptuous:
            case EnemyMood.Territorial:
            case EnemyMood.Curious: return _story;

            default: return _neutral; // Normal
        }
    }
}
