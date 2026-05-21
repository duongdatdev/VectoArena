using UnityEngine;
using UnityEngine.UIElements;

public class CollectionScreenController
{
    private readonly VisualElement root;
    private readonly VisualElement skinList;
    private readonly Label coinsAmount;
    private readonly Label statusLabel;
    private readonly Button backButton;

    public CollectionScreenController(VisualElement parentRoot)
    {
        root = parentRoot.Q<VisualElement>("CollectionScreen");
        skinList = parentRoot.Q<VisualElement>("CollectionSkinList");
        coinsAmount = parentRoot.Q<Label>("CollectionCoinsAmount");
        statusLabel = parentRoot.Q<Label>("CollectionStatus");
        backButton = parentRoot.Q<Button>("CollectionBackButton");

        if (backButton != null)
        {
            backButton.clicked += Hide;
        }

        PlayerInventory.Changed += Refresh;
        Hide();
        Refresh();
    }

    public void Show()
    {
        _ = PlayerInventory.LoadFromServer();
        Refresh();
        root?.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        root?.AddToClassList("hidden");
    }

    public void Dispose()
    {
        PlayerInventory.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (skinList == null)
        {
            return;
        }

        PlayerInventory.EnsureInitialized();
        if (coinsAmount != null)
        {
            coinsAmount.text = $"{PlayerInventory.Coins:N0} COINS  |  {PlayerInventory.VecUnlockedBalance:N0} VEC";
        }
        skinList.Clear();

        foreach (SkinCatalogItem item in SkinCatalog.Items)
        {
            VisualElement card = CreateCard(item);
            skinList.Add(card);
        }
    }

    private VisualElement CreateCard(SkinCatalogItem item)
    {
        SkinOwnershipState stateInfo = PlayerInventory.GetSkinState(item);
        bool owned = stateInfo != null && stateInfo.Owned;
        bool canEquip = stateInfo != null && stateInfo.CanEquip;
        bool isNft = stateInfo != null && stateInfo.IsNft;
        bool equipped = (stateInfo != null && stateInfo.Equipped) || PlayerInventory.EquippedSkinId == item.Id;

        VisualElement card = new VisualElement();
        card.AddToClassList("collection-card");

        VisualElement icon = new VisualElement();
        icon.AddToClassList("collection-card-icon");
        Texture2D texture = SkinCatalog.LoadIcon(item);
        if (texture != null)
        {
            icon.style.backgroundImage = new StyleBackground(Background.FromTexture2D(texture));
        }

        Label name = new Label(item.DisplayName.ToUpper());
        name.AddToClassList("collection-card-name");

        Label state = new Label(GetStateText(item, stateInfo, owned, equipped, isNft));
        state.AddToClassList("collection-card-state");

        Button action = new Button();
        action.AddToClassList("button-long");
        action.AddToClassList("button-long--yellow");
        action.AddToClassList("collection-card-button");
        action.text = GetButtonText(owned, canEquip, equipped, isNft);
        action.SetEnabled(!equipped && ((owned && canEquip) || !owned));
        action.clicked += async () =>
        {
            if (isNft && !owned)
            {
                action.SetEnabled(false);
                bool nftSuccess = await PlayerInventory.TryBuyNftSkinAsync(item, status =>
                {
                    action.text = status;
                    if (statusLabel != null)
                    {
                        statusLabel.text = status;
                    }
                });

                if (statusLabel != null)
                {
                    if (nftSuccess)
                    {
                        statusLabel.text = $"{item.DisplayName.ToUpper()} READY";
                    }
                    else if (!string.IsNullOrEmpty(PlayerInventory.LastOperationError))
                    {
                        statusLabel.text = PlayerInventory.LastOperationError;
                    }
                    else
                    {
                        statusLabel.text = "NFT PURCHASE FAILED";
                    }
                }

                Refresh();
                return;
            }

            if (!owned)
            {
                action.SetEnabled(false);
                bool buySuccess = await PlayerInventory.TryBuySkinAsync(item);
                if (statusLabel != null)
                {
                    if (buySuccess)
                    {
                        statusLabel.text = $"{item.DisplayName.ToUpper()} READY";
                    }
                    else if (!string.IsNullOrEmpty(PlayerInventory.LastOperationError))
                    {
                        statusLabel.text = PlayerInventory.LastOperationError;
                    }
                    else
                    {
                        statusLabel.text = $"NOT ENOUGH {GetCurrencyLabel(item, stateInfo)}";
                    }
                }

                Refresh();
                return;
            }

            action.SetEnabled(false);
            bool success = await PlayerInventory.EquipSkinAsync(item.Id);
            if (statusLabel != null)
            {
                statusLabel.text = success ? $"EQUIPPED {item.DisplayName.ToUpper()}" : "UNABLE TO EQUIP SKIN";
            }
            Refresh();
        };

        card.Add(icon);
        card.Add(name);
        card.Add(state);
        card.Add(action);
        return card;
    }

    private string GetStateText(SkinCatalogItem item, SkinOwnershipState state, bool owned, bool equipped, bool isNft)
    {
        if (equipped)
        {
            return "EQUIPPED";
        }

        if (owned)
        {
            return "OWNED";
        }

        if (isNft)
        {
            return "NFT REQUIRED";
        }

        int price = state != null ? state.Price : item.Price;
        return $"LOCKED - {price:N0} {GetCurrencyLabel(item, state)}";
    }

    private string GetButtonText(bool owned, bool canEquip, bool equipped, bool isNft)
    {
        if (equipped)
        {
            return "EQUIPPED";
        }

        if (owned && canEquip)
        {
            return "EQUIP";
        }

        return isNft ? "BUY NFT" : "BUY";
    }

    private string GetCurrencyLabel(SkinCatalogItem item, SkinOwnershipState state = null)
    {
        string currencyType = state != null && !string.IsNullOrEmpty(state.CurrencyType) ? state.CurrencyType : item.CurrencyType;
        return string.Equals(currencyType, "VEC", System.StringComparison.OrdinalIgnoreCase) ? "VEC" : "COINS";
    }
}
