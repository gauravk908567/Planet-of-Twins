using UnityEngine;

public struct DamageData
{
    public int Amount;
    public GameObject Source;
    public Vector3 HitPoint;

    public DamageData(int amount, GameObject source, Vector3 hitPoint)
    {
        Amount = amount;
        Source = source;
        HitPoint = hitPoint;
    }
}