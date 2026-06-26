using UnityEngine;

[CreateAssetMenu(fileName = "EnclosureData", menuName = "SkyZoo/Enclosure Data")]
public class EnclosureData : ScriptableObject
{
    public string     enclosureName;
    public Vector2Int size      = Vector2Int.one;
    public int        baseValue = 10;
    public GameObject prefab;
    public Vector3    prefabOffset;                         // fine-tune placement (e.g. y=0.5 if pivot is at mesh center)
    public Color      footprintColor = new(0.2f, 0.8f, 0.2f, 0.6f);
}
