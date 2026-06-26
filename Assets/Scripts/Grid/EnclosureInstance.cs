using UnityEngine;

public class EnclosureInstance
{
    public EnclosureData Data         { get; }
    public Vector2Int    GridPosition { get; set; }

    public EnclosureInstance(EnclosureData data, Vector2Int position)
    {
        Data         = data;
        GridPosition = position;
    }
}
