using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class TouchEffectPlayer : MonoBehaviour
{
    private Image targetImage;
    private Sprite[] frames;
    private Action<TouchEffectPlayer> returnToPool;
    private float frameDuration;
    private float elapsedTime;
    private int frameIndex;
    private bool isPlaying;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        targetImage.raycastTarget = false;
    }

    public void Play(
        Sprite[] animationFrames,
        float framesPerSecond,
        Vector2 anchoredPosition,
        Action<TouchEffectPlayer> onComplete)
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
            targetImage.raycastTarget = false;
        }

        if (animationFrames == null || animationFrames.Length == 0)
        {
            onComplete?.Invoke(this);
            return;
        }

        frames = animationFrames;
        frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        elapsedTime = 0f;
        frameIndex = 0;
        returnToPool = onComplete;
        isPlaying = true;

        RectTransform rectTransform = (RectTransform)transform;
        rectTransform.anchoredPosition = anchoredPosition;
        targetImage.sprite = frames[0];
        targetImage.enabled = true;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        elapsedTime += Time.unscaledDeltaTime;
        while (elapsedTime >= frameDuration)
        {
            elapsedTime -= frameDuration;
            frameIndex++;

            if (frameIndex >= frames.Length)
            {
                Complete();
                return;
            }

            targetImage.sprite = frames[frameIndex];
        }
    }

    private void Complete()
    {
        isPlaying = false;
        targetImage.enabled = false;

        Action<TouchEffectPlayer> completion = returnToPool;
        returnToPool = null;
        frames = null;
        completion?.Invoke(this);
    }
}
