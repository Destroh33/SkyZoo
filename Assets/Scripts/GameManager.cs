using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("=======GAME MANAGER SINGLETON=======")]
    [DoNotSerialize] public static GameManager instance;

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
    ManaPool mana;

    private void Awake()
    {
        instance = this;
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
