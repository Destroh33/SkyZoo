using System.Collections.Generic;

// Plain C# — the player's card pool. No size limit and no cost to hold a
// card; only playing one (spending mana) removes it and does something to
// the grid. Cards never score on their own.
//
// Stores CardInstance wrappers, not raw CardData — two copies of the same
// card asset (e.g. two "Lion Enclosure" cards from reward picks) each get
// their own instance, so selecting/removing one never affects the other.
public class Hand
{
    private readonly List<CardInstance> _cards = new();
    public IReadOnlyList<CardInstance> Cards => _cards;

    public CardInstance Add(CardData data)
    {
        var instance = new CardInstance(data);
        _cards.Add(instance);
        return instance;
    }

    public bool Remove(CardInstance instance) => _cards.Remove(instance);
}
