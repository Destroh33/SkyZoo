using System.Collections.Generic;
using UnityEngine;

// Pure data model — no Unity lifecycle, no rendering concerns.
// Edge coordinate convention:
//   hEdges[x, y] = horizontal edge connecting (x, y)→(x+1, y)   x: 0..Width-1,  y: 0..Height
//   vEdges[x, y] = vertical edge connecting   (x, y)→(x, y+1)   x: 0..Width,     y: 0..Height-1
public class GridModel
{
    public int Width  { get; }
    public int Height { get; }

    private readonly EnclosureInstance[,] _cells;

    private readonly bool[,] _hEdges;
    private readonly bool[,] _vEdges;

    // Interior edges of multi-cell enclosures — blocked from path placement
    private readonly bool[,] _hBlocked;
    private readonly bool[,] _vBlocked;

    private readonly List<EnclosureInstance> _enclosures = new();
    public IReadOnlyList<EnclosureInstance> Enclosures => _enclosures;

    public GridModel(int width, int height)
    {
        Width  = width;
        Height = height;
        _cells    = new EnclosureInstance[width, height];
        _hEdges   = new bool[width,     height + 1];
        _vEdges   = new bool[width + 1, height];
        _hBlocked = new bool[width,     height + 1];
        _vBlocked = new bool[width + 1, height];
    }

    // ── Enclosure queries ────────────────────────────────────────────────────

    public EnclosureInstance GetCell(int x, int y) => _cells[x, y];

    public bool CanPlaceEnclosure(Vector2Int pos, Vector2Int size)
    {
        if (pos.x < 0 || pos.y < 0 || pos.x + size.x > Width || pos.y + size.y > Height)
            return false;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                if (_cells[x, y] != null) return false;

        return true;
    }

    public EnclosureInstance PlaceEnclosure(EnclosureData data, Vector2Int pos)
    {
        var instance = new EnclosureInstance(data, pos);
        var size     = data.size;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                _cells[x, y] = instance;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y + 1; y < pos.y + size.y; y++)
                _hBlocked[x, y] = true;

        for (int x = pos.x + 1; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                _vBlocked[x, y] = true;

        _enclosures.Add(instance);
        return instance;
    }

    public void RemoveEnclosure(EnclosureInstance instance)
    {
        var pos  = instance.GridPosition;
        var size = instance.Data.size;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                _cells[x, y] = null;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y + 1; y < pos.y + size.y; y++)
                _hBlocked[x, y] = false;

        for (int x = pos.x + 1; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                _vBlocked[x, y] = false;

        _enclosures.Remove(instance);
    }

    // ── Edge queries ─────────────────────────────────────────────────────────

    public bool GetHEdge(int x, int y)        => _hEdges[x, y];
    public bool GetVEdge(int x, int y)        => _vEdges[x, y];
    public bool IsHEdgeBlocked(int x, int y)  => _hBlocked[x, y];
    public bool IsVEdgeBlocked(int x, int y)  => _vBlocked[x, y];

    // Returns false if the edge is out of bounds or blocked by an enclosure interior.
    public bool ToggleHEdge(int x, int y)
    {
        if (x < 0 || x >= Width  || y < 0 || y > Height)  return false;
        if (_hBlocked[x, y]) return false;
        _hEdges[x, y] = !_hEdges[x, y];
        return true;
    }

    public bool ToggleVEdge(int x, int y)
    {
        if (x < 0 || x > Width || y < 0 || y >= Height) return false;
        if (_vBlocked[x, y]) return false;
        _vEdges[x, y] = !_vEdges[x, y];
        return true;
    }

    // ── Scoring helpers ───────────────────────────────────────────────────────

    public int CountPerimeterPathEdges(EnclosureInstance instance)
    {
        int count = 0;
        var pos  = instance.GridPosition;
        var size = instance.Data.size;

        // Bottom and top rows
        for (int x = pos.x; x < pos.x + size.x; x++)
        {
            if (_hEdges[x, pos.y])          count++;
            if (_hEdges[x, pos.y + size.y]) count++;
        }
        // Left and right columns
        for (int y = pos.y; y < pos.y + size.y; y++)
        {
            if (_vEdges[pos.x,          y]) count++;
            if (_vEdges[pos.x + size.x, y]) count++;
        }
        return count;
    }
}
