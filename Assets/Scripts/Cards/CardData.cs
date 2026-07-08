using UnityEngine;

public enum CardTargetMode
{
    PlaceEnclosure,     // click an empty grid area to place a new enclosure
    SelectOneEnclosure, // click an existing enclosure to target it
    MoveEnclosure       // click an existing enclosure, then click an empty area to relocate it
}

public abstract class CardData : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public int    manaCost = 1;

    public abstract CardTargetMode TargetMode { get; }
}
