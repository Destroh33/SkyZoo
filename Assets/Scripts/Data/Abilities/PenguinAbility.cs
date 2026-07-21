using UnityEngine;

// Huddle colony: +X points for each adjacent cold-biome enclosure (Ice or
// Tundra) — penguins keep warm and score by packing together in the cold.
[CreateAssetMenu(fileName = "PenguinAbility", menuName = "SkyZoo/Abilities/Penguin Ability")]
public class PenguinAbility : EnclosureAbility
{
    public int bonusPerColdNeighbor = 6;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int bonus = 0;
        foreach (var neighbor in model.GetAdjacentEnclosures(self))
        {
            var b = neighbor.Data.biomeType;
            if (b == BiomeType.Ice || b == BiomeType.Tundra)
                bonus += bonusPerColdNeighbor;
        }
        return bonus;
    }
}
