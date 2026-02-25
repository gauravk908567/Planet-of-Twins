using System;
using System.Collections;
using UnityEngine;

public class TeleportAbility : AbilityBase
{
    private Player movement1;
    private Player movement2;
    private Player soulPlayer;
    private bool shouldTick = false;
    public TeleportAbility(AbilityData data, Player amovement, Player smovement, Player soulPlayer) : base(data)
    {
        this.movement1 = amovement;
        this.movement2 = smovement;
        this.soulPlayer = soulPlayer;
    }

    protected override bool Activate()
    {
        GlobalTimeController.Instance.FreezeWorld();
        soulPlayer.gameObject.SetActive(true);
        (soulPlayer as SoulPlayer).ShouldSoulSleep(false);
        // soulVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // soulPlayer.transform.position = GetTeleportPos().position;
        shouldTick = false;
        soulPlayer.transform.position = movement1.transform.position;
        GlobalTimeController.Instance.StartCoroutine(TeleportMove(soulPlayer.transform, GetTeleportPos(), 40f, RunTickStatus, false));
        // soulVisual.transform.localScale = Vector3.one * 0.5f;
        return true;
    }

    public Transform GetTeleportPos()
    {
        return movement2.transform;
    }

    private void RunTickStatus(bool shouldTick)
    {
        this.shouldTick = shouldTick;
    }

    public override void Tick()
    {
        if (!shouldTick)
            return;
        base.Tick();

        if (!isActive) return;

        float speed = 8f;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 dir = new Vector3(h, 0, v).normalized;

        soulPlayer.transform.position +=
            dir * speed * Time.unscaledDeltaTime;
    }

    IEnumerator TeleportMove(Transform soulPlayer, Transform target, float speed, Action<bool> callback, bool soulback)
    {
        while (Vector3.Distance(soulPlayer.position, target.position) > 0.01f)
        {
            soulPlayer.position = Vector3.MoveTowards(
                soulPlayer.position,
                target.position,
                speed * Time.deltaTime
            );

            yield return null;
        }
        callback?.Invoke(!soulback);
        if (soulback)
        {
            soulPlayer.gameObject.SetActive(false);
        }
    }

    protected override void End()
    {
        // soulPlayer.transform.position = movement1.transform.position;
        GlobalTimeController.Instance.StartCoroutine(TeleportMove(soulPlayer.transform, movement1.transform, 40f, RunTickStatus, true));
        (soulPlayer as SoulPlayer).ShouldSoulSleep(true);
        GlobalTimeController.Instance.UnfreezeWorld();
        base.End();
    }
}