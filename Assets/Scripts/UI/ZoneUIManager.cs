using UnityEngine;
using TMPro;

public class ZoneUIManager : MonoBehaviour
{
    [Header("References")]
    public ZoneController zoneController;
    public Transform playerTransform;
    public TextMeshProUGUI timerText;

    void Update()
    {
        if (zoneController == null || playerTransform == null || timerText == null) return;

        float timeRemaining = zoneController.GetTimeRemaining();
        int minutes = Mathf.FloorToInt(timeRemaining / 60F);
        int seconds = Mathf.FloorToInt(timeRemaining - minutes * 60);
        string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);

        bool isShrinking = zoneController.currentState == ZoneController.ZoneState.Shrinking;
        
        if (zoneController.currentState == ZoneController.ZoneState.Waiting)
        {
            timerText.text = "Thu bo sau: " + timeString;
        }
        else if (isShrinking)
        {
            timerText.text = "Đang thu bo: " + timeString;
        }
        else
        {
            timerText.text = "Bo cuối đã đóng!";
            timerText.color = Color.red;
            return;
        }

        bool outsideCurrent = zoneController.IsPositionOutsideZone(playerTransform.position);
        bool outsideNext = zoneController.IsPositionOutsideNextZone(playerTransform.position);

        if (outsideCurrent)
        {
            timerText.color = Color.red;
        }
        else if (!outsideNext)
        {
            timerText.color = Color.white;
        }
        else
        {
            if (isShrinking)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = Color.yellow;
            }
        }
    }
}