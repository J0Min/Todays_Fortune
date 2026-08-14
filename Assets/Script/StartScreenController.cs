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

    [Header("Intro Video")]
    [SerializeField] private VideoClip transitionVideo;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private VideoPlayer transitionVideoPlayer;
    private RawImage transitionImage;
    private bool hasStartedTransition;

    private void Awake()
    {
        EnsureEventSystem();
    }

    private void Update()
    {
        if (WasPrimaryInputPressedThisFrame())
        {
            BeginTransition();
        }
    }

    private void OnDestroy()
    {
        if (transitionVideoPlayer == null)
        {
            return;
        }

        transitionVideoPlayer.prepareCompleted -= OnTransitionPrepared;
        transitionVideoPlayer.frameReady -= OnTransitionFrameReady;
        transitionVideoPlayer.loopPointReached -= OnTransitionFinished;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        BeginTransition();
    }

    private void BeginTransition()
    {
        if (hasStartedTransition)
        {
            return;
        }

        if (transitionVideo == null)
        {
            Debug.LogError("StartScreenController needs a transition video.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneNameToOpen))
        {
            Debug.LogError("StartScreenController needs a scene name to open.", this);
            return;
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("StartScreenController needs a child Canvas.", this);
            return;
        }

        hasStartedTransition = true;
        Debug.Log(TouchMessage);
        CreateTransitionPlayer(canvas.transform);
        transitionVideoPlayer.Prepare();
    }

    private void CreateTransitionPlayer(Transform canvasTransform)
    {
        GameObject transitionObject = new GameObject(
            "Transition Video",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter),
            typeof(VideoPlayer));
        transitionObject.transform.SetParent(canvasTransform, false);
        transitionObject.transform.SetAsLastSibling();

        RectTransform transitionRect = transitionObject.GetComponent<RectTransform>();
        transitionRect.anchorMin = new Vector2(0.5f, 0.5f);
        transitionRect.anchorMax = new Vector2(0.5f, 0.5f);
        transitionRect.pivot = new Vector2(0.5f, 0.5f);
        transitionRect.anchoredPosition = Vector2.zero;

        AspectRatioFitter aspectRatioFitter = transitionObject.GetComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectRatioFitter.aspectRatio = transitionVideo.height > 0
            ? (float)transitionVideo.width / transitionVideo.height
            : 16f / 9f;

        transitionImage = transitionObject.GetComponent<RawImage>();
        transitionImage.color = Color.white;
        transitionImage.raycastTarget = true;
        transitionImage.enabled = false;

        transitionVideoPlayer = transitionObject.GetComponent<VideoPlayer>();
        transitionVideoPlayer.playOnAwake = false;
        transitionVideoPlayer.isLooping = false;
        transitionVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        transitionVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        transitionVideoPlayer.source = VideoSource.VideoClip;
        transitionVideoPlayer.clip = transitionVideo;
        transitionVideoPlayer.sendFrameReadyEvents = true;
        transitionVideoPlayer.prepareCompleted += OnTransitionPrepared;
        transitionVideoPlayer.frameReady += OnTransitionFrameReady;
        transitionVideoPlayer.loopPointReached += OnTransitionFinished;
    }

    private void OnTransitionPrepared(VideoPlayer preparedPlayer)
    {
        preparedPlayer.Play();
    }

    private void OnTransitionFrameReady(VideoPlayer videoPlayer, long frameIndex)
    {
        if (transitionImage.enabled || videoPlayer.texture == null)
        {
            return;
        }

        transitionImage.texture = videoPlayer.texture;
        transitionImage.enabled = true;
    }

    private void OnTransitionFinished(VideoPlayer videoPlayer)
    {
        if (videoPlayer != transitionVideoPlayer)
        {
            return;
        }

        Debug.Log(TransitionFinishedMessage);
        SceneManager.LoadScene(sceneNameToOpen);
    }

    private static bool WasPrimaryInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touchPressed = Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return mousePressed || touchPressed;
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
