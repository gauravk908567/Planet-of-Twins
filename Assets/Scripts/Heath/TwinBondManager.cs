using UnityEngine;

public class TwinBondManager : MonoBehaviour
{
    [SerializeField] private Transform leftTwin;
    [SerializeField] private Transform rightTwin;

    [SerializeField] private DistanceHealthSystem distanceSystem;

    [SerializeField] private int upgradeLevel = 0;

    private PlayerHealthComponent leftHealth;
    private PlayerHealthComponent rightHealth;

    private void Start()
    {
        leftHealth = leftTwin.GetComponent<PlayerHealthComponent>();
        rightHealth = rightTwin.GetComponent<PlayerHealthComponent>();
    }

    private void Update()
    {
        float distance = Vector3.Distance(
            leftTwin.position,
            rightTwin.position
        );

        float modifier =
            distanceSystem.CalculateDistanceModifier(distance*0.2f, upgradeLevel);

        leftHealth.SetDistanceModifier(modifier);
        rightHealth.SetDistanceModifier(modifier);
    }

    public void IncreaseUpgradeLevel(int amount)
    {
        upgradeLevel += amount;
    }
}