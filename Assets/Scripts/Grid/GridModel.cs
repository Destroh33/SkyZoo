using System.Collections.Generic;
using UnityEngine;

public class GridModel
{
    public int Width  { get; }
    public int Height { get; }

    private readonly EnclosureInstance[,] _cells;

    private readonly bool[,] _hEdges;
    private readonly bool[,] _vEdges;

    private readonly bool[,] _hBlocked;
    private readonly bool[,] _vBlocked;

    private readonly List<EnclosureInstance> _enclosures = new();
    public IReadOnlyList<EnclosureInstance> Enclosures => _enclosures;

    public int MaxPaths      { get; }
    public int PathsUsed     { get; private set; }
    public int PathsRemaining => MaxPaths - PathsUsed;

    public Vector2Int StartVertex { get; }
    public Vector2Int EndVertex   { get; }

    public int CurrentDay { get; set; }

    public GridModel(int width, int height, int maxPaths = 8)
    {
        Width  = width;
        Height = height;
        _cells    = new EnclosureInstance[width, height];
        _hEdges   = new bool[width,     height + 1];
        _vEdges   = new bool[width + 1, height];
        _hBlocked = new bool[width,     height + 1];
        _vBlocked = new bool[width + 1, height];

        MaxPaths = maxPaths;

        StartVertex = new Vector2Int(0,     height / 2);
        EndVertex   = new Vector2Int(width, height / 2);
    }

    public EnclosureInstance GetCell(int x, int y) => _cells[x, y];

    public bool CanPlaceEnclosure(EnclosureData data, Vector2Int originHalf, int rotation = 0)
        => CanPlaceEnclosureIgnoring(null, data, originHalf, rotation);

    public bool CanPlaceEnclosureIgnoring(EnclosureInstance ignore, EnclosureData data, Vector2Int originHalf, int rotation = 0)
    {
        if (!data.IsValidOrigin(originHalf, rotation)) return false;

        for (int i = 0; i < data.CellCount; i++)
        {
            var c = data.GetCell(i, originHalf, rotation);
            if (c.x < 0 || c.y < 0 || c.x >= Width || c.y >= Height) return false;

            var occupant = _cells[c.x, c.y];
            if (occupant != null && occupant != ignore) return false;
        }

        return true;
    }

    public EnclosureInstance PlaceEnclosure(EnclosureData data, Vector2Int originHalf, int rotation = 0, int manaCostPaid = 0)
    {
        var instance = new EnclosureInstance(data, originHalf, rotation) { ManaCostPaid = manaCostPaid };
        RegisterEnclosureAt(instance, originHalf, rotation);
        return instance;
    }

    public void RemoveEnclosure(EnclosureInstance instance) => UnregisterEnclosureAt(instance);

    public bool MoveEnclosure(EnclosureInstance instance, Vector2Int newOriginHalf, int rotation)
    {
        if (!CanPlaceEnclosureIgnoring(instance, instance.Data, newOriginHalf, rotation)) return false;

        UnregisterEnclosureAt(instance);
        RegisterEnclosureAt(instance, newOriginHalf, rotation);
        return true;
    }

