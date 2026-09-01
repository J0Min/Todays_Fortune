using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SfxAudioManager : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip popupClip;
    [SerializeField, Min(0f)] private float popupStartOffset = 0.47f;
    [SerializeField] private AudioClip buttonPressClip;

    [Header("Touch Feedback")]
    [Tooltip("Add scene names here when global touch feedback should be disabled for that scene.")]
    [SerializeField] private string[] touchExcludedSceneNames = Array.Empty<string>();

    public static SfxAudioManager Instance { get; private set; }

    private AudioSource audioSource;
    private AudioSource popupAudioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        popupAudioSource = gameObject.AddComponent<AudioSource>();
        popupAudioSource.playOnAwake = false;
        popupAudioSource.loop = false;
        popupAudioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (IsTouchFeedbackEnabledForActiveScene() && WasPrimaryPressStartedThisFrame())
        {
            PlayOneShot(buttonPressClip);
        }
    }

    public void PlayPopup()
    {
        if (popupAudioSource == null || popupClip == null)
        {
            return;
        }

        popupAudioSource.Stop();
        popupAudioSource.clip = popupClip;
        popupAudioSource.time = Mathf.Min(popupStartOffset, popupClip.length);
        popupAudioSource.Play();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private bool IsTouchFeedbackEnabledForActiveScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < touchExcludedSceneNames.Length; i++)
        {
            if (touchExcludedSceneNames[i] == activeSceneName)
            {
                return false;
            }
        }

        return true;
    }

    private static bool WasPrimaryPressStartedThisFrame()
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
