using UnityEngine;

/// <summary>
/// Implemented by any entity that can receive knockback force.
/// EmpowerSystem and future ability systems call ReceiveKnockback on
/// all enemies in range — enemies that don't implement this are skipped silently.
/// </summary>
public interface IKnockbackReceiver
{
    /// <param name="data">Force vector + source context. Magnitude encodes strength.</param>
    void ReceiveKnockback(KnockbackData data);
}