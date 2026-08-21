using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class StartScreenController : MonoBehaviour, IPointerClickHandler
{
    private const string TouchMessage = "Start Screen Touched";
    private const string TransitionFinishedMessage = "Start Transition Finished";
    private static bool hasPendingEndingReturn;
    private static bool pendingReturnNeedsInputRelease;
#if UNITY_EDITOR
    private const string IntroShortVideoAssetPath = "Assets/Video/intro3.mp4";
#endif

    [Header("Title Exit")]
    [SerializeField] private TitleExitAnimation titleExitAnimation;

    [Header("Intro Short Video")]
    [SerializeField] private VideoClip introShortVideo;
    [Min(0.01f)]
    [SerializeField] private float introCrossfadeDuration = 0.25f;
    [SerializeField, Min(1f)] private float introPrepareTimeoutSeconds = 15f;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private bool hasStartedTransition;
    private bool hasReportedMissingIntroShortVideo;
    private VideoPlayer introVideoPlayer;
    private RawImage introVideoImage;
    private bool isTitleExitFinished;
    private bool isIntroVideoPrepared;
    private bool isReturnInputGuardActive;
    private bool hasLoggedWaitingForRelease;
    private bool hasObservedReturnInputRelease;
    private Coroutine introPrepareTimeoutRoutine;

    private void Awake()
    {
        Buttons.ResetPauseState();
        ApplyPendingEndingReturnGuard();
#if UNITY_EDITOR
        ResolveIntroShortVideoReference();
#endif
        EnsureEventSystem();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveIntroShortVideoReference();
    }

    private void ResolveIntroShortVideoReference()
    {
        if (introShortVideo != null)
        {
            return;
        }

        introShortVideo = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(IntroShortVideoAssetPath);
        if (introShortVideo != null)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

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

    private void OnDestroy()
    {
        StopIntroPrepareTimeout();

        if (introVideoPlayer == null)
        {
            return;
        }

        introVideoPlayer.prepareCompleted -= OnIntroVideoPrepared;
        introVideoPlayer.frameReady -= OnIntroVideoFrameReady;
        introVideoPlayer.loopPointReached -= OnIntroVideoFinished;
        introVideoPlayer.errorReceived -= OnIntroVideoError;
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

        if (string.IsNullOrWhiteSpace(sceneNameToOpen) ||
            !Application.CanStreamedLevelBeLoaded(sceneNameToOpen))
        {
            Debug.LogError("StartScreenController needs a scene registered in Build Settings.", this);
            return;
        }

        if (introShortVideo == null)
        {
            if (!hasReportedMissingIntroShortVideo)
            {
                hasReportedMissingIntroShortVideo = true;
                Debug.LogError("StartScreenController needs an intro short video.", this);
            }
            return;
        }

        hasStartedTransition = true;
        Debug.Log(TouchMessage);

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("StartScreenController needs a child Canvas.", this);
            hasStartedTransition = false;
            return;
        }

        CreateIntroVideoPlayer(canvas.transform);
        introPrepareTimeoutRoutine = StartCoroutine(WaitForIntroVideoPrepare());
        introVideoPlayer.Prepare();
        titleExitAnimation.Play(OnTitleExitFinished);
    }

    private void OnTitleExitFinished()
    {
        isTitleExitFinished = true;
        PlayIntroVideoWhenReady();
    }

    private void CreateIntroVideoPlayer(Transform canvasTransform)
    {
        GameObject videoObject = new GameObject(
            "Intro Short Video",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter),
            typeof(VideoPlayer));
        videoObject.transform.SetParent(canvasTransform, false);
        videoObject.transform.SetAsLastSibling();

        RectTransform rect = videoObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        AspectRatioFitter fitter = videoObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = introShortVideo.height > 0
            ? (float)introShortVideo.width / introShortVideo.height
            : 16f / 9f;

        introVideoImage = videoObject.GetComponent<RawImage>();
        introVideoImage.color = new Color(1f, 1f, 1f, 0f);
        introVideoImage.raycastTarget = true;
        introVideoImage.enabled = false;

        introVideoPlayer = videoObject.GetComponent<VideoPlayer>();
        introVideoPlayer.playOnAwake = false;
        introVideoPlayer.isLooping = false;
        introVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        introVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        introVideoPlayer.source = VideoSource.VideoClip;
        introVideoPlayer.clip = introShortVideo;
        introVideoPlayer.sendFrameReadyEvents = true;
        introVideoPlayer.prepareCompleted += OnIntroVideoPrepared;
        introVideoPlayer.frameReady += OnIntroVideoFrameReady;
        introVideoPlayer.loopPointReached += OnIntroVideoFinished;
        introVideoPlayer.errorReceived += OnIntroVideoError;
    }

    private void OnIntroVideoPrepared(VideoPlayer player)
    {
        if (player != introVideoPlayer)
        {
            return;
        }

        StopIntroPrepareTimeout();
        isIntroVideoPrepared = true;
        PlayIntroVideoWhenReady();
    }

    private void PlayIntroVideoWhenReady()
    {
        if (isTitleExitFinished && isIntroVideoPrepared)
        {
            introVideoPlayer.Play();
        }
    }

    private void OnIntroVideoFrameReady(VideoPlayer player, long frameIndex)
    {
        if (introVideoImage.enabled || player.texture == null)
        {
            return;
        }

        introVideoImage.texture = player.texture;
        introVideoImage.enabled = true;
        StartCoroutine(FadeInIntroVideo());
    }

    private IEnumerator FadeInIntroVideo()
    {
        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        while (elapsed < introCrossfadeDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / introCrossfadeDuration);
            float alpha = Mathf.SmoothStep(0f, 1f, normalizedTime);
            introVideoImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        introVideoImage.color = Color.white;
    }

    private void OnIntroVideoFinished(VideoPlayer player)
    {
        if (player != introVideoPlayer)
        {
            return;
        }

        Debug.Log(TransitionFinishedMessage);
        OpenTargetScene();
    }

    private void OnIntroVideoError(VideoPlayer player, string message)
    {
        if (player != introVideoPlayer)
        {
            return;
        }

        Debug.LogError("Intro short video failed: " + message, this);
        OpenTargetScene();
    }

    private IEnumerator WaitForIntroVideoPrepare()
    {
        yield return new WaitForSecondsRealtime(introPrepareTimeoutSeconds);

        if (!isIntroVideoPrepared)
        {
            Debug.LogError("Intro short video preparation timed out.", this);
            OpenTargetScene();
        }
    }

    private void StopIntroPrepareTimeout()
    {
        if (introPrepareTimeoutRoutine == null)
        {
            return;
        }

        StopCoroutine(introPrepareTimeoutRoutine);
        introPrepareTimeoutRoutine = null;
    }

    private void OpenTargetScene()
    {
        StopIntroPrepareTimeout();

        if (!Application.CanStreamedLevelBeLoaded(sceneNameToOpen))
        {
            hasStartedTransition = false;
            Debug.LogError("StartScreenController target scene is not registered in Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(sceneNameToOpen);
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
