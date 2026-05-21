using UnityEngine;
using UnityEngine.UIElements;

public class StoreScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Button backButton;
    private VisualElement productList;
    private Label coinsAmount;
    private Label storeVecAmount;
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
        storeVecAmount = root.Q<Label>("StoreVecAmount");
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
            coinsAmount.text = PlayerInventory.Coins.ToString("N0");
        }
        if (storeVecAmount != null)
        {
            storeVecAmount.text = PlayerInventory.VecUnlockedBalance.ToString("N0");
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

        VisualElement iconFrame = new VisualElement();
        iconFrame.AddToClassList("product-icon-frame");

        VisualElement icon = new VisualElement();
        icon.AddToClassList("product-icon");
        Texture2D texture = SkinCatalog.LoadIcon(item);
        if (texture != null)
        {
            icon.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
        }

        VisualElement priceBadge = CreatePriceBadge(item, state, owned, equipped);
        iconFrame.Add(icon);
        iconFrame.Add(priceBadge);

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

        card.Add(iconFrame);
        card.Add(name);
        card.Add(buyButton);
        return card;
    }

    private VisualElement CreatePriceBadge(SkinCatalogItem item, SkinOwnershipState state, bool owned, bool equipped)
    {
        VisualElement badge = new VisualElement();
        badge.AddToClassList("product-price-badge");

        string badgeText = GetPriceBadgeText(item, state, owned, equipped);
        bool isPrice = !owned && !equipped;
        bool canAfford = HasEnoughCurrency(item, state);

        if (!isPrice)
        {
            badge.AddToClassList("product-price-badge--status");
        }
        else if (!canAfford)
        {
            badge.AddToClassList("product-price-badge--locked");
        }

        VisualElement currencyIcon = new VisualElement();
        currencyIcon.AddToClassList("product-price-badge__icon");
        if (isPrice && IsVecCurrency(item, state))
        {
            currencyIcon.AddToClassList("product-price-badge__icon--vec");
        }
        else
        {
            currencyIcon.AddToClassList("product-price-badge__icon--coin");
        }

        Label price = new Label(badgeText);
        price.AddToClassList("product-price-badge__text");

        badge.Add(currencyIcon);
        badge.Add(price);
        return badge;
    }

    private string GetPriceBadgeText(SkinCatalogItem item, SkinOwnershipState state, bool owned, bool equipped)
    {
        if (equipped)
        {
            return "EQUIPPED";
        }

        if (owned)
        {
            return "OWNED";
        }

        int price = state != null ? state.Price : item.Price;
        if (price <= 0)
        {
            return "FREE";
        }

        return $"{price:N0} {GetCurrencyLabel(item, state)}";
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

    private bool HasEnoughCurrency(SkinCatalogItem item, SkinOwnershipState state = null)
    {
        if (state != null && state.IsNft)
        {
            return true;
        }

        int price = state != null ? state.Price : item.Price;
        if (price <= 0)
        {
            return true;
        }

        return IsVecCurrency(item, state)
            ? PlayerInventory.VecUnlockedBalance >= price
            : PlayerInventory.Coins >= price;
    }

    private bool IsVecCurrency(SkinCatalogItem item, SkinOwnershipState state = null)
    {
        string currencyType = state != null && !string.IsNullOrEmpty(state.CurrencyType) ? state.CurrencyType : item.CurrencyType;
        return string.Equals(currencyType, "VEC", System.StringComparison.OrdinalIgnoreCase);
    }
}
