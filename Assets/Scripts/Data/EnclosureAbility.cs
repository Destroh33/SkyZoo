using UnityEngine;

// Base class for a unique per-enclosure-type ability. Subclass and override
// whichever hook the ability needs — new animals get new subclasses, no
// changes required to GridModel or EnclosureData.
public abstract class EnclosureAbility : ScriptableObject
{
    // Flat point bonus added to `self`'s own base value before the
    // perimeter-path multiplier is applied. Called once per scoring pass.
    // Default: no bonus.
    public virtual int CalculateBonus(EnclosureInstance self, GridModel model) => 0;

    // Multiplier `self` applies to an adjacent enclosure `neighbor`'s final
    // score (e.g. the unicorn buffing everything touching it). Called once
    // per scoring pass for every enclosure adjacent to `self`. Default: 1 (no effect).
    public virtual float GetNeighborMultiplier(EnclosureInstance self, EnclosureInstance neighbor, GridModel model) => 1f;
}
