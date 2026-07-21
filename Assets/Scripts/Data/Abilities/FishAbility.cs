using UnityEngine;

// Schooling aquarium fish: +X points for each adjacent enclosure that shares
// the aquatic biome — fish score best swimming alongside other water life.
[CreateAssetMenu(fileName = "FishAbility", menuName = "SkyZoo/Abilities/Fish Ability")]
public class FishAbility : EnclosureAbility
{
    public int       bonusPerAquaticNeighbor = 5;
    public BiomeType schoolBiome             = BiomeType.Aquatic;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int bonus = 0;
        foreach (var neighbor in model.GetAdjacentEnclosures(self))
            if (neighbor.Data.biomeType == schoolBiome)
                bonus += bonusPerAquaticNeighbor;
        return bonus;
    }
}
