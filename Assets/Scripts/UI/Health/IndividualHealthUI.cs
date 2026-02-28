using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndividualHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthAmountBar;
    [SerializeField] private TMP_Text healthAmountText;
    [SerializeField] private GameObject playerTarget;


    private IHealthUpdate healthUpdate;

    private void Awake()
    {
        if (playerTarget != null)
        {
            healthUpdate = playerTarget.GetComponent<IHealthUpdate>();
            if (healthUpdate == null)
            {
                Debug.LogError($"{playerTarget.name} does not have a component implementing IHealthUpdate!");
            }
        }
    }

    private void OnEnable()
    {
        if (healthUpdate != null)
        {
            healthUpdate.OnHealthUpdated += UpdateUI;

            UpdateUI(healthUpdate.FinalHealth);
        }
    }

    private void OnDisable()
    {
        if (healthUpdate != null)
        {
            healthUpdate.OnHealthUpdated -= UpdateUI;
        }
    }

    private void UpdateUI(float currentHealth)
    {
        if (healthAmountBar != null && healthUpdate.MaxHealth > 0)
        {
            healthAmountBar.value = currentHealth / healthUpdate.MaxHealth;
            healthAmountText.text = healthUpdate.FinalHealth.ToString("F0");
        }
    }
}

