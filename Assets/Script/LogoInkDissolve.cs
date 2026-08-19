using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogoInkDissolve : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material dissolveMaterial;

    [Header("Dissolve")]
    [Min(0.01f)]
    [SerializeField] private float dissolveDuration = 1.1f;
    [Range(2f, 30f)]
    [SerializeField] private float noiseScale = 10f;
    [Range(0.001f, 0.25f)]
    [SerializeField] private float edgeFeather = 0.075f;
    [SerializeField] private Vector2 thresholdRange = new Vector2(-0.15f, 1.15f);
    [SerializeField] private AnimationCurve dissolveProgress =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int FeatherId = Shader.PropertyToID("_Feather");
    private const float MaximumNoiseValue = 1f;
    private const float IntroTransitionThreshold = 0.65f;

    private Graphic[] logoGraphics;
    private Material runtimeMaterial;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        logoGraphics = GetComponentsInChildren<Graphic>(true);
        if (dissolveMaterial == null || logoGraphics.Length == 0)
        {
            return;
        }

        runtimeMaterial = Instantiate(dissolveMaterial);
        runtimeMaterial.name = dissolveMaterial.name + " (Runtime)";
        ApplyMaterialValues(thresholdRange.x);
        foreach (Graphic graphic in logoGraphics)
        {
            graphic.material = runtimeMaterial;
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    public void Play(Action onTransitionReady = null)
    {
        if (isPlaying)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            onTransitionReady?.Invoke();
            return;
        }

        isPlaying = true;
        StartCoroutine(PlayDissolve(onTransitionReady));
    }

    private IEnumerator PlayDissolve(Action onTransitionReady)
    {
        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        bool hasStartedIntroTransition = false;
        while (elapsed < dissolveDuration)
        {
            elapsed = Time.unscaledTime - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / dissolveDuration);
            float progress = dissolveProgress.Evaluate(normalizedTime);
            float threshold = Mathf.LerpUnclamped(thresholdRange.x, thresholdRange.y, progress);
            ApplyMaterialValues(threshold);

            if (!hasStartedIntroTransition && threshold >= IntroTransitionThreshold)
            {
                hasStartedIntroTransition = true;
                onTransitionReady?.Invoke();
            }

            if (threshold >= MaximumNoiseValue + edgeFeather)
            {
                break;
            }

            yield return null;
        }

        ApplyMaterialValues(MaximumNoiseValue + edgeFeather);
        isPlaying = false;
        if (!hasStartedIntroTransition)
        {
            onTransitionReady?.Invoke();
        }
    }

    private void ApplyMaterialValues(float threshold)
    {
        runtimeMaterial.SetFloat(DissolveAmountId, threshold);
        runtimeMaterial.SetFloat(NoiseScaleId, noiseScale);
        runtimeMaterial.SetFloat(FeatherId, edgeFeather);
    }
}
