#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Callbacks;

public static class IOSWalletBuildPostprocessor
{
    private static readonly string[] WalletQuerySchemes =
    {
        "metamask"
    };

    [PostProcessBuild(100)]
    public static void ConfigureWalletDeepLinks(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(buildPath, "Info.plist");
        XDocument plist = XDocument.Load(plistPath, LoadOptions.PreserveWhitespace);
        XElement rootDictionary = plist.Root?.Element("dict")
            ?? throw new InvalidOperationException("Generated iOS Info.plist has no root dictionary.");

        XElement querySchemes = GetOrCreateArray(rootDictionary, "LSApplicationQueriesSchemes");
        foreach (string scheme in WalletQuerySchemes)
        {
            bool alreadyRegistered = querySchemes.Elements("string")
                .Any(element => string.Equals(element.Value, scheme, StringComparison.OrdinalIgnoreCase));

            if (!alreadyRegistered)
            {
                querySchemes.Add(new XElement("string", scheme));
            }
        }

        plist.Save(plistPath, SaveOptions.DisableFormatting);
    }

    private static XElement GetOrCreateArray(XElement dictionary, string keyName)
    {
        XElement key = dictionary.Elements("key").FirstOrDefault(element => element.Value == keyName);
        if (key != null)
        {
            XElement existingArray = key.ElementsAfterSelf().FirstOrDefault();
            if (existingArray?.Name == "array")
            {
                return existingArray;
            }

            throw new InvalidOperationException($"Info.plist key '{keyName}' is not an array.");
        }

        key = new XElement("key", keyName);
        XElement array = new XElement("array");
        dictionary.Add(key, array);
        return array;
    }
}
#endif
