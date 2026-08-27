using UnityEngine;
using UnityEngine.UI;

public sealed class InactivityTimerWarningPopup : MonoBehaviour
{
    [SerializeField] private GameObject warningPopup;
    [SerializeField] private RawImage countdownImage;
    [SerializeField] private Texture[] countdownTextures;

    private InactivityTimer inactivityTimer;

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();

        if (inactivityTimer == null)
        {
            return;
        }

        inactivityTimer.WarningSecondChanged += RefreshPopup;
        inactivityTimer.TimerReset += HidePopup;
        RefreshPopup(inactivityTimer.RemainingSeconds);
    }

    private void OnDisable()
    {
        if (inactivityTimer != null)
        {
            inactivityTimer.WarningSecondChanged -= RefreshPopup;
            inactivityTimer.TimerReset -= HidePopup;
        }
    }

    private void RefreshPopup(int remainingSeconds)
    {
        int imageIndex = inactivityTimer.WarningSeconds - remainingSeconds;
        bool shouldShowPopup = imageIndex >= 0 && imageIndex < countdownTextures.Length;

        warningPopup.SetActive(shouldShowPopup);

        if (shouldShowPopup)
        {
            countdownImage.texture = countdownTextures[imageIndex];
        }
    }

    private void HidePopup()
    {
        warningPopup.SetActive(false);
    }
}
