#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

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
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementArray querySchemes = GetOrCreateArray(
            plist.root,
            "LSApplicationQueriesSchemes");
        foreach (string scheme in WalletQuerySchemes)
        {
            bool alreadyRegistered = querySchemes.values
                .Any(element => string.Equals(
                    element.AsString(),
                    scheme,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyRegistered)
            {
                querySchemes.AddString(scheme);
            }
        }

        plist.WriteToFile(plistPath);
    }

    private static PlistElementArray GetOrCreateArray(PlistElementDict dictionary, string keyName)
    {
        if (dictionary.values.TryGetValue(keyName, out PlistElement existingElement))
        {
            if (existingElement is PlistElementArray existingArray)
            {
                return existingArray;
            }

            throw new InvalidOperationException($"Info.plist key '{keyName}' is not an array.");
        }

        return dictionary.CreateArray(keyName);
    }
}
#endif
