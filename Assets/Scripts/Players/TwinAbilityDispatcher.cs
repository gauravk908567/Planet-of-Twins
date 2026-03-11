using UnityEngine;
using System.Collections;

public class TwinAbilityDispatcher : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputProviderObject;
    [SerializeField] private MonoBehaviour twinSelectorObject;

    private IInputProvider _input;
    private ITwinSelector _selector;
    private AbilityController _currentAbilityController;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        _selector = twinSelectorObject as ITwinSelector;
    }

    private void OnEnable()
    {
        if (_selector != null)
            _selector.OnTwinSelected += OnTwinSelected;
    }

    private void OnDisable()
    {
        if (_selector != null)
            _selector.OnTwinSelected -= OnTwinSelected;
    }

    private void Start()
    {
        StartCoroutine(ResolveInitialController());
    }

    private IEnumerator ResolveInitialController()
    {
        yield return null; // wait one frame for TwinSelector.Start() to run

        if (_selector?.SelectedTransform != null)
        {
            _currentAbilityController =
                _selector.SelectedTransform.GetComponent<AbilityController>();
        }
        else
        {
            Debug.LogError("[TwinAbilityDispatcher] Could not resolve initial controller.", this);
        }
    }

    private void OnTwinSelected(Transform selectedTransform)
    {
        _currentAbilityController =
            selectedTransform?.GetComponent<AbilityController>();
    }

    private void Update()
    {
        if (_currentAbilityController == null) return;

        // FIX: single press — GetAbilityDown (was GetAbilityUp)
        // No hold-to-preview — preview is managed by the ability itself
        if (_input.GetAbilityDown())
            _currentAbilityController.ActivatePrimary();
    }
}