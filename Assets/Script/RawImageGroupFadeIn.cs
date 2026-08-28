using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class RawImageGroupFadeIn : MonoBehaviour
{
    [Header("First Group")]
    [SerializeField] private RawImage[] firstGroup;
    [SerializeField, Min(0f)] private float firstFadeDelay = 0f;
    [SerializeField, Min(0f)] private float firstFadeDuration = 1f;

    [Header("Second Group")]
    [SerializeField] private RawImage[] secondGroup;
    [SerializeField, Min(0f)] private float secondFadeDelay = 0f;
    [SerializeField, Min(0f)] private float secondFadeDuration = 1f;

    private void OnEnable()
    {
        SetAlpha(firstGroup, 0f);
        SetAlpha(secondGroup, 0f);

        StartCoroutine(FadeIn(firstGroup, firstFadeDelay, firstFadeDuration));
        StartCoroutine(FadeIn(secondGroup, secondFadeDelay, secondFadeDuration));
    }

    private static IEnumerator FadeIn(RawImage[] images, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (duration <= 0f)
        {
            SetAlpha(images, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(images, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetAlpha(images, 1f);
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
