using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class Buttons : MonoBehaviour
{
    private static bool gamePaused;
    private static int resumedFrame = -1;
    [SerializeField] private Texture soundOnTexture;
    [SerializeField] private Texture soundOffTexture;
    [SerializeField] private RawImage soundButtonImage;

    public static bool IsWorldInputBlocked =>
        gamePaused || Time.frameCount == resumedFrame;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyMuteState();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void PauseGame()
    {
        gamePaused = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        gamePaused = false;
        resumedFrame = Time.frameCount;
    }

    public void ToggleMute()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError("PlayerFortuneState 인스턴스를 찾을 수 없습니다.", this);
            return;
        }

        state.SetMuted(!state.IsMuted);
        ApplyMuteState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMuteState();
    }

    private void ApplyMuteState()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            return;
        }

        AudioListener.volume = state.IsMuted ? 0f : 1f;

        if (soundButtonImage != null)
        {
            soundButtonImage.texture = state.IsMuted ? soundOffTexture : soundOnTexture;
        }
    }

    public static void ResetPauseState()
    {
        Time.timeScale = 1f;
        gamePaused = false;
        resumedFrame = -1;
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Buttons needs a scene name.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Scene '{sceneName}' is not registered or enabled in Build Settings.",
                this);
            return;
        }

        ResetPauseState();
        SceneManager.LoadScene(sceneName);
    }
}
