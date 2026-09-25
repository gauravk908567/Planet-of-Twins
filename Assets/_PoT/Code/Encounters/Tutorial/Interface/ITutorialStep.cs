using System.Collections;
using UnityEngine;

public interface ITutorialStep
{
    /// <summary>
    /// Execute this step. Coroutine — yield return until step is complete.
    /// context holds all scene refs the step might need.
    /// executor is the MonoBehaviour used to start nested coroutines.
    /// </summary>
    IEnumerator Execute(TutorialStepContext context, MonoBehaviour executor);
}