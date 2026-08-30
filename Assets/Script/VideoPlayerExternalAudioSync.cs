using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Plays a separate AudioSource together with a VideoPlayer and keeps their
/// playback state and time synchronized.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
[RequireComponent(typeof(AudioSource))]
public sealed class VideoPlayerExternalAudioSync : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Min(0.01f)] private float resyncTolerance = 0.1f;
    [SerializeField] private bool followVideoPlaybackSpeed = true;

    private void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ConfigureAudioSource();
    }

    private void OnEnable()
    {
        videoPlayer.started += HandleVideoStarted;
        videoPlayer.seekCompleted += HandleVideoSeekCompleted;
        videoPlayer.loopPointReached += HandleVideoFinished;
    }

    private void OnDisable()
    {
        videoPlayer.started -= HandleVideoStarted;
        videoPlayer.seekCompleted -= HandleVideoSeekCompleted;
        videoPlayer.loopPointReached -= HandleVideoFinished;
        audioSource.Stop();
    }

    private void Update()
    {
        if (videoPlayer == null || audioSource == null || audioSource.clip == null)
        {
            return;
        }

        ApplyPlaybackSpeed();

        if (videoPlayer.isPlaying)
        {
            if (!audioSource.isPlaying)
            {
                SyncAudioTime();
                if (audioSource.time < audioSource.clip.length)
                {
                    audioSource.Play();
                }
            }
            else if (Mathf.Abs(audioSource.time - (float)videoPlayer.time) > resyncTolerance)
            {
                SyncAudioTime();
            }

            return;
        }

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    private void HandleVideoStarted(VideoPlayer startedPlayer)
    {
        if (startedPlayer != videoPlayer || audioSource.clip == null)
        {
            return;
        }

        ApplyPlaybackSpeed();
        SyncAudioTime();
        audioSource.Play();
    }

    private void HandleVideoSeekCompleted(VideoPlayer seekedPlayer)
    {
        if (seekedPlayer != videoPlayer || audioSource.clip == null)
        {
            return;
        }

        SyncAudioTime();
    }

    private void HandleVideoFinished(VideoPlayer finishedPlayer)
    {
        if (finishedPlayer != videoPlayer)
        {
            return;
        }

        if (videoPlayer.isLooping && audioSource.clip != null)
        {
            audioSource.time = 0f;
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void ApplyPlaybackSpeed()
    {
        if (followVideoPlaybackSpeed)
        {
            audioSource.pitch = Mathf.Clamp(videoPlayer.playbackSpeed, -3f, 3f);
        }
    }

    private void SyncAudioTime()
    {
        float lastPlayableTime = Mathf.Max(0f, audioSource.clip.length - 0.001f);
        audioSource.time = Mathf.Clamp((float)videoPlayer.time, 0f, lastPlayableTime);
    }
}
