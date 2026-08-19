using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base for all tutorial step ScriptableObjects.
/// Holds common fields shared by every step type.
/// Concrete step types inherit this and implement Execute().
///
/// SOLID:
///   S � each subclass does exactly one thing
///   O � new step type = new class, no existing code changes
///   L � all step types substitutable via ITutorialStep
///   I � ITutorialStep is minimal (one method)
///   D � steps depend on TutorialStepContext interface, not concrete types
/// </summary>
public abstract class TutorialStepBase : ScriptableObject, ITutorialStep
{
    [Header("Stage � set on TutorialContext when this step starts")]
    public TutorialStage stage = TutorialStage.None;

    [Header("Input unlocks � applied when this step starts")]
    public bool unlockSwitch = false;
    public bool unlockAttack = false;
    public bool unlockAbility = false;
    public bool unlockRescue = false;

    [Header("Persistent hint shown during this step")]
    public string hintMessage = "";
    /// <summary>Apply input unlocks and set stage. Call at start of Execute().</summary>
    protected void ApplyCommonSetup(TutorialStepContext ctx)
    {
        TutorialContext.Instance?.SetStage(stage);

        if (unlockSwitch)
        {
            ctx.inputGate?.AllowSwitch(true);
        }
            if (unlockAttack) ctx.inputGate?.AllowAttack(true);
        if (unlockAbility) ctx.inputGate?.AllowAbility(true);
        if (unlockRescue)
        {
            ctx.inputGate?.AllowRescue(true);
            ctx.inputGate?.AllowTeleport(true);
        }
            if (!string.IsNullOrEmpty(hintMessage))
            ctx.hintDisplay?.Show(hintMessage);
        else
            ctx.hintDisplay?.Hide();
    }

    public abstract IEnumerator Execute(TutorialStepContext context, MonoBehaviour executor);
}