using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AmbientAudioManager : MonoBehaviour
{
    private const float ContentVolumeRatio = 0.4f;
    private const float SelectionFadeTargetRatio = 1f;

    [SerializeField, Min(0.01f)] private float loopCrossfadeDuration = 0.08f;

    public static AmbientAudioManager Instance { get; private set; }

    private AudioSource[] audioSources;
    private int activeSourceIndex;
    private float baseVolume;
    private float masterVolumeRatio = 1f;
    private float fadeStartRatio;
    private float fadeTargetRatio = 1f;
    private float fadeDuration;
    private float fadeElapsed;
    private bool isVolumeFading;
    private bool isNextSourceScheduled;
    private double activeSourceEndDspTime;
    private double nextSourceStartDspTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        AudioSource primarySource = GetComponent<AudioSource>();
        baseVolume = primarySource.volume;
        ConfigureSource(primarySource);

        if (primarySource.clip == null)
        {
            Debug.LogError("AmbientAudioManager needs an AudioClip.", this);
            return;
        }

        AudioSource secondarySource = gameObject.AddComponent<AudioSource>();
        secondarySource.clip = primarySource.clip;
        secondarySource.outputAudioMixerGroup = primarySource.outputAudioMixerGroup;
        secondarySource.pitch = primarySource.pitch;
        secondarySource.priority = primarySource.priority;
        ConfigureSource(secondarySource);

        audioSources = new[] { primarySource, secondarySource };
        StartLoopPlayback();
    }

    private void Update()
    {
        UpdateVolumeFade();
        UpdateLoopCrossfade();
    }

    public void FadeToContentVolume(float duration) =>
        FadeToVolumeRatio(ContentVolumeRatio, duration);

    public void RestoreBaseVolumeImmediately() => SetVolumeRatioImmediately(1f);

    public void FadeThroughSelection(float duration) =>
        FadeToVolumeRatio(SelectionFadeTargetRatio, duration);

    public void FadeToBaseVolume(float duration) => FadeToVolumeRatio(1f, duration);

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private static void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void StartLoopPlayback()
    {
        double startDspTime = AudioSettings.dspTime + 0.05d;
        AudioSource activeSource = audioSources[activeSourceIndex];
        activeSource.volume = baseVolume * masterVolumeRatio;
        activeSource.PlayScheduled(startDspTime);
        activeSourceEndDspTime = startDspTime + activeSource.clip.length;
    }

    private void UpdateLoopCrossfade()
    {
        if (audioSources == null || audioSources.Length < 2)
        {
            return;
        }

        double dspTime = AudioSettings.dspTime;
        double crossfadeDuration = GetCrossfadeDuration();
        double crossfadeStartDspTime = activeSourceEndDspTime - crossfadeDuration;

        if (!isNextSourceScheduled && dspTime >= crossfadeStartDspTime - 1d)
        {
            AudioSource nextSource = audioSources[1 - activeSourceIndex];
            nextSource.volume = 0f;
            nextSource.time = 0f;
            nextSourceStartDspTime = crossfadeStartDspTime;
            nextSource.PlayScheduled(nextSourceStartDspTime);
            isNextSourceScheduled = true;
        }

        if (isNextSourceScheduled && dspTime >= crossfadeStartDspTime)
        {
            ApplySourceVolumes();
        }

        if (isNextSourceScheduled && dspTime >= activeSourceEndDspTime)
        {
            audioSources[activeSourceIndex].Stop();
            activeSourceIndex = 1 - activeSourceIndex;
            activeSourceEndDspTime = nextSourceStartDspTime + audioSources[activeSourceIndex].clip.length;
            isNextSourceScheduled = false;
            ApplySourceVolumes();
        }
    }

    private void UpdateVolumeFade()
    {
        if (!isVolumeFading)
        {
            return;
        }

        fadeElapsed += Time.unscaledDeltaTime;
        float progress = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
        masterVolumeRatio = Mathf.Lerp(fadeStartRatio, fadeTargetRatio, progress);
        ApplySourceVolumes();

        if (progress >= 1f)
        {
            isVolumeFading = false;
        }
    }

    private void SetVolumeRatioImmediately(float ratio)
    {
        isVolumeFading = false;
        masterVolumeRatio = Mathf.Clamp01(ratio);
        ApplySourceVolumes();
    }

    private void FadeToVolumeRatio(float ratio, float duration)
    {
        fadeStartRatio = masterVolumeRatio;
        fadeTargetRatio = Mathf.Clamp01(ratio);
        fadeDuration = Mathf.Max(0f, duration);
        fadeElapsed = 0f;
        isVolumeFading = fadeDuration > 0f;

        if (!isVolumeFading)
        {
            masterVolumeRatio = fadeTargetRatio;
            ApplySourceVolumes();
        }
    }

    private void ApplySourceVolumes()
    {
        if (audioSources == null)
        {
            return;
        }

        float masterVolume = baseVolume * masterVolumeRatio;
        double dspTime = AudioSettings.dspTime;
        double crossfadeDuration = GetCrossfadeDuration();
        double crossfadeStartDspTime = activeSourceEndDspTime - crossfadeDuration;

        if (isNextSourceScheduled && dspTime >= crossfadeStartDspTime)
        {
            float progress = crossfadeDuration <= 0d
                ? 1f
                : Mathf.Clamp01((float)((dspTime - crossfadeStartDspTime) / crossfadeDuration));
            audioSources[activeSourceIndex].volume = masterVolume * (1f - progress);
            audioSources[1 - activeSourceIndex].volume = masterVolume * progress;
            return;
        }

        audioSources[activeSourceIndex].volume = masterVolume;
        audioSources[1 - activeSourceIndex].volume = 0f;
    }

    private double GetCrossfadeDuration()
    {
        return Mathf.Min(loopCrossfadeDuration, audioSources[activeSourceIndex].clip.length * 0.25f);
    }
}
