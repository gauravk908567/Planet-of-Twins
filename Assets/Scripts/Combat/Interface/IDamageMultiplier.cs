public interface IDamageMultiplier
{
    float DamageOutMultiplier { get; }   // 1.0 normally, 1.35 during Soul Convergence
    float DamageInMultiplier { get; }   // applied to SharedHealthPool incoming damage
}