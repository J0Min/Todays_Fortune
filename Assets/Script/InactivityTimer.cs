using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class InactivityTimer : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float inactivityTimeout = 30f;
    [SerializeField, Min(1)] private int warningSeconds = 5;
    [SerializeField] private string[] inactiveSceneNames = { "StartScene" };

    public event Action TimedOut;
    public event Action<int> WarningSecondChanged;
    public event Action TimerReset;

    public float ElapsedTime;
    public bool HasTimedOut { get; private set; }
    public bool IsPaused => pauseSources.Count > 0;
    public float RemainingTime => Mathf.Max(0f, inactivityTimeout - ElapsedTime);
    public int RemainingSeconds => Mathf.CeilToInt(RemainingTime);
    public int WarningSeconds => warningSeconds;

    private readonly HashSet<object> pauseSources = new();
    private int lastPublishedRemainingSecond = -1;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ResetTimer();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Update()
    {
        if (IsInactiveScene() || IsPaused)
        {
            return;
        }

        if (WasInputDetected())
        {
            ResetTimer();
            return;
        }

        if (HasTimedOut)
        {
            return;
        }

        ElapsedTime += Time.deltaTime;
        PublishWarningSecond();

        if (ElapsedTime >= inactivityTimeout)
        {
            HandleTimeout();
        }
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        ResetTimer();
    }

    public void ResetTimer()
    {
        ElapsedTime = 0f;
        HasTimedOut = false;
        lastPublishedRemainingSecond = -1;
        TimerReset?.Invoke();
    }

    public void Pause(object source)
    {
        pauseSources.Add(source);
    }

    public void Resume(object source)
    {
        pauseSources.Remove(source);
    }

    private static bool WasInputDetected()
    {
        bool wasMouseClicked = Mouse.current != null &&
                               Mouse.current.leftButton.isPressed;
        bool wasScreenTouched = Touchscreen.current != null &&
                                Touchscreen.current.primaryTouch.press.isPressed;

        return wasMouseClicked || wasScreenTouched;
    }

    private bool IsInactiveScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        foreach (string sceneName in inactiveSceneNames)
        {
            if (sceneName == activeSceneName)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleTimeout()
    {
        HasTimedOut = true;
        Debug.Log("[InactivityTimer] Timeout - 대기 화면 복귀 필요", this);
        TimedOut?.Invoke();
    }

    private void PublishWarningSecond()
    {
        int remainingSeconds = RemainingSeconds;

        if (remainingSeconds > warningSeconds)
        {
            lastPublishedRemainingSecond = -1;
            return;
        }

        if (remainingSeconds == lastPublishedRemainingSecond)
        {
            return;
        }

        lastPublishedRemainingSecond = remainingSeconds;
        WarningSecondChanged?.Invoke(remainingSeconds);
    }
}
