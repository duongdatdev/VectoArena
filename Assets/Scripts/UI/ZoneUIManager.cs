using TMPro;
using UnityEngine;

public class ZoneUIManager : MonoBehaviour
{
    // Blast Royale's HUD is authored against a 2340x1080 width-matched panel.
    // This project's UGUI canvas is 1920x1080, so positional animation values
    // must be converted while the authored element sizes stay unchanged.
    private const float BlastReferenceScale = 1920f / 2340f;
    private const float NotificationDuration = 2f;
    private const float NotificationSlideDistance = 178.7f * BlastReferenceScale;
    private const float NotificationEnterDuration = 0.26666668f;
    private const float NotificationExitStart = 1.75f;
    private const float NotificationExitDuration = 0.25f;
    private const float NotificationPopStart = 0.21666667f;
    private const float NotificationPopPeakStart = 0.3f;
    private const float NotificationPopPeakEnd = 0.38333333f;
    private const float NotificationPopEnd = 0.46666667f;
    private const float NotificationPopScale = 1.15f;
    private const float CounterPulseDuration = 1f;
    private const float CounterPulseRestScale = 2f / 3f;
    private const float CounterPulsePeakScale = 1f;

    [Header("References")]
    public ZoneController zoneController;
    public Transform playerTransform;
    public TextMeshProUGUI timerText;

    [Header("Blast Royale Timing")]
    [SerializeField, Min(0f)] private float warningTime = 10f;

    [SerializeField] private RectTransform counterRoot;
    [SerializeField] private RectTransform counterPulse;
    [SerializeField] private RectTransform statusRoot;
    [SerializeField] private TextMeshProUGUI statusText;

    private CanvasGroup statusCanvasGroup;
    private Vector2 statusBasePosition;
    private Vector3 statusBaseScale = Vector3.one;
    private float notificationElapsed = NotificationDuration;
    private float counterPulseElapsed = CounterPulseDuration;
    private int lastDisplayedSeconds = -1;
    private ZoneController.ZoneState previousState;
    private bool hasPreviousState;
    private bool warningNotified;

    private void Start()
    {
        ResolveHudReferences();
        SetCounterVisible(false);
        HideNotification();
    }

    private void Update()
    {
        if (zoneController == null)
        {
            zoneController = FindAnyObjectByType<ZoneController>();
        }

        ResolveHudReferences();

        if (zoneController == null || timerText == null || statusText == null)
        {
            SetCounterVisible(false);
            HideNotification();
            return;
        }

        ZoneController.ZoneState state = zoneController.currentState;
        float timeRemaining = Mathf.Max(0f, zoneController.GetTimeRemaining());

        if (state == ZoneController.ZoneState.MatchEnded)
        {
            SetCounterVisible(false);
            HideNotification();
            hasPreviousState = true;
            previousState = state;
            return;
        }

        bool stateChanged = !hasPreviousState || previousState != state;

        if (state == ZoneController.ZoneState.Waiting)
        {
            if (stateChanged)
            {
                warningNotified = false;
            }

            float activeWarningTime = Mathf.Min(warningTime, zoneController.waitTime);
            bool warningActive = timeRemaining <= activeWarningTime;
            SetCounterVisible(warningActive);

            if (warningActive)
            {
                UpdateCounter(timeRemaining);

                if (!warningNotified)
                {
                    warningNotified = true;
                    ShowNotification("GO TO THE SAFE AREA");
                }
            }

            ResetCounterPulse();
        }
        else
        {
            SetCounterVisible(true);
            bool counterChanged = UpdateCounter(timeRemaining);

            if (stateChanged)
            {
                ShowNotification("THE AREA IS SHRINKING");
            }

            if (stateChanged || counterChanged)
            {
                StartCounterPulse();
            }

            UpdateCounterPulse();
        }

        UpdateNotificationAnimation();

        hasPreviousState = true;
        previousState = state;
    }

    private void ResolveHudReferences()
    {
        if (counterRoot == null)
        {
            counterRoot = transform.Find("ZoneTimerCounter") as RectTransform;
        }

        if (counterPulse == null && counterRoot != null)
        {
            counterPulse = counterRoot.Find("PingBG") as RectTransform;
        }

        if (statusRoot == null)
        {
            statusRoot = transform.Find("ZoneStatusNotification") as RectTransform;
        }

        if (statusText == null && statusRoot != null)
        {
            statusText = statusRoot.Find("NotificationText")?.GetComponent<TextMeshProUGUI>();
        }

        if (statusCanvasGroup == null && statusRoot != null)
        {
            statusCanvasGroup = statusRoot.GetComponent<CanvasGroup>();
            if (statusCanvasGroup == null)
            {
                statusCanvasGroup = statusRoot.gameObject.AddComponent<CanvasGroup>();
            }

            statusBasePosition = statusRoot.anchoredPosition;
            statusBaseScale = statusRoot.localScale;
        }
    }

