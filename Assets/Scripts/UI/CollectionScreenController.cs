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
        bool owned = PlayerInventory.IsSkinOwned(item.Id);
        bool equipped = PlayerInventory.EquippedSkinId == item.Id;

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

        Label state = new Label(owned ? equipped ? "EQUIPPED" : "OWNED" : $"LOCKED - {item.Price:N0} {GetCurrencyLabel(item)}");
        state.AddToClassList("collection-card-state");

        Button action = new Button();
        action.AddToClassList("button-long");
        action.AddToClassList("button-long--yellow");
        action.AddToClassList("collection-card-button");
        action.text = owned ? equipped ? "SELECTED" : "EQUIP" : "BUY IN SHOP";
        action.SetEnabled(owned && !equipped);
        action.clicked += async () =>
        {
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

    private string GetCurrencyLabel(SkinCatalogItem item)
    {
        return string.Equals(item.CurrencyType, "VEC", System.StringComparison.OrdinalIgnoreCase) ? "VEC" : "COINS";
    }
}
