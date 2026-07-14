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

    // ── Path budget ──────────────────────────────────────────────────────────
    // Global pool shared across the whole game — never refills on its own.
    // Removing a placed path edge frees budget back up for placing elsewhere.
    public int MaxPaths      { get; }
    public int PathsUsed     { get; private set; }
    public int PathsRemaining => MaxPaths - PathsUsed;

    // ── Start / end vertices ─────────────────────────────────────────────────
    // Auto-picked at construction: midpoints of the left and right grid borders.
    // A vertex is identified by grid-vertex coordinates (0..Width, 0..Height).
    public Vector2Int StartVertex { get; }
    public Vector2Int EndVertex   { get; }

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

    public EnclosureInstance PlaceEnclosure(EnclosureData data, Vector2Int pos, int manaCostPaid = 0)
    {
        var instance = new EnclosureInstance(data, pos) { ManaCostPaid = manaCostPaid };
        RegisterEnclosureAt(instance, pos);
        return instance;
    }

    public void RemoveEnclosure(EnclosureInstance instance) => UnregisterEnclosureAt(instance);

    // Same as CanPlaceEnclosure, but treats cells occupied by `ignore` as
    // empty — used when checking whether an enclosure can move to a new spot
    // without its own current footprint counting against itself.
    public bool CanPlaceEnclosureIgnoring(EnclosureInstance ignore, Vector2Int pos, Vector2Int size)
    {
        if (pos.x < 0 || pos.y < 0 || pos.x + size.x > Width || pos.y + size.y > Height)
            return false;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                if (_cells[x, y] != null && _cells[x, y] != ignore) return false;

        return true;
    }

    // Relocates an enclosure to a new position, preserving its accumulated
    // bonuses and mana-cost record. Fails if the destination isn't open.
    public bool MoveEnclosure(EnclosureInstance instance, Vector2Int newPos)
    {
        if (!CanPlaceEnclosureIgnoring(instance, newPos, instance.Data.size)) return false;

        UnregisterEnclosureAt(instance);
        RegisterEnclosureAt(instance, newPos);
        return true;
    }

    private void RegisterEnclosureAt(EnclosureInstance instance, Vector2Int pos)
    {
        instance.GridPosition = pos;
        var size = instance.Data.size;

        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
                _cells[x, y] = instance;

        // Interior edges are blocked from path placement — and any path piece
        // already sitting on one (e.g. placed on empty land before this
        // enclosure existed, or left over from before a move) is cleared, so
        // nothing is ever trapped inside the enclosure's own footprint.
        for (int x = pos.x; x < pos.x + size.x; x++)
            for (int y = pos.y + 1; y < pos.y + size.y; y++)
            {
                _hBlocked[x, y] = true;
                _hEdges[x, y]   = false;
            }

        for (int x = pos.x + 1; x < pos.x + size.x; x++)
            for (int y = pos.y; y < pos.y + size.y; y++)
            {
                _vBlocked[x, y] = true;
                _vEdges[x, y]   = false;
            }

        _enclosures.Add(instance);
    }

    private void UnregisterEnclosureAt(EnclosureInstance instance)
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

    // Returns false if the edge is out of bounds, blocked by an enclosure
    // interior, or (when placing a new path) the path budget is exhausted.
    public bool ToggleHEdge(int x, int y)
    {
        if (x < 0 || x >= Width  || y < 0 || y > Height)  return false;
        if (_hBlocked[x, y]) return false;
        if (!_hEdges[x, y] && PathsRemaining <= 0) return false;

        _hEdges[x, y] = !_hEdges[x, y];
        PathsUsed    += _hEdges[x, y] ? 1 : -1;
        return true;
    }

    public bool ToggleVEdge(int x, int y)
    {
        if (x < 0 || x > Width || y < 0 || y >= Height) return false;
        if (_vBlocked[x, y]) return false;
        if (!_vEdges[x, y] && PathsRemaining <= 0) return false;

        _vEdges[x, y] = !_vEdges[x, y];
        PathsUsed    += _vEdges[x, y] ? 1 : -1;
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

    public float GetEnclosureScore(EnclosureInstance instance)
    {
        int   abilityBonus = instance.Data.ability != null ? instance.Data.ability.CalculateBonus(instance, this) : 0;
        int   value        = instance.Data.baseValue + instance.TotalBonus + abilityBonus;

        // Non-stacking: each perimeter path edge adds a flat 0.5x of the base
        // value instead of compounding (was Mathf.Pow(1.5f, edges)).
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

    // ── Adjacency ────────────────────────────────────────────────────────────

    // Enclosures that share at least one edge with `instance`'s footprint
    // (touching corner-only does not count). Used by abilities that care
    // about "in contact with" neighbors, e.g. the lion's predator bonus.
    public List<EnclosureInstance> GetAdjacentEnclosures(EnclosureInstance instance)
    {
        var result = new List<EnclosureInstance>();
        var pos    = instance.GridPosition;
        var size   = instance.Data.size;

        void TryAdd(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            var cell = _cells[x, y];
            if (cell != null && cell != instance && !result.Contains(cell)) result.Add(cell);
        }

        for (int x = pos.x; x < pos.x + size.x; x++)
        {
            TryAdd(x, pos.y - 1);
            TryAdd(x, pos.y + size.y);
        }
        for (int y = pos.y; y < pos.y + size.y; y++)
        {
            TryAdd(pos.x - 1, y);
            TryAdd(pos.x + size.x, y);
        }

        return result;
    }

    // ── Path connectivity ────────────────────────────────────────────────────

    // True if placed path edges form an unbroken line of connected vertices
    // from StartVertex to EndVertex. Scoring/advancing the day is gated on this.
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

    // Grid-vertices reachable from `v` via a single placed path edge.
    private IEnumerable<Vector2Int> GetConnectedVertices(Vector2Int v)
    {
        // Horizontal edge (v.x, v.y) → (v.x+1, v.y)
        if (v.x < Width && _hEdges[v.x, v.y]) yield return new Vector2Int(v.x + 1, v.y);
        // Horizontal edge (v.x-1, v.y) → (v.x, v.y)
        if (v.x > 0 && _hEdges[v.x - 1, v.y]) yield return new Vector2Int(v.x - 1, v.y);
        // Vertical edge (v.x, v.y) → (v.x, v.y+1)
        if (v.y < Height && _vEdges[v.x, v.y]) yield return new Vector2Int(v.x, v.y + 1);
        // Vertical edge (v.x, v.y-1) → (v.x, v.y)
        if (v.y > 0 && _vEdges[v.x, v.y - 1]) yield return new Vector2Int(v.x, v.y - 1);
    }
}
