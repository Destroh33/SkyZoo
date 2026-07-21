using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopItemsSO", menuName = "SkyZoo/ShopItemsSO")]
public class ShopItemsSO : ScriptableObject
{
    /*
     This SO holds all cards that can be bought. All cards in a given tier will have the same pull rate.
     A random one will be chosen from the list after the rarity for a given spot is chosen.
     All cards should be here - animals, abilities, concessions, etc.
    */

    [Header("NOTE - MAKE SURE ALL PULL RATES ADD TO 1")]
    [Header("All Possible Shop Cards - t1 = most common, t4 = least common")]
    public List<CardData> tierOneRarityCards;
    [Range(0f, 1f)] public float tierOnePullRate;
    public List<CardData> tierTwoRarityCards;
    [Range(0f, 1f)] public float tierTwoPullRate;
    public List<CardData> tierThreeRarityCards;
    [Range(0f, 1f)] public float tierThreePullRate;
    public List<CardData> tierFourRarityCards;
    [Range(0f, 1f)] public float tierFourPullRate;

    [Header("Tile Items")]
    public List<GameObject> buyableLandTiles;
}
