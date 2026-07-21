using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("Shop Data")]
    [SerializeField] private ShopItemsSO shopItemsData;

    [Header("UI Elements")]
    [SerializeField] private List<Button> buyCardsButtons;
    [SerializeField] private List<Button> buyLandButtons;
    [SerializeField] private Button gachaPackOneBtn;
    [SerializeField] private Button gachaPackTwoBtn;

    private Shop activeShop;
    private GameManager gameManagerInstance;

    void Awake()
    {
        gameManagerInstance = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();
        activeShop = new Shop(shopItemsData, buyCardsButtons.Count, buyLandButtons.Count, 1);
        SetupShopButtons();
        UpdateShopVisuals();
    }

    private void Start()
    {
        if (gameManagerInstance == null)
        {
            gameManagerInstance = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();
        }

        UpdateShopVisuals();
    }

    private void SetupShopButtons()
    {
        for (int i = 0; i < buyCardsButtons.Count; i++)
        {
            int index = i;
            buyCardsButtons[index].onClick.RemoveAllListeners();
            buyCardsButtons[index].onClick.AddListener(() => OnBuyCardClicked(index));
        }

        for (int i = 0; i < buyLandButtons.Count; i++)
        {
            int index = i;
            buyLandButtons[index].onClick.RemoveAllListeners();
            buyLandButtons[index].onClick.AddListener(() => OnBuyLandClicked(index));
        }

        if (gachaPackOneBtn != null)
        {
            gachaPackOneBtn.onClick.RemoveAllListeners();
            gachaPackOneBtn.onClick.AddListener(OnBuyGachaPackOne);
        }

        if (gachaPackTwoBtn != null)
        {
            gachaPackTwoBtn.onClick.RemoveAllListeners();
            gachaPackTwoBtn.onClick.AddListener(OnBuyGachaPackTwo);
        }
    }

    // --- Button Click Handlers ---

    private void OnBuyCardClicked(int buttonIndex)
    {
        if (activeShop.BuyCard(buttonIndex, GetGameManager()))
        {
            UpdateShopVisuals();
        }
    }

    private void OnBuyLandClicked(int buttonIndex)
    {
        if (activeShop.BuyLand(buttonIndex, GetGameManager()))
        {
            UpdateShopVisuals();
        }
    }

    private void OnBuyGachaPackOne()
    {
        //TODO: Implement Gacha Pack One logic in Shop.cs
    }

    private void OnBuyGachaPackTwo()
    {
        //TODO: Implement Gacha Pack Two logic in Shop.cs
    }

    // --- Visual Updates ---

    private void UpdateShopVisuals()
    {
        if (activeShop == null) return;

        for (int i = 0; i < buyCardsButtons.Count; i++)
        {
            UpdateCardButton(buyCardsButtons[i], i);
        }

        for (int i = 0; i < buyLandButtons.Count; i++)
        {
            UpdateLandButton(buyLandButtons[i], i);
        }
    }

    private void UpdateCardButton(Button button, int index)
    {
        var gm = GetGameManager();
        if (button == null || activeShop.buyCardsArea == null || index < 0 || index >= activeShop.buyCardsArea.Count) return;

        CardData card = activeShop.buyCardsArea[index];
        bool hasOffer = card != null;
        bool canAfford = hasOffer && gm != null && gm.CanAfford(gm.BuyCardCost);

        button.interactable = hasOffer && canAfford;
        SetButtonLabel(button, hasOffer ? $"{card.cardName}\nCost: {(gm != null ? gm.BuyCardCost : 0)}" : "Sold Out");
    }

    private void UpdateLandButton(Button button, int index)
    {
        var gm = GetGameManager();
        if (button == null || activeShop.buyLandArea == null || index < 0 || index >= activeShop.buyLandArea.Count) return;

        GameObject item = activeShop.buyLandArea[index];
        bool hasOffer = item != null;
        bool canAfford = hasOffer && gm != null && gm.CanAfford(gm.BuyLandCost);

        button.interactable = hasOffer && canAfford;
        SetButtonLabel(button, hasOffer ? $"{item.name}\nCost: {(gm != null ? gm.BuyLandCost : 0)}" : "Sold Out");
    }

    private void SetButtonLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = text;
        }
    }

    private GameManager GetGameManager()
    {
        if (gameManagerInstance == null)
        {
            gameManagerInstance = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();
        }

        return gameManagerInstance;
    }
}