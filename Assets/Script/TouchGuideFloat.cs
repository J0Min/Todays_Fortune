using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class TouchGuideFloat : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveDistance = 12f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;

    private RectTransform targetRectTransform;
    private Vector2 centerPosition;
    private float elapsedTime;

    private void Awake()
    {
        targetRectTransform = GetComponent<RectTransform>();
        centerPosition = targetRectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        if (targetRectTransform == null)
        {
            targetRectTransform = GetComponent<RectTransform>();
        }

        centerPosition = targetRectTransform.anchoredPosition;
        elapsedTime = 0f;
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        float verticalOffset = Mathf.Sin(elapsedTime * moveSpeed) * moveDistance;
        targetRectTransform.anchoredPosition = centerPosition + Vector2.up * verticalOffset;
    }
}
