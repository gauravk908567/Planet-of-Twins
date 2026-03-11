using UnityEngine;

public class TwinMovementDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private Player soulTwin;
    [SerializeField] private MonoBehaviour inputProviderObject;

    private IInputProvider _input;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        if (_input == null)
            Debug.LogError("[TwinMovementDispatcher] inputProviderObject missing IInputProvider.", this);
    }

    private void Update()
    {
        if (_input == null) return;

        Vector2 raw = _input.GetMovementInput();
        IPlayerCommand cmd = new MoveCommand(raw);

        // Both twins always receive input — frozen twin ignores it internally
        leftTwin?.Movement?.ExecuteCommand(cmd);
        rightTwin?.Movement?.ExecuteCommand(cmd);

        // Soul only when active
        if (soulTwin != null && soulTwin.gameObject.activeSelf)
            soulTwin.Movement?.ExecuteCommand(cmd);
    }
}
