using UnityEngine;

// Danger tax: -3 points for each Predator-type enclosure in contact, but
// +8 points if it has zero predator neighbors (a "safe zone" bonus).
[CreateAssetMenu(fileName = "ZebraAbility", menuName = "SkyZoo/Abilities/Zebra Ability")]
public class ZebraAbility : EnclosureAbility
{
    public int penaltyPerPredatorNeighbor = 3;
    public int noPredatorBonus            = 8;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int predatorCount = 0;
        foreach (var neighbor in model.GetAdjacentEnclosures(self))
            if (neighbor.Data.animalType == AnimalType.Predator)
                predatorCount++;

        return predatorCount == 0 ? noPredatorBonus : -penaltyPerPredatorNeighbor * predatorCount;
    }
}
