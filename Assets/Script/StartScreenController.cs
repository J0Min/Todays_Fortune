using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class StartScreenController : MonoBehaviour, IPointerClickHandler
{
    private const string TouchMessage = "Start Screen Touched";
    private static bool hasPendingEndingReturn;
    private static bool pendingReturnNeedsInputRelease;

    [Header("Title Exit")]
    [SerializeField] private TitleExitAnimation titleExitAnimation;

    [Header("Scene Start Video")]
    [SerializeField] private SceneVideoController sceneVideoController;
    [SerializeField] private CanvasGroup canvasToFade;
    [SerializeField, Min(0f)] private float canvasFadeDuration = 0.25f;
    [SerializeField] private RawImage imageToShowWhenVideoFinishes;

    [Header("Events")]
    [SerializeField] private UnityEvent onTitleExitFinished;

    private bool hasStartedTransition;
    private bool isReturnInputGuardActive;
    private bool hasLoggedWaitingForRelease;
    private bool hasObservedReturnInputRelease;
    private Coroutine canvasFadeRoutine;

    private void OnEnable()
    {
        if (sceneVideoController != null)
        {
            sceneVideoController.IntroVideoFinished += ShowVideoEndImage;
        }
    }

    private void OnDisable()
    {
        if (sceneVideoController != null)
        {
            sceneVideoController.IntroVideoFinished -= ShowVideoEndImage;
        }
    }

    private void Awake()
    {
        Buttons.ResetPauseState();
        ApplyPendingEndingReturnGuard();
        if (canvasToFade != null)
        {
            canvasToFade.alpha = 1f;
        }
        EnsureEventSystem();
    }

    private void Update()
    {
        if (isReturnInputGuardActive)
        {
            UpdateReturnInputGuard();
            return;
        }

        if (WasPrimaryInputPressedThisFrame())
        {
            Debug.Log("[StartScreen] New start input detected", this);
            BeginTransition();
        }
    }

    private void Start()
    {
        AmbientAudioManager.Instance?.RestoreBaseVolumeImmediately();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isReturnInputGuardActive)
        {
            return;
        }

        Debug.Log("[StartScreen] New start input detected", this);
        BeginTransition();
    }

    public static void PrepareForEndingReturn(bool waitForInputRelease)
    {
        hasPendingEndingReturn = true;
        pendingReturnNeedsInputRelease = waitForInputRelease;
    }

    private void ApplyPendingEndingReturnGuard()
    {
        if (!hasPendingEndingReturn)
        {
            return;
        }

        hasPendingEndingReturn = false;
        isReturnInputGuardActive = pendingReturnNeedsInputRelease;
        pendingReturnNeedsInputRelease = false;

        if (isReturnInputGuardActive)
        {
            Debug.Log("[StartScreen] Return input guard enabled", this);
            Debug.Log("[StartScreen] Waiting for previous input release", this);
            hasLoggedWaitingForRelease = true;
        }
        else
        {
            Debug.Log("[StartScreen] Entered from auto return - no held input", this);
        }
    }

    private void UpdateReturnInputGuard()
    {
        if (!hasObservedReturnInputRelease)
        {
            if (IsPrimaryInputHeld())
            {
                if (!hasLoggedWaitingForRelease)
                {
                    Debug.Log("[StartScreen] Waiting for previous input release", this);
                    hasLoggedWaitingForRelease = true;
                }
                return;
            }

            hasObservedReturnInputRelease = true;
            Debug.Log("[StartScreen] Previous input released", this);
            return;
        }

        isReturnInputGuardActive = false;
        Debug.Log("[StartScreen] Start input enabled", this);
    }

    private void BeginTransition()
    {
        if (hasStartedTransition)
        {
            return;
        }

        if (titleExitAnimation == null)
        {
            Debug.LogError("StartScreenController needs a title exit animation.", this);
            return;
        }

        hasStartedTransition = true;
        AmbientAudioManager.Instance?.FadeToContentVolume(
            titleExitAnimation.TitleFullyHiddenDuration);
        Debug.Log(TouchMessage);
        titleExitAnimation.Play(HandleTitleExitFinished);
    }

    private void HandleTitleExitFinished()
    {
        onTitleExitFinished?.Invoke();

        if (sceneVideoController != null && sceneVideoController.IsWaitingForFirstFrame)
            StartCoroutine(WaitForFirstVideoFrame());
        else
            StartCanvasFade();
    }

    private void ShowVideoEndImage()
    {
        if (imageToShowWhenVideoFinishes == null)
        {
            Debug.LogWarning(
                "StartScreenController needs an image to show when the video finishes.",
                this);
            return;
        }

        imageToShowWhenVideoFinishes.gameObject.SetActive(true);
        imageToShowWhenVideoFinishes.enabled = true;
    }

    private IEnumerator WaitForFirstVideoFrame()
    {
        while (sceneVideoController != null && sceneVideoController.IsWaitingForFirstFrame)
            yield return null;

        StartCanvasFade();
    }

    private void StartCanvasFade()
    {
        if (canvasToFade == null)
        {
            return;
        }

        if (canvasFadeDuration <= 0f)
        {
            canvasToFade.alpha = 0f;
            return;
        }

        if (canvasFadeRoutine != null)
        {
            StopCoroutine(canvasFadeRoutine);
        }

        canvasFadeRoutine = StartCoroutine(FadeOutCanvas());
    }

    private IEnumerator FadeOutCanvas()
    {
        float startedAt = Time.unscaledTime;
        float elapsed = 0f;

        while (elapsed < canvasFadeDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / canvasFadeDuration);
            canvasToFade.alpha = 1f - Mathf.SmoothStep(0f, 1f, normalizedTime);
            yield return null;
        }

        canvasToFade.alpha = 0f;
        canvasFadeRoutine = null;
    }

    private static bool WasPrimaryInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        bool pointerPressed = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        bool touchPressed = Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return pointerPressed || touchPressed;
#else
        return Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
#endif
    }

    private static bool IsPrimaryInputHeld()
    {
#if ENABLE_INPUT_SYSTEM
        bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool touchHeld = Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed;
        return mouseHeld || touchHeld;
#else
        bool touchHeld = Input.touchCount > 0 &&
            Input.GetTouch(0).phase != TouchPhase.Ended &&
            Input.GetTouch(0).phase != TouchPhase.Canceled;
        return Input.GetMouseButton(0) || touchHeld;
#endif
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
#else
        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
#endif
    }
}
