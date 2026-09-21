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
    [Tooltip("Seconds after the video's timeline starts before the external audio begins.")]
    [SerializeField, Min(0f)] private float audioStartDelaySeconds;
    [SerializeField, Min(0.01f)] private float resyncTolerance = 0.1f;
    [SerializeField] private bool followVideoPlaybackSpeed = true;
    [Tooltip("Stop the external audio when the video ends. Disable this to keep it playing through the following cutscene.")]
    [SerializeField] private bool stopAudioWhenVideoEnds = true;

    private bool hasStartedAudio;
    private bool isContinuingAudioAfterVideoEnd;

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
        hasStartedAudio = false;
        isContinuingAudioAfterVideoEnd = false;
        audioSource.Stop();
    }

    private void Update()
    {
        if (videoPlayer == null || audioSource == null || audioSource.clip == null)
        {
            return;
        }

        if (isContinuingAudioAfterVideoEnd)
        {
            return;
        }

        ApplyPlaybackSpeed();

        if (videoPlayer.isPlaying)
        {
            float expectedAudioTime = GetExpectedAudioTime();
            if (expectedAudioTime < 0f)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                hasStartedAudio = false;
                return;
            }

            if (!audioSource.isPlaying)
            {
                TryStartSyncedAudio();
            }
            else if (Mathf.Abs(audioSource.time - expectedAudioTime) > resyncTolerance)
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
        if (startedPlayer != videoPlayer || audioSource.clip == null ||
            isContinuingAudioAfterVideoEnd)
        {
            return;
        }

        ApplyPlaybackSpeed();
        TryStartSyncedAudio();
    }

    private void HandleVideoSeekCompleted(VideoPlayer seekedPlayer)
    {
        if (seekedPlayer != videoPlayer || audioSource.clip == null ||
            isContinuingAudioAfterVideoEnd)
        {
            return;
        }

        if (GetExpectedAudioTime() < 0f)
        {
            audioSource.Stop();
            hasStartedAudio = false;
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

        if (hasStartedAudio && !stopAudioWhenVideoEnds)
        {
            isContinuingAudioAfterVideoEnd = true;
            return;
        }

        if (videoPlayer.isLooping && audioSource.clip != null)
        {
            audioSource.time = 0f;
            audioSource.Play();
        }
        else if (stopAudioWhenVideoEnds)
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
        audioSource.time = Mathf.Clamp(GetExpectedAudioTime(), 0f, lastPlayableTime);
    }

    private bool TryStartSyncedAudio()
    {
        float expectedAudioTime = GetExpectedAudioTime();
        if (expectedAudioTime < 0f || expectedAudioTime >= audioSource.clip.length)
        {
            return false;
        }

        SyncAudioTime();
        audioSource.Play();
        hasStartedAudio = true;
        return true;
    }

    private float GetExpectedAudioTime()
    {
        return (float)videoPlayer.time - audioStartDelaySeconds;
    }
}
