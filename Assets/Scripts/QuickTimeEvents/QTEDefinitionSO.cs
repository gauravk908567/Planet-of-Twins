using UnityEngine;

/// <summary>
/// Data-only ScriptableObject for one QTE event.
/// No scene references — holds timings, input key, and a unique event ID.
///
/// Create: right-click Assets > Create > PoT > QTE Definition
/// One asset per distinct QTE event (ParkGate, Street1Bridge, etc.)
/// </summary>
[CreateAssetMenu(menuName = "PoT/QTE Definition", fileName = "QTE_")]
public class QTEDefinitionSO : ScriptableObject
{
    [Header("Mash")]
    public float mashDuration = 5f;
    public int mashCountRequired = 20;
    [Tooltip("LEGACY (P13): reads route through IInputProvider.GetQTEMashDown (the QTEMash action, F). " +
             "Kept only so existing assets keep their serialized value; not read at runtime.")]
    public KeyCode mashKey = KeyCode.F;

    [Header("Approach")]
    [Tooltip("If true, a countdown timer also runs during WaitingForPlayers phase.")]
    public bool isTimedApproach = false;
    public float windowDuration = 15f;

    [Header("UI Text")]
    public string instructionText = "Press F!";

    [Header("Identity")]
    [Tooltip("Unique string ID — QTESuccessWatcher and analytics use this to " +
             "identify which QTE fired. Keep it stable once assigned.")]
    public string eventId;
}
