using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial sequence runner. Genuinely dumb — iterates steps[], nothing else.
///
/// Does NOT auto-start. Call StartTutorial() from a Timeline Signal
/// at the end of the intro cutscene.
///
/// SETUP:
///   1. Wire TutorialStepContext fields in Inspector
///   2. Create step SOs, drag into steps[] in order
///   3. Add Signal Track to Timeline, emit signal at cutscene end
///   4. Add SignalReceiver to TutorialManager GO
///   5. Wire signal → TutorialDirector.StartTutorial()
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [Header("Steps — drag SOs in order")]
    [SerializeField] private TutorialStepBase[] steps;

    [Header("Scene context — all scene refs steps might need")]
    [SerializeField] private TutorialStepContext context;

    private bool _started = false;

    private void Awake()
    {
        context.Resolve();
        context.inputGate?.LockAll();
        context.SelectionLock?.LockSelection();
    }

    /// <summary>
    /// Called by Timeline Signal at end of intro cutscene.
    /// Safe to call multiple times — only starts once.
    /// </summary>
    public void StartTutorial()
    {

        Debug.Log($"[TutorialDirector] StartTutorial called — _started={_started}");
        if (_started) return;
        _started = true;
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(0.3f);

        foreach (var step in steps)
        {
            if (step == null) continue;
            yield return step.Execute(context, this);
        }
    }
}