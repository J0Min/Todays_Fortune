using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class LoadingEndingController : MonoBehaviour
{
    private const int RequiredEndingLayerCount = 6;

    [Header("Loading Video")]
    [SerializeField] private VideoClip loadingVideo;

    [Header("Temporary Ending Textures (01 - 06)")]
    [SerializeField] private Texture2D[] endingTextures;
    [Min(0f)]
    [SerializeField] private float endingFadeDuration = 0.5f;
    [Min(0f)]
    [SerializeField] private float endingHoldDuration = 0.5f;
    [Min(0f)]
    [SerializeField] private float finalHoldDuration = 2f;

    [Header("Next Scene (Temporary)")]
    [SerializeField] private string nextSceneName = "DataTest";

    private VideoPlayer loadingVideoPlayer;
    private RawImage loadingVideoImage;
    private CanvasGroup[] endingLayers;
    private InactivityTimer inactivityTimer;
    private bool hasStartedEnding;
    private bool hasFinishedLoadingVideo;
    private bool isTransitioning;

    private void Awake()
    {
        CreatePresentation();
    }

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();
        inactivityTimer?.Pause(this);

        if (loadingVideoPlayer == null)
        {
            Debug.LogError("LoadingEndingController needs a Loading VideoPlayer.", this);
            return;
        }

        loadingVideoPlayer.prepareCompleted += HandleVideoPrepared;
        loadingVideoPlayer.frameReady += HandleVideoFrameReady;
        loadingVideoPlayer.loopPointReached += HandleVideoFinished;
        loadingVideoPlayer.errorReceived += HandleVideoError;

        loadingVideoPlayer.Prepare();
    }

    private void OnDisable()
    {
        inactivityTimer?.Resume(this);

        if (loadingVideoPlayer == null)
        {
            return;
        }

        loadingVideoPlayer.prepareCompleted -= HandleVideoPrepared;
        loadingVideoPlayer.frameReady -= HandleVideoFrameReady;
        loadingVideoPlayer.loopPointReached -= HandleVideoFinished;
        loadingVideoPlayer.errorReceived -= HandleVideoError;
    }

    private void HandleVideoPrepared(VideoPlayer player)
    {
        if (player != loadingVideoPlayer)
        {
            return;
        }

        player.time = 0d;
        player.Play();
    }

    private void HandleVideoFrameReady(VideoPlayer player, long frameIndex)
    {
        if (hasFinishedLoadingVideo || player != loadingVideoPlayer || loadingVideoImage == null ||
            loadingVideoImage.enabled || player.texture == null)
        {
            return;
        }

        loadingVideoImage.texture = player.texture;
        loadingVideoImage.enabled = true;
    }

    private void HandleVideoFinished(VideoPlayer player)
    {
        if (player != loadingVideoPlayer || player.isLooping)
        {
            return;
        }

        Debug.Log("[LoadingEnding] Loading finished.", this);
        StartEndingOnce();
    }

    private void HandleVideoError(VideoPlayer player, string message)
    {
        Debug.LogError("Loading video failed: " + message, this);
        StartEndingOnce();
    }

    private void StartEndingOnce()
    {
        if (hasStartedEnding)
        {
            return;
        }

        hasStartedEnding = true;
        hasFinishedLoadingVideo = true;
        if (loadingVideoImage != null)
        {
            loadingVideoImage.enabled = false;
            loadingVideoImage.gameObject.SetActive(false);
        }

        ResetEndingLayersForPlayback();
        Debug.Log("[LoadingEnding] Ending started.", this);
        StartCoroutine(PlayEnding());
    }

    private IEnumerator PlayEnding()
    {
        if (!HasCompleteEndingSetup())
        {
            yield break;
        }

        for (int i = 0; i < RequiredEndingLayerCount; i++)
        {
            CanvasGroup layer = endingLayers[i];
            Debug.Log($"[LoadingEnding] Ending layer {i + 1:00} started.", this);
            yield return FadeIn(layer);

            if (endingHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(endingHoldDuration);
            }
        }

        if (finalHoldDuration > 0f)
        {
            Debug.Log("[LoadingEnding] Final hold started.", this);
            yield return new WaitForSecondsRealtime(finalHoldDuration);
        }
        else
        {
            Debug.Log("[LoadingEnding] Final hold started (duration: 0).", this);
        }

        TransitionToNextSceneOnce();
    }

    private bool HasCompleteEndingSetup()
    {
        if (endingTextures == null || endingTextures.Length != RequiredEndingLayerCount ||
            endingLayers == null || endingLayers.Length != RequiredEndingLayerCount)
        {
            Debug.LogError(
                $"LoadingEndingController needs exactly {RequiredEndingLayerCount} Ending textures and layers.",
                this);
            return false;
        }

        for (int i = 0; i < endingLayers.Length; i++)
        {
            if (endingTextures[i] == null || endingLayers[i] == null)
            {
                Debug.LogError(
                    $"[LoadingEnding] Ending Layer {i + 1:00} reference is missing.",
                    this);
                return false;
            }

            RawImage layerImage = endingLayers[i].GetComponent<RawImage>();
            if (layerImage == null)
            {
                Debug.LogError(
                    $"[LoadingEnding] Ending Layer {i + 1:00} RawImage is missing.",
                    this);
                return false;
            }
        }

        return true;
    }

    private void TransitionToNextSceneOnce()
    {
        if (isTransitioning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("LoadingEndingController needs a Next Scene name.", this);
            return;
        }

        isTransitioning = true;
        Debug.Log($"[LoadingEnding] Loading scene '{nextSceneName}'.", this);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator FadeIn(CanvasGroup layer)
    {
        if (endingFadeDuration <= 0f)
        {
            layer.alpha = 1f;
            yield break;
        }

        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        while (elapsed < endingFadeDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / endingFadeDuration);
            layer.alpha = Mathf.SmoothStep(0f, 1f, normalizedTime);
            yield return null;
        }

        layer.alpha = 1f;
    }

    private void SetEndingLayersHidden()
    {
        if (endingLayers == null)
        {
            return;
        }

        foreach (CanvasGroup layer in endingLayers)
        {
            if (layer != null)
            {
                layer.alpha = 0f;
                layer.interactable = false;
                layer.blocksRaycasts = false;
            }
        }
    }

    private void ResetEndingLayersForPlayback()
    {
        if (endingLayers == null)
        {
            return;
        }

        foreach (CanvasGroup layer in endingLayers)
        {
            if (layer == null)
            {
                continue;
            }

            layer.gameObject.SetActive(true);
            RawImage layerImage = layer.GetComponent<RawImage>();
            if (layerImage != null)
            {
                layerImage.enabled = true;
            }

            layer.alpha = 0f;
            layer.interactable = false;
            layer.blocksRaycasts = false;
        }
    }

    private void CreatePresentation()
    {
        CreateCamera();

        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RawImage background = CreateFullscreenImage("Background", canvasObject.transform);
        background.color = Color.black;

        int layerCount = endingTextures?.Length ?? 0;
        endingLayers = new CanvasGroup[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            RawImage layerImage = CreateFullscreenImage(
                $"Ending Layer {i + 1:00}",
                canvasObject.transform);
            layerImage.texture = endingTextures[i];
            layerImage.color = Color.white;

            CanvasGroup group = layerImage.gameObject.AddComponent<CanvasGroup>();
            endingLayers[i] = group;
        }

        SetEndingLayersHidden();

        loadingVideoImage = CreateFullscreenImage("Loading Video", canvasObject.transform);
        loadingVideoImage.color = Color.white;
        loadingVideoImage.enabled = false;
        AspectRatioFitter fitter = loadingVideoImage.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = loadingVideo != null && loadingVideo.height > 0
            ? (float)loadingVideo.width / loadingVideo.height
            : 16f / 9f;

        loadingVideoPlayer = gameObject.AddComponent<VideoPlayer>();
        loadingVideoPlayer.playOnAwake = false;
        loadingVideoPlayer.waitForFirstFrame = true;
        loadingVideoPlayer.isLooping = false;
        loadingVideoPlayer.skipOnDrop = true;
        loadingVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        loadingVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        loadingVideoPlayer.source = VideoSource.VideoClip;
        loadingVideoPlayer.clip = loadingVideo;
        loadingVideoPlayer.sendFrameReadyEvents = true;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static RawImage CreateFullscreenImage(string objectName, Transform parent)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }
}