    private bool UpdateCounter(float timeRemaining)
    {
        int secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
        if (secondsRemaining == lastDisplayedSeconds)
        {
            return false;
        }

        lastDisplayedSeconds = secondsRemaining;
        timerText.text = secondsRemaining.ToString();
        return true;
    }

    private void StartCounterPulse()
    {
        counterPulseElapsed = 0f;
        SetPulseScale(CounterPulseRestScale);
    }

    private void UpdateCounterPulse()
    {
        if (counterPulseElapsed >= CounterPulseDuration)
        {
            SetPulseScale(CounterPulseRestScale);
            return;
        }

        counterPulseElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(counterPulseElapsed / CounterPulseDuration);
        float pulse = Mathf.Sin(progress * Mathf.PI);
        SetPulseScale(Mathf.Lerp(CounterPulseRestScale, CounterPulsePeakScale, pulse));
    }

    private void ResetCounterPulse()
    {
        counterPulseElapsed = CounterPulseDuration;
        SetPulseScale(CounterPulseRestScale);
    }

    private void ShowNotification(string message)
    {
        if (statusRoot == null || statusText == null || statusCanvasGroup == null)
        {
            return;
        }

        statusText.text = message;
        notificationElapsed = 0f;
        statusRoot.anchoredPosition = statusBasePosition + Vector2.left * NotificationSlideDistance;
        statusRoot.localScale = statusBaseScale;
        statusCanvasGroup.alpha = 0f;
        statusRoot.gameObject.SetActive(true);
    }

    private void UpdateNotificationAnimation()
    {
        if (statusRoot == null || statusCanvasGroup == null || !statusRoot.gameObject.activeSelf)
        {
            return;
        }

        notificationElapsed += Time.unscaledDeltaTime;
        float horizontalOffset;
        float opacity;

        if (notificationElapsed < NotificationEnterDuration)
        {
            float enterProgress = Mathf.SmoothStep(0f, 1f, notificationElapsed / NotificationEnterDuration);
            horizontalOffset = Mathf.Lerp(-NotificationSlideDistance, 0f, enterProgress);
            opacity = enterProgress;
        }
        else if (notificationElapsed < NotificationExitStart)
        {
            horizontalOffset = 0f;
            opacity = 1f;
        }
        else
        {
            float exitProgress = Mathf.SmoothStep(
                0f,
                1f,
                (notificationElapsed - NotificationExitStart) / NotificationExitDuration);
            horizontalOffset = Mathf.Lerp(0f, NotificationSlideDistance, exitProgress);
            opacity = 1f - exitProgress;
        }

        statusRoot.anchoredPosition = statusBasePosition + Vector2.right * horizontalOffset;
        statusRoot.localScale = statusBaseScale * GetNotificationScale(notificationElapsed);
        statusCanvasGroup.alpha = opacity;

        if (notificationElapsed >= NotificationDuration)
        {
            HideNotification();
        }
    }

    private static float GetNotificationScale(float elapsed)
    {
        if (elapsed < NotificationPopStart || elapsed >= NotificationPopEnd)
        {
            return 1f;
        }

        if (elapsed < NotificationPopPeakStart)
        {
            float popIn = Mathf.SmoothStep(
                0f,
                1f,
                (elapsed - NotificationPopStart) / (NotificationPopPeakStart - NotificationPopStart));
            return Mathf.Lerp(1f, NotificationPopScale, popIn);
        }

        if (elapsed < NotificationPopPeakEnd)
        {
            return NotificationPopScale;
        }

        float popOut = Mathf.SmoothStep(
            0f,
            1f,
            (elapsed - NotificationPopPeakEnd) / (NotificationPopEnd - NotificationPopPeakEnd));
        return Mathf.Lerp(NotificationPopScale, 1f, popOut);
    }

    private void HideNotification()
    {
        notificationElapsed = NotificationDuration;

        if (statusRoot != null)
        {
            statusRoot.anchoredPosition = statusBasePosition;
            statusRoot.localScale = statusBaseScale;
            statusRoot.gameObject.SetActive(false);
        }

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = 0f;
        }
    }

    private void SetCounterVisible(bool visible)
    {
        if (counterRoot != null)
        {
            counterRoot.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            ResetCounterPulse();
            lastDisplayedSeconds = -1;
            if (timerText != null)
            {
                timerText.text = string.Empty;
            }
        }
    }

    private void SetPulseScale(float scale)
    {
        if (counterPulse != null)
        {
            counterPulse.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
