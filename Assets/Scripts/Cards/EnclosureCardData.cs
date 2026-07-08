using UnityEngine;

// Playing this card places its wrapped enclosure on the grid.
// Every placeable enclosure needs one of these — enclosures are never
// placed directly, only ever via their card.
[CreateAssetMenu(fileName = "EnclosureCard", menuName = "SkyZoo/Cards/Enclosure Card")]
public class EnclosureCardData : CardData
{
    public EnclosureData enclosure;

    public override CardTargetMode TargetMode => CardTargetMode.PlaceEnclosure;
}
