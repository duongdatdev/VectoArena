using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;

public class KillFeedManager : MonoBehaviour
{
    private UIDocument document;
    private VisualElement killFeedContainer;
    public VisualTreeAsset killFeedItemTemplate;

    [Tooltip("How long before a kill feed item disappears")]
    public float itemLifetime = 4.0f;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document != null && document.rootVisualElement != null)
        {
            killFeedContainer = document.rootVisualElement.Q<VisualElement>("KillFeedContainer");
        }

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
    }

    private void HandleKillFeed(NetworkManager.KillFeedMessage msg)
    {
        if (killFeedContainer == null || killFeedItemTemplate == null)
            return;

        VisualElement newItem = killFeedItemTemplate.Instantiate();
        newItem.AddToClassList("kill-feed-item");
        
        Label killerLabel = newItem.Q<Label>("KillerName");
        Label victimLabel = newItem.Q<Label>("VictimName");
        Label weaponLabel = newItem.Q<Label>("WeaponName");

        if (killerLabel != null) killerLabel.text = msg.killerName;
        if (victimLabel != null) victimLabel.text = msg.victimName;
        if (weaponLabel != null) weaponLabel.text = msg.weapon;

        killFeedContainer.Add(newItem);

        StartCoroutine(RemoveItemAfterDelay(newItem));
    }

    private IEnumerator RemoveItemAfterDelay(VisualElement item)
    {
        yield return new WaitForSeconds(itemLifetime);
        item.AddToClassList("kill-feed-item--fade-out");
        
        yield return new WaitForSeconds(0.5f); // wait for fade animation
        if (killFeedContainer != null && killFeedContainer.Contains(item))
        {
            killFeedContainer.Remove(item);
        }
    }
}
