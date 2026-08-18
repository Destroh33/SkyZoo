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

    [Header("Appearance")]
    public Sprite art;
    public Color  accentColor = new(0.35f, 0.45f, 0.6f, 1f);

    public virtual Sprite Art => art;

    public abstract CardTargetMode TargetMode { get; }
}
