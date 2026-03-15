public class WeaversGateDurationUpgrade : ISkillEffect
{
    private AbilityData ability;
    private float increase;

    public WeaversGateDurationUpgrade( AbilityData ability, float increase)
    {
        this.ability = ability;
        this.increase = increase;
    }

    public void Apply()
    {
        ability.IncreaseDuration(increase);
    }
}