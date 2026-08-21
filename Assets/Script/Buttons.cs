using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Buttons : MonoBehaviour
{
    private static bool gamePaused;
    private static int resumedFrame = -1;

    public static bool IsWorldInputBlocked =>
        gamePaused || Time.frameCount == resumedFrame;

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
