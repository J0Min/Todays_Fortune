using System.Collections;
using UnityEngine;
using UnityEngine.Events;
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

    private enum VideoPhase
    {
        Intro,
        Outro
    }

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField, Min(1f)] private float prepareTimeoutSeconds = 15f;

    [Header("Events")]
    [SerializeField] private UnityEvent onIntroVideoFinished;
    [FormerlySerializedAs("onVideoFinished")]
    [SerializeField] private UnityEvent onOutroVideoFinished;

    [Header("Scene Start Playback")]
    [SerializeField] private bool playOnSceneStart;
    [SerializeField] private VideoClip sceneStartVideo;

    [Header("UI")]
    [SerializeField] private Canvas[] uiCanvases;
    [SerializeField] private bool hideUiWhileVideoPlays = true;

    [Header("Video Clips")]
    [Tooltip("The selected result ID 1 maps to element 0, ID 2 maps to element 1, and so on.")]
    [SerializeField] private VideoClip[] videoClips;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private InactivityTimer inactivityTimer;
    private AsyncOperation preloadOperation;
    private string preloadedSceneName;
    private Coroutine prepareTimeoutRoutine;
    private Coroutine sceneActivationRoutine;
    private Coroutine handoffCompletionRoutine;
    private bool isPreparing;
    private bool isFinishing;
    private VideoPhase currentPhase;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
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

        if (preloadOperation != null && preloadedSceneName == sceneNameToOpen)
        {
            if (sceneActivationRoutine == null)
            {
                PrepareAdditiveHandoff();
                preloadOperation.allowSceneActivation = true;
                sceneActivationRoutine = StartCoroutine(WaitForPreloadedSceneActivation());
            }
            return;
        }

        SceneManager.LoadScene(sceneNameToOpen);
    }

    public void SkipVideo()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        FinishVideo();
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
        StopPrepareTimeout();
        inactivityTimer?.Resume(this);

        if (currentPhase == VideoPhase.Intro)
        {
            HideVideoOutput();
            CompletePendingHandoff(gameObject.scene);
            SetUiVisible(true);
            onIntroVideoFinished?.Invoke();
        }
        else
        {
            // Keep the final outro frame visible until the preloaded scene activates.
            onOutroVideoFinished?.Invoke();
        }
    }

    private void PlaySceneStartVideo()
    {
        if (videoPlayer == null || sceneStartVideo == null)
            return;

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = sceneStartVideo;
        PrepareVideo(VideoPhase.Intro);
    }

    private void PrepareVideo(VideoPhase phase)
    {
        inactivityTimer?.Pause(this);
        currentPhase = phase;
        isPreparing = true;
        isFinishing = false;
        videoPlayer.targetCameraAlpha = 0f;
        StopPrepareTimeout();
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
        if (!hideUiWhileVideoPlays || uiCanvases == null || uiCanvases.Length == 0)
            return;

        for (int i = 0; i < uiCanvases.Length; i++)
        {
            if (uiCanvases[i] != null)
                uiCanvases[i].enabled = visible;
        }
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

        HideUi();
        videoPlayer.targetCameraAlpha = 1f;
        videoPlayer.Play();
    }

    private void HandleVideoFrameReady(VideoPlayer preparedVideoPlayer, long frameIndex)
    {
        if (preparedVideoPlayer != videoPlayer || currentPhase != VideoPhase.Intro ||
            !IsPendingIncomingScene(gameObject.scene) || handoffCompletionRoutine != null)
        {
            return;
        }

        handoffCompletionRoutine = StartCoroutine(CompleteHandoffAfterFrame());
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

        Camera videoCamera = videoPlayer != null ? videoPlayer.targetCamera : null;
        if (videoCamera != null)
        {
            // Keep the outgoing video's final frame above the incoming scene until
            // the incoming intro video has produced its first frame.
            videoCamera.depth = short.MaxValue;
        }

        SetSceneAudioListenersEnabled(gameObject.scene, false);
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

        if (!HasPlayableSceneIntro(incomingScene))
        {
            CompletePendingHandoff(incomingScene);
        }
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
        outgoingController.HideVideoOutput();

        if (outgoingScene.IsValid() && outgoingScene.isLoaded && outgoingScene != incomingScene)
        {
            SceneManager.UnloadSceneAsync(outgoingScene);
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
