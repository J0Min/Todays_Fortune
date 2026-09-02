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
    public static event Action<Vector2> PrimaryPressed;

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
        if (IsTouchFeedbackEnabledForActiveScene() && TryGetPrimaryPressPosition(out Vector2 screenPosition))
        {
            PlayOneShot(buttonPressClip);
            PrimaryPressed?.Invoke(screenPosition);
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

    private static bool TryGetPrimaryPressPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
