using System;
using UnityEngine;

/// <summary>
/// Couch — "either player drives shared UI" aggregator (the P-A keystone). Wraps the two per-slot
/// <see cref="IInputProvider"/>s (P1 + P2) so a SHARED, non-per-twin control answers to EITHER device:
/// pause, skill-tree tab/instant-buy/back, popup dismissal, overview, intro-skip.
///
/// <para><b>Where it's used.</b> Returned by <see cref="PlayerInputRouter.Shared"/> only. Per-twin gameplay
/// stays on <see cref="PlayerInputRouter.For"/> (concrete P1/P2) and is UNAFFECTED. Menu cursor MOVE + Submit
/// already reach any device through the deviceless EventSystem UI module — this aggregator is what unblocks the
/// POLLED reads (LB/RB, Y instant-buy, B cancel, pause) that route through a single provider.</para>
///
/// <para><b>Semantics.</b> Button getters = P1 OR P2, evaluated <b>P1 first</b> so P1 wins a same-frame tie
/// (the user's "exact same time → pick one" rule). Movement = P1 when non-zero, else P2. Device-identity reads
/// that must name ONE device — <see cref="GetBindingDisplay"/> (glyph text), <see cref="ResetBindingsToDefault"/>,
/// <see cref="SetGameplayFrozen"/> (a shared static anyway) — delegate to P1. In solo, P2 falls back to P1
/// (same object), so every read collapses to P1 → <b>byte-identical to the pre-couch single-reader path</b>.</para>
///
/// <para>Holds no state and caches nothing — it asks the router for the current P1/P2 each call, so it always
/// reflects the live pairing (a device joining/leaving mid-session just changes what the getters resolve to).</para>
/// </summary>
public sealed class AnyPlayerInputProvider : IInputProvider
{
    private readonly Func<IInputProvider> _p1;
    private readonly Func<IInputProvider> _p2;

    public AnyPlayerInputProvider(Func<IInputProvider> p1, Func<IInputProvider> p2)
    {
        _p1 = p1;
        _p2 = p2;
    }

    private IInputProvider P1 => _p1?.Invoke();
    private IInputProvider P2 => _p2?.Invoke();

    // P1 OR P2, P1 first (P1 wins same-frame ties). Skips P2 when it resolves to the same object as P1 (solo
    // fallback) so a read is never double-counted and solo stays exactly P1.
    private bool Any(Func<IInputProvider, bool> read)
    {
        var a = P1;
        if (a != null && read(a)) return true;
        var b = P2;
        return b != null && !ReferenceEquals(b, a) && read(b);
    }

    // ── Movement — P1 priority, else P2 ───────────────────────
    public Vector2 GetMovementInput()
    {
        var a = P1;
        Vector2 va = a != null ? a.GetMovementInput() : Vector2.zero;
        if (va.sqrMagnitude > 0.0001f) return va;
        var b = P2;
        return (b != null && !ReferenceEquals(b, a)) ? b.GetMovementInput() : va;
    }

    public Vector3 GetMovementDirection()
    {
        Vector2 v = GetMovementInput();
        return new Vector3(v.x, 0f, v.y).normalized;
    }

    // ── Gameplay bools (implemented for completeness; shared consumers don't read these — they use For(twin)) ──
    public bool GetAttackDown()        => Any(p => p.GetAttackDown());
    public bool GetSwitchDown()        => Any(p => p.GetSwitchDown());
    public bool GetAbilityDown()       => Any(p => p.GetAbilityDown());
    public bool GetTeleportHeld()      => Any(p => p.GetTeleportHeld());
    public bool GetTeleportReleased()  => Any(p => p.GetTeleportReleased());
    public bool GetRescueMash()        => Any(p => p.GetRescueMash());
    public bool GetInteractDown()      => Any(p => p.GetInteractDown());
    public bool GetCancelHeld()        => Any(p => p.GetCancelHeld());
    public bool GetEmpowerHeld()       => Any(p => p.GetEmpowerHeld());
    public bool GetStruggleMash()      => Any(p => p.GetStruggleMash());
    public bool GetSoulBreakMash()     => Any(p => p.GetSoulBreakMash());
    public bool GetConvergenceHeld()   => Any(p => p.GetConvergenceHeld());

    // ── Shared-UI action reads — the ones this aggregator exists for ──
    public bool GetOverviewDown()      => Any(p => p.GetOverviewDown());
    public bool GetOverviewHeld()      => Any(p => p.GetOverviewHeld());
    public bool GetPauseDown()         => Any(p => p.GetPauseDown());
    public bool GetSkillTreeToggleDown() => Any(p => p.GetSkillTreeToggleDown());
    public bool GetQTEMashDown()       => Any(p => p.GetQTEMashDown());
    public bool GetAnySkipDown()       => Any(p => p.GetAnySkipDown());
    public bool GetHintsToggleDown()   => Any(p => p.GetHintsToggleDown());
    public bool GetUITabLeftDown()     => Any(p => p.GetUITabLeftDown());
    public bool GetUITabRightDown()    => Any(p => p.GetUITabRightDown());
    public bool GetInstantBuyDown()    => Any(p => p.GetInstantBuyDown());
    public bool GetUICancelDown()      => Any(p => p.GetUICancelDown());
    public bool GetUIPreviewDown()     => Any(p => p.GetUIPreviewDown());

    // ── Device-identity reads — must name ONE device → delegate to P1 ──
    public void SetGameplayFrozen(bool frozen) => P1?.SetGameplayFrozen(frozen);   // static shared policy anyway
    public string GetBindingDisplay(string actionName, bool preferGamepad = false)
        => P1?.GetBindingDisplay(actionName, preferGamepad) ?? "?";
    public void ResetBindingsToDefault() => P1?.ResetBindingsToDefault();
}
