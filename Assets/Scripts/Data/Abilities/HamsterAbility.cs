using UnityEngine;

[CreateAssetMenu(fileName = "HamsterAbility", menuName = "SkyZoo/Abilities/Hamster Ability")]
public class HamsterAbility : EnclosureAbility
{
    public int bonusPerDayAlive = 6;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
        => bonusPerDayAlive * Mathf.Max(1, model.CurrentDay - self.DayPlaced);
}
