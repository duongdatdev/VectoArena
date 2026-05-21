using UnityEngine;
using UnityEngine.UIElements;

public class StoreScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Button backButton;
    private VisualElement productList;
    private Label coinsAmount;
    private Label storeStatus;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document == null) return;
        
        root = document.rootVisualElement;

        // Start hidden
        root.AddToClassList("hidden");

        backButton = root.Q<Button>("BackButton");
        backButton.clicked += Hide;
        productList = root.Q<VisualElement>("ProductList");
        coinsAmount = root.Q<Label>("CoinsAmount");
        storeStatus = root.Q<Label>("StoreStatus");
        PlayerInventory.Changed += RefreshProducts;
        RefreshProducts();
    }

    private void OnDisable()
    {
        PlayerInventory.Changed -= RefreshProducts;
    }

    public void Show()
    {
        _ = PlayerInventory.LoadFromServer();
        RefreshProducts();
        root.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        root.AddToClassList("hidden");
    }

    private void RefreshProducts()
    {
        if (productList == null)
        {
            return;
        }

        PlayerInventory.EnsureInitialized();
        if (coinsAmount != null)
        {
            coinsAmount.text = $"{PlayerInventory.Coins:N0} COINS  |  {PlayerInventory.VecUnlockedBalance:N0} VEC";
        }
        productList.Clear();

        foreach (SkinCatalogItem item in SkinCatalog.Items)
        {
            productList.Add(CreateSkinProduct(item));
        }
    }

    private VisualElement CreateSkinProduct(SkinCatalogItem item)
    {
        SkinOwnershipState state = PlayerInventory.GetSkinState(item);
        bool owned = state != null && state.Owned;
        bool canEquip = state != null && state.CanEquip;
        bool isNft = state != null && state.IsNft;
        bool equipped = (state != null && state.Equipped) || PlayerInventory.EquippedSkinId == item.Id;

        VisualElement card = new VisualElement();
        card.AddToClassList("product-card");

        VisualElement icon = new VisualElement();
        icon.AddToClassList("product-icon");
        Texture2D texture = SkinCatalog.LoadIcon(item);
        if (texture != null)
        {
            icon.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
        }

        Label name = new Label(item.DisplayName.ToUpper());
        name.AddToClassList("product-name");

        Button buyButton = new Button();
        buyButton.AddToClassList("button-long");
        buyButton.AddToClassList("button-long--yellow");
        buyButton.AddToClassList("product-price-btn");
        buyButton.text = GetButtonText(item, state, owned, canEquip, equipped, isNft);
        buyButton.SetEnabled(!equipped);
        buyButton.clicked += async () =>
        {
            if (equipped)
            {
                return;
            }

            buyButton.SetEnabled(false);
            bool success;
            if (owned && canEquip)
            {
                success = await PlayerInventory.EquipSkinAsync(item.Id);
            }
            else if (isNft)
            {
                success = await PlayerInventory.TryBuyNftSkinAsync(item, status =>
                {
                    buyButton.text = status;
                    if (storeStatus != null)
                    {
                        storeStatus.text = status;
                    }
                });
            }
            else
            {
                success = await PlayerInventory.TryBuySkinAsync(item);
            }

            if (storeStatus != null)
            {
                if (success)
                {
                    SkinOwnershipState refreshedState = PlayerInventory.GetSkinState(item);
                    bool nowEquipped = (refreshedState != null && refreshedState.Equipped) || PlayerInventory.EquippedSkinId == item.Id;
                    storeStatus.text = nowEquipped ? $"EQUIPPED {item.DisplayName.ToUpper()}" : $"{item.DisplayName.ToUpper()} READY";
                }
                else if (!string.IsNullOrEmpty(PlayerInventory.LastOperationError))
                {
                    storeStatus.text = PlayerInventory.LastOperationError;
                }
                else if (!isNft)
                {
                    storeStatus.text = $"NOT ENOUGH {GetCurrencyLabel(item, state)}";
                }
            }
            RefreshProducts();
        };

        card.Add(icon);
        card.Add(name);
        card.Add(buyButton);
        return card;
    }

    private string GetButtonText(SkinCatalogItem item, SkinOwnershipState state, bool owned, bool canEquip, bool equipped, bool isNft)
    {
        if (equipped)
        {
            return "EQUIPPED";
        }

        if (owned && canEquip)
        {
            return "EQUIP";
        }

        if (isNft)
        {
            return "BUY NFT";
        }

        return "BUY";
    }

    private string GetCurrencyLabel(SkinCatalogItem item, SkinOwnershipState state = null)
    {
        string currencyType = state != null && !string.IsNullOrEmpty(state.CurrencyType) ? state.CurrencyType : item.CurrencyType;
        return string.Equals(currencyType, "VEC", System.StringComparison.OrdinalIgnoreCase) ? "VEC" : "COINS";
    }
}
