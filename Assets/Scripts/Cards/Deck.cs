using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<CardData> Cards = new List<CardData>();

    public void AddCard(CardData card)
    {
        if (card == null) return;
        Cards.Add(card);
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
