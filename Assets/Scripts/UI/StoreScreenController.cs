using UnityEngine;
using UnityEngine.UIElements;

public class StoreScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Button backButton;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document == null) return;
        
        root = document.rootVisualElement;

        // Start hidden
        root.AddToClassList("hidden");

        backButton = root.Q<Button>("BackButton");
        backButton.clicked += Hide;
    }

    public void Show()
    {
        root.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        root.AddToClassList("hidden");
    }
}
