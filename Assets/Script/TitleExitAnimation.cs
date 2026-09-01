using System;
using System.Collections;
using UnityEngine;

public sealed class TitleExitAnimation : MonoBehaviour
{
    [Header("Exit Groups")]
    [SerializeField] private RectTransform leftExitGroup;
    [SerializeField] private RectTransform rightExitGroup;
    [SerializeField] private RectTransform bottomExitGroup;

    [Header("Group Members")]
    [SerializeField] private RectTransform[] leftExitMembers;
    [SerializeField] private RectTransform[] rightExitMembers;
    [SerializeField] private RectTransform[] bottomExitMembers;

    [Header("Touch UI")]
    [SerializeField] private GameObject touchGuide;
    [SerializeField] private GameObject backgroundImage;
    [SerializeField] private LogoInkDissolve logoDissolve;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float exitDuration = 1f;

    private const float ExitMargin = 32f;
    private bool isPlaying;
    private bool isLogoDissolveComplete;
    public bool IsPlaying => isPlaying;
    public float TitleFullyHiddenDuration => logoDissolve != null
        ? logoDissolve.FullyDissolvedDuration
        : exitDuration;

    public void Play(Action onCompleted)
    {
        if (isPlaying)
        {
            return;
        }

        if (!HasRequiredReferences())
        {
            Debug.LogError("TitleExitAnimation needs all exit groups and a Canvas.", this);
            return;
        }

        isPlaying = true;
        if (touchGuide != null)
        {
            touchGuide.SetActive(false);
        }

        // The completed title is the untouched pre-input reference image. Hide it only
        // after input so the identically aligned, independently animated layers can exit.
        if (backgroundImage != null)
        {
            backgroundImage.SetActive(false);
        }

        if (logoDissolve != null)
        {
            isLogoDissolveComplete = false;
            logoDissolve.Play(OnLogoDissolveCompleted);
        }
        else
        {
            isLogoDissolveComplete = true;
        }

        ReparentMembers(leftExitMembers, leftExitGroup);
        ReparentMembers(rightExitMembers, rightExitGroup);
        ReparentMembers(bottomExitMembers, bottomExitGroup);
        StartCoroutine(PlayExit(onCompleted));
    }

    private IEnumerator PlayExit(Action onCompleted)
    {
        RectTransform canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        Vector2 leftStart = leftExitGroup.anchoredPosition;
        Vector2 rightStart = rightExitGroup.anchoredPosition;
        Vector2 bottomStart = bottomExitGroup.anchoredPosition;

        Bounds leftBounds = CalculateBounds(leftExitMembers, canvasRect);
        Bounds rightBounds = CalculateBounds(rightExitMembers, canvasRect);
        Bounds bottomBounds = CalculateBounds(bottomExitMembers, canvasRect);
        Rect canvasBounds = canvasRect.rect;

        Vector2 leftEnd = leftStart + Vector2.left *
            (leftBounds.max.x - canvasBounds.xMin + ExitMargin);
        Vector2 rightEnd = rightStart + Vector2.right *
            (canvasBounds.xMax - rightBounds.min.x + ExitMargin);
        Vector2 bottomEnd = bottomStart + Vector2.down *
            (bottomBounds.max.y - canvasBounds.yMin + ExitMargin);

        float elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            leftExitGroup.anchoredPosition = Vector2.LerpUnclamped(leftStart, leftEnd, easedT);
            rightExitGroup.anchoredPosition = Vector2.LerpUnclamped(rightStart, rightEnd, easedT);
            bottomExitGroup.anchoredPosition = Vector2.LerpUnclamped(bottomStart, bottomEnd, easedT);
            yield return null;
        }

        leftExitGroup.anchoredPosition = leftEnd;
        rightExitGroup.anchoredPosition = rightEnd;
        bottomExitGroup.anchoredPosition = bottomEnd;

        while (!isLogoDissolveComplete)
        {
            yield return null;
        }

        onCompleted?.Invoke();
    }

    private void OnLogoDissolveCompleted()
    {
        isLogoDissolveComplete = true;
    }

    private bool HasRequiredReferences()
    {
        return leftExitGroup != null && rightExitGroup != null && bottomExitGroup != null &&
            GetComponentInParent<Canvas>() != null;
    }

    private static void ReparentMembers(RectTransform[] members, RectTransform group)
    {
        if (members == null)
        {
            return;
        }

        foreach (RectTransform member in members)
        {
            if (member != null)
            {
                member.SetParent(group, true);
            }
        }
    }

    private static Bounds CalculateBounds(RectTransform[] members, RectTransform canvasRect)
    {
        bool hasPoint = false;
        Bounds bounds = new Bounds();
        Vector3[] corners = new Vector3[4];

        if (members != null)
        {
            foreach (RectTransform member in members)
            {
                if (member == null || !member.gameObject.activeInHierarchy)
                {
                    continue;
                }

                member.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 canvasPoint = canvasRect.InverseTransformPoint(corner);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(canvasPoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(canvasPoint);
                    }
                }
            }
        }

        return bounds;
    }
}
