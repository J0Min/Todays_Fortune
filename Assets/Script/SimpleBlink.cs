using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps an Image between open and closed eye sprites at randomized intervals.
/// If no closed sprite is assigned, the image remains open without starting a coroutine.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class SimpleBlink : MonoBehaviour
{
    [SerializeField] private Sprite openSprite;
    [SerializeField] private Sprite closedSprite;
    [SerializeField, Min(0f)] private float blinkIntervalMin = 3f;
    [SerializeField, Min(0f)] private float blinkIntervalMax = 5f;
    [SerializeField, Min(0.01f)] private float closedDuration = 0.15f;

    private Image targetImage;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (openSprite != null)
        {
            targetImage.sprite = openSprite;
        }

        if (openSprite != null && closedSprite != null)
        {
            blinkCoroutine = StartCoroutine(BlinkLoop());
        }
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (targetImage != null && openSprite != null)
        {
            targetImage.sprite = openSprite;
        }
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            float minimum = Mathf.Min(blinkIntervalMin, blinkIntervalMax);
            float maximum = Mathf.Max(blinkIntervalMin, blinkIntervalMax);
            yield return new WaitForSeconds(Random.Range(minimum, maximum));

            targetImage.sprite = closedSprite;
            yield return new WaitForSeconds(closedDuration);
            targetImage.sprite = openSprite;
        }
    }
}
