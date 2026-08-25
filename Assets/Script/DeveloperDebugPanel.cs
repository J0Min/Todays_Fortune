using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class DeveloperDebugPanel : MonoBehaviour
{
    private bool isOpen;
    private float fps;

    private void Update()
    {
        if (Time.unscaledDeltaTime > 0f)
        {
            fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.1f);
        }

        if (WasToggleKeyPressed())
        {
            isOpen = !isOpen;
        }
    }

    private void OnGUI()
    {
        if (!isOpen)
        {
            return;
        }

        const float panelWidth = 300f;
        const float panelHeight = 600f;
        float panelY = (Screen.height - panelHeight) * 0.5f;
        GUILayout.BeginArea(new Rect(0f, panelY, panelWidth, panelHeight), GUI.skin.box);
        GUILayout.Label("Developer Debug (F1)");
        GUILayout.Label($"FPS: {fps:0.0}");
        GUILayout.Label($"Current Scene: {SceneManager.GetActiveScene().name}");

        GUILayout.Space(8f);
        GUILayout.Label("Open Scene");

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            if (GUILayout.Button(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        GUILayout.Space(8f);

        if (GUILayout.Button("Skip Current Video"))
        {
            FindAnyObjectByType<SceneVideoController>()?.SkipVideo();
        }

        if (GUILayout.Button("Quit"))
        {
            QuitApplication();
        }

        GUILayout.EndArea();
    }

    private static bool WasToggleKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F1);
#endif
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
