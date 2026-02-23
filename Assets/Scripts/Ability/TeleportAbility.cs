using UnityEngine;

public class TeleportAbility : AbilityBase
{
    private PlayerMovementController movement1;
    private PlayerMovementController movement2;
    private GameObject soulVisual;

    public TeleportAbility(
        AbilityData data,
        PlayerMovementController amovement, PlayerMovementController smovement)
        : base(data)
    {
        this.movement1 = amovement;
        this.movement2 = smovement;
    }

    protected override bool Activate()
    {
        GlobalTimeController.Instance.FreezeWorld();

        movement1.enabled = false;
        movement2.enabled = false;
        soulVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soulVisual.transform.position = GetTeleportPos().position;
        soulVisual.transform.localScale = Vector3.one * 0.5f;
        return true;
    }

    public Transform GetTeleportPos()
    {
        return movement2.transform;
    }

    public override void Tick()
    {
        base.Tick();

        if (!isActive) return;

        float speed = 8f;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 dir = new Vector3(h, 0, v).normalized;

        soulVisual.transform.position +=
            dir * speed * Time.unscaledDeltaTime;
    }

    protected override void End()
    {
        soulVisual.transform.position = movement1.transform.position;

        Object.Destroy(soulVisual);

        movement1.enabled = true;
        movement2.enabled = true;
        GlobalTimeController.Instance.UnfreezeWorld();

        base.End();
    }
}