using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// DamageNumberDisplay — attach to each enemy prefab.
///
/// SCENE SETUP:
///   Add this component to the enemy root or a child GO.
///   Wire _damageText to the TMP_Text component on the world-space Canvas child.
///   Style the TMP_Text directly in the Inspector (font, size, bold, colour).
///   Code only controls vertical offset stacking and alpha fade — nothing else.
///
/// USAGE:
///   Call ShowDamage(amount) from EnemyHealthComponent or any damage handler.
/// </summary>
public class DamageNumberDisplay : MonoBehaviour
{
    [Header("TMP Reference — style entirely in Inspector on the TMP component")]
    [Tooltip("Drag the TMP_Text from the world-space Canvas child here.")]
    [SerializeField] private TMP_Text _damageText;

    [Header("Animation")]
    [Tooltip("How far upward each number floats over its lifetime.")]
    [SerializeField] private float _floatDistance = 1.2f;

    [Tooltip("How long the number stays visible before fading.")]
    [SerializeField] private float _lifetime = 0.8f;

    [Tooltip("Fraction of lifetime spent fading out. 0.3 = last 30% of lifetime is fade.")]
    [SerializeField] private float _fadeFraction = 0.35f;

    [Tooltip("Vertical gap between stacked numbers when multiple hits land close together.")]
    [SerializeField] private float _stackSpacing = 0.4f;

    [Tooltip("How fast the number floats upward. Higher = faster rise.")]
    [SerializeField] private float _floatSpeed = 1.5f;

    // ── Runtime ───────────────────────────────────────────────
    private readonly List<Coroutine> _active = new();
    // Monotonically increasing — never decremented — so new numbers always
    // spawn above currently fading ones, never overlapping them.
    private int _spawnCount = 0;

    /// <summary>
    /// Call this whenever the enemy takes damage.
    /// amount — raw damage value, displayed as a whole number.
    /// </summary>
    public void ShowDamage(float amount)
    {
        if (_damageText == null) return;

        // Use spawn count not active count — fading numbers don't reduce offset
        float stackOffset = _spawnCount * _stackSpacing;
        _spawnCount++;

        var co = StartCoroutine(AnimateNumber(Mathf.RoundToInt(amount), stackOffset));
        _active.Add(co);
    }

    private IEnumerator AnimateNumber(int amount, float startOffset)
    {
        // Store original values so we restore after — text is shared
        // Each coroutine works on its own copy of position/alpha
        // since TMP only has one text field we use a pool approach:
        // spawn a runtime clone of the text GO for each number

        if (_damageText == null) { _active.RemoveAt(0); yield break; }

        // Clone the TMP GO so simultaneous numbers are independent
        var clone = Instantiate(_damageText.gameObject, _damageText.transform.parent);
        clone.SetActive(true);

        var tmp = clone.GetComponent<TMP_Text>();
        if (tmp == null) { Destroy(clone); _active.RemoveAt(0); yield break; }

        // Set text — designer controls all style via the prefab TMP component
        tmp.text = amount.ToString();

        // Starting position — apply stack offset
        Vector3 localStart = _damageText.transform.localPosition + Vector3.up * startOffset;
        clone.transform.localPosition = localStart;

        // Store original alpha to respect designer colour
        Color originalColor = tmp.color;
        float elapsed = 0f;
        float fadeStartTime = _lifetime * (1f - _fadeFraction);

        while (elapsed < _lifetime)
        {
            float t = elapsed / _lifetime;

            // Float upward — speed-driven, independent of lifetime
            clone.transform.localPosition = localStart + Vector3.up * (_floatSpeed * elapsed);

            // Fade alpha in the last _fadeFraction of lifetime
            if (elapsed >= fadeStartTime)
            {
                float fadeT = (elapsed - fadeStartTime) / (_lifetime - fadeStartTime);
                tmp.color = new Color(originalColor.r, originalColor.g, originalColor.b,
                                      Mathf.Lerp(1f, 0f, fadeT));
            }
            else
            {
                tmp.color = originalColor;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(clone);

        // Remove this coroutine from active list
        if (_active.Count > 0) _active.RemoveAt(0);

        // Reset spawn counter when all numbers have cleared — keeps offsets
        // from drifting infinitely upward across a long fight
        if (_active.Count == 0) _spawnCount = 0;
    }
}