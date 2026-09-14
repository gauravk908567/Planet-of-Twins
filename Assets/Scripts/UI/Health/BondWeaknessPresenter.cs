using System;
using UnityEngine;

/// <summary>
/// Feeds each twin's BOND WEAKNESS into its own half of a <see cref="UIBarHealthView"/>.
///
/// This exists because health and bond are two different facts that the old HUD merged into one
/// number. <c>PlayerHealthComponent.DisplayHealth</c> multiplies real health by the distance
/// modifier, so simply walking apart made the bar fall as if damage had been taken — the players
/// read "we are dying" when nothing had been lost. The fix is not a better bar, it is two
/// channels:
///
///   FILL  ← <see cref="PlayerHealthComponent.SurvivalHealth01"/> — what you actually have left.
///           Driven by SharedHealthPresenter (the pool), never by this class.
///   DRAIN ← <see cref="PlayerHealthComponent.BondWeakness01"/> — how weakened the bond is.
///           Greys out the colour of a half WITHOUT moving its fill. Grey reads as "weakened";
///           a shorter bar reads as "dying". Only one of those is true when the twins separate.
///
/// A half can therefore be 100% full and 100% drained at the same time, which is exactly the
/// state the game means when the twins are far apart at full health.
///
/// GENERIC BY DESIGN: it drives a LIST of twin→bar-index pairs rather than hard-coding Kai and
/// Lyra. The shared emblem uses two entries (one per half); a future per-twin bar uses one entry
/// each; a three-way bar would use three. Nothing here knows which twin is which.
///
/// R1: the twins and this HUD both live in Persistent, so serialized references are correct and
/// no lookup is needed. R8: named handlers, subscribed in Start (the twins' components exist by
/// Awake but resolving others is Start's job), unsubscribed in OnDestroy.
/// </summary>
[DisallowMultipleComponent]
public class BondWeaknessPresenter : MonoBehaviour
{
    [Serializable]
    public struct TwinBinding
    {
        [Tooltip("The twin whose bond weakness drives this bar half.")]
        public PlayerHealthComponent health;

        [Tooltip("Index into the target view's bars array. On the shared emblem: 0 = left half, " +
                 "1 = right half. Must match the order the halves are listed in UIBarHealthView.")]
        public int barIndex;
    }

    [Tooltip("The bar view whose halves are drained. Usually the shared emblem's UIBarHealthView.")]
    [SerializeField] private UIBarHealthView _target;

    [Tooltip("One entry per bar half that should show a twin's bond weakness.")]
    [SerializeField] private TwinBinding[] _bindings;

    // Handlers are stored per binding so each twin's event can be unsubscribed with the exact
    // delegate it was subscribed with. A lambda closing over the loop variable could not be
    // removed later (R8 bans -=-proof lambdas), and every twin needs a different bar index, so
    // one shared method would not know which half to write.
    private Action<float>[] _handlers;

    // ── BOND REFRAME (game.md §4.1, 2026-09-09) ────────────────────────────────
    // The BUG-091 rollback that disabled this grey-drain channel is LIFTED. The shared-health FILL
    // now reads the real, distance-independent pool (SharedHealthPresenter → CombinedSurvival01), so
    // distance no longer moves the fill — this channel is the ONLY thing distance drives (grey, never
    // shrink). One signal per channel means the double-count that forced the rollback can't recur.
    // (The old `_rollbackDisabled` serialized bool was removed; any leftover value in the scene YAML
    // is a dead remnant and is ignored.)
    private void Start()
    {
        if (_target == null)
        {
            Debug.LogError($"[{nameof(BondWeaknessPresenter)}] No UIBarHealthView assigned — bond " +
                           "weakness will never show. Disabling.", this);
            enabled = false;
            return;
        }

        if (_bindings == null || _bindings.Length == 0)
        {
            Debug.LogError($"[{nameof(BondWeaknessPresenter)}] No twin bindings assigned — bond " +
                           "weakness will never show. Disabling.", this);
            enabled = false;
            return;
        }

        _handlers = new Action<float>[_bindings.Length];

        for (int i = 0; i < _bindings.Length; i++)
        {
            var binding = _bindings[i];
            if (binding.health == null)
            {
                Debug.LogError($"[{nameof(BondWeaknessPresenter)}] Binding {i} has no " +
                               "PlayerHealthComponent — that bar half will never drain.", this);
                continue;
            }

            int barIndex = binding.barIndex;                       // captured per binding
            _handlers[i] = weakness => _target.SetBondWeakness(barIndex, weakness);
            binding.health.OnBondWeaknessChanged += _handlers[i];

            // Push the current value immediately. The event only fires when the distance modifier
            // CHANGES, so a half that starts weakened (mid-save load, checkpoint respawn far
            // apart) would otherwise sit un-drained until the twins next moved.
            _target.SetBondWeakness(barIndex, binding.health.BondWeakness01);
        }
    }

    private void OnDestroy()
    {
        if (_handlers == null || _bindings == null) return;

        for (int i = 0; i < _bindings.Length && i < _handlers.Length; i++)
        {
            if (_bindings[i].health == null || _handlers[i] == null) continue;
            _bindings[i].health.OnBondWeaknessChanged -= _handlers[i];
        }
        _handlers = null;
    }
}
