using System.Collections.Generic;
using UnityEngine;

public class EnclosureInstance
{
    public EnclosureData Data         { get; }
    public Vector2Int    GridPosition { get; set; }

    // Mana spent to play the card that created this enclosure — used to compute
    // the partial refund when the enclosure is later deleted.
    public int ManaCostPaid { get; set; }

    public int PermanentBonus { get; private set; }

    private readonly List<TimedBonus> _timedBonuses = new();

    public EnclosureInstance(EnclosureData data, Vector2Int position)
    {
        Data         = data;
        GridPosition = position;
    }

    public void AddPermanentBonus(int amount) => PermanentBonus += amount;

    public void AddTimedBonus(int amount, int expiresOnDay)
        => _timedBonuses.Add(new TimedBonus(amount, expiresOnDay));

    // Call once per day advance; removes any bonus whose expiry has passed.
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
