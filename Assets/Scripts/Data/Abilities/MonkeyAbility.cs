using UnityEngine;

// Troop mentality: +X points for each other Monkey enclosure in contact —
// monkeys embolden each other, so a packed troop scores far more than a lone
// one. "Same animal" is identified by shared EnclosureData asset.
[CreateAssetMenu(fileName = "MonkeyAbility", menuName = "SkyZoo/Abilities/Monkey Ability")]
public class MonkeyAbility : EnclosureAbility
{
    public int bonusPerMonkeyNeighbor = 8;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
    {
        int bonus = 0;
        foreach (var neighbor in model.GetAdjacentEnclosures(self))
            if (neighbor.Data == self.Data)
                bonus += bonusPerMonkeyNeighbor;
        return bonus;
    }
}
