using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class StartScreenController : MonoBehaviour, IPointerClickHandler
{
    private const string CanvasName = "Start Screen Canvas";
    private const string TouchMessage = "Start Screen Touched";
    private const string TransitionFinishedMessage = "Start Transition Finished";

    [SerializeField] private VideoClip waitingVideo;
    [SerializeField] private VideoClip transitionVideo;
    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private VideoPlayer backgroundVideoPlayer;
    private VideoPlayer transitionVideoPlayer;
    private RawImage transitionImage;
    private bool hasBeenTouched;
    private bool transitionFinished;

    private void Awake()
    {
        CreateInterface();
        EnsureEventSystem();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hasBeenTouched)
        {
            return;
        }

        hasBeenTouched = true;
        Debug.Log(TouchMessage);

        if (backgroundVideoPlayer == null || transitionVideo == null)
        {
            Debug.LogError("Start screen transition video is not assigned.");
            return;
        }

        backgroundVideoPlayer.Pause();
        transitionVideoPlayer.Prepare();
    }

    private void CreateInterface()
    {
        if (transform.Find(CanvasName) != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            CanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject(
            "Video Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter),
            typeof(VideoPlayer));
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        StretchToParent(backgroundRect);

        RawImage background = backgroundObject.GetComponent<RawImage>();
        background.color = Color.white;
        background.raycastTarget = true;

        AspectRatioFitter aspectRatioFitter = backgroundObject.GetComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectRatioFitter.aspectRatio = waitingVideo != null && waitingVideo.height > 0
            ? (float)waitingVideo.width / waitingVideo.height
            : 16f / 9f;

        backgroundVideoPlayer = backgroundObject.GetComponent<VideoPlayer>();
        backgroundVideoPlayer.playOnAwake = true;
        backgroundVideoPlayer.isLooping = true;
        backgroundVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        backgroundVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        backgroundVideoPlayer.source = VideoSource.VideoClip;
        backgroundVideoPlayer.clip = waitingVideo;
        backgroundVideoPlayer.prepareCompleted += preparedPlayer =>
        {
            background.texture = preparedPlayer.texture;
            if (preparedPlayer.clip != null && preparedPlayer.clip.height > 0)
            {
                aspectRatioFitter.aspectRatio =
                    (float)preparedPlayer.clip.width / preparedPlayer.clip.height;
            }

            preparedPlayer.Play();
        };
        GameObject transitionObject = new GameObject(
            "Transition Video",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter),
            typeof(VideoPlayer));
        transitionObject.transform.SetParent(canvasObject.transform, false);

        RectTransform transitionRect = transitionObject.GetComponent<RectTransform>();
        StretchToParent(transitionRect);

        transitionImage = transitionObject.GetComponent<RawImage>();
        transitionImage.color = Color.white;
        transitionImage.raycastTarget = false;
        transitionImage.enabled = false;

        AspectRatioFitter transitionAspectRatioFitter =
            transitionObject.GetComponent<AspectRatioFitter>();
        transitionAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        transitionAspectRatioFitter.aspectRatio = transitionVideo != null && transitionVideo.height > 0
            ? (float)transitionVideo.width / transitionVideo.height
            : 16f / 9f;

        transitionVideoPlayer = transitionObject.GetComponent<VideoPlayer>();
        transitionVideoPlayer.playOnAwake = false;
        transitionVideoPlayer.isLooping = false;
        transitionVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        transitionVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        transitionVideoPlayer.source = VideoSource.VideoClip;
        transitionVideoPlayer.clip = transitionVideo;
        transitionVideoPlayer.sendFrameReadyEvents = true;
        transitionVideoPlayer.prepareCompleted += preparedPlayer => preparedPlayer.Play();
        transitionVideoPlayer.frameReady += OnTransitionFrameReady;
        transitionVideoPlayer.loopPointReached += OnVideoFinished;

        if (waitingVideo != null)
        {
            backgroundVideoPlayer.Prepare();
        }
        else
        {
            Debug.LogError("Start screen waiting video is not assigned.");
        }

    }

    private void OnTransitionFrameReady(VideoPlayer videoPlayer, long frameIndex)
    {
        if (transitionImage.enabled || videoPlayer.texture == null)
        {
            return;
        }

        transitionImage.texture = videoPlayer.texture;
        transitionImage.enabled = true;
        backgroundVideoPlayer.Stop();
    }

    private void OnVideoFinished(VideoPlayer videoPlayer)
    {
        if (!hasBeenTouched ||
            videoPlayer != transitionVideoPlayer ||
            transitionFinished)
        {
            return;
        }

        transitionFinished = true;
        Debug.Log(TransitionFinishedMessage);

        if (string.IsNullOrWhiteSpace(sceneNameToOpen))
        {
            Debug.LogError("StartScreenController needs a scene name to open.", this);
            return;
        }

        SceneManager.LoadScene(sceneNameToOpen);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
