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
        bool owned = PlayerInventory.IsSkinOwned(item.Id);
        bool equipped = PlayerInventory.EquippedSkinId == item.Id;

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
        buyButton.text = owned ? equipped ? "EQUIPPED" : "EQUIP" : $"{item.Price:N0} {GetCurrencyLabel(item)}";
        buyButton.clicked += async () =>
        {
            buyButton.SetEnabled(false);
            bool success = await PlayerInventory.TryBuySkinAsync(item);
            if (storeStatus != null)
            {
                storeStatus.text = success ? $"{item.DisplayName.ToUpper()} READY" : $"NOT ENOUGH {GetCurrencyLabel(item)}";
            }
            RefreshProducts();
        };

        card.Add(icon);
        card.Add(name);
        card.Add(buyButton);
        return card;
    }

    private string GetCurrencyLabel(SkinCatalogItem item)
    {
        return string.Equals(item.CurrencyType, "VEC", System.StringComparison.OrdinalIgnoreCase) ? "VEC" : "COINS";
    }
}
