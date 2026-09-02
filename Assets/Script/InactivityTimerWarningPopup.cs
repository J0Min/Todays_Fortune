using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InactivityTimerWarningPopup : MonoBehaviour
{
    [SerializeField] private GameObject warningPopup;
    [SerializeField] private RawImage countdownImage;
    [SerializeField] private Texture[] countdownTextures;

    private InactivityTimer inactivityTimer;
    private readonly List<RaycastResult> touchHits = new();

    private void OnEnable()
    {
        SfxAudioManager.PrimaryPressed += OnPrimaryPressed;
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
        SfxAudioManager.PrimaryPressed -= OnPrimaryPressed;
        if (inactivityTimer != null)
        {
            inactivityTimer.WarningSecondChanged -= RefreshPopup;
            inactivityTimer.TimerReset -= HidePopup;
        }
    }

    private void OnPrimaryPressed(Vector2 screenPosition)
    {
        if (warningPopup == null || !warningPopup.activeInHierarchy || EventSystem.current == null)
        {
            return;
        }

        var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
        touchHits.Clear();
        EventSystem.current.RaycastAll(pointer, touchHits);
        if (touchHits.Count == 0)
        {
            return;
        }

        Transform hit = touchHits[0].gameObject.transform;
        if (hit.IsChildOf(warningPopup.transform) && hit.GetComponentInParent<Button>() == null)
        {
            SfxAudioManager.Instance?.PlayCountdownPopupPress();
        }
        touchHits.Clear();
    }

    private void RefreshPopup(int remainingSeconds)
    {
        int imageIndex = inactivityTimer.WarningSeconds - remainingSeconds;
        bool shouldShowPopup = imageIndex >= 0 && imageIndex < countdownTextures.Length;
        bool wasPopupVisible = warningPopup.activeSelf;

        warningPopup.SetActive(shouldShowPopup);

        if (shouldShowPopup)
        {
            countdownImage.texture = countdownTextures[imageIndex];
            if (!wasPopupVisible)
            {
                SfxAudioManager.Instance?.PlayPopup();
            }
        }
    }

    private void HidePopup()
    {
        warningPopup.SetActive(false);
    }
}
