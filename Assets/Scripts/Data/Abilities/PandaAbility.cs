using UnityEngine;

// Needs seclusion: a large flat bonus, but only while NO path edge touches its
// perimeter. Pandas are shy — routing visitors right up to the enclosure scares
// them and the bonus vanishes. Rewards tucking it away from the path.
[CreateAssetMenu(fileName = "PandaAbility", menuName = "SkyZoo/Abilities/Panda Ability")]
public class PandaAbility : EnclosureAbility
{
    public int seclusionBonus = 20;

    public override int CalculateBonus(EnclosureInstance self, GridModel model)
        => model.CountPerimeterPathEdges(self) == 0 ? seclusionBonus : 0;
}
