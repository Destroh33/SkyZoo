using System.Collections.Generic;
using UnityEngine;

public class EnclosureInstance
{
    public EnclosureData Data         { get; }

    public Vector2Int    PivotHalf    { get; set; }

    public int           Rotation     { get; set; }

    public int ManaCostPaid { get; set; }

    public int PermanentBonus { get; private set; }

    private readonly List<TimedBonus> _timedBonuses = new();

    public EnclosureInstance(EnclosureData data, Vector2Int pivotHalf, int rotation = 0)
    {
        Data      = data;
        PivotHalf = pivotHalf;
        Rotation  = rotation;
    }

    public int CellCount => Data.CellCount;

    public Vector2Int GetCell(int index) => Data.GetCell(index, PivotHalf, Rotation);

    public IEnumerable<Vector2Int> Cells
    {
        get
        {
            for (int i = 0; i < Data.CellCount; i++)
                yield return Data.GetCell(i, PivotHalf, Rotation);
        }
    }

    public RectInt Bounds => Data.GetBounds(PivotHalf, Rotation);

    public Vector2 PivotInCells => new(PivotHalf.x * 0.5f, PivotHalf.y * 0.5f);

    public void AddPermanentBonus(int amount) => PermanentBonus += amount;

    public void AddTimedBonus(int amount, int expiresOnDay)
        => _timedBonuses.Add(new TimedBonus(amount, expiresOnDay));

    public void ExpireBonuses(int currentDay)
        => _timedBonuses.RemoveAll(b => currentDay >= b.ExpiresOnDay);

    public int TotalBonus
    {
        get
        {
            int total = PermanentBonus;
            foreach (var b in _timedBonuses) total += b.Amount;
            return total;
        }
    }

    private readonly struct TimedBonus
    {
        public readonly int Amount;
        public readonly int ExpiresOnDay;

        public TimedBonus(int amount, int expiresOnDay)
        {
            Amount       = amount;
            ExpiresOnDay = expiresOnDay;
        }
    }
}
