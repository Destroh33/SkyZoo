using UnityEngine;

// Crowd pleaser: +2 points for each adjacent enclosure, regardless of type.
[CreateAssetMenu(fileName = "SealAbility", menuName = "SkyZoo/Abilities/Seal Ability")]
public class SealAbility : EnclosureAbility
{
    public int bonusPerNeighbor = 2;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
        => model.GetAdjacentEnclosures(self).Count * bonusPerNeighbor;
}
