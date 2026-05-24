using UnityEngine;

/// <summary>
/// Place on any GO with a trigger collider to start a tutorial sequence
/// when the required twin(s) enter the zone.
///
/// RequiredTwin options:
///   Either — any twin entering fires the tutorial
///   Left   — only Lyra entering fires it
///   Right  — only Kai entering fires it
///   Both   — both twins must be inside simultaneously
///
/// SETUP:
///   Add to a GO with a trigger collider.
///   Wire director → TutorialDirector in scene.
///   Wire leftTwin and rightTwin.
///   Set requiredTwin and stageOnEnter.
///   fireOnce = true for story tutorials (recommended).
/// </summary>
public class TutorialZoneTrigger : MonoBehaviour
{
    public enum RequiredTwin { Either, Left, Right, Both }

    [Header("Twins")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Director to start")]
    [SerializeField] private TutorialDirector director;

    [Header("Stage set on TutorialContext when fired")]
    [SerializeField] private TutorialStage stageOnEnter = TutorialStage.None;

    [Header("Trigger settings")]
    [SerializeField] private RequiredTwin requiredTwin = RequiredTwin.Either;
    [SerializeField] private bool fireOnce = true;

    private bool _fired = false;
    private bool _leftInside = false;
    private bool _rightInside = false;

    private void OnTriggerEnter(Collider other)
    {
        if (fireOnce && _fired) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        if (player == leftTwin) _leftInside = true;
        if (player == rightTwin) _rightInside = true;

        if (ShouldFire(player))
            Fire();
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        if (player == leftTwin) _leftInside = false;
        if (player == rightTwin) _rightInside = false;
    }

    private bool ShouldFire(Player entering)
    {
        return requiredTwin switch
        {
            RequiredTwin.Either => true,
            RequiredTwin.Left => entering == leftTwin,
            RequiredTwin.Right => entering == rightTwin,
            RequiredTwin.Both => _leftInside && _rightInside,
            _ => false
        };
    }

    private void Fire()
    {
        _fired = true;

        if (stageOnEnter != TutorialStage.None)
            TutorialContext.Instance?.SetStage(stageOnEnter);

        director?.StartTutorial();
    }

    public void ResetTrigger()
    {
        _fired = false;
        _leftInside = false;
        _rightInside = false;
    }
}