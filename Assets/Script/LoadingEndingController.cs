using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class LoadingEndingController : MonoBehaviour
{
    private const int RequiredEndingLayerCount = 6;

    [Header("Loading Video")]
    [SerializeField] private VideoClip loadingVideo;
    [Min(0f)]
    [SerializeField] private float loadingFadeFromBlackDuration = 1.2f;

    [Header("Temporary Ending Textures (01 - 06)")]
    [SerializeField] private Texture2D[] endingTextures;
    [Min(0f)]
    [SerializeField] private float endingFadeDuration = 0.5f;
    [Min(0f)]
    [SerializeField] private float endingHoldDuration = 0.5f;

    [Header("Return To Start")]
    [Min(0f)]
    [SerializeField] private float inputEnableDelay = 2f;
    [Min(0f)]
    [SerializeField] private float autoReturnDelay = 20f;
    [SerializeField] private string startSceneName = "StartScene";

    private VideoPlayer loadingVideoPlayer;
    private RawImage loadingVideoImage;
    private RawImage loadingFadeOverlay;
    private CanvasGroup[] endingLayers;
    private InactivityTimer inactivityTimer;
    private bool hasStartedEnding;
    private bool hasFinishedLoadingVideo;
    private bool isInputEnabled;
    private bool isReturning;
    private Coroutine autoReturnCoroutine;
    private Coroutine loadingFadeCoroutine;

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

    private void Update()
    {
        if (!isInputEnabled || isReturning)
        {
            return;
        }

        if (WasReturnInputPressedThisFrame())
        {
            Debug.Log("[LoadingEnding] User input detected", this);
            BeginReturnToWaitingScreen("user input", true);
        }
    }

    private void HandleVideoPrepared(VideoPlayer player)
    {
        if (player != loadingVideoPlayer)
        {
            return;
        }

        player.time = 0d;
        float playbackDuration = player.clip != null && player.playbackSpeed > 0f
            ? (float)(player.clip.length / player.playbackSpeed)
            : 0f;
        AmbientAudioManager.Instance?.FadeToBaseVolume(playbackDuration);
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

        if (loadingFadeCoroutine == null)
        {
            loadingFadeCoroutine = StartCoroutine(FadeLoadingFromBlack());
        }
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
        AmbientAudioManager.Instance?.RestoreBaseVolumeImmediately();
        hasFinishedLoadingVideo = true;
        if (loadingVideoImage != null)
        {
            loadingVideoImage.enabled = false;
            loadingVideoImage.gameObject.SetActive(false);
        }

        if (loadingFadeOverlay != null)
        {
            loadingFadeOverlay.enabled = false;
            loadingFadeOverlay.gameObject.SetActive(false);
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

        Debug.Log("[LoadingEnding] Ending completed", this);
        Debug.Log("[LoadingEnding] Input enable delay started.", this);
        if (inputEnableDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(inputEnableDelay);
        }

        isInputEnabled = true;
        Debug.Log("[LoadingEnding] Input enabled", this);
        Debug.Log($"[LoadingEnding] Auto return timer started: {autoReturnDelay:0.##}s", this);
        autoReturnCoroutine = StartCoroutine(AutoReturnAfterDelay());
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

    private IEnumerator AutoReturnAfterDelay()
    {
        if (autoReturnDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(autoReturnDelay);
        }

        if (isReturning)
        {
            yield break;
        }

        Debug.Log("[LoadingEnding] Auto return timeout", this);
        autoReturnCoroutine = null;
        BeginReturnToWaitingScreen("auto timeout", false);
    }

    private void BeginReturnToWaitingScreen(string reason, bool waitForInputRelease)
    {
        if (isReturning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogError("LoadingEndingController needs a Start Scene name.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError(
                $"[LoadingEnding] Scene '{startSceneName}' is not available in Build Settings.",
                this);
            return;
        }

        isReturning = true;
        isInputEnabled = false;
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }

        Debug.Log(
            $"[LoadingEnding] Returning to waiting screen: {startSceneName} ({reason})",
            this);
        PlayerFortuneState.Instance?.ResetData();
        StartScreenController.PrepareForEndingReturn(waitForInputRelease);
        SceneManager.LoadScene(startSceneName);
    }

    private static bool WasReturnInputPressedThisFrame()
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

    private IEnumerator FadeLoadingFromBlack()
    {
        if (loadingFadeOverlay == null)
        {
            yield break;
        }

        if (loadingFadeFromBlackDuration <= 0f)
        {
            loadingFadeOverlay.enabled = false;
            loadingFadeOverlay.gameObject.SetActive(false);
            yield break;
        }

        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        while (elapsed < loadingFadeFromBlackDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float progress = Mathf.Clamp01(elapsed / loadingFadeFromBlackDuration);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
            loadingFadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        loadingFadeOverlay.enabled = false;
        loadingFadeOverlay.gameObject.SetActive(false);
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

        loadingFadeOverlay = CreateFullscreenImage("Loading Fade From Black", canvasObject.transform);
        loadingFadeOverlay.color = Color.black;
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
