/// <summary>
/// Optional tutorial gate. TwinInputReader checks this before returning
/// any gated input. If no gate is wired (null), all input is allowed —
/// works correctly in non-tutorial scenes without TutorialManager.
/// 
/// Implemented by TutorialInputGate. Wire it to TwinInputReader in the
/// tutorial scene only. All other scenes leave the field null.
/// </summary>
public interface ITutorialGate
{
    bool IsAttackAllowed { get; }
    bool IsAbilityAllowed { get; }
    bool IsTeleportAllowed { get; }
    bool IsRescueAllowed { get; }
    bool IsSwitchAllowed { get; }
    bool IsInteractAllowed { get; }
}