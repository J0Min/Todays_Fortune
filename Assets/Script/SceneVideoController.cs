using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Controls one scene-specific video and notifies listeners when it finishes.
/// </summary>
public sealed class SceneVideoController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField] private UnityEvent onVideoFinished;

    [Header("Video Clips")]
    [Tooltip("The selected result ID 1 maps to element 0, ID 2 maps to element 1, and so on.")]
    [SerializeField] private VideoClip[] videoClips;

    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private InactivityTimer inactivityTimer;
    private bool isPreparing;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
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
    }

    public void VideoPlay()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        if (!TrySetSelectedVideo())
        {
            return;
        }

        inactivityTimer?.Pause(this);
        isPreparing = true;
        videoPlayer.Prepare();
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
        videoPlayer.Stop();
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
        inactivityTimer?.Resume(this);
        onVideoFinished?.Invoke();
    }

    private bool TrySetSelectedVideo()
    {
        // An empty list preserves the existing setup where the clip is assigned directly
        // to the VideoPlayer in the Inspector.
        if (videoClips == null || videoClips.Length == 0)
        {
            return true;
        }

        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError("Selected video playback needs an active PlayerFortuneState.", this);
            return false;
        }

        int videoIndex = state.CardId - 1;
        if (videoIndex < 0 || videoIndex >= videoClips.Length || videoClips[videoIndex] == null)
        {
            Debug.LogError(
                $"No video is registered for selected ID={state.CardId}. " +
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

        videoPlayer.Play();
    }
}
