using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preloads the next scene additively, then activates it when the outgoing
/// presentation begins to leave the screen. Activation starts the incoming
/// scene's existing video controller.
/// </summary>
public sealed class AdditiveSceneVideoPreloader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "LoadingEnding";

    private AsyncOperation preloadOperation;
    private readonly List<Canvas> hiddenIncomingCanvases = new();
    private readonly List<Camera> hiddenIncomingCameras = new();
    private Scene sourceScene;
    private bool incomingPresentationHidden;
    private Coroutine hideIncomingPresentationRoutine;

    private void OnEnable()
    {
        sourceScene = gameObject.scene;
        BeginPreload();
    }

    public void BeginPreload()
    {
        if (preloadOperation != null)
        {
            return;
        }

        if (IsNextSceneLoaded())
        {
            StartHidingIncomingPresentation();
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName) ||
            !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                "[AdditiveSceneVideoPreloader] Next Scene must be enabled in Build Settings.",
                this);
            return;
        }

        preloadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
        if (preloadOperation == null)
        {
            Debug.LogError(
                $"[AdditiveSceneVideoPreloader] Failed to preload '{nextSceneName}'.",
                this);
            return;
        }

        preloadOperation.allowSceneActivation = false;
    }

    public void BeginIncomingSceneActivation()
    {
        if (preloadOperation == null && !IsNextSceneLoaded())
        {
            BeginPreload();
        }

        if (preloadOperation != null)
        {
            preloadOperation.allowSceneActivation = true;
        }

        StartHidingIncomingPresentation();
    }

    public IEnumerator CompleteTransition()
    {
        BeginIncomingSceneActivation();

        while (preloadOperation != null && !preloadOperation.isDone)
        {
            yield return null;
        }

        while (!incomingPresentationHidden)
        {
            yield return null;
        }

        Scene incomingScene = SceneManager.GetSceneByName(nextSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            Debug.LogError(
                $"[AdditiveSceneVideoPreloader] Scene '{nextSceneName}' was not activated.",
                this);
            yield break;
        }

        RestoreIncomingPresentation();
        SceneManager.SetActiveScene(incomingScene);
        if (sourceScene.IsValid() && sourceScene.isLoaded && sourceScene != incomingScene)
        {
            SceneManager.UnloadSceneAsync(sourceScene);
        }
    }

    private bool IsNextSceneLoaded()
    {
        Scene nextScene = SceneManager.GetSceneByName(nextSceneName);
        return nextScene.IsValid() && nextScene.isLoaded;
    }

    private void StartHidingIncomingPresentation()
    {
        if (hideIncomingPresentationRoutine == null && !incomingPresentationHidden)
        {
            hideIncomingPresentationRoutine = StartCoroutine(HideIncomingPresentationWhenReady());
        }
    }

    private IEnumerator HideIncomingPresentationWhenReady()
    {
        while (preloadOperation != null && !preloadOperation.isDone)
        {
            yield return null;
        }

        Scene incomingScene = SceneManager.GetSceneByName(nextSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            yield break;
        }

        GameObject[] roots = incomingScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Canvas[] canvases = roots[rootIndex].GetComponentsInChildren<Canvas>(true);
            for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
            {
                if (canvases[canvasIndex].enabled)
                {
                    canvases[canvasIndex].enabled = false;
                    hiddenIncomingCanvases.Add(canvases[canvasIndex]);
                }
            }

            Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
            for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            {
                if (cameras[cameraIndex].enabled)
                {
                    cameras[cameraIndex].enabled = false;
                    hiddenIncomingCameras.Add(cameras[cameraIndex]);
                }
            }
        }

        incomingPresentationHidden = true;
        hideIncomingPresentationRoutine = null;
    }

    private void RestoreIncomingPresentation()
    {
        for (int i = 0; i < hiddenIncomingCameras.Count; i++)
        {
            if (hiddenIncomingCameras[i] != null)
            {
                hiddenIncomingCameras[i].enabled = true;
            }
        }

        for (int i = 0; i < hiddenIncomingCanvases.Count; i++)
        {
            if (hiddenIncomingCanvases[i] != null)
            {
                hiddenIncomingCanvases[i].enabled = true;
            }
        }
    }
}
