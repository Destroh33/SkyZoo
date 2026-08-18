using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnclosureData", menuName = "SkyZoo/Enclosure Data")]
public class EnclosureData : ScriptableObject
{
    public string     enclosureName;

    public Vector2Int size = Vector2Int.one;

    [SerializeField] private List<Vector2Int> shapeCells = new();

    [SerializeField] private Vector2Int pivotHalf = PivotUnset;

    [SerializeField] private Vector2Int pivotCell;

    private static readonly Vector2Int PivotUnset = new(int.MinValue, int.MinValue);

    [SerializeField] private int shapeCanvasSize = 3;

    public int        baseValue = 10;
    public int        lifespanDays;
    public AnimalType animalType;
    public BiomeType  biomeType;
    public EnclosureAbility ability;
    public GameObject prefab;
    public Vector3    prefabOffset;
    public Color      footprintColor = new(0.2f, 0.8f, 0.2f, 0.6f);
    public Sprite cardShopImage;
    public int shopCost;

    public IReadOnlyList<Vector2Int> ShapeCells { get { EnsureShape(); return shapeCells; } }
    public Vector2Int PivotHalf      { get { EnsureShape(); return pivotHalf; } }
    public int        CellCount      => ShapeCells.Count;
    public int        ShapeCanvasSize => Mathf.Max(1, shapeCanvasSize);

    public static Vector2Int CellCenterHalf(Vector2Int cell) => new(cell.x * 2 + 1, cell.y * 2 + 1);

    public Vector2Int GetCell(int index, Vector2Int originHalf, int rotation)
    {
        EnsureShape();
        var half = originHalf + Rotate(CellCenterHalf(shapeCells[index]) - pivotHalf, rotation);
        return new Vector2Int(Mathf.FloorToInt(half.x * 0.5f), Mathf.FloorToInt(half.y * 0.5f));
    }

    public Vector2Int GetOriginParity(int rotation)
    {
        EnsureShape();
        var offset = Rotate(CellCenterHalf(shapeCells[0]) - pivotHalf, rotation);
        return new Vector2Int(Parity(1 - offset.x), Parity(1 - offset.y));
    }

    public bool IsValidOrigin(Vector2Int originHalf, int rotation)
    {
        var parity = GetOriginParity(rotation);
        return Parity(originHalf.x) == parity.x && Parity(originHalf.y) == parity.y;
    }

    private static int Parity(int v) => ((v % 2) + 2) % 2;

    public static Vector2Int Rotate(Vector2Int offset, int rotation)
    {
        switch (((rotation % 4) + 4) % 4)
        {
            case 1:  return new Vector2Int( offset.y, -offset.x);
            case 2:  return new Vector2Int(-offset.x, -offset.y);
            case 3:  return new Vector2Int(-offset.y,  offset.x);
            default: return offset;
        }
    }

    public RectInt GetBounds(Vector2Int originHalf, int rotation)
    {
        var min = GetCell(0, originHalf, rotation);
        var max = min;
        for (int i = 1; i < CellCount; i++)
        {
            var c = GetCell(i, originHalf, rotation);
            min = Vector2Int.Min(min, c);
            max = Vector2Int.Max(max, c);
        }
        return new RectInt(min, max - min + Vector2Int.one);
    }

    void OnEnable()   => EnsureShape();
    void OnValidate() => EnsureShape();

    private void EnsureShape()
    {
        shapeCells ??= new List<Vector2Int>();

        if (shapeCells.Count == 0)
        {
            var s = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            for (int x = 0; x < s.x; x++)
                for (int y = 0; y < s.y; y++)
                    shapeCells.Add(new Vector2Int(x, y));

            pivotHalf       = s;
            shapeCanvasSize = Mathf.Max(3, Mathf.Max(s.x, s.y));
            return;
        }
        if (pivotHalf == PivotUnset) pivotHalf = CellCenterHalf(pivotCell);
    }

#if UNITY_EDITOR

    public void EditorSetShape(List<Vector2Int> cells, Vector2Int pivot, int canvasSize)
    {
        shapeCells      = new List<Vector2Int>(cells);
        pivotHalf       = pivot;
        shapeCanvasSize = Mathf.Max(1, canvasSize);

        if (shapeCells.Count == 0)
            shapeCells.Add(new Vector2Int(Mathf.FloorToInt(pivot.x * 0.5f), Mathf.FloorToInt(pivot.y * 0.5f)));

        var min = shapeCells[0];
        var max = min;
        foreach (var c in shapeCells)
        {
            min = Vector2Int.Min(min, c);
            max = Vector2Int.Max(max, c);
        }
        size = max - min + Vector2Int.one;
    }
#endif
}
