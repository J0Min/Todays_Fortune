using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class FortuneResultRawImage : MonoBehaviour
{
    [Tooltip("ID 1 maps to element 0, ID 2 maps to element 1, and so on.")]
    [SerializeField] private Texture[] resultImages;

    private RawImage rawImage;

    private void Awake()
    {
        TryGetComponent(out rawImage);
    }

    private void OnEnable()
    {
        if (rawImage == null && !TryGetComponent(out rawImage))
        {
            Debug.LogError("[FortuneResultRawImage] RawImage is missing.", this);
            return;
        }

        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError("[FortuneResultRawImage] PlayerFortuneState.Instance is missing.", this);
            return;
        }

        int imageIndex = state.ID - 1;
        if (resultImages == null || imageIndex < 0 || imageIndex >= resultImages.Length ||
            resultImages[imageIndex] == null)
        {
            Debug.LogError(
                $"[FortuneResultRawImage] No result image is registered for ID={state.ID}.",
                this);
            return;
        }

        rawImage.texture = resultImages[imageIndex];
    }
}
