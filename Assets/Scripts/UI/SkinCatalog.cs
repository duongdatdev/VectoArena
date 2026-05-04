using System;
using UnityEngine;

[Serializable]
public class SkinCatalogItem
{
    public string Id;
    public string DisplayName;
    public int Price;
    public string PrefabResourcePath;
    public string IconResourcePath;
}

public static class SkinCatalog
{
    public static readonly SkinCatalogItem[] Items =
    {
        new SkinCatalogItem
        {
            Id = "Female01",
            DisplayName = "Vecto Hero",
            Price = 0,
            PrefabResourcePath = "",
            IconResourcePath = "CharacterSkins/Female01/Icon_Char_Female01"
        },
        new SkinCatalogItem
        {
            Id = "Female02",
            DisplayName = "Nova Runner",
            Price = 500,
            PrefabResourcePath = "CharacterSkins/Female02/Char_Female02",
            IconResourcePath = "CharacterSkins/Female02/Icon_Char_Female02"
        },
        new SkinCatalogItem
        {
            Id = "CorposFemale",
            DisplayName = "Corpos Agent",
            Price = 800,
            PrefabResourcePath = "CharacterSkins/CorposFemale/Char_CorposFemale",
            IconResourcePath = "CharacterSkins/CorposFemale/Icon_Char_CorposFemale"
        },
        new SkinCatalogItem
        {
            Id = "AssassinFemale",
            DisplayName = "Shadow Assassin",
            Price = 1200,
            PrefabResourcePath = "CharacterSkins/AssassinFemale/Char_AssassinFemale",
            IconResourcePath = "CharacterSkins/AssassinFemale/Icon_Char_AssassinFemale"
        }
    };

    public static SkinCatalogItem GetById(string id)
    {
        foreach (SkinCatalogItem item in Items)
        {
            if (item.Id == id)
            {
                return item;
            }
        }

        return Items[0];
    }

    public static Texture2D LoadIcon(SkinCatalogItem item)
    {
        return Resources.Load<Texture2D>(item.IconResourcePath);
    }
}
