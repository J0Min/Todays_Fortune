using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PersistentGlobalLight2D
{
    private const string ObjectName = "Persistent Global Light 2D";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        Light2D[] existingLights = Object.FindObjectsByType<Light2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < existingLights.Length; i++)
        {
            if (existingLights[i].gameObject.name == ObjectName)
            {
                return;
            }
        }

        GameObject lightObject = new GameObject(ObjectName);
        Object.DontDestroyOnLoad(lightObject);

        Light2D globalLight = lightObject.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.blendStyleIndex = 0;
        globalLight.color = Color.white;
        globalLight.intensity = 1f;
        globalLight.targetSortingLayers = new[] { SortingLayer.NameToID("Default") };
    }
}
