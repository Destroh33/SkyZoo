using UnityEngine;

// Relocates one enclosure the player selects to any open, in-bounds spot on
// the grid. Keeps its accumulated bonuses and mana-cost record — only the
// position changes.
[CreateAssetMenu(fileName = "MoveEnclosureCard", menuName = "SkyZoo/Cards/Move Enclosure Card")]
public class MoveEnclosureCardData : CardData
{
    public override CardTargetMode TargetMode => CardTargetMode.MoveEnclosure;
}
