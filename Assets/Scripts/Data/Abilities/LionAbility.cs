using UnityEngine;

// Apex predator: +5 points for each Prey-type enclosure in contact.
[CreateAssetMenu(fileName = "LionAbility", menuName = "SkyZoo/Abilities/Lion Ability")]
public class LionAbility : EnclosureAbility
{
    public int bonusPerPreyNeighbor = 5;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int bonus = 0;
        foreach (var neighbor in model.GetAdjacentEnclosures(self))
            if (neighbor.Data.animalType == AnimalType.Prey)
                bonus += bonusPerPreyNeighbor;
        return bonus;
    }
}
