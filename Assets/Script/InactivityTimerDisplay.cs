using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class InactivityTimerDisplay : MonoBehaviour
{
    [SerializeField] private string messageFormat = "{0}초 후 초기 화면으로 돌아갑니다.";

    private TMP_Text countdownText;
    private InactivityTimer inactivityTimer;

    private void Awake()
    {
        countdownText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();
        RefreshText();
    }

    private void Update()
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (inactivityTimer == null)
        {
            return;
        }

        int remainingSeconds = Mathf.CeilToInt(inactivityTimer.RemainingTime);
        countdownText.text = string.Format(messageFormat, remainingSeconds);
    }
}
