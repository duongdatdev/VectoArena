using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class KillFeedManager : MonoBehaviour
{
    private const int MaxVisibleItems = 3;
    private const float ReleaseDelaySeconds = 1f;

    [SerializeField] private VisualTreeAsset killFeedItemTemplate;
    [SerializeField, Min(0.1f)] private float itemLifetime = 5f;

    private readonly List<VisualElement> visibleItems = new List<VisualElement>(MaxVisibleItems);
    private UIDocument document;
    private VisualElement killFeedContainer;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        killFeedContainer = document != null
            ? document.rootVisualElement?.Q<VisualElement>("KillFeedContainer")
            : null;

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnKillFeedReceived += HandleKillFeed;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnKillFeedReceived -= HandleKillFeed;
        }

        StopAllCoroutines();
        visibleItems.Clear();
    }

    private void HandleKillFeed(NetworkManager.KillFeedMessage message)
    {
        if (message == null || killFeedContainer == null || killFeedItemTemplate == null)
        {
            return;
        }

        TemplateContainer templateRoot = killFeedItemTemplate.Instantiate();
        VisualElement notification = templateRoot.Q<VisualElement>("KillFeedItem");
        if (notification == null)
        {
            return;
        }

        bool suicide = string.IsNullOrWhiteSpace(message.killerName) ||
                       string.Equals(message.killerName, message.victimName, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(message.weapon, "Zone", StringComparison.OrdinalIgnoreCase);

        SetName(notification.Q<Label>("KillerName"), message.killerName);
        SetName(notification.Q<Label>("VictimName"), message.victimName);
        SetPortrait(notification.Q<VisualElement>("KillerPfp"), ResolvePlayerPortrait(message.killerName));
        SetPortrait(notification.Q<VisualElement>("VictimPfp"), ResolvePlayerPortrait(message.victimName));
        notification.EnableInClassList("death-notification--suicide", suicide);

        if (visibleItems.Count >= MaxVisibleItems)
        {
            VisualElement oldest = visibleItems[0];
            visibleItems.RemoveAt(0);
            HideAndRelease(oldest);
        }

        killFeedContainer.Add(templateRoot);
        visibleItems.Add(notification);

        notification.schedule.Execute(() => notification.RemoveFromClassList("death-notification--hidden"))
            .StartingIn(10);
        StartCoroutine(RemoveItemAfterDelay(notification));
    }

    private IEnumerator RemoveItemAfterDelay(VisualElement notification)
    {
        yield return new WaitForSeconds(itemLifetime);

        if (!visibleItems.Remove(notification))
        {
            yield break;
        }

        HideAndRelease(notification);
    }

    private void HideAndRelease(VisualElement notification)
    {
        if (notification == null || notification.panel == null)
        {
            return;
        }

        notification.RemoveFromClassList("death-notification--hidden");
        notification.AddToClassList("death-notification--hidden-end");
        notification.schedule.Execute(() =>
        {
            VisualElement templateRoot = notification.parent;
            if (templateRoot != null)
            {
                templateRoot.RemoveFromHierarchy();
            }
        }).StartingIn(Mathf.CeilToInt(ReleaseDelaySeconds * 1000f));
    }

    private static void SetName(Label label, string value)
    {
        if (label != null)
        {
            label.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.ToUpperInvariant();
        }
    }

    private static void SetPortrait(VisualElement portraitElement, Sprite portrait)
    {
        if (portraitElement != null && portrait != null)
        {
            portraitElement.style.backgroundImage = new StyleBackground(portrait);
        }
    }

    private static Sprite ResolvePlayerPortrait(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || NetworkManager.Instance == null)
        {
            return null;
        }

        GameObject playerObject = NetworkManager.Instance.FindPlayerObjectByUsername(playerName);
        NetworkPlayerSync playerSync = playerObject != null ? playerObject.GetComponent<NetworkPlayerSync>() : null;
        string skinId = playerSync?.GetState()?.skinId;
        SkinCatalogItem skin = SkinCatalog.GetById(skinId);
        return skin != null ? Resources.Load<Sprite>(skin.IconResourcePath) : null;
    }
}
