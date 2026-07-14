using UnityEngine;

// +10 points if this enclosure sits in a sunny/warm biome (desert).
[CreateAssetMenu(fileName = "SnakeAbility", menuName = "SkyZoo/Abilities/Snake Ability")]
public class SnakeAbility : EnclosureAbility
{
    public int    warmBiomeBonus = 10;
    public BiomeType warmBiome   = BiomeType.Desert;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
        => self.Data.biomeType == warmBiome ? warmBiomeBonus : 0;
}
