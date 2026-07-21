using System.Collections.Generic;
using UnityEngine;

public class Shop
{
    public ShopItemsSO items;

    public List<CardData> buyCardsArea;
    public int buyCardsCount;
    public List<GameObject> buyLandArea;
    public int buyLandCount;
    public List<GameObject> gachaPackOne;
    public List<GameObject> gachaPackTwo;
    public int gachaCount;
    
    public Shop(ShopItemsSO items, int buyCardsCount, int buyLandCount, int gachaCount) 
    {
        this.items = items;
        this.buyCardsCount = buyCardsCount;
        this.buyLandCount = buyLandCount;
        this.gachaCount = gachaCount;

        buyCardsArea = new List<CardData>(buyCardsCount);
        buyLandArea = new List<GameObject>(buyLandCount);
        gachaPackOne = new List<GameObject>();
        gachaPackTwo = new List<GameObject>();

        for (int i = 0; i < buyCardsCount; i++)
        {
            buyCardsArea.Add(null);
        }

        for (int i = 0; i < buyLandCount; i++)
        {
            buyLandArea.Add(null);
        }

        PopulateShopCardlist(buyCardsArea);
        PopulateShopLandList(buyLandArea);
    }
    

    private void PopulateShopCardlist(List<CardData> shopList) 
    {
        if (items == null) return;

        float totalPullRate = items.tierOnePullRate + items.tierTwoPullRate + items.tierThreePullRate + items.tierFourPullRate;
        if (Mathf.Abs(totalPullRate - 1f) > 0.001f) 
        {
            Debug.LogError("Card pull rates don't add up to 1");
        }

        for (int i = 0; i < shopList.Count; i++) 
        {
            int rarity = Random.Range(0, 100);
            shopList[i] = PickRandomCard(rarity);
        }
    }

    private CardData PickRandomCard(int rarity)
    {
        int tierOneThreshold = Mathf.RoundToInt(items.tierOnePullRate * 100f);
        int tierTwoThreshold = tierOneThreshold + Mathf.RoundToInt(items.tierTwoPullRate * 100f);
        int tierThreeThreshold = tierTwoThreshold + Mathf.RoundToInt(items.tierThreePullRate * 100f);

        if (rarity < tierOneThreshold)
        {
            return PickFromList(items.tierOneRarityCards);
        }

        if (rarity < tierTwoThreshold)
        {
            return PickFromList(items.tierTwoRarityCards);
        }

        if (rarity < tierThreeThreshold)
        {
            return PickFromList(items.tierThreeRarityCards);
        }

        return PickFromList(items.tierFourRarityCards);
    }

    private CardData PickFromList(List<CardData> options)
    {
        if (options == null || options.Count == 0) return null;
        return options[Random.Range(0, options.Count)];
    }

    private void PopulateShopLandList(List<GameObject> shopList) 
    {
        if (items == null || items.buyableLandTiles == null || items.buyableLandTiles.Count == 0) return;

        for (int i = 0; i < shopList.Count; i++) 
        {
            shopList[i] = items.buyableLandTiles[Random.Range(0, items.buyableLandTiles.Count)];
        }
    }

    public bool BuyLand(int index, GameManager gameManager) 
    {
        if (!IsValidIndex(buyLandArea, index) || gameManager == null) return false;
        if (buyLandArea[index] == null) return false;
        if (!gameManager.TryBuyLand(buyLandArea[index])) return false;

        buyLandArea[index] = null;
        return true;
    }

    public bool BuyCard(int index, GameManager gameManager) 
    {
        if (!IsValidIndex(buyCardsArea, index) || gameManager == null) return false;
        if (buyCardsArea[index] == null) return false;
        if (!gameManager.TryBuyCard(buyCardsArea[index])) return false;

        buyCardsArea[index] = null;
        return true;
    }

    private bool IsValidIndex<T>(List<T> list, int index)
    {
        return list != null && index >= 0 && index < list.Count;
    }
}
