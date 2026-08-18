using UnityEngine;

[CreateAssetMenu(fileName = "PolarBearAbility", menuName = "SkyZoo/Abilities/Polar Bear Ability")]
public class PolarBearAbility : EnclosureAbility
{
    public int radius          = 2;
    public int pointsPerDegree = 4;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int temperature = 0;
        foreach (var other in model.GetEnclosuresInRadius(self, radius))
            temperature += BiomeTemperature.Of(other.Data.biomeType);

        return -temperature * pointsPerDegree;
    }
}
