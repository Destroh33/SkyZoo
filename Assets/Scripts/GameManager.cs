using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum Phase { Build, Scoring, Reward }

    [Header("=======GAME MANAGER SINGLETON=======")]
    [DoNotSerialize] public static GameManager instance;

    [Header("Grid")]
    [SerializeField] private GridView gridView;

    [Header("Money Used for ")]
    public int money;
    [Header("Your current value for the target you're trying to reach")]
    public int goodReviewCount;

    [Header("Shop Costs")]
    [SerializeField] private int buyCardCost = 1;
    [SerializeField] private int buyLandCost = 1;

    public int BuyCardCost => buyCardCost;
    public int BuyLandCost => buyLandCost;

    [Header("Purchased Shop Items")]
    public List<GameObject> purchasedLand = new List<GameObject>();

    public Deck deck;

    [Header("Mana")]
    [SerializeField] private int   startingMana            = 3;
    [SerializeField] private float enclosureRefundFraction = 0.5f;

    [Header("Starting Hand (assign card assets in Inspector)")]
    [SerializeField] private CardData[] startingHand;

    [Header("Phase / Week")]
    [SerializeField] private int   daysPerWeek        = 5;
    [SerializeField] private float startingQuota      = 20f;
    [SerializeField] private float quotaGrowthPerWeek = 1.25f;
    [SerializeField] private CardData[] cardPool;

    [Header("Score Wave")]
    [SerializeField] private float scoreWaveStagger = 0.25f;
    [SerializeField] private float endOfDayPause    = 2f;

    private Hand     _hand;
    private ManaPool _mana;

    private Phase _phase      = Phase.Build;
    private int   _day        = 1;
    private int   _week       = 1;
    private int   _currentDay;
    private float _quota;
    private float _weekScore;
    private List<CardData> _rewardOptions;

    public event Action OnHandChanged;
    public event Action OnEconomyChanged;
    public event Action OnPhaseChanged;
    public event Action<EnclosureInstance, float> OnEnclosureScored;

    public Phase CurrentPhase  => _phase;
    public bool  InBuildPhase  => _phase == Phase.Build;
    public int   Day           => _day;
    public int   DaysPerWeek   => daysPerWeek;
    public int   Week          => _week;
    public int   CurrentDay    => _currentDay;
    public float Quota         => _quota;
    public float WeekScore     => _weekScore;
    public int   Mana          => _mana.Current;
    public int   MaxMana       => _mana.Max;
    public IReadOnlyList<CardInstance> HandCards     => _hand.Cards;
    public IReadOnlyList<CardData>     RewardOptions => _rewardOptions;

    public int PathsRemaining => Grid != null ? Grid.PathsRemaining : 0;
    public int MaxPaths       => Grid != null ? Grid.MaxPaths       : 0;

    private GridView Grid
    {
        get
        {
            if (gridView == null) gridView = FindAnyObjectByType<GridView>();
            return gridView;
        }
    }

    private void Awake()
    {
        instance = this;

        _mana  = new ManaPool(startingMana);
        _hand  = new Hand();
        _quota = startingQuota;

        if (startingHand != null)
            foreach (var card in startingHand)
                if (card != null) _hand.Add(card);
    }

    private void Start()
    {
        LogState("Game start");
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && money >= amount;
    }

    public bool TrySpendMoney(int amount)
    {
        if (!CanAfford(amount)) return false;
        money -= amount;
        return true;
    }

    public bool TryBuyCard(CardData card)
    {
        if (card == null || deck == null) return false;
        if (!TrySpendMoney(buyCardCost)) return false;
        deck.AddCard(card);
        return true;
    }

    public bool TryBuyLand(GameObject landTile)
    {
        if (landTile == null) return false;
        if (!TrySpendMoney(buyLandCost)) return false;
        purchasedLand.Add(landTile);
        return true;
    }

    public bool CanAffordMana(int amount) => _mana.CanAfford(amount);

    public bool TryPlayCard(CardInstance card)
    {
        if (card == null) return false;
        if (!_mana.TrySpend(card.Data.manaCost))
        {
            Debug.Log($"[SkyZoo] Not enough mana to play '{card.Data.cardName}' (need {card.Data.manaCost}, have {_mana.Current}).");
            return false;
        }

        _hand.Remove(card);
        OnHandChanged?.Invoke();
        OnEconomyChanged?.Invoke();
        return true;
    }

    public int RefundForEnclosure(EnclosureInstance instance)
    {
        int refund = Mathf.FloorToInt(instance.ManaCostPaid * enclosureRefundFraction);
        _mana.Refund(refund);
        OnEconomyChanged?.Invoke();
        return refund;
    }

    public void AdvanceDayPhase()
    {
        if (_phase != Phase.Build) return;

        var grid = Grid;
        if (grid == null) return;

        if (!grid.Model.HasValidPath())
        {
            Debug.Log("[SkyZoo] Can't advance — no valid path from start to end.");
            return;
        }

        grid.CancelSelection();
        _phase = Phase.Scoring;
        OnPhaseChanged?.Invoke();

        StartCoroutine(ScoreDayCoroutine());
    }

    private IEnumerator ScoreDayCoroutine()
    {
        var model = Grid.Model;

        _currentDay++;
        model.CurrentDay = _currentDay;

        var start   = new Vector2(model.StartVertex.x * 2f, model.StartVertex.y * 2f);
        var ordered = new List<EnclosureInstance>(model.Enclosures);
        ordered.Sort((a, b) =>
            Vector2.SqrMagnitude((Vector2)a.PivotHalf - start)
                .CompareTo(Vector2.SqrMagnitude((Vector2)b.PivotHalf - start)));

        foreach (var instance in ordered)
        {
            float score = model.GetEnclosureScore(instance);
            _weekScore += score;

            OnEnclosureScored?.Invoke(instance, score);
            OnEconomyChanged?.Invoke();

            if (scoreWaveStagger > 0f) yield return new WaitForSeconds(scoreWaveStagger);
        }

        foreach (var e in model.Enclosures) e.ExpireBonuses(_currentDay);

        foreach (var expired in model.GetExpiredEnclosures(_currentDay))
        {
            Debug.Log($"[SkyZoo] '{expired.Data.enclosureName}' reached the end of its lifespan and left the zoo.");
            Grid.DespawnEnclosure(expired);
        }

        _mana.RefillForNewDay();

        LogState($"Day {_day}/{daysPerWeek} scored → week total {_weekScore:0.#}/{_quota:0.#}");

        if (_day >= daysPerWeek)
        {
            bool passed = _weekScore >= _quota;
            Debug.Log(passed
                ? $"[SkyZoo] Week {_week} complete — quota met! ({_weekScore:0.#}/{_quota:0.#})"
                : $"[SkyZoo] Week {_week} FAILED quota. ({_weekScore:0.#}/{_quota:0.#})");

            _week++;
            _quota    *= quotaGrowthPerWeek;
            _weekScore = 0f;
            _day       = 1;
        }
        else
        {
            _day++;
        }

        OnEconomyChanged?.Invoke();

        if (endOfDayPause > 0f) yield return new WaitForSeconds(endOfDayPause);

        _rewardOptions = PickRandomCards(3);
        _phase = Phase.Reward;
        OnPhaseChanged?.Invoke();
    }

    public void ChooseReward(CardData card)
    {
        if (_phase != Phase.Reward || card == null) return;

        _hand.Add(card);
        _rewardOptions = null;
        _phase = Phase.Build;

        LogState($"Added '{card.cardName}' to hand from daily reward");
        OnHandChanged?.Invoke();
        OnPhaseChanged?.Invoke();
    }

    private List<CardData> PickRandomCards(int n)
    {
        var pool   = new List<CardData>(cardPool);
        var result = new List<CardData>();
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    public void NotifyEconomyChanged() => OnEconomyChanged?.Invoke();

    public void LogState(string action)
    {
        var names = new List<string>(_hand.Cards.Count);
        foreach (var c in _hand.Cards) names.Add($"{c.Data.cardName}({c.Data.manaCost})");
        string hand = names.Count > 0 ? string.Join(", ", names) : "(empty)";

        Debug.Log($"[SkyZoo] {action} — mana {_mana.Current}/{_mana.Max} | hand: {hand}");
    }
}
