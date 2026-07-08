// A single physical card sitting in the hand. Wraps a CardData asset, but has
// its own identity — two CardInstances can point at the same CardData (e.g.
// two "Lion Enclosure" cards) without being the same object. Selection,
// highlighting, and removal all key off this identity, not the shared asset,
// so duplicate card types in hand don't get confused for one another.
public class CardInstance
{
    public CardData Data { get; }

    public CardInstance(CardData data) => Data = data;
}
