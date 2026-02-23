using UnityEngine;

public class Player : MonoBehaviour, ITimeFreezable
{
    [SerializeField] private PlayerMovementController movement;
    [SerializeField] private PlayerHealthComponent health;


    public PlayerMovementController Movement => movement;
    public PlayerHealthComponent Health => health;

    void Start()
    {
        GlobalTimeController.Instance.Register(this);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnFreeze()
    {
        movement.SetSoulMode(true);
    }

    public void OnUnfreeze()
    {
        movement.SetSoulMode(false);
    }

    private void OnDestroy()
    {
        if (GlobalTimeController.Instance != null)
            GlobalTimeController.Instance.Unregister(this);
    }
}
