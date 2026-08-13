using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InactivityTimer : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float inactivityTimeout = 30f;

    public event Action TimedOut;

    public float ElapsedTime { get; private set; }
    public bool HasTimedOut { get; private set; }

    private void OnEnable()
    {
        ResetTimer();
    }

    private void Update()
    {
        if (WasInputDetected())
        {
            ResetTimer();
            return;
        }

        if (HasTimedOut)
        {
            return;
        }

        ElapsedTime += Time.unscaledDeltaTime;

        if (ElapsedTime >= inactivityTimeout)
        {
            HandleTimeout();
        }
    }

    public void ResetTimer()
    {
        ElapsedTime = 0f;
        HasTimedOut = false;
    }

    private static bool WasInputDetected()
    {
        bool wasMouseClicked = Mouse.current != null &&
                               Mouse.current.leftButton.wasPressedThisFrame;
        bool wasScreenTouched = Touchscreen.current != null &&
                                Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        return wasMouseClicked || wasScreenTouched;
    }

    private void HandleTimeout()
    {
        HasTimedOut = true;
        Debug.Log("[InactivityTimer] Timeout - 대기 화면 복귀 필요", this);
        TimedOut?.Invoke();
    }
}
