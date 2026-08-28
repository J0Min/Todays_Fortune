using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class RawImageGroupFadeIn : MonoBehaviour
{
    [Header("First Group")]
    [SerializeField] private RawImage[] firstGroup;
    [SerializeField, Min(0f)] private float firstFadeDelay = 0f;
    [SerializeField, Min(0f)] private float firstFadeDuration = 1f;
    [SerializeField, Min(0f)] private float firstFadeOutDelay = 0f;
    [SerializeField, Min(0f)] private float firstFadeOutDuration = 1f;

    [Header("Second Group")]
    [SerializeField] private RawImage[] secondGroup;
    [SerializeField, Min(0f)] private float secondFadeDelay = 0f;
    [SerializeField, Min(0f)] private float secondFadeDuration = 1f;
    [SerializeField, Min(0f)] private float secondFadeOutDelay = 0f;
    [SerializeField, Min(0f)] private float secondFadeOutDuration = 1f;

    private Coroutine firstGroupFadeRoutine;
    private Coroutine secondGroupFadeRoutine;

    private void OnEnable()
    {
        SetAlpha(firstGroup, 0f);
        SetAlpha(secondGroup, 0f);

        StartGroupFade(ref firstGroupFadeRoutine, firstGroup, firstFadeDelay, firstFadeDuration, 1f);
        StartGroupFade(ref secondGroupFadeRoutine, secondGroup, secondFadeDelay, secondFadeDuration, 1f);
    }

    public void PlayFadeOut()
    {
        StartGroupFade(
            ref firstGroupFadeRoutine,
            firstGroup,
            firstFadeOutDelay,
            firstFadeOutDuration,
            0f);
        StartGroupFade(
            ref secondGroupFadeRoutine,
            secondGroup,
            secondFadeOutDelay,
            secondFadeOutDuration,
            0f);
    }

    private void StartGroupFade(
        ref Coroutine routine,
        RawImage[] images,
        float delay,
        float duration,
        float targetAlpha)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(FadeTo(images, delay, duration, targetAlpha));
    }

    private static IEnumerator FadeTo(RawImage[] images, float delay, float duration, float targetAlpha)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (duration <= 0f)
        {
            SetAlpha(images, targetAlpha);
            yield break;
        }

        float[] startAlphas = GetAlphas(images);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(images, startAlphas, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetAlpha(images, targetAlpha);
    }

    private static float[] GetAlphas(RawImage[] images)
    {
        if (images == null)
        {
            return new float[0];
        }

        float[] alphas = new float[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
            {
                alphas[i] = images[i].color.a;
            }
        }

        return alphas;
    }

    private static void SetAlpha(RawImage[] images, float[] startAlphas, float targetAlpha, float progress)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            RawImage image = images[i];
            if (image == null)
            {
                continue;
            }

            Color color = image.color;
            color.a = Mathf.Lerp(startAlphas[i], targetAlpha, progress);
            image.color = color;
        }
    }

    private static void SetAlpha(RawImage[] images, float alpha)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            RawImage image = images[i];
            if (image == null)
            {
                continue;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
