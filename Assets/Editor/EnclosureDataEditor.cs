using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnclosureData))]
public class EnclosureDataEditor : Editor
{
    private const float CellPx        = 28f;
    private const float PreviewCellPx = 12f;
    private const float PreviewGap    = 10f;
    private const float PivotDotPx    = 11f;

    private static readonly Color EmptyColor  = new(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color BorderColor = new(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color PivotColor  = new(1f,    0.85f, 0.2f,  1f);
    private static readonly Color PivotRing   = new(0.1f,  0.08f, 0f,    1f);

    private enum Paint { None, Fill, Erase }
    private Paint _paint = Paint.None;
    private bool  _movingPivot;
    private int   _strokeUndo;

    public override void OnInspectorGUI()
    {
        var data = (EnclosureData)target;

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "size",
                                "shapeCells", "pivotHalf", "pivotCell", "shapeCanvasSize");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        var cells  = new List<Vector2Int>(data.ShapeCells);
        var pivot  = data.PivotHalf;
        int canvas = data.ShapeCanvasSize;

        int newCanvas = EditorGUILayout.IntSlider("Canvas Size", canvas, 1, 8);
        if (newCanvas != canvas)
        {
            cells.RemoveAll(c => c.x < 0 || c.y < 0 || c.x >= newCanvas || c.y >= newCanvas);
            pivot = ClampPivot(pivot, newCanvas);
            Apply(data, cells, pivot, newCanvas);
            canvas = newCanvas;
        }

        EditorGUILayout.HelpBox(
            "Left-click a cell to add it, or a filled one to remove it — drag to keep going.\n" +
            "Right-click or drag to move the gold pivot dot to the nearest cell centre, edge or corner.",
            MessageType.None);

        DrawShapeCanvas(data, cells, pivot, canvas);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear"))
                Apply(data, new List<Vector2Int> { PivotCellOf(pivot, canvas) }, pivot, canvas);

            if (GUILayout.Button("Fill Canvas"))
            {
                var all = new List<Vector2Int>();
                for (int x = 0; x < canvas; x++)
                    for (int y = 0; y < canvas; y++)
                        all.Add(new Vector2Int(x, y));
                Apply(data, all, pivot, canvas);
            }

            if (GUILayout.Button("Center Pivot"))
                Apply(data, cells, ShapeCenter(cells), canvas);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rotations (R in-game)", EditorStyles.miniBoldLabel);
        DrawRotationPreview(data);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bounding box", $"{data.size.x} × {data.size.y}   ({data.CellCount} cells)");
        EditorGUILayout.LabelField("Pivot", DescribePivot(pivot));
    }

    private void DrawShapeCanvas(EnclosureData data, List<Vector2Int> cells, Vector2Int pivot, int canvas)
    {
        float side = canvas * CellPx;
        Rect  area = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));

        EditorGUI.DrawRect(new Rect(area.x - 1, area.y - 1, side + 2, side + 2), BorderColor);

        var filled = new HashSet<Vector2Int>(cells);
        for (int x = 0; x < canvas; x++)
            for (int y = 0; y < canvas; y++)
            {
                Color col = filled.Contains(new Vector2Int(x, y)) ? data.footprintColor : EmptyColor;
                col.a = 1f;
                EditorGUI.DrawRect(CellRect(area, x, y, canvas), col);
            }

