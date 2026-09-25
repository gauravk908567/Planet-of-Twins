using UnityEngine;

/// <summary>
/// Lightweight ability feedback text above a twin's head.
/// Completely separate from the DialoguePlayer pipeline.
/// Uses NameplateDisplay but through its suppressed-by-dialogue API.
///
/// DOES NOT:
///   - go through DialoguePlayer
///   - use DialogueSequenceSO
///   - affect cooldowns or priority queues
///
/// CONFLICT PREVENTION:
///   NameplateDisplay.ShowAbilityFeedback() returns false if a dialogue
///   line is currently showing on that nameplate. AbilityFeedbackDisplay
///   respects this — if suppressed, it simply doesn't show. No override.
///
/// SETUP:
///   Add one AbilityFeedbackDisplay to each twin prefab.
///   Wire _nameplate to the NameplateDisplay on the same twin.
///   Ability scripts call Show() when ability activates.
///
/// USAGE:
///   _abilityFeedback.Show("Weavers Gate!", 1.5f);
///   _abilityFeedback.Show("Empower!", 1.2f);
/// </summary>
public class AbilityFeedbackDisplay : MonoBehaviour
{
    [SerializeField] private NameplateDisplay _nameplate;

    [Header("Default duration if not specified")]
    [SerializeField] private float _defaultDuration = 1.5f;

    [SerializeField] private AudioSource _voiceSource;
    private void Awake()
    {
        if (_nameplate == null)
            _nameplate = GetComponentInChildren<NameplateDisplay>();
    }

    /// <summary>
    /// Show ability name above this twin's head.
    /// Suppressed silently if a dialogue line is currently showing.
    /// </summary>
    public void Show(string abilityName, AudioClip voiceLine = null, float duration = -1f)
    {
        if (_nameplate == null) return;
        float d = duration > 0f ? duration : _defaultDuration;
        bool shown = _nameplate.ShowAbilityFeedback(abilityName, d);
        if (shown && voiceLine != null && _voiceSource != null)
            _voiceSource.PlayOneShot(voiceLine);
    }
}