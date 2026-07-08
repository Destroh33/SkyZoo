using UnityEngine;

// Plain C# — the mana budget for a single day's build phase.
public class ManaPool
{
    public int Max     { get; }
    public int Current { get; private set; }

    public ManaPool(int max)
    {
        Max     = max;
        Current = max;
    }

    public bool CanAfford(int amount) => Current >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        Current -= amount;
        return true;
    }

    // Deletion refund — never exceeds the day's max.
    public void Refund(int amount) => Current = Mathf.Min(Max, Current + amount);

    public void RefillForNewDay() => Current = Max;
}