        DrawPivotDot(area, pivot, canvas);
        HandleCanvasInput(data, cells, pivot, canvas, area);
    }

    private static Rect CellRect(Rect area, int x, int y, int canvas)
        => new(area.x + x * CellPx, area.y + (canvas - 1 - y) * CellPx, CellPx - 2f, CellPx - 2f);

    private static Vector2 HalfToPixel(Rect area, Vector2Int half, int canvas)
        => new(area.x + half.x * CellPx * 0.5f,
               area.y + (canvas * 2 - half.y) * CellPx * 0.5f);

    private static void DrawPivotDot(Rect area, Vector2Int pivot, int canvas)
    {
        var  c = HalfToPixel(area, pivot, canvas);
        float r = PivotDotPx * 0.5f;
        EditorGUI.DrawRect(new Rect(c.x - r - 1f, c.y - r - 1f, PivotDotPx + 2f, PivotDotPx + 2f), PivotRing);
        EditorGUI.DrawRect(new Rect(c.x - r,      c.y - r,      PivotDotPx,      PivotDotPx),      PivotColor);
    }

    private void HandleCanvasInput(EnclosureData data, List<Vector2Int> cells, Vector2Int pivot, int canvas, Rect area)
    {
        var e  = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (!area.Contains(e.mousePosition)) break;
                if (e.button != 0 && e.button != 1) break;

                GUIUtility.hotControl = id;
                _movingPivot = e.button == 1;
                _paint       = Paint.None;
                _strokeUndo  = Undo.GetCurrentGroup();

                Stroke(data, cells, pivot, canvas, area, e.mousePosition);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                Stroke(data, cells, pivot, canvas, area, e.mousePosition);
                e.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                _paint       = Paint.None;
                _movingPivot = false;
                Undo.CollapseUndoOperations(_strokeUndo);
                e.Use();
                break;
        }
    }

    private void Stroke(EnclosureData data, List<Vector2Int> cells, Vector2Int pivot, int canvas, Rect area, Vector2 mouse)
    {
        if (_movingPivot)
        {
            float hx = (mouse.x - area.x) / (CellPx * 0.5f);
            float hy = canvas * 2f - (mouse.y - area.y) / (CellPx * 0.5f);
            var   p  = ClampPivot(new Vector2Int(Mathf.RoundToInt(hx), Mathf.RoundToInt(hy)), canvas);
            if (p != pivot) Apply(data, cells, p, canvas);
            return;
        }

        int x = Mathf.FloorToInt((mouse.x - area.x) / CellPx);
        int y = canvas - 1 - Mathf.FloorToInt((mouse.y - area.y) / CellPx);
        if (x < 0 || y < 0 || x >= canvas || y >= canvas) return;

        var cell = new Vector2Int(x, y);

        if (_paint == Paint.None)
            _paint = cells.Contains(cell) ? Paint.Erase : Paint.Fill;

        if (_paint == Paint.Fill)
        {
            if (cells.Contains(cell)) return;
            cells.Add(cell);
        }
        else
        {
            if (cells.Count == 1 || !cells.Remove(cell)) return;
        }

        Apply(data, cells, pivot, canvas);
    }
    private void DrawRotationPreview(EnclosureData data)
    {
        var origins = new Vector2Int[4];
        var bounds  = new RectInt[4];
        int m       = 1;

        for (int r = 0; r < 4; r++)
        {
            var parity = data.GetOriginParity(r);
            origins[r] = new Vector2Int(16 + parity.x, 16 + parity.y);
            bounds[r]  = data.GetBounds(origins[r], r);
            m = Mathf.Max(m, Mathf.Max(bounds[r].width, bounds[r].height));
        }

        float side = m * PreviewCellPx;
        Rect  row  = GUILayoutUtility.GetRect(4f * (side + PreviewGap), side, GUILayout.ExpandWidth(false));

        var fill = data.footprintColor;
        fill.a   = 1f;

        for (int r = 0; r < 4; r++)
        {
            var box = new Rect(row.x + r * (side + PreviewGap), row.y, side, side);
            EditorGUI.DrawRect(box, EmptyColor);

            var min = bounds[r].min;

            for (int i = 0; i < data.CellCount; i++)
            {
                var c = data.GetCell(i, origins[r], r) - min;
                EditorGUI.DrawRect(new Rect(box.x + c.x * PreviewCellPx,
                                            box.y + (m - 1 - c.y) * PreviewCellPx,
                                            PreviewCellPx - 1f, PreviewCellPx - 1f), fill);
            }

            var half = origins[r] - min * 2;
            var dot  = new Vector2(box.x + half.x * PreviewCellPx * 0.5f,
                                   box.y + (m * 2 - half.y) * PreviewCellPx * 0.5f);
            EditorGUI.DrawRect(new Rect(dot.x - 2.5f, dot.y - 2.5f, 5f, 5f), PivotColor);
        }
    }

    private void Apply(EnclosureData data, List<Vector2Int> cells, Vector2Int pivot, int canvas)
    {
        Undo.RecordObject(data, "Edit Enclosure Shape");
        data.EditorSetShape(cells, pivot, canvas);
        EditorUtility.SetDirty(data);
        Repaint();
    }

    private static Vector2Int ClampPivot(Vector2Int pivot, int canvas)
        => new(Mathf.Clamp(pivot.x, 0, canvas * 2), Mathf.Clamp(pivot.y, 0, canvas * 2));

    private static Vector2Int PivotCellOf(Vector2Int pivot, int canvas)
        => new(Mathf.Clamp(Mathf.FloorToInt(pivot.x * 0.5f), 0, canvas - 1),
               Mathf.Clamp(Mathf.FloorToInt(pivot.y * 0.5f), 0, canvas - 1));
    private static Vector2Int ShapeCenter(List<Vector2Int> cells)
    {
        if (cells.Count == 0) return Vector2Int.zero;

        var min = cells[0];
        var max = min;
        foreach (var c in cells)
        {
            min = Vector2Int.Min(min, c);
            max = Vector2Int.Max(max, c);
        }
        return min + max + Vector2Int.one;
    }

    private static string DescribePivot(Vector2Int pivot)
    {
        bool oddX = pivot.x % 2 != 0, oddY = pivot.y % 2 != 0;
        string kind = oddX && oddY ? "cell centre"
                    : !oddX && !oddY ? "corner"
                    : "edge midpoint";
        return $"({pivot.x * 0.5f}, {pivot.y * 0.5f}) — {kind}";
    }
}