    private bool IsOwnCell(EnclosureInstance instance, int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height && _cells[x, y] == instance;

    private void RegisterEnclosureAt(EnclosureInstance instance, Vector2Int originHalf, int rotation)
    {
        instance.PivotHalf = originHalf;
        instance.Rotation  = rotation;

        foreach (var c in instance.Cells) _cells[c.x, c.y] = instance;

        foreach (var c in instance.Cells)
        {
            if (IsOwnCell(instance, c.x, c.y - 1))
            {
                _hBlocked[c.x, c.y] = true;
                if (_hEdges[c.x, c.y]) { _hEdges[c.x, c.y] = false; PathsUsed--; }
            }

            if (IsOwnCell(instance, c.x - 1, c.y))
            {
                _vBlocked[c.x, c.y] = true;
                if (_vEdges[c.x, c.y]) { _vEdges[c.x, c.y] = false; PathsUsed--; }
            }
        }

        _enclosures.Add(instance);
    }

    private void UnregisterEnclosureAt(EnclosureInstance instance)
    {
        foreach (var c in instance.Cells)
        {
            if (IsOwnCell(instance, c.x, c.y - 1)) _hBlocked[c.x, c.y] = false;
            if (IsOwnCell(instance, c.x - 1, c.y)) _vBlocked[c.x, c.y] = false;
        }

        foreach (var c in instance.Cells) _cells[c.x, c.y] = null;

        _enclosures.Remove(instance);
    }

    public bool GetHEdge(int x, int y)        => _hEdges[x, y];
    public bool GetVEdge(int x, int y)        => _vEdges[x, y];
    public bool IsHEdgeBlocked(int x, int y)  => _hBlocked[x, y];
    public bool IsVEdgeBlocked(int x, int y)  => _vBlocked[x, y];

    public bool ToggleHEdge(int x, int y)
    {
        if (!CanToggleHEdge(x, y)) return false;

        _hEdges[x, y] = !_hEdges[x, y];
        PathsUsed    += _hEdges[x, y] ? 1 : -1;
        return true;
    }

    public bool ToggleVEdge(int x, int y)
    {
        if (!CanToggleVEdge(x, y)) return false;

        _vEdges[x, y] = !_vEdges[x, y];
        PathsUsed    += _vEdges[x, y] ? 1 : -1;
        return true;
    }

    public bool PlaceHEdge(int x, int y)
    {
        if (!CanPlaceHEdge(x, y)) return false;

        _hEdges[x, y] = true;
        PathsUsed++;
        return true;
    }

    public bool PlaceVEdge(int x, int y)
    {
        if (!CanPlaceVEdge(x, y)) return false;

        _vEdges[x, y] = true;
        PathsUsed++;
        return true;
    }

    private bool CanToggleHEdge(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y <= Height
            && !_hBlocked[x, y]
            && (_hEdges[x, y] || PathsRemaining > 0);
    }

    private bool CanToggleVEdge(int x, int y)
    {
        return x >= 0 && x <= Width && y >= 0 && y < Height
            && !_vBlocked[x, y]
            && (_vEdges[x, y] || PathsRemaining > 0);
    }

    private bool CanPlaceHEdge(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y <= Height
            && !_hBlocked[x, y]
            && !_hEdges[x, y]
            && PathsRemaining > 0;
    }

    private bool CanPlaceVEdge(int x, int y)
    {
        return x >= 0 && x <= Width && y >= 0 && y < Height
            && !_vBlocked[x, y]
            && !_vEdges[x, y]
            && PathsRemaining > 0;
    }

    public int CountPerimeterPathEdges(EnclosureInstance instance)
    {
        int count = 0;

        foreach (var c in instance.Cells)
        {
            if (!IsOwnCell(instance, c.x, c.y - 1) && _hEdges[c.x, c.y])         count++;
            if (!IsOwnCell(instance, c.x, c.y + 1) && _hEdges[c.x, c.y + 1])     count++;
            if (!IsOwnCell(instance, c.x - 1, c.y) && _vEdges[c.x,     c.y])     count++;
            if (!IsOwnCell(instance, c.x + 1, c.y) && _vEdges[c.x + 1, c.y])     count++;
        }

        return count;
    }

    public float GetEnclosureScore(EnclosureInstance instance)
    {
        int   abilityBonus = instance.Data.ability != null ? instance.Data.ability.CalculateBonus(instance, this) : 0;
        int   value        = instance.Data.baseValue + instance.TotalBonus + abilityBonus;

        float multiplier = 1f + 0.5f * CountPerimeterPathEdges(instance);

        foreach (var neighbor in GetAdjacentEnclosures(instance))
            if (neighbor.Data.ability != null)
                multiplier *= neighbor.Data.ability.GetNeighborMultiplier(neighbor, instance, this);

        return value * multiplier;
    }

    public float GetTotalScore()
    {
        float total = 0f;
        foreach (var e in _enclosures) total += GetEnclosureScore(e);
        return total;
    }

    public List<EnclosureInstance> GetAdjacentEnclosures(EnclosureInstance instance)
    {
        var result = new List<EnclosureInstance>();

        void TryAdd(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            var cell = _cells[x, y];
            if (cell != null && cell != instance && !result.Contains(cell)) result.Add(cell);
        }

        foreach (var c in instance.Cells)
        {
            TryAdd(c.x,     c.y - 1);
            TryAdd(c.x,     c.y + 1);
            TryAdd(c.x - 1, c.y);
            TryAdd(c.x + 1, c.y);
        }

        return result;
    }

    public bool HasValidPath()
    {
        var visited = new HashSet<Vector2Int>();
        var queue   = new Queue<Vector2Int>();
        queue.Enqueue(StartVertex);
        visited.Add(StartVertex);

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            if (v == EndVertex) return true;

            foreach (var next in GetConnectedVertices(v))
                if (visited.Add(next)) queue.Enqueue(next);
        }

        return false;
    }

    private IEnumerable<Vector2Int> GetConnectedVertices(Vector2Int v)
    {
        if (v.x < Width && _hEdges[v.x, v.y]) yield return new Vector2Int(v.x + 1, v.y);

        if (v.x > 0 && _hEdges[v.x - 1, v.y]) yield return new Vector2Int(v.x - 1, v.y);

        if (v.y < Height && _vEdges[v.x, v.y]) yield return new Vector2Int(v.x, v.y + 1);

        if (v.y > 0 && _vEdges[v.x, v.y - 1]) yield return new Vector2Int(v.x, v.y - 1);
    }
}
