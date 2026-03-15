public class StunDurationEffect : ISkillEffect
{
    private AbilityData ability;
    private float increaseAmount;

    public StunDurationEffect(AbilityData ability, float increaseAmount)
    {
        this.ability = ability;
        this.increaseAmount = increaseAmount;
    }

    public void Apply()
    {
        ability.IncreaseDuration(increaseAmount);
    }
}