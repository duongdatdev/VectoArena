using System;
using UnityEngine;

[Serializable]
public class SkinCatalogItem
{
    public string Id;
    public string DisplayName;
    public string PrefabKey;
    public int Price;
    public string CurrencyType = "COIN";
    public string OwnershipType = "OFFCHAIN";
    public SkinNftMapping Nft;
    public string PrefabResourcePath;
    public string IconResourcePath;
    public string AnimatorControllerResourcePath;
}

[Serializable]
public class SkinNftMapping
{
    public int ChainId;
    public string ContractAddress;
    public string TokenId;
    public string CollectionKey;
}

public static class SkinCatalog
{
    public static readonly SkinCatalogItem[] Items =
    {
        new SkinCatalogItem
        {
            Id = "Female01",
            DisplayName = "Vecto Hero",
            PrefabKey = "CharacterSkins/Female01/Char_Female01",
            Price = 0,
            CurrencyType = "COIN",
            OwnershipType = "OFFCHAIN",
            PrefabResourcePath = "",
            IconResourcePath = "CharacterSkins/Female01/Icon_Char_Female01"
        },
        new SkinCatalogItem
        {
            Id = "Female02",
            DisplayName = "Nova Runner",
            PrefabKey = "CharacterSkins/Female02/Char_Female02",
            Price = 500,
            CurrencyType = "COIN",
            OwnershipType = "OFFCHAIN",
            PrefabResourcePath = "CharacterSkins/Female02/Char_Female02",
            IconResourcePath = "CharacterSkins/Female02/Icon_Char_Female02"
        },
        new SkinCatalogItem
        {
            Id = "CorposFemale",
            DisplayName = "Corpos Agent",
            PrefabKey = "CharacterSkins/CorposFemale/Char_CorposFemale",
            Price = 800,
            CurrencyType = "COIN",
            OwnershipType = "OFFCHAIN",
            PrefabResourcePath = "CharacterSkins/CorposFemale/Char_CorposFemale",
            IconResourcePath = "CharacterSkins/CorposFemale/Icon_Char_CorposFemale"
        },
        new SkinCatalogItem
        {
            Id = "AssassinFemale",
            DisplayName = "Shadow Assassin",
            PrefabKey = "CharacterSkins/AssassinFemale/Char_AssassinFemale",
            Price = 1200,
            CurrencyType = "COIN",
            OwnershipType = "OFFCHAIN",
            PrefabResourcePath = "CharacterSkins/AssassinFemale/Char_AssassinFemale",
            IconResourcePath = "CharacterSkins/AssassinFemale/Icon_Char_AssassinFemale"
        },
        new SkinCatalogItem
        {
            Id = "CyberBunny",
            DisplayName = "Cyber Bunny",
            PrefabKey = "CharacterSkins/CyberBunny/Char_CyberBunny",
            Price = 35,
            CurrencyType = "VEC",
            OwnershipType = "NFT",
            Nft = new SkinNftMapping
            {
                ChainId = 0,
                ContractAddress = "",
                TokenId = "",
                CollectionKey = "vectoarena-genesis-skins"
            },
            PrefabResourcePath = "CharacterSkins/CyberBunny/Char_CyberBunny",
            IconResourcePath = "CharacterSkins/CyberBunny/Icon_Char_CyberBunny",
            AnimatorControllerResourcePath = "CharacterSkins/CyberBunny/Char_CyberBunny_animator"
        },
        new SkinCatalogItem
        {
            Id = "Iceking",
            DisplayName = "Ice King",
            PrefabKey = "CharacterSkins/Iceking/Char_Iceking",
            Price = 45,
            CurrencyType = "VEC",
            OwnershipType = "NFT",
            Nft = new SkinNftMapping
            {
                ChainId = 0,
                ContractAddress = "",
                TokenId = "",
                CollectionKey = "vectoarena-genesis-skins"
            },
            PrefabResourcePath = "CharacterSkins/Iceking/Char_Iceking",
            IconResourcePath = "CharacterSkins/Iceking/Icon_Char_Iceking"
        },
        new SkinCatalogItem
        {
            Id = "Anubis",
            DisplayName = "Anubis",
            PrefabKey = "CharacterSkins/Anubis/Char_Anubis",
            Price = 55,
            CurrencyType = "VEC",
            OwnershipType = "NFT",
            Nft = new SkinNftMapping
            {
                ChainId = 0,
                ContractAddress = "",
                TokenId = "",
                CollectionKey = "vectoarena-genesis-skins"
            },
            PrefabResourcePath = "CharacterSkins/Anubis/Char_Anubis",
            IconResourcePath = "CharacterSkins/Anubis/Icon_Char_Anubis"
        },
        new SkinCatalogItem
        {
            Id = "GearedApe",
            DisplayName = "Geared Ape",
            PrefabKey = "CharacterSkins/GearedApe/Char_GearedApe",
            Price = 40,
            CurrencyType = "VEC",
            OwnershipType = "NFT",
            Nft = new SkinNftMapping
            {
                ChainId = 0,
                ContractAddress = "",
                TokenId = "",
                CollectionKey = "vectoarena-genesis-skins"
            },
            PrefabResourcePath = "CharacterSkins/GearedApe/Char_GearedApe",
            IconResourcePath = "CharacterSkins/GearedApe/Icon_Char_GearedApe",
            AnimatorControllerResourcePath = "CharacterSkins/GearedApe/Char_GearedApe_animator"
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

    public static RuntimeAnimatorController LoadAnimatorController(SkinCatalogItem item)
    {
        if (item == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(item.AnimatorControllerResourcePath))
        {
            return Resources.Load<RuntimeAnimatorController>(item.AnimatorControllerResourcePath);
        }

        if (!string.IsNullOrEmpty(item.PrefabResourcePath))
        {
            return Resources.Load<RuntimeAnimatorController>(item.PrefabResourcePath + "_animator");
        }

        return null;
    }
}
