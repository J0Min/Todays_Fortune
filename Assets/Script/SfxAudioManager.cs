using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
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
    private readonly Dictionary<Button, ButtonSoundBinding> buttonBindings = new();
    private readonly List<Button> removedButtons = new();
    private Selectable[] selectables = new Selectable[32];
    private bool countdownPressHandled;

    private sealed class ButtonSoundBinding
    {
        public Button.ButtonClickedEvent ClickEvent;
        public UnityAction Listener;
    }

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

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            RegisterButton(button);
        }
    }

    private void Update()
    {
        // Selectable keeps an allocation-free list of enabled UI, including runtime creations.
        if (selectables.Length < Selectable.allSelectableCount)
        {
            Array.Resize(ref selectables, Mathf.NextPowerOfTwo(Selectable.allSelectableCount));
        }
        int count = Selectable.AllSelectablesNoAlloc(selectables);
        for (int i = 0; i < count; i++)
        {
            if (selectables[i] is Button button)
            {
                RegisterButton(button);
            }
            selectables[i] = null;
        }
        if (Time.frameCount % 60 == 0)
        {
            RemoveDestroyedButtonBindings();
        }

        if (IsTouchFeedbackEnabledForActiveScene() && TryGetPrimaryPressPosition(out Vector2 screenPosition))
        {
            PrimaryPressed?.Invoke(screenPosition);
        }
    }

    private void LateUpdate()
    {
        // Keep suppression through EventSystem's release/click processing. A countdown
        // touch can hide the overlay before that same input reaches an underlying button.
        if (!IsPrimaryPressHeld())
        {
            countdownPressHandled = false;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                RegisterButton(button);
            }
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RemoveDestroyedButtonBindings();
    }

    private void RemoveDestroyedButtonBindings()
    {
        removedButtons.Clear();
        foreach (var pair in buttonBindings)
        {
            if (pair.Key == null)
            {
                pair.Value.ClickEvent.RemoveListener(pair.Value.Listener);
                removedButtons.Add(pair.Key);
            }
        }
        foreach (Button button in removedButtons)
        {
            buttonBindings.Remove(button);
        }
        removedButtons.Clear();
    }

    public void RegisterButton(Button button)
    {
        if (button == null)
        {
            return;
        }
        if (buttonBindings.TryGetValue(button, out ButtonSoundBinding previous))
        {
            if (ReferenceEquals(previous.ClickEvent, button.onClick))
            {
                return;
            }
            previous.ClickEvent.RemoveListener(previous.Listener);
        }

        Button.ButtonClickedEvent clickEvent = button.onClick;
        UnityAction listener = () =>
        {
            // ToggleMute owns its sound order. Check the original Inspector callbacks,
            // so the generic listener never plays a second sound after unmuting.
            for (int i = 0; i < clickEvent.GetPersistentEventCount(); i++)
            {
                if (clickEvent.GetPersistentTarget(i) is Buttons &&
                    clickEvent.GetPersistentMethodName(i) == nameof(Buttons.ToggleMute) &&
                    clickEvent.GetPersistentListenerState(i) != UnityEventCallState.Off)
                {
                    return;
                }
            }
            PlayButtonPress();
        };
        clickEvent.AddListener(listener);
        buttonBindings[button] = new ButtonSoundBinding { ClickEvent = clickEvent, Listener = listener };
    }

    public void PlayCountdownPopupPress()
    {
        if (countdownPressHandled)
        {
            return;
        }
        PlayButtonPress();
        countdownPressHandled = true;
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

    public void PlayButtonPress(bool finishWhenMuted = false)
    {
        if (countdownPressHandled || audioSource == null || buttonPressClip == null ||
            (PlayerFortuneState.Instance != null && PlayerFortuneState.Instance.IsMuted))
        {
            return;
        }

        // Only the click that enables mute may finish after listener volume becomes zero.
        // New clicks while muted are rejected above; popup/ambient sources remain unchanged.
        audioSource.ignoreListenerVolume = finishWhenMuted;
        audioSource.PlayOneShot(buttonPressClip);
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

    private static bool IsPrimaryPressHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) ||
               (Mouse.current != null && Mouse.current.leftButton.isPressed);
#else
        return Input.touchCount > 0 || Input.GetMouseButton(0);
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            foreach (ButtonSoundBinding binding in buttonBindings.Values)
            {
                binding.ClickEvent.RemoveListener(binding.Listener);
            }
            buttonBindings.Clear();
            Instance = null;
        }
    }
}
