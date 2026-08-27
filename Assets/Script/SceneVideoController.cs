using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Video;

/// <summary>
/// Controls one scene-specific video and notifies listeners when it finishes.
/// </summary>
public sealed class SceneVideoController : MonoBehaviour
{
    private static SceneVideoController pendingOutgoingController;
    private static string pendingIncomingSceneName;
    private static float pendingIncomingIntroStartSeconds;

    private enum VideoPhase
    {
        Intro,
        Outro
    }

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField, Min(1f)] private float prepareTimeoutSeconds = 15f;
    [SerializeField, Min(0f)] private float fadeInDuration;

    [Header("Events")]
    [SerializeField] private UnityEvent onIntroVideoFinished;
    [FormerlySerializedAs("onVideoFinished")]
    [SerializeField] private UnityEvent onOutroVideoFinished;

    [Header("Scene Start Playback")]
    [SerializeField] private bool playOnSceneStart;
    [SerializeField] private VideoClip sceneStartVideo;
    [SerializeField] private bool preloadSceneDuringIntro;

    [Header("UI")]
    [SerializeField, InspectorName("UI")] private GameObject[] ui;
    [HideInInspector, SerializeField] private Canvas[] uiCanvases;
    [SerializeField] private bool hideUiWhileVideoPlays = true;

    [Header("Object After Outro Video")]
    [Tooltip("The object to activate after the configured delay.")]
    [SerializeField] private GameObject objectToShowAfterOutroVideo;
    [Tooltip("Activate the object this many seconds after an outro video's first visible frame. Set to a negative value to disable this behavior.")]
    [SerializeField] private float showObjectAfterOutroVideoSeconds = -1f;

    [Header("Video Clips")]
    [Tooltip("The selected result ID 1 maps to element 0, ID 2 maps to element 1, and so on.")]
    [SerializeField] private VideoClip[] videoClips;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;
    [SerializeField, Min(0f)] private float incomingSceneActivationLeadTime = 2f;

    private InactivityTimer inactivityTimer;
    private AsyncOperation preloadOperation;
    private string preloadedSceneName;
    private Coroutine prepareTimeoutRoutine;
    private Coroutine fadeInRoutine;
    private Coroutine showObjectAfterVideoRoutine;
    private Coroutine sceneActivationRoutine;
    private Coroutine scenePreActivationRoutine;
    private Coroutine handoffCompletionRoutine;
    private bool hasReceivedFirstFrame;
    private bool isWaitingForFirstFrame;
    private bool isPreparing;
    private bool isFinishing;
    private bool hasFinishedTransitionVideo;
    private VideoPhase currentPhase;
    private float introStartTimeAtHandoff;

    public bool IsWaitingForFirstFrame => isWaitingForFirstFrame;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        MigrateLegacyUiCanvases();
    }

    private void OnValidate()
    {
        MigrateLegacyUiCanvases();
    }

    private void Start()
    {
        if (playOnSceneStart)
            PlaySceneStartVideo();
    }

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += HandleVideoPrepared;
            videoPlayer.frameReady += HandleVideoFrameReady;
            videoPlayer.loopPointReached += HandleVideoFinished;
            videoPlayer.errorReceived += HandleVideoError;
            videoPlayer.sendFrameReadyEvents = true;
        }
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandleVideoPrepared;
            videoPlayer.frameReady -= HandleVideoFrameReady;
            videoPlayer.loopPointReached -= HandleVideoFinished;
            videoPlayer.errorReceived -= HandleVideoError;
        }

        StopPrepareTimeout();
        StopFadeIn();
        StopShowObjectAfterVideo();
        StopScenePreActivation();
        isWaitingForFirstFrame = false;
        isPreparing = false;
        inactivityTimer?.Resume(this);
        HideVideoOutput();
        SetUiVisible(true);
    }

    public void VideoPlay()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        int selectedId = state != null && state.CardId > 0 ? state.CardId : state?.RopeId ?? 0;
        VideoPlay(selectedId);
    }

    public void VideoPlay(int selectedId)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        if (!TrySetSelectedVideo(selectedId))
        {
            return;
        }

        BeginScenePreload();
        PrepareVideo(VideoPhase.Outro);
    }

    public void VideoPlay(VideoClip videoClip)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        if (videoClip == null)
        {
            Debug.LogError("Scene start playback needs a Video Clip.", this);
            return;
        }

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        BeginScenePreload();
        PrepareVideo(VideoPhase.Outro);
    }

    public void OpenScene()
    {
        if (!CanOpenConfiguredScene())
        {
            return;
        }

        if (sceneNameToOpen == "StartScene")
        {
            PlayerFortuneState.Instance?.ResetData();
        }

        if (preloadOperation != null && preloadedSceneName == sceneNameToOpen)
        {
            ActivatePreloadedScene();
            return;
        }

        SceneManager.LoadScene(sceneNameToOpen);
    }

    public void SetVideoSpeed1x()
    {
        SetVideoPlaybackSpeed(1f);
    }

    public void SetVideoSpeed2x()
    {
        SetVideoPlaybackSpeed(2f);
    }

    public void SkipVideo()
    {
        SkipVideo(0f);
    }

    public void SkipVideo(float nextIntroStartSeconds)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        bool skipsToPreloadedScene = preloadOperation != null &&
            (currentPhase == VideoPhase.Outro ||
             (currentPhase == VideoPhase.Intro && preloadSceneDuringIntro));
        if (skipsToPreloadedScene)
            pendingIncomingIntroStartSeconds = Mathf.Max(0f, nextIntroStartSeconds);

        FinishVideo();
    }

    public void VideoPause()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        videoPlayer.Pause();
    }

    public void VideoResume()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        videoPlayer.Play();
    }

    private void SetVideoPlaybackSpeed(float speed)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        videoPlayer.playbackSpeed = speed;
    }

    private void HandleVideoFinished(VideoPlayer finishedVideoPlayer)
    {
        if (finishedVideoPlayer != videoPlayer || finishedVideoPlayer.isLooping)
            return;

        FinishVideo();
    }

    private void FinishVideo()
    {
        if (isFinishing)
        {
            return;
        }

        isFinishing = true;
        isPreparing = false;
        isWaitingForFirstFrame = false;
        StopPrepareTimeout();
        StopFadeIn();
        StopShowObjectAfterVideo();
        inactivityTimer?.Resume(this);

        if (currentPhase == VideoPhase.Intro)
        {
            if (preloadSceneDuringIntro && preloadOperation != null)
            {
                hasFinishedTransitionVideo = true;
                StopScenePreActivation();
                onIntroVideoFinished?.Invoke();
                CompleteHandoffIfReady();
                return;
            }

            HideVideoOutput();
            CompletePendingHandoff(gameObject.scene);
            SetUiVisible(true);
            onIntroVideoFinished?.Invoke();
        }
        else
        {
            hasFinishedTransitionVideo = true;
            StopScenePreActivation();
            // Keep the final outro frame visible until the preloaded scene is ready.
            onOutroVideoFinished?.Invoke();
            CompleteHandoffIfReady();
        }
    }

    public void PlaySceneStartVideo()
    {
        if (videoPlayer == null || sceneStartVideo == null)
            return;

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = sceneStartVideo;
        if (preloadSceneDuringIntro)
        {
            BeginScenePreload();
        }
        PrepareVideo(VideoPhase.Intro);
    }

    public void PlaySceneStartVideo(float fadeDuration)
    {
        fadeInDuration = Mathf.Max(0f, fadeDuration);
        PlaySceneStartVideo();
    }

    private void PrepareVideo(VideoPhase phase)
    {
        inactivityTimer?.Pause(this);
        currentPhase = phase;
        introStartTimeAtHandoff = phase == VideoPhase.Intro &&
                                    IsPendingIncomingScene(gameObject.scene)
            ? pendingIncomingIntroStartSeconds
            : 0f;
        if (phase == VideoPhase.Outro)
        {
            hasFinishedTransitionVideo = false;
            StopScenePreActivation();
        }
        else if (preloadSceneDuringIntro)
        {
            hasFinishedTransitionVideo = false;
            StopScenePreActivation();
        }
        isPreparing = true;
        isFinishing = false;
        hasReceivedFirstFrame = false;
        isWaitingForFirstFrame = true;
        videoPlayer.targetCameraAlpha = 0f;
        StopPrepareTimeout();
        StopFadeIn();
        StopShowObjectAfterVideo();
        prepareTimeoutRoutine = StartCoroutine(WaitForVideoPrepare());
        videoPlayer.Prepare();
    }

    private IEnumerator WaitForVideoPrepare()
    {
        yield return new WaitForSecondsRealtime(prepareTimeoutSeconds);

        if (isPreparing)
        {
            HandleVideoFailure("Video preparation timed out.");
        }
    }

    private void StopPrepareTimeout()
    {
        if (prepareTimeoutRoutine == null)
        {
            return;
        }

        StopCoroutine(prepareTimeoutRoutine);
        prepareTimeoutRoutine = null;
    }

    private void BeginScenePreload()
    {
        if (!CanOpenConfiguredScene())
        {
            return;
        }

        if (preloadOperation != null)
        {
            return;
        }

        preloadOperation = SceneManager.LoadSceneAsync(sceneNameToOpen, LoadSceneMode.Additive);
        if (preloadOperation == null)
        {
            Debug.LogError($"Failed to begin loading scene '{sceneNameToOpen}'.", this);
            return;
        }

        preloadedSceneName = sceneNameToOpen;
        preloadOperation.allowSceneActivation = false;
    }

    private bool CanOpenConfiguredScene()
    {
        if (string.IsNullOrWhiteSpace(sceneNameToOpen))
        {
            Debug.LogError("SceneVideoController needs a scene name to open.", this);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneNameToOpen))
        {
            Debug.LogError(
                $"Scene '{sceneNameToOpen}' is not registered or enabled in Build Settings.",
                this);
            return false;
        }

        return true;
    }

    private void HideVideoOutput()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();
        videoPlayer.targetCameraAlpha = 0f;
    }

    private void SetUiVisible(bool visible)
    {
        if (!hideUiWhileVideoPlays || ui == null || ui.Length == 0)
            return;

        for (int i = 0; i < ui.Length; i++)
        {
            if (ui[i] != null)
                ui[i].SetActive(visible);
        }
    }

    private void MigrateLegacyUiCanvases()
    {
        if (ui != null || uiCanvases == null || uiCanvases.Length == 0)
            return;

        ui = new GameObject[uiCanvases.Length];
        for (int i = 0; i < uiCanvases.Length; i++)
        {
            if (uiCanvases[i] != null)
                ui[i] = uiCanvases[i].gameObject;
        }

        uiCanvases = null;
    }

    private void HideUi()
    {
        SetUiVisible(false);
    }

    private bool TrySetSelectedVideo(int selectedId)
    {
        // An empty list preserves the existing setup where the clip is assigned directly
        // to the VideoPlayer in the Inspector.
        if (videoClips == null || videoClips.Length == 0)
        {
            return true;
        }

        int videoIndex = selectedId - 1;
        if (videoIndex < 0 || videoIndex >= videoClips.Length || videoClips[videoIndex] == null)
        {
            Debug.LogError(
                $"No video is registered for selected ID={selectedId}. " +
                "Set the matching Video Clips array element in the Inspector.",
                this);
            return false;
        }

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClips[videoIndex];
        return true;
    }

    private void HandleVideoPrepared(VideoPlayer preparedVideoPlayer)
    {
        if (preparedVideoPlayer != videoPlayer || !isPreparing)
        {
            return;
        }

        isPreparing = false;
        StopPrepareTimeout();

        if (restartFromBeginning)
        {
            videoPlayer.time = 0d;
        }

        // Preparation does not guarantee that a decoded frame has reached the
        // camera output yet. Keep the video hidden until frameReady confirms it.
        SetDirectAudioMuted(ShouldHideIncomingVideoUntilHandoff());
        videoPlayer.Play();

        if (currentPhase == VideoPhase.Outro ||
            (currentPhase == VideoPhase.Intro && preloadSceneDuringIntro))
        {
            scenePreActivationRoutine = StartCoroutine(ActivatePreloadedSceneBeforeOutroEnds());
        }
    }

    private void HandleVideoFrameReady(VideoPlayer preparedVideoPlayer, long frameIndex)
    {
        if (preparedVideoPlayer != videoPlayer)
        {
            return;
        }

        if (!hasReceivedFirstFrame)
        {
            hasReceivedFirstFrame = true;
            isWaitingForFirstFrame = false;
            HideUi();
            StartShowObjectAfterOutroVideo();
            if (ShouldHideIncomingVideoUntilHandoff())
            {
                videoPlayer.targetCameraAlpha = 0f;
            }
            else if (fadeInDuration > 0f)
            {
                fadeInRoutine = StartCoroutine(FadeInVideo());
            }
            else
            {
                videoPlayer.targetCameraAlpha = 1f;
            }
        }

        if (currentPhase != VideoPhase.Intro || !IsPendingIncomingScene(gameObject.scene) ||
            !HasPendingOutgoingVideoFinished() || handoffCompletionRoutine != null)
        {
            return;
        }

        handoffCompletionRoutine = StartCoroutine(CompleteHandoffAfterFrame());
    }

    private IEnumerator FadeInVideo()
    {
        float startedAt = Time.unscaledTime;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / fadeInDuration);
            videoPlayer.targetCameraAlpha = Mathf.SmoothStep(0f, 1f, normalizedTime);
            yield return null;
        }

        videoPlayer.targetCameraAlpha = 1f;
        fadeInRoutine = null;
    }

    private void StopFadeIn()
    {
        if (fadeInRoutine == null)
        {
            return;
        }

        StopCoroutine(fadeInRoutine);
        fadeInRoutine = null;
    }

    private void StartShowObjectAfterOutroVideo()
    {
        if (currentPhase != VideoPhase.Outro || objectToShowAfterOutroVideo == null ||
            showObjectAfterOutroVideoSeconds < 0f)
        {
            return;
        }

        StopShowObjectAfterVideo();
        showObjectAfterVideoRoutine = StartCoroutine(ShowObjectAfterOutroVideo());
    }

    private IEnumerator ShowObjectAfterOutroVideo()
    {
        yield return new WaitForSecondsRealtime(showObjectAfterOutroVideoSeconds);

        if (!isFinishing && currentPhase == VideoPhase.Outro)
        {
            objectToShowAfterOutroVideo.SetActive(true);
        }

        showObjectAfterVideoRoutine = null;
    }

    private void StopShowObjectAfterVideo()
    {
        if (showObjectAfterVideoRoutine == null)
        {
            return;
        }

        StopCoroutine(showObjectAfterVideoRoutine);
        showObjectAfterVideoRoutine = null;
    }

    private IEnumerator CompleteHandoffAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        CompletePendingHandoff(gameObject.scene);
        handoffCompletionRoutine = null;
    }

    private void PrepareAdditiveHandoff()
    {
        pendingOutgoingController = this;
        pendingIncomingSceneName = preloadedSceneName;

        // The incoming scene may have its own Global Light 2D. Disable this
        // scene's 2D lights before activating it so the two do not overlap.
        SetSceneLightsEnabled(gameObject.scene, false);

        Camera videoCamera = videoPlayer != null ? videoPlayer.targetCamera : null;
        if (videoCamera != null)
        {
            // Keep the outgoing video's final frame above the incoming scene until
            // the incoming intro video has produced its first frame.
            videoCamera.depth = short.MaxValue;
        }

    }

    private void ActivatePreloadedScene()
    {
        if (sceneActivationRoutine != null)
        {
            return;
        }

        PrepareAdditiveHandoff();
        preloadOperation.allowSceneActivation = true;
        sceneActivationRoutine = StartCoroutine(WaitForPreloadedSceneActivation());
    }

    private bool IsTransitionVideoPlaying()
    {
        return currentPhase == VideoPhase.Outro ||
               (currentPhase == VideoPhase.Intro && preloadSceneDuringIntro);
    }

    private IEnumerator ActivatePreloadedSceneBeforeOutroEnds()
    {
        while (!isFinishing && IsTransitionVideoPlaying() &&
               (videoPlayer.length <= 0d ||
                videoPlayer.time < videoPlayer.length - incomingSceneActivationLeadTime))
        {
            yield return null;
        }

        if (!isFinishing && IsTransitionVideoPlaying() && preloadOperation != null)
        {
            ActivatePreloadedScene();
        }

        scenePreActivationRoutine = null;
    }

    private void StopScenePreActivation()
    {
        if (scenePreActivationRoutine == null)
        {
            return;
        }

        StopCoroutine(scenePreActivationRoutine);
        scenePreActivationRoutine = null;
    }

    private IEnumerator WaitForPreloadedSceneActivation()
    {
        while (preloadOperation != null && !preloadOperation.isDone)
        {
            yield return null;
        }

        Scene incomingScene = SceneManager.GetSceneByName(preloadedSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            Debug.LogError($"Failed to activate preloaded scene '{preloadedSceneName}'.", this);
            yield break;
        }

        SetSceneAudioListenersEnabled(incomingScene, false);
        CompleteHandoffIfReady();
    }

    private static bool HasPlayableSceneIntro(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            SceneVideoController[] controllers =
                rootObjects[i].GetComponentsInChildren<SceneVideoController>(true);

            for (int j = 0; j < controllers.Length; j++)
            {
                SceneVideoController controller = controllers[j];
                if (controller.isActiveAndEnabled && controller.playOnSceneStart &&
                    controller.sceneStartVideo != null && controller.videoPlayer != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPendingIncomingScene(Scene scene)
    {
        return pendingOutgoingController != null && scene.IsValid() &&
               scene.name == pendingIncomingSceneName;
    }

    private static bool HasPendingOutgoingVideoFinished()
    {
        return pendingOutgoingController != null &&
               pendingOutgoingController.hasFinishedTransitionVideo;
    }

    private bool ShouldHideIncomingVideoUntilHandoff()
    {
        return currentPhase == VideoPhase.Intro && IsPendingIncomingScene(gameObject.scene) &&
               !HasPendingOutgoingVideoFinished();
    }

    private void CompleteHandoffIfReady()
    {
        if (!hasFinishedTransitionVideo || string.IsNullOrEmpty(preloadedSceneName))
        {
            return;
        }

        Scene incomingScene = SceneManager.GetSceneByName(preloadedSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            return;
        }

        if (!HasPlayableSceneIntro(incomingScene) || IsSceneIntroReady(incomingScene))
        {
            CompletePendingHandoff(incomingScene);
        }
    }

    private static void CompletePendingHandoff(Scene incomingScene)
    {
        if (!IsPendingIncomingScene(incomingScene))
        {
            return;
        }

        SceneVideoController outgoingController = pendingOutgoingController;
        Scene outgoingScene = outgoingController.gameObject.scene;

        pendingOutgoingController = null;
        pendingIncomingSceneName = null;

        SceneManager.SetActiveScene(incomingScene);
        SetSceneCamerasEnabled(outgoingScene, false);
        SetSceneAudioListenersEnabled(outgoingScene, false);
        SetSceneAudioListenersEnabled(incomingScene, true);
        outgoingController.HideVideoOutput();
        RevealIncomingSceneVideos(incomingScene);
        pendingIncomingIntroStartSeconds = 0f;

        if (outgoingScene.IsValid() && outgoingScene.isLoaded && outgoingScene != incomingScene)
        {
            SceneManager.UnloadSceneAsync(outgoingScene);
        }
    }

    private static bool IsSceneIntroReady(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            SceneVideoController[] controllers =
                rootObjects[i].GetComponentsInChildren<SceneVideoController>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                SceneVideoController controller = controllers[j];
                if (controller.isActiveAndEnabled && controller.currentPhase == VideoPhase.Intro &&
                    controller.hasReceivedFirstFrame)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void RevealIncomingSceneVideos(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            SceneVideoController[] controllers =
                rootObjects[i].GetComponentsInChildren<SceneVideoController>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                controllers[j].RevealVideoAfterHandoff();
            }
        }
    }

    private void RevealVideoAfterHandoff()
    {
        SetDirectAudioMuted(false);
        if (currentPhase != VideoPhase.Intro || !hasReceivedFirstFrame || videoPlayer == null)
        {
            return;
        }

        if (introStartTimeAtHandoff > 0f)
        {
            videoPlayer.time = introStartTimeAtHandoff;
            videoPlayer.Play();
            introStartTimeAtHandoff = 0f;
        }

        if (fadeInDuration > 0f)
        {
            StopFadeIn();
            fadeInRoutine = StartCoroutine(FadeInVideo());
        }
        else
        {
            videoPlayer.targetCameraAlpha = 1f;
        }
    }

    private void SetDirectAudioMuted(bool muted)
    {
        if (videoPlayer == null || videoPlayer.audioOutputMode != VideoAudioOutputMode.Direct)
        {
            return;
        }

        for (ushort trackIndex = 0; trackIndex < videoPlayer.audioTrackCount; trackIndex++)
        {
            videoPlayer.SetDirectAudioMute(trackIndex, muted);
        }
    }

    private static void SetSceneCamerasEnabled(Scene scene, bool enabled)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Camera[] cameras = rootObjects[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                cameras[j].enabled = enabled;
            }
        }
    }

    private static void SetSceneLightsEnabled(Scene scene, bool enabled)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Light2D[] lights = rootObjects[i].GetComponentsInChildren<Light2D>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                lights[j].enabled = enabled;
            }
        }
    }

    private static void SetSceneAudioListenersEnabled(Scene scene, bool enabled)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            AudioListener[] listeners = rootObjects[i].GetComponentsInChildren<AudioListener>(true);
            for (int j = 0; j < listeners.Length; j++)
            {
                listeners[j].enabled = enabled;
            }
        }
    }

    private void HandleVideoError(VideoPlayer failedVideoPlayer, string message)
    {
        if (failedVideoPlayer != videoPlayer)
        {
            return;
        }

        HandleVideoFailure($"Video playback failed: {message}");
    }

    private void HandleVideoFailure(string message)
    {
        Debug.LogError(message, this);
        VideoPhase failedPhase = currentPhase;
        FinishVideo();

        if (failedPhase == VideoPhase.Outro)
        {
            OpenScene();
        }
    }
}
