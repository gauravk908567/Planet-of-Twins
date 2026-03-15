public class PossessionDurationEffect : ISkillEffect
{
    private AbilityData ability;
    private float increaseAmount;

    public PossessionDurationEffect( AbilityData ability, float increase)
    {
        this.ability = ability;
        increaseAmount = increase;
    }

    public void Apply()
    {
        ability.IncreaseDuration(increaseAmount);
    }
}