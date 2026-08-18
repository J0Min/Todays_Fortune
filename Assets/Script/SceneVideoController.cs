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
    [Header("Scene Transition")]
    [SerializeField] private string sceneNameToOpen;

    private InactivityTimer inactivityTimer;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();

        if (videoPlayer != null)
            videoPlayer.loopPointReached += HandleVideoFinished;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= HandleVideoFinished;

        inactivityTimer?.Resume(this);
    }

    public void VideoPlay()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("SceneVideoController needs a VideoPlayer.", this);
            return;
        }

        if (restartFromBeginning)
            videoPlayer.time = 0d;

        inactivityTimer?.Pause(this);
        videoPlayer.Play();
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

    private void HandleVideoFinished(VideoPlayer finishedVideoPlayer)
    {
        if (finishedVideoPlayer != videoPlayer || finishedVideoPlayer.isLooping)
            return;

        inactivityTimer?.Resume(this);
        onVideoFinished?.Invoke();
    }
}
