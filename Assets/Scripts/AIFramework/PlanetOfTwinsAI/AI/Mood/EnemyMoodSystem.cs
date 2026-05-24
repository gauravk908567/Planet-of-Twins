using UnityEngine;

/// <summary>
/// Tracks and transitions enemy mood state.
/// Mood itself snaps instantly (clear Ikari moment).
/// Stat modifiers (speed, cooldown, anticipation) lerp smoothly
/// over modifierLerpDuration from the profile.
/// Writes PoTNames.EnemyMoodState to Blackboard each frame.
/// </summary>
public class EnemyMoodSystem : MonoBehaviour
{
    [SerializeField] private MoodTransitionProfile _profile;

    private Enemy _enemy;
    private EnemyMood _currentMood;
    private float _moodTimer;
    private EnemyMood _decayMood;

    // --- Modifier lerp ---
    private MoodModifierEntry _currentModifiers;
    private MoodModifierEntry _targetModifiers;
    private float _lerpTimer;
    private float _lerpDuration;

    public EnemyMood CurrentMood => _currentMood;

    /// <summary>Current interpolated modifier values — use these in BT actions.</summary>
    public MoodModifierEntry CurrentModifiers => _currentModifiers;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _currentMood = _profile?.defaultMood ?? EnemyMood.Normal;
        var entry = GetModifierEntry(_currentMood);
        _currentModifiers = entry;
        _targetModifiers = entry;
        _lerpTimer = 0f;
        _lerpDuration = _profile?.modifierLerpDuration ?? 0.3f;
    }

    private void OnEnable() => MoodEventBus.OnSocialEvent += HandleSocialEvent;
    private void OnDisable() => MoodEventBus.OnSocialEvent -= HandleSocialEvent;

    private void Update()
    {
        if (_profile == null || _enemy == null) return;

        // Mood timer
        if (_moodTimer > 0f)
        {
            _moodTimer -= Time.deltaTime;
            if (_moodTimer <= 0f)
                TransitionTo(_decayMood, 0f, EnemyMood.Normal);
        }
        else
        {
            EvaluateHPThresholds();
        }

        // Modifier lerp
        if (_lerpTimer < _lerpDuration)
        {
            _lerpTimer += Time.deltaTime;
            float t = _lerpDuration > 0f ? Mathf.Clamp01(_lerpTimer / _lerpDuration) : 1f;
            _currentModifiers = LerpModifiers(_currentModifiers, _targetModifiers, t);
        }

        WriteToBlackboard();
    }

    private void HandleSocialEvent(EnemySocialEvent evt, GameObject source)
    {
        if (_enemy == null || _enemy.Health.IsDead || _profile == null) return;

        foreach (var rule in _profile.rules)
        {
            if (!rule.useEventTrigger || rule.triggerEvent != evt) continue;
            if (!PassesConditions(rule, source)) continue;

            TransitionTo(rule.targetMood, rule.duration, rule.decayMood);

            if (rule.vfxTag != MoodVFXTag.None)
                _enemy.GetComponentInChildren<EnemyVFXController>()
                      ?.PlayMoodReaction(rule.vfxTag);
            break;
        }
    }

    private void EvaluateHPThresholds()
    {
        if (_moodTimer > 0f) return;
        float hpNorm = _enemy.Health.CurrentHealth / _enemy.Health.MaxHealth;

        if (hpNorm <= _profile.woundedThreshold)
        {
            if (_currentMood != EnemyMood.Enraged && _currentMood != EnemyMood.Opportunistic)
                TransitionTo(EnemyMood.Wounded, 0f, EnemyMood.Wounded);
        }
        else if (hpNorm <= _profile.cautiousThreshold)
        {
            if (_currentMood == EnemyMood.Normal || _currentMood == EnemyMood.Confident)
                TransitionTo(EnemyMood.Cautious, 0f, EnemyMood.Cautious);
        }
        else if (_currentMood == EnemyMood.Cautious || _currentMood == EnemyMood.Wounded)
        {
            TransitionTo(EnemyMood.Normal, 0f, EnemyMood.Normal);
        }
    }

    public void TransitionTo(EnemyMood newMood, float duration = 0f,
                             EnemyMood decayTo = EnemyMood.Normal)
    {
        if (_currentMood == newMood) return;
        _currentMood = newMood;
        _moodTimer = duration;
        _decayMood = decayTo;

        // Start modifier lerp toward new target
        _targetModifiers = GetModifierEntry(newMood);
        _lerpTimer = 0f;
        _lerpDuration = _profile?.modifierLerpDuration ?? 0.3f;

        // Ikari fires instantly — mood snap is the readable moment
        var stateUI = GetComponentInChildren<EnemyStateUIController>();
        switch (newMood)
        {
            case EnemyMood.Opportunistic: stateUI?.ShowIkariOpportunistic(); break;
            case EnemyMood.Grieving: stateUI?.ShowIkariGrieving(); break;
            case EnemyMood.Contemptuous: stateUI?.ShowIkariContemptuous(); break;
            case EnemyMood.Enraged:
            case EnemyMood.Aggressive:
            case EnemyMood.Frustrated: stateUI?.ShowIkariRage(); break;
            case EnemyMood.Cautious:
            case EnemyMood.Wounded: stateUI?.ShowIkariFear(); break;
            case EnemyMood.Panicked: stateUI?.ShowIkariPanic(); break;
        }

        Debug.Log($"[Mood] {_enemy?.name} → {newMood} (duration={duration:F1}s)");
    }

    private MoodModifierEntry GetModifierEntry(EnemyMood mood)
    {
        if (_profile?.modifiers != null)
            foreach (var m in _profile.modifiers)
                if (m.mood == mood) return m;

        // Default fallback — Normal values
        return new MoodModifierEntry
        {
            mood = mood,
            utilityMultiplier = 1f,
            speedMultiplier = 1f,
            attackCooldownMult = 1f,
            anticipationFactor = 0.5f
        };
    }

    private static MoodModifierEntry LerpModifiers(MoodModifierEntry a,
                                                    MoodModifierEntry b,
                                                    float t)
    {
        return new MoodModifierEntry
        {
            mood = b.mood,
            utilityMultiplier = Mathf.Lerp(a.utilityMultiplier, b.utilityMultiplier, t),
            speedMultiplier = Mathf.Lerp(a.speedMultiplier, b.speedMultiplier, t),
            attackCooldownMult = Mathf.Lerp(a.attackCooldownMult, b.attackCooldownMult, t),
            anticipationFactor = Mathf.Lerp(a.anticipationFactor, b.anticipationFactor, t)
        };
    }

    private bool PassesConditions(MoodTransitionRule rule, GameObject source)
    {
        float hpNorm = _enemy.Health.CurrentHealth / _enemy.Health.MaxHealth;
        if (hpNorm > rule.maxOwnHP) return false;
        if (hpNorm < rule.minOwnHP) return false;

        // Check dark energy minimum
        if (rule.minEnergy > 0f)
        {
            var de = GetComponent<EnemyDarkEnergy>();
            if (de == null || de.CurrentEnergy < rule.minEnergy) return false;
        }

        // excludeMoods — block rule if current mood is in the exclude list
        if (rule.excludeMoods.Length > 0)
        {
            foreach (var m in rule.excludeMoods)
                if (m == _currentMood) return false;
        }

        if (rule.maxEventRange > 0f && source != null)
        {
            float dist = Vector3.Distance(transform.position, source.transform.position);
            if (dist > rule.maxEventRange) return false;
        }

        return true;
    }

    private void WriteToBlackboard()
    {
        var brain = GetComponent<PoTGOAPBrainBase>();
        brain?.LinkedBlackboard?.Set(PoTNames.EnemyMoodState, (int)_currentMood);
    }
}