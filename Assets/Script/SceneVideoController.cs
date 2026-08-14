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

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += HandleVideoFinished;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= HandleVideoFinished;
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

        onVideoFinished?.Invoke();
    }
}
