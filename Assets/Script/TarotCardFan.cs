using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places this object's child cards in a wide, tarot-style fan.
/// Add card GameObjects as children (each with a SpriteRenderer), then attach
/// this component to their common parent.
/// </summary>
public class TarotCardFan : MonoBehaviour
{
    [Header("Cards")]
    [Tooltip("Leave empty to use every direct child as a card.")]
    [SerializeField] private Transform[] cards;

    [Header("Fan Shape")]
    [SerializeField, Min(0f)] private float width = 10f;
    [SerializeField, Range(0f, 180f)] private float fanAngle = 50f;
    [SerializeField] private float arcHeight = 1.2f;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;

    [Header("Motion")]
    [SerializeField, Min(0f), Tooltip("Travel time per card position. The stack moves continuously through the whole fan.")]
    private float placementDuration = 0.12f;
    [SerializeField] private bool unfoldOnStart = true;
    [SerializeField] private AnimationCurve unfoldEase = null;

    [Header("2D Draw Order")]
    [SerializeField] private int sortingOrder = 0;
    [SerializeField] private string sortingLayerName = "Default";

    private Coroutine unfoldRoutine;

    private void Reset()
    {
        unfoldEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Start()
    {
        if (unfoldOnStart)
            Unfold();
        else
            SnapToFan();
    }

    [ContextMenu("Unfold")]
    public void Unfold()
    {
        if (unfoldRoutine != null)
            StopCoroutine(unfoldRoutine);

        unfoldRoutine = StartCoroutine(UnfoldCards());
    }

    [ContextMenu("Snap To Fan")]
    public void SnapToFan()
    {
        List<Transform> activeCards = GetCards();
        for (int i = 0; i < activeCards.Count; i++)
        {
            ApplyTarget(activeCards[i], i, activeCards.Count);
        }
    }

    private IEnumerator UnfoldCards()
    {
        List<Transform> activeCards = GetCards();
        Vector3[] targetPositions = new Vector3[activeCards.Count];
        Quaternion[] targetRotations = new Quaternion[activeCards.Count];

        if (activeCards.Count == 0)
        {
            unfoldRoutine = null;
            yield break;
        }

        // All cards begin as one stack. It travels through every target in one
        // continuous spline motion; a card stays behind when its target is passed.
        Vector3 stackPosition = activeCards[0].localPosition;
        Quaternion stackRotation = activeCards[0].localRotation;
        for (int i = 0; i < activeCards.Count; i++)
        {
            Transform card = activeCards[i];
            card.localPosition = stackPosition;
            card.localRotation = stackRotation;
            GetTarget(i, activeCards.Count, out targetPositions[i], out targetRotations[i]);
            SetDrawOrder(card, i);
        }

        float totalDuration = placementDuration * activeCards.Count;
        float elapsed = 0f;
        int placedCount = 0;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = totalDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / totalDuration);
            float pathT = unfoldEase == null ? rawT : unfoldEase.Evaluate(rawT);
            float pathProgress = pathT * activeCards.Count;

            int segment = Mathf.Min(Mathf.FloorToInt(pathProgress), activeCards.Count - 1);
            float segmentT = pathProgress - segment;
            Vector3 currentPosition = GetSplinePosition(stackPosition, targetPositions, segment, segmentT);
            Quaternion currentRotation = GetSplineRotation(stackRotation, targetRotations, segment, segmentT);

            // Leave behind every card whose target point the stack has reached.
            int newlyPlacedCount = Mathf.Min(Mathf.FloorToInt(pathProgress), activeCards.Count);
            while (placedCount < newlyPlacedCount)
            {
                activeCards[placedCount].localPosition = targetPositions[placedCount];
                activeCards[placedCount].localRotation = targetRotations[placedCount];
                placedCount++;
            }

            // Unplaced cards remain a single stack and never stop at a card position.
            for (int i = placedCount; i < activeCards.Count; i++)
            {
                activeCards[i].localPosition = currentPosition;
                activeCards[i].localRotation = currentRotation;
            }
            yield return null;
        }

        for (int i = 0; i < activeCards.Count; i++)
        {
            activeCards[i].localPosition = targetPositions[i];
            activeCards[i].localRotation = targetRotations[i];
        }
        unfoldRoutine = null;
    }

    private static Vector3 GetSplinePosition(Vector3 start, Vector3[] targets, int segment, float t)
    {
        // The path nodes are: start -> target 0 -> target 1 -> ...
        Vector3 p1 = segment == 0 ? start : targets[segment - 1];
        Vector3 p2 = targets[segment];
        Vector3 p0 = segment == 0 ? 2f * p1 - p2 : (segment == 1 ? start : targets[segment - 2]);
        Vector3 p3 = segment + 1 < targets.Length
            ? targets[segment + 1]
            : 2f * p2 - p1;
        return CatmullRom(p0, p1, p2, p3, t);
    }

    private static Quaternion GetSplineRotation(Quaternion start, Quaternion[] targets, int segment, float t)
    {
        Quaternion from = segment == 0 ? start : targets[segment - 1];
        Quaternion to = targets[segment];
        return Quaternion.Slerp(from, to, t);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private List<Transform> GetCards()
    {
        var result = new List<Transform>();
        if (cards != null && cards.Length > 0)
        {
            foreach (Transform card in cards)
                if (card != null && card.gameObject.activeSelf) result.Add(card);
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform card = transform.GetChild(i);
                if (card.gameObject.activeSelf) result.Add(card);
            }
        }
        return result;
    }

    private void ApplyTarget(Transform card, int index, int count)
    {
        GetTarget(index, count, out Vector3 position, out Quaternion rotation);
        card.localPosition = position;
        card.localRotation = rotation;
        SetDrawOrder(card, index);
    }

    private void GetTarget(int index, int count, out Vector3 position, out Quaternion rotation)
    {
        float t = count <= 1 ? 0.5f : (float)index / (count - 1);
        float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
        // The middle cards sit highest, giving the row a gentle upward arc.
        float y = arcHeight * (1f - 4f * Mathf.Pow(t - 0.5f, 2f));
        float zAngle = Mathf.Lerp(fanAngle * 0.5f, -fanAngle * 0.5f, t);

        position = new Vector3(x + centerOffset.x, y + centerOffset.y, 0f);
        rotation = Quaternion.Euler(0f, 0f, zAngle);
    }

    private void SetDrawOrder(Transform card, int index)
    {
        SpriteRenderer spriteRenderer = card.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder + index;
    }
}
