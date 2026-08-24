using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class RawImageAlphaRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField, Range(0f, 1f)]
    private float alphaThreshold = 0.1f;

    private RawImage rawImage;
    private bool warnedAboutUnreadableTexture;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }

        if (rawImage == null || !rawImage.raycastTarget)
        {
            return false;
        }

        if (rawImage.texture is not Texture2D texture || !texture.isReadable)
        {
            WarnAboutUnreadableTexture();
            return false;
        }

        RectTransform rectTransform = rawImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;
        if (!rect.Contains(localPoint))
        {
            return false;
        }

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        Rect uvRect = rawImage.uvRect;
        float u = uvRect.x + normalizedX * uvRect.width;
        float v = uvRect.y + normalizedY * uvRect.height;

        return texture.GetPixelBilinear(u, v).a >= alphaThreshold;
    }

    private void WarnAboutUnreadableTexture()
    {
        if (warnedAboutUnreadableTexture)
        {
            return;
        }

        warnedAboutUnreadableTexture = true;
        Debug.LogWarning(
            "RawImageAlphaRaycastFilter needs a readable Texture2D. Enable Read/Write in the texture import settings.",
            this);
    }
}
