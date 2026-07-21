using UnityEngine;

// Pollinator: multiplies the score of every adjacent Forest-biome enclosure by
// 1.25x — the hummingbird flits between the plants and jungle life, helping
// everything around it flourish.
[CreateAssetMenu(fileName = "HummingbirdAbility", menuName = "SkyZoo/Abilities/Hummingbird Ability")]
public class HummingbirdAbility : EnclosureAbility
{
    public float forestMultiplier = 1.25f;

    public override float GetNeighborMultiplier(EnclosureInstance self, EnclosureInstance neighbor, GridModel model)
        => neighbor.Data.biomeType == BiomeType.Forest ? forestMultiplier : 1f;
}
