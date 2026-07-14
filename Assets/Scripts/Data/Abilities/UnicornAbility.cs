using UnityEngine;

// Mythical: multiplies the score of every enclosure in contact with it by 1.5x.
[CreateAssetMenu(fileName = "UnicornAbility", menuName = "SkyZoo/Abilities/Unicorn Ability")]
public class UnicornAbility : EnclosureAbility
{
    public float neighborMultiplier = 1.5f;

    public override float GetNeighborMultiplier(EnclosureInstance self, EnclosureInstance neighbor, GridModel model)
        => neighborMultiplier;
}
