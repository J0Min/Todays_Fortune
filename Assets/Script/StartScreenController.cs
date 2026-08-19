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
#if UNITY_EDITOR
    private const string IntroShortVideoAssetPath = "Assets/Video/intro2.mp4";
#endif

    [Header("Title Exit")]
    [SerializeField] private TitleExitAnimation titleExitAnimation;

    [Header("Intro Short Video")]
    [SerializeField] private VideoClip introShortVideo;
    [Min(0.01f)]
    [SerializeField] private float introCrossfadeDuration = 0.25f;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private bool hasStartedTransition;
    private bool hasReportedMissingIntroShortVideo;
    private VideoPlayer introVideoPlayer;
    private RawImage introVideoImage;

    private void Awake()
    {
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
        if (WasPrimaryInputPressedThisFrame())
        {
            BeginTransition();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        BeginTransition();
    }

    private void OnDestroy()
    {
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

        if (string.IsNullOrWhiteSpace(sceneNameToOpen))
        {
            Debug.LogError("StartScreenController needs a scene name to open.", this);
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
        titleExitAnimation.Play(OnTitleExitFinished);
    }

    private void OnTitleExitFinished()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("StartScreenController needs a child Canvas.", this);
            return;
        }

        CreateIntroVideoPlayer(canvas.transform);
        introVideoPlayer.Prepare();
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
        player.Play();
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
        SceneManager.LoadScene(sceneNameToOpen);
    }

    private void OnIntroVideoError(VideoPlayer player, string message)
    {
        Debug.LogError("Intro short video failed: " + message, this);
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
