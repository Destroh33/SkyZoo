using UnityEngine;

// Prey instincts: +X points on odd days.
[CreateAssetMenu(fileName = "ZebraAbility", menuName = "SkyZoo/Abilities/Zebra Ability")]
public class ZebraAbility : EnclosureAbility
{
    public int oddDayBonus = 8;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
        => model.CurrentDay % 2 != 0 ? oddDayBonus : 0;
}
