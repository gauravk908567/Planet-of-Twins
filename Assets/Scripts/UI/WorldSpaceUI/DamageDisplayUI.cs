using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// DamageNumberDisplay — Overwatch-style accumulating damage number.
///
/// BEHAVIOUR:
///   First hit  → number spawns at enemy position, floats up to _floatHeight,
///                then stays there and begins fading.
///   Subsequent hit within _lifetime → accumulated total updates, fade resets.
///                Number does NOT re-float — stays at settled height.
///   After _lifetime with no new hits → fades out and destroys.
///
/// SCENE SETUP:
///   Attach to enemy root or a child GO.
///   Wire _damageText to the TMP_Text on the world-space Canvas child.
///   Style font/size/colour entirely in Inspector — code only controls position/alpha.
/// </summary>
public class DamageNumberDisplay : MonoBehaviour
{
    [Header("TMP Reference")]
    [Tooltip("Drag the TMP_Text from the world-space Canvas child here.")]
    [SerializeField] private TMP_Text _damageText;

    [Header("Float Animation")]
    [Tooltip("Distance the number rises from spawn before settling.")]
    [SerializeField] private float _floatHeight = 1.0f;

    [Tooltip("Time in seconds to complete the float-up animation.")]
    [SerializeField] private float _floatDuration = 0.25f;

    [Header("Lifetime and Fade")]
    [Tooltip("Total seconds a number lives without a new hit. Resets on new damage.")]
    [SerializeField] private float _lifetime = 1.5f;

    [Tooltip("Fraction of lifetime spent fading out. 0.4 = last 40% fades.")]
    [SerializeField] private float _fadeFraction = 0.4f;

    // ── Runtime ───────────────────────────────────────────────
    private float _accumulated;
    private Coroutine _displayCoroutine;
    private bool _resetPending;
    private GameObject _activeClone; // tracked so OnDisable can destroy it if pool returns mid-animation

    private void OnDisable()
    {
        // Enemy returned to pool mid-animation — destroy clone so it doesn't persist
        if (_activeClone != null)
        {
            Destroy(_activeClone);
            _activeClone = null;
        }
        _accumulated = 0f;
        _displayCoroutine = null;
        _resetPending = false;
    }

    /// <summary>Call whenever this enemy takes damage.</summary>
    public void ShowDamage(float amount)
    {
        if (amount <= 0f) return;
        _accumulated += amount;

        if (_displayCoroutine != null)
        {
            _resetPending = true; // signal active coroutine to reset its fade timer
        }
        else
        {
            _accumulated = amount;
            _displayCoroutine = StartCoroutine(AnimateNumber());
        }
    }

    private IEnumerator AnimateNumber()
    {
        if (_damageText == null) yield break;

        // Clone the TMP GO so the original prefab is untouched
        var clone = Instantiate(_damageText.gameObject, _damageText.transform.parent);
        clone.SetActive(true);
        _activeClone = clone;

        var tmp = clone.GetComponent<TMP_Text>();
        if (tmp == null) { Destroy(clone); _displayCoroutine = null; yield break; }

        Color baseColor = _damageText.color;
        Vector3 spawnLocal = _damageText.transform.localPosition;
        Vector3 settledLocal = spawnLocal + Vector3.up * _floatHeight;
        clone.transform.localPosition = spawnLocal;

        // ── Phase 1: Float up ──────────────────────────────────
        float elapsed = 0f;
        while (elapsed < _floatDuration)
        {
            tmp.text = Mathf.RoundToInt(_accumulated).ToString();
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _floatDuration);
            clone.transform.localPosition = Vector3.Lerp(spawnLocal, settledLocal, t);
            SetAlpha(tmp, baseColor, 1f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        clone.transform.localPosition = settledLocal;

        // ── Phase 2: Settle + Fade (resets on new hit) ─────────
        float fadeStartNorm = 1f - _fadeFraction;
        float age = 0f;
        _resetPending = false;

        while (true)
        {
            tmp.text = Mathf.RoundToInt(_accumulated).ToString();

            if (_resetPending)
            {
                age = 0f;
                _resetPending = false;
                SetAlpha(tmp, baseColor, 1f);
            }

            float norm = age / _lifetime;

            if (norm >= fadeStartNorm)
            {
                float fadeT = Mathf.InverseLerp(fadeStartNorm, 1f, norm);
                SetAlpha(tmp, baseColor, Mathf.Lerp(1f, 0f, fadeT));
            }
            else
            {
                SetAlpha(tmp, baseColor, 1f);
            }

            if (age >= _lifetime) break;

            age += Time.deltaTime;
            yield return null;
        }

        Destroy(clone);
        _activeClone = null;
        _accumulated = 0f;
        _displayCoroutine = null;
    }

    private static void SetAlpha(TMP_Text tmp, Color baseColor, float alpha)
    {
        tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }
}