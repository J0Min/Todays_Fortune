using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Controls one scene-specific video and notifies listeners when it finishes.
/// </summary>
public sealed class SceneVideoController : MonoBehaviour
{
    private enum VideoPhase
    {
        Intro,
        Outro
    }

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField] private UnityEvent onVideoFinished;

    [Header("Scene Start Playback")]
    [SerializeField] private bool playOnSceneStart;
    [SerializeField] private VideoClip sceneStartVideo;

    [Header("UI")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private bool hideUiWhileVideoPlays = true;
    [SerializeField, Min(0f)] private float uiFadeDelay;
    [SerializeField, Min(0f)] private float uiFadeDuration = 0.25f;

    [Header("Video Clips")]
    [Tooltip("The selected result ID 1 maps to element 0, ID 2 maps to element 1, and so on.")]
    [SerializeField] private VideoClip[] videoClips;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private InactivityTimer inactivityTimer;
    private bool isPreparing;
    private Coroutine uiFadeRoutine;
    private CanvasGroup uiCanvasGroup;
    private VideoPhase currentPhase;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (uiCanvasGroup == null && uiCanvas != null)
            uiCanvasGroup = uiCanvas.GetComponent<CanvasGroup>();
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
            videoPlayer.loopPointReached += HandleVideoFinished;
        }
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandleVideoPrepared;
            videoPlayer.loopPointReached -= HandleVideoFinished;
        }

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
        PrepareVideo(VideoPhase.Outro);
    }

    public void OpenScene()
    {
        if (string.IsNullOrWhiteSpace(sceneNameToOpen))
        {
            Debug.LogError("SceneVideoController needs a scene name to open.", this);
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

        isPreparing = false;
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
        HideVideoOutput();
        inactivityTimer?.Resume(this);
        SetUiVisible(true);

        if (currentPhase == VideoPhase.Outro)
            onVideoFinished?.Invoke();
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
        videoPlayer.targetCameraAlpha = 0f;
        videoPlayer.Prepare();
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
        if (!hideUiWhileVideoPlays || uiCanvas == null)
            return;

        if (uiFadeRoutine != null)
        {
            StopCoroutine(uiFadeRoutine);
            uiFadeRoutine = null;
        }

        if (uiCanvasGroup == null)
            uiCanvasGroup = uiCanvas.GetComponent<CanvasGroup>();

        if (uiCanvasGroup != null)
            uiCanvasGroup.alpha = visible ? 1f : 0f;
    }

    private void HideUi()
    {
        if (!hideUiWhileVideoPlays || uiCanvas == null)
            return;

        if (uiCanvasGroup == null)
            uiCanvasGroup = uiCanvas.GetComponent<CanvasGroup>();

        if (uiCanvasGroup == null)
            return;

        if (uiFadeRoutine != null)
            StopCoroutine(uiFadeRoutine);

        uiFadeRoutine = StartCoroutine(FadeOutUi());
    }

    private IEnumerator FadeOutUi()
    {
        if (uiFadeDelay > 0f)
            yield return new WaitForSeconds(uiFadeDelay);

        float startAlpha = uiCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < uiFadeDuration)
        {
            elapsed += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / uiFadeDuration);
            yield return null;
        }

        uiCanvasGroup.alpha = 0f;
        uiFadeRoutine = null;
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

        if (restartFromBeginning)
        {
            videoPlayer.time = 0d;
        }

        HideUi();
        videoPlayer.targetCameraAlpha = 1f;
        videoPlayer.Play();
    }
}
