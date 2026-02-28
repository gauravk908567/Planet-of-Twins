using UnityEngine;
using UnityEngine.Events;

public class BoundaryController : MonoBehaviour
{
    [SerializeField] private GameObject conditionEvaluatorObject;

    [Header("Reactions")]
    [Tooltip("What should happen when the conditions are met?")]
    [SerializeField] private UnityEvent onConditionsMetReaction;

    private IConditionEvaluator conditionEvaluator;

    private void Awake()
    {
        if (conditionEvaluatorObject != null)
        {
            conditionEvaluator = conditionEvaluatorObject.GetComponent<IConditionEvaluator>();

            if (conditionEvaluator == null)
            {
                Debug.LogError($"{conditionEvaluatorObject.name} does not implement IConditionEvaluator!");
            }
        }
    }

    private void OnEnable()
    {
        if (conditionEvaluator != null)
        {
            conditionEvaluator.OnConditionsMet += ExecuteReactions;
        }
    }

    private void OnDisable()
    {
        if (conditionEvaluator != null)
        {
            conditionEvaluator.OnConditionsMet -= ExecuteReactions;
        }
    }

    private void ExecuteReactions()
    {
        // The script doesn't know WHAT it is doing, only that it is allowed to do it.
        onConditionsMetReaction?.Invoke();
    }
}
