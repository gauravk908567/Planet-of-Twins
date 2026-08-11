/// <summary>
/// Implemented by AccordStateSystem.
/// TwinAttackDispatcher reads IsAccordActive to route E input
/// to accord melee instead of normal melee during Accord State.
/// </summary>
public interface IAccordModeProvider
{
    bool IsAccordActive { get; }

    /// <summary>
    /// Called by TwinAttackDispatcher on E press during Accord State.
    /// AccordStateSystem executes the shared accord melee on both twins.
    /// </summary>
    void ExecuteAccordMelee();
}