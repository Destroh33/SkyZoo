# SkyZoo — Implementation Design Document

**Version:** 2.0  
**Studio:** Studio Black Belts  
**Engine:** Unity 6.5+ / URP

---

## Table of Contents

1. [Core Decisions](#core-decisions)
2. [Seed System](#seed-system)
3. [Folder Structure](#folder-structure)
4. [Animal Tags](#animal-tags)
5. [Game Loop & Money Flow](#game-loop--money-flow)
6. [Island Generation & Expansion](#island-generation--expansion)
7. [Grid Model](#grid-model)
8. [Scoring Engine](#scoring-engine)
9. [Path Validator](#path-validator)
10. [Ability System](#ability-system)
11. [Card System](#card-system)
12. [Phase State Machine](#phase-state-machine)
13. [Shop System](#shop-system)
14. [Game Context](#game-context)
15. [View Layer](#view-layer)
16. [Event Bus & Game Events](#event-bus--game-events)
17. [Implementation Phases](#implementation-phases)
18. [Open Questions](#open-questions)

---

## Core Decisions

| Question | Answer |
|---|---|
| Randomness | Seed-based — all randomness derived from a single run seed |
| Entrance / Exit | Any two distinct vertices on the outer perimeter of the grid, placed by seed |
| Card cost | Money — deducted from your weekly total directly (no mana) |
| Island expansion | Buy islands in the shop; they attach to the existing grid, growing it |
| Quota scaling | TBD — leave as a configurable curve for now |
| Metaprogression | None — every run is fresh |

---

## Seed System

Every run has a single `int runSeed`. All randomness is derived from it using a lightweight seeded RNG wrapper. The seed is displayed on the game over / victory screen so runs can be shared and replicated.

### SeededRandom

```
SeededRandom
  int seed
  System.Random rng             ← new System.Random(seed)

  int   Next(int min, int max)
  float NextFloat()
  T     Pick<T>(IList<T> list)
  void  Shuffle<T>(IList<T> list)
```

### Derived Seeds

Different systems receive derived seeds so their sequences don't correlate:

| System | Derived seed |
|---|---|
| IslandGenerator | `runSeed ^ 0x1A2B3C` |
| ShopSystem | `runSeed ^ 0x4D5E6F` |
| DeckManager | `runSeed ^ 0x7A8B9C` |

Each shop phase re-initialises the ShopSystem RNG with `runSeed ^ 0x4D5E6F ^ weekNumber` so offers are deterministic per week.

---

## Folder Structure

```
Assets/Scripts/
  Data/
    EnclosureData.cs               ScriptableObject: size, color, base value, tags, ability defs
    AbilityDefinition.cs           ScriptableObject (abstract): factory for AbilityInstances
    CardData.cs                    ScriptableObject: name, money cost, CardEffect reference
    CardEffect.cs                  ScriptableObject (abstract): Execute(GameContext)
    AnimalTag.cs                   Enum of all animal tags
    IslandChunkData.cs             ScriptableObject: chunk size, terrain biome, description
    abilities/
      NearbyTypeAbilityDef.cs
      CountSelfAbilityDef.cs
      RegionBonusAbilityDef.cs
      DestroyTriggerAbilityDef.cs
      PredatorPenaltyAbilityDef.cs
    effects/
      PlaceEnclosureEffect.cs
      UpgradeEnclosureEffect.cs
      SwapEnclosuresEffect.cs
      AttachIslandEffect.cs
      DrawCardsEffect.cs
      RerollHandEffect.cs
      RemoveCardEffect.cs

  Grid/
    GridModel.cs                   DONE — cell/edge data, place/remove/toggle
    EnclosureInstance.cs           DONE — runtime enclosure (position, level, ability instances)
    PathValidator.cs               BFS entrance→exit connectivity check
    IslandGenerator.cs             Generates initial grid layout from seed
    IslandAttacher.cs              Merges a new island chunk into existing GridModel

  Abilities/
    AbilityInstance.cs             Abstract runtime ability
    NearbyTypeAbility.cs
    CountSelfAbility.cs
    RegionBonusAbility.cs
    DestroyTriggerAbility.cs
    PredatorPenaltyAbility.cs

  Events/
    EventBus.cs
    GameEvents.cs                  All event structs in one file

  Core/
    SeededRandom.cs
    ScoringEngine.cs
    PhaseStateMachine.cs
    DeckManager.cs
    ShopSystem.cs
    GameContext.cs
    RunConfig.cs                   Holds seed, starting deck, difficulty curve

  View/
    GridView.cs                    DONE — grid rendering + mouse input
    CardHandView.cs
    PhaseHUD.cs
    ShopView.cs
    ScorePopupView.cs
    IslandAttachView.cs
```

---

## Animal Tags

```csharp
public enum AnimalTag
{
    // Habitat type
    Water,
    Land,
    Air,

    // Region
    Savannah,
    Arctic,
    Jungle,
    Ocean,
    Desert,

    // Predator / prey
    Predator,
    Prey,

    // Social behaviour (for count-based synergies)
    Pack,       // bonus for many of same species
    Solitary,   // penalty for adjacent same species
}
```

Each `EnclosureData` has a `List<AnimalTag> tags`. Abilities and predator/prey rules filter on these.

**Predator/Prey rule:** if an enclosure tagged `Predator` is adjacent to one tagged `Prey`, both receive a score penalty. Enforced by `PredatorPenaltyAbility` reacting to `ExhibitPlaced` / `ExhibitRepositioned`. The GridView also highlights invalid placements so the player knows before they commit.

---

## Game Loop & Money Flow

### Weekly Structure

```
Week start
  └─ Build Phase
       player plays cards (each deducts money)
       ↓  [player clicks "Open Zoo" — path must be valid]
     Exhibition Phase  ×7 days
       each day: money += ScoringEngine.ScoreExhibition()
       ↓  [after day 7]
     Quota check
       money >= quota  →  Shop Phase  →  Week + 1
       money <  quota  →  Game Over
  └─ Shop Phase
       player spends remaining money on cards / islands
       ↓  [player clicks "Next Week"]
     Week + 1  (repeat for 10 weeks, then Victory)
```

### Money Tension

Cards cost money that comes out of the same pool you're building toward the quota. Every card played is a direct trade-off: better zoo = more score, but you're spending some of this week's earnings to get there. Unspent money carries into the shop phase. Whether it carries into the **next week** is a balance toggle on `RunConfig`.

---

## Island Generation & Expansion

### Initial Island — IslandGenerator

```
IslandGenerator
  (GridModel grid, Vector2Int entrance, Vector2Int exit)
    Generate(int width, int height, SeededRandom rng)
```

Steps:
1. Create a `GridModel` of the given dimensions.
2. **Entrance / exit placement:** enumerate all outer-perimeter vertices `(x, y)` where `x ∈ {0, Width}` or `y ∈ {0, Height}`. Pick two that are at least `perimeter / 2` apart (using seeded pick). These become `entrance` and `exit`, stored in `GameContext`.
3. Stamp terrain biome tags onto cells (optional for MVP — used later for habitat placement rules).
4. Return the grid and the two vertex positions.

### Island Expansion — IslandAttacher & IslandChunkData

**IslandChunkData** (ScriptableObject): defines the chunk's width/height, terrain biome, display name, and shop cost.

When the player buys an island:
1. `ShopSystem` selects a seeded `IslandChunkData` for this week.
2. `AttachIslandEffect.Execute()` opens `IslandAttachView`.
3. Player picks which side to attach to (North / South / East / West).
4. `IslandAttacher.Attach(grid, chunk, side, rng)` expands the `GridModel` by resizing its arrays and adding new empty cells. Existing enclosures, edges, and blocked edges are preserved.
5. The path can now route through the new cells.

```
IslandAttacher
  void Attach(GridModel grid, IslandChunkData chunk, AttachSide side)

enum AttachSide { North, South, East, West }
```

`GridModel` needs a `Resize(newWidth, newHeight, Vector2Int offset)` that expands all arrays while shifting existing data if the offset is non-zero (e.g., attaching to the South or West grows the grid upward/rightward and shifts indices).

---

## Grid Model

*Already implemented. Summary for reference.*

**Coordinate convention:**
- Cell `(x, y)` occupies world space `(x, y)` to `(x+1, y+1)`.
- `hEdges[x, y]` — horizontal edge from `(x, y)` to `(x+1, y)`. `x: 0..Width-1`, `y: 0..Height`.
- `vEdges[x, y]` — vertical edge from `(x, y)` to `(x, y+1)`. `x: 0..Width`, `y: 0..Height-1`.
- Interior edges of multi-cell enclosures are stored in `hBlocked` / `vBlocked` and cannot receive path pieces.

**Key methods:**
- `CanPlaceEnclosure(pos, size)` — bounds + overlap check
- `PlaceEnclosure(data, pos)` — occupies cells, blocks interior edges, returns `EnclosureInstance`
- `RemoveEnclosure(instance)` — frees cells, unblocks interior edges
- `ToggleHEdge(x, y)` / `ToggleVEdge(x, y)` — toggle path on an edge (fails silently if blocked)
- `CountPerimeterPathEdges(instance)` — counts active path edges on the enclosure's perimeter

**Perimeter edge count examples:**
- 3×3 enclosure → 12 perimeter edges
- 3×1 enclosure → 8 perimeter edges

---

## Scoring Engine

Called once per Exhibition phase (7 times per week). Each enclosure is scored independently.

### Formula

```
score = (baseValue + abilityBonus) × 1.5^n

where:
  baseValue    = enclosure.Data.baseValuePerLevel[enclosure.Level]
  abilityBonus = sum of enclosure.Abilities.Select(a => a.GetBonus())
  n            = grid.CountPerimeterPathEdges(enclosure)
```

The exponential path multiplier means routing the main path around high-value enclosures is the primary spatial optimisation. Abilities drive up `(base + bonus)`; path routing drives up `1.5^n`. These two axes create the core strategic tension.

### Implementation

```
ScoringEngine
  int ScoreExhibition(GridModel grid, EventBus events)
    total = 0
    foreach enclosure in grid.Enclosures:
      base   = enclosure.Data.baseValuePerLevel[enclosure.Level]
      bonus  = enclosure.Abilities.Sum(a => a.GetBonus())
      n      = grid.CountPerimeterPathEdges(enclosure)
      score  = RoundToInt((base + bonus) * Pow(1.5f, n))
      total += score
      events.Publish(new ExhibitScored { enclosure, base, bonus, n, score })
    return total
```

`ExhibitScored` is consumed by `ScorePopupView` to animate numbers over each enclosure.

---

## Path Validator

BFS over the vertex graph. Grid vertices are integer coordinate points `(x, y)` where `x ∈ [0, Width]` and `y ∈ [0, Height]`. Two vertices are connected if:
1. The edge between them is active in `GridModel`, AND
2. That edge is not blocked (not interior to an enclosure).

```
PathValidator
  bool IsPathValid(GridModel grid, Vector2Int entrance, Vector2Int exit)
    → BFS from entrance to exit over active, unblocked edges
    → returns true if exit is reachable
```

`PhaseStateMachine` calls this when the player clicks "Open Zoo". If the path is invalid, the transition to Exhibition is blocked and the player sees an error highlight.

---

## Ability System

### Design Principle

Abilities maintain **cached internal state** updated reactively via the `EventBus`. At scoring time they simply return their cached value. This avoids per-frame grid queries and lets abilities react to events they care about (placements, destructions, upgrades, repositions).

### AbilityDefinition (abstract ScriptableObject)

```
AbilityDefinition
  string displayName
  string descriptionTemplate      "gives +{0} per nearby {1} exhibit"

  abstract AbilityInstance CreateInstance(EnclosureInstance owner, IGridQuery grid)
```

Concrete definitions live in `Data/abilities/`. Adding a new ability = add one `AbilityDefinition` subclass + one `AbilityInstance` subclass, then attach the SO asset to any `EnclosureData`.

### AbilityInstance (abstract)

```
AbilityInstance
  EnclosureInstance Owner
  IGridQuery        Grid
  EventBus          Bus

  void Init(EventBus bus)       subscribe to relevant events
  void Destroy()                unsubscribe, clean up
  abstract int GetBonus()
```

### IGridQuery (interface implemented by GridModel)

```
IGridQuery
  IEnumerable<EnclosureInstance> GetAdjacentEnclosures(Vector2Int pos, Vector2Int size)
  IEnumerable<EnclosureInstance> GetEnclosuresInRadius(Vector2Int center, int radius)
  EnclosureInstance GetCell(int x, int y)
```

### Level Scaling

`AbilityDefinition` stores `int[] bonusPerLevel`. `AbilityInstance` reads `Owner.Level` at `GetBonus()` time to index into it. Upgrading an enclosure fires `ExhibitUpgraded` — abilities that scale with level recompute on this event.

### Repositioning

When a swap card fires `ExhibitRepositioned` for both swapped enclosures, any `AbilityInstance` whose owner moved OR whose spatial neighbourhood changed calls `FullRescan()` — a complete rebuild of its cached count from the current grid state. Swaps are infrequent so the linear scan is fine.

### Concrete Abilities

#### NearbyTypeAbility
- **Def parameters:** `List<AnimalTag> filterTags`, `RangeType (Adjacent | Radius)`, `int radius`, `int[] bonusPerLevelPerMatch`
- **Listens to:** `ExhibitPlaced`, `ExhibitDestroyed`, `ExhibitRepositioned`
- **On Repositioned:** if own owner moved or a neighbour moved → `FullRescan()`
- **Cache:** `int matchCount`
- **GetBonus:** `matchCount × bonusPerLevelPerMatch[owner.Level]`
- **Example:** Seal enclosure — +3 per adjacent Water-tagged exhibit

#### CountSelfAbility
- **Def parameters:** `int[] bonusPerLevelPerExtra`
- **Listens to:** `ExhibitPlaced`, `ExhibitDestroyed` (counts enclosures with same `EnclosureData`)
- **Cache:** `int totalCount`
- **GetBonus:** `max(0, totalCount - 1) × bonusPerLevelPerExtra[owner.Level]`
- **Example:** Ant colony — every ant enclosure gets +X for each other ant enclosure on the island

#### RegionBonusAbility
- Same structure as `NearbyTypeAbility` but filters on region tags (Savannah, Arctic, etc.)
- **Example:** Lion + Zebra both tagged Savannah → regional bonus for being in a Savannah cluster

#### DestroyTriggerAbility
- **Listens to:** `ExhibitDestroyed` (any enclosure destroyed, not just owner)
- **Cache:** `int destroyedCount` (accumulates over the whole run)
- **GetBonus:** `destroyedCount × bonusPerDestroy`
- **Example:** Vulture — gains a stacking bonus every time any enclosure is demolished

#### PredatorPenaltyAbility
- **Listens to:** `ExhibitPlaced`, `ExhibitDestroyed`, `ExhibitRepositioned`
- **Checks:** adjacency between owner and any exhibit with conflicting predator/prey tags
- **GetBonus:** returns a **negative** value if a conflict is detected
- Also consulted by `GridView` at placement time to highlight danger zones

---

## Card System

### CardData (ScriptableObject)

```
CardData
  string     cardName
  int        moneyCost
  CardEffect effect
  Sprite     artwork
  string     description
```

### CardEffect (abstract ScriptableObject)

```
CardEffect
  abstract void Execute(GameContext ctx)
  virtual bool CanExecute(GameContext ctx)   default: ctx.Money >= card.moneyCost
  virtual string GetDescription()
```

### DeckManager

```
DeckManager
  List<CardData> drawPile        shuffled by SeededRandom on run start
  List<CardData> hand
  List<CardData> discardPile
  SeededRandom   rng

  void StartTurn()               draw up to handSize (default 5)
  bool TryPlayCard(CardData, GameContext)
    → CanExecute check
    → ctx.SpendMoney(card.moneyCost)
    → effect.Execute(ctx)
    → move card to discard
  void Discard(CardData)         manual discard
  void EndTurn()                 discard remaining hand; reshuffle discard into draw if draw empty
  void AddCard(CardData)         from shop
  void RemoveCard(CardData)      deck thinning
```

### Concrete Card Effects

| Effect | Behaviour |
|---|---|
| `PlaceEnclosureEffect(EnclosureData)` | Sets `GridView` into enclosure placement mode for the given data |
| `UpgradeEnclosureEffect` | Player clicks an existing enclosure → increment its level, fire `ExhibitUpgraded` |
| `SwapEnclosuresEffect` | Player clicks two same-size enclosures → swap positions, fire `ExhibitRepositioned` for both |
| `AttachIslandEffect` | Opens `IslandAttachView` → player picks attachment side → `IslandAttacher.Attach()` |
| `DrawCardsEffect(int n)` | Draw n extra cards into hand immediately |
| `RerollHandEffect` | Discard hand, draw a new hand (costs `moneyCost`) |
| `RemoveCardEffect` | Player picks a card from deck/discard to permanently remove |

---

## Phase State Machine

```
States: Build | Exhibition | Shop | GameOver | Victory

Fields:
  int day          1–7
  int week         1–10
  int money        current money pool
  int quota        target for this week (set from RunConfig curve)

Transitions:
  Build      →  Exhibition  : "Open Zoo" clicked AND PathValidator.IsPathValid() == true
  Exhibition →  Build       : score animation done, day < 7 → day++
  Exhibition →  Shop        : score animation done, day == 7 AND money >= quota
  Exhibition →  GameOver    : score animation done, day == 7 AND money < quota
  Shop       →  Build       : "Next Week" clicked → week++, reset day = 1, compute new quota
  [week == 10, Shop → Build transition] →  Victory after next week clears
```

On entering **Exhibition**: fires `DayStarted { day, week }`, then calls `ScoringEngine.ScoreExhibition()`, adds result to `money`, fires `MoneyChanged`.

On entering **Shop**: fires `WeekEnded { week, money, quota, survived=true }`.

On **GameOver**: fires `WeekEnded { week, money, quota, survived=false }`.

---

## Shop System

```
ShopSystem
  SeededRandom      rng              re-seeded each week: runSeed ^ 0x4D5E6F ^ weekNumber
  List<CardData>    cardPool         all card ScriptableObjects in the game
  List<CardData>    offer            4 cards, generated at week start
  IslandChunkData   islandOffer      1 island, generated at week start
  int               rerollCost

  void GenerateOffer(int week)
    → deterministic pick of 4 cards from pool (weighted by rarity / week number)
    → deterministic pick of 1 island chunk

  bool TryBuyCard(CardData card, GameContext ctx)
    → ctx.SpendMoney(card.shopCost), ctx.Deck.AddCard(card)

  bool TryBuyIsland(GameContext ctx)
    → ctx.SpendMoney(islandOffer.shopCost)
    → queues AttachIslandEffect

  bool TryReroll(GameContext ctx)
    → ctx.SpendMoney(rerollCost)
    → regenerates offer (advances rng state, not re-seeded)
```

---

## Game Context

Passed into `CardEffect.Execute()` and anywhere cross-system access is needed. Acts as a run-scoped service locator.

```
GameContext
  int               RunSeed
  SeededRandom      RootRng
  GridModel         Grid
  Vector2Int        Entrance
  Vector2Int        Exit
  GridView          GridView
  DeckManager       Deck
  PhaseStateMachine Phase
  ScoringEngine     Scoring
  ShopSystem        Shop
  EventBus          Events
  int               Money

  void SpendMoney(int amount)    deducts, fires MoneyChanged, asserts non-negative
  void EarnMoney(int amount)     adds, fires MoneyChanged
```

---

## View Layer

| Component | Responsibility |
|---|---|
| `GridView` | **DONE** — grid rendering, enclosure placement preview, path edge placement, hover |
| `CardHandView` | Displays `DeckManager.hand`; click/drag to play; grays out unaffordable cards; shows money cost |
| `PhaseHUD` | Shows Phase, Day X/7, Week X/10, Money, Quota progress bar, "Open Zoo" / "Next Week" buttons |
| `ShopView` | Displays `ShopSystem.offer` and `islandOffer`; buy and reroll buttons; remaining money |
| `ScorePopupView` | Subscribes to `ExhibitScored`; animates score numbers floating above each enclosure during Exhibition |
| `IslandAttachView` | Shown when `AttachIslandEffect` executes; lets player pick North/South/East/West attachment side |

---

## Event Bus & Game Events

### EventBus

```
EventBus
  void Publish<T>(T evt)
  void Subscribe<T>(Action<T> handler)
  void Unsubscribe<T>(Action<T> handler)
```

Uses a `Dictionary<Type, Delegate>` internally. Generic, allocation-light (no boxing for struct events).

### GameEvents (all structs)

```csharp
struct ExhibitPlaced       { EnclosureInstance Instance; Vector2Int Position; }
struct ExhibitDestroyed    { EnclosureInstance Instance; Vector2Int Position; }
struct ExhibitRepositioned { EnclosureInstance Instance; Vector2Int OldPos; Vector2Int NewPos; }
struct ExhibitUpgraded     { EnclosureInstance Instance; int NewLevel; }
struct ExhibitScored       { EnclosureInstance Instance; int Base; int Bonus; int PathEdges; int Total; }
struct PathChanged         { }
struct PhaseChanged        { Phase Previous; Phase Next; }
struct DayStarted          { int Day; int Week; }
struct WeekEnded           { int Week; int TotalEarned; int Quota; bool Survived; }
struct CardPlayed          { CardData Card; }
struct MoneyChanged        { int Current; }
```

---

## Implementation Phases

| # | Phase | What gets built | Playable result |
|---|---|---|---|
| 1 | Grid | GridModel, EnclosureData, GridView, EnclosureInstance | Place enclosures and path edges on a grid |
| 2 | Seed + Island Gen | SeededRandom, IslandGenerator, entrance/exit placement | Grid is generated from a seed; entrance/exit marked |
| 3 | Path Validation | PathValidator, "Open Zoo" button, error highlight | Cannot advance to Exhibition with a broken path |
| 4 | Scoring | ScoringEngine, EventBus, GameEvents, ScorePopupView | Enclosures score correctly; numbers animate |
| 5 | Phase Loop | PhaseStateMachine, PhaseHUD, money tracking | Full Build→Exhibition→Shop→Week cycle runs end to end |
| 6 | Cards | CardData, CardEffect, DeckManager, CardHandView | Cards in a hand trigger placement; money spent on play |
| 7 | Abilities | AbilityInstance (abstract + all concrete), wired into EnclosureInstance | Synergy bonuses affect scoring |
| 8 | Shop | ShopSystem, ShopView, card pool assets | Buy cards between weeks |
| 9 | Island Expansion | IslandChunkData, IslandAttacher, IslandAttachView, AttachIslandEffect | Buy and attach new island chunks |
| 10 | Content | EnclosureData assets, CardData assets, balance pass | Playable with real content |

---

## Open Questions

| Question | Notes |
|---|---|
| Does unspent money carry into the next week? | Balance toggle — carrying over rewards early scoring; not carrying keeps every week as a fresh race. Try both. |
| Quota scaling curve | Linear ramp? Exponential? Fixed Balatro-style targets? Needs playtesting. |
| How many cards in the starting deck? | Typical roguelike starting decks are 10–15. Balance TBD. |
| Hand size? | Default 5 (like Balatro). May want this as a run modifier. |
| Are there card rarities? | Affects shop weighting and which cards appear early vs. late in the run. |
| Irregular island shapes? | Current design assumes rectangular chunks. Non-rectangular shapes (L-shapes, etc.) need inactive cell support in GridModel. |
| Predator/prey pairs — hardcoded or data-driven? | A `PredatorPreyPairData` SO listing which EnclosureDatas conflict would be cleanest. |
