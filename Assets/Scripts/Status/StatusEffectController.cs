using UnityEngine;
using System.Collections.Generic;

public class StatusEffectController : MonoBehaviour
{
    private List<IStatusEffect> activeEffects = new List<IStatusEffect>();

    public void ApplyEffect(IStatusEffect effect)
    {
        effect.OnApply();
        activeEffects.Add(effect);
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].OnUpdate();

            if (activeEffects[i].IsFinished)
            {
                activeEffects[i].OnRemove();
                activeEffects.RemoveAt(i);
            }
        }
    }
}