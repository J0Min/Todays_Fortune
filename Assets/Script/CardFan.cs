using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Places this object's child cards in a wide, tarot-style fan.
/// Add card GameObjects as children (each with a SpriteRenderer), then attach
/// this component to their common parent.
/// </summary>
public class CardFan : MonoBehaviour
{
    [Header("Card Spawn")]
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField, Min(1)] private int cardCount = PlayerFortuneState.DefaultCardIdCount;

    [Header("Fan Shape")]
    [SerializeField, Min(0f)] private float width = 10f;
    [SerializeField, Range(0f, 180f)] private float fanAngle = 50f;
    [SerializeField] private float arcHeight = 1.2f;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;

    [Header("Motion")]
    [SerializeField, Min(0f), Tooltip("Travel time per card position. The stack moves continuously through the whole fan.")]
    private float placementDuration = 0.12f;
    [SerializeField] private bool unfoldOnStart = true;
    [SerializeField, Tooltip("Starts the stacked cards at the first card's fan position and angle instead of their current pose.")]
    private bool startAtFirstCardFanAngle = true;
    [SerializeField, Min(0f), Tooltip("Time to wait after the initial stacked-card pose is shown, before unfolding begins.")]
    private float unfoldStartDelay = 0f;
    [SerializeField] private AnimationCurve unfoldEase = null;

    [Header("Card Selection")]
    [SerializeField, Tooltip("A child Transform that defines where the selected card moves after selection.")]
    private Transform selectedCardPivot;
    [SerializeField, Min(0f)] private float selectionDuration = 0.35f;
    [SerializeField, Tooltip("Local scale applied to the selected card at the selection pivot.")]
    private Vector3 selectedCardScale = Vector3.one;
    [SerializeField, Tooltip("Sprite shown on the selected card before it moves to the selection pivot.")]
    private Sprite selectedCardSprite;
    [SerializeField] private UnityEvent onCardSelectionFinished;

    [Header("Card Hover")]
    [SerializeField] private bool enableHover = true;
    [SerializeField, Min(0f), Tooltip("How far the left and right card groups separate from the hovered card along the fan curve.")]
    private float hoverSpreadDistance = 0.4f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.15f;

    [Header("2D Draw Order")]
    [SerializeField] private int sortingOrder = 0;
    [SerializeField] private string sortingLayerName = "Default";

    private Coroutine unfoldRoutine;
    private Coroutine selectionRoutine;
    private Coroutine hoverRoutine;
    private readonly List<Transform> spawnedCards = new List<Transform>();
    private bool isUnfolding;
    private bool cardSelected;
    private bool isCardPointerHeld;
    private bool pointerStartedOnPendingCard;
    private Transform pendingSelectionCard;
    private Transform hoveredCard;

    private void Reset()
    {
        unfoldEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Start()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state != null)
        {
            cardCount = state.CardIdCount;
        }

        SpawnCards();

        if (unfoldOnStart)
            Unfold();
        else
            SnapToFan();
    }

    private void Update()
    {
        bool mouseReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        bool touchReleased = Touchscreen.current != null &&
                             Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        if (!isCardPointerHeld || (!mouseReleased && !touchReleased)) return;

        isCardPointerHeld = false;
        if (hoveredCard == null) return;

        if (pointerStartedOnPendingCard && hoveredCard == pendingSelectionCard)
        {
            SelectCard(hoveredCard);
        }
        else
        {
            pendingSelectionCard = hoveredCard;
        }

        pointerStartedOnPendingCard = false;
    }

    [ContextMenu("Spawn Cards")]
    public void SpawnCards()
    {
        ClearSpawnedCards();
        cardSelected = false;
        isCardPointerHeld = false;
        pointerStartedOnPendingCard = false;
        pendingSelectionCard = null;

        if (cardBackSprite == null)
        {
            Debug.LogWarning("CardFan needs a Card Back Sprite before it can spawn cards.", this);
            return;
        }

        int count = Mathf.Max(1, cardCount);
        for (int i = 0; i < count; i++)
        {
            GameObject cardObject = new GameObject($"Card {i + 1:00}");
            cardObject.transform.SetParent(transform, false);

            SpriteRenderer spriteRenderer = cardObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = cardBackSprite;

            BoxCollider2D cardCollider = cardObject.AddComponent<BoxCollider2D>();
            cardCollider.size = cardBackSprite.bounds.size;

            Card2D card = cardObject.AddComponent<Card2D>();
            card.Initialize(this);
            spawnedCards.Add(cardObject.transform);
        }
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

    public void FocusCard(Transform selectedCard)
    {
        if (selectedCard == null || selectedCardPivot == null) return;

        if (selectionRoutine != null)
            StopCoroutine(selectionRoutine);

        selectionRoutine = StartCoroutine(FocusCardRoutine(selectedCard));
    }

    public void SelectCard(Transform selectedCard)
    {
        if (isUnfolding || cardSelected || !spawnedCards.Contains(selectedCard)) return;

        cardSelected = true;
        pendingSelectionCard = null;
        hoveredCard = null;
        if (hoverRoutine != null)
            StopCoroutine(hoverRoutine);

        if (selectedCardSprite != null)
        {
            SpriteRenderer spriteRenderer = selectedCard.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = selectedCardSprite;
        }

        FocusCard(selectedCard);
    }

    public void BeginCardPointerHold(Transform card)
    {
        if (isUnfolding || cardSelected || !spawnedCards.Contains(card)) return;

        isCardPointerHeld = true;
        pointerStartedOnPendingCard = card == pendingSelectionCard;

        if (card == hoveredCard) return;

        hoveredCard = card;
        StartHoverLayoutAnimation();
    }

    public void HoverCard(Transform card)
    {
        if (!enableHover || isUnfolding || cardSelected || (!isCardPointerHeld && pendingSelectionCard != null) || card == hoveredCard || !spawnedCards.Contains(card)) return;

        hoveredCard = card;
        StartHoverLayoutAnimation();
    }

    public void ClearHoverCard(Transform card)
    {
        if (card != hoveredCard || cardSelected || isCardPointerHeld || card == pendingSelectionCard) return;

        hoveredCard = null;
        StartHoverLayoutAnimation();
    }

    private IEnumerator FocusCardRoutine(Transform selectedCard)
    {
        List<Transform> activeCards = GetCards();
        Vector3 selectedStartPosition = selectedCard.localPosition;
        Quaternion selectedStartRotation = selectedCard.localRotation;
        Vector3 selectedStartScale = selectedCard.localScale;
        Color[] otherStartColors = new Color[activeCards.Count];

        for (int i = 0; i < activeCards.Count; i++)
        {
            if (activeCards[i] == selectedCard) continue;

            SpriteRenderer spriteRenderer = activeCards[i].GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                otherStartColors[i] = spriteRenderer.color;
        }

        float elapsed = 0f;
        while (elapsed < selectionDuration)
        {
            elapsed += Time.deltaTime;
            float t = selectionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / selectionDuration);
            selectedCard.localPosition = Vector3.Lerp(selectedStartPosition, selectedCardPivot.localPosition, t);
            selectedCard.localRotation = Quaternion.Slerp(selectedStartRotation, selectedCardPivot.localRotation, t);
            selectedCard.localScale = Vector3.Lerp(selectedStartScale, selectedCardScale, t);

            for (int i = 0; i < activeCards.Count; i++)
            {
                if (activeCards[i] == selectedCard) continue;

                SpriteRenderer spriteRenderer = activeCards[i].GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) continue;

                Color color = otherStartColors[i];
                color.a *= 1f - t;
                spriteRenderer.color = color;
            }
            yield return null;
        }

        selectedCard.localPosition = selectedCardPivot.localPosition;
        selectedCard.localRotation = selectedCardPivot.localRotation;
        selectedCard.localScale = selectedCardScale;
        selectionRoutine = null;
        onCardSelectionFinished?.Invoke();
    }

    private void StartHoverLayoutAnimation()
    {
        if (hoverRoutine != null)
            StopCoroutine(hoverRoutine);

        hoverRoutine = StartCoroutine(AnimateHoverLayout());
    }

    private IEnumerator AnimateHoverLayout()
    {
        List<Transform> activeCards = GetCards();
        Vector3[] startPositions = new Vector3[activeCards.Count];
        Vector3[] targetPositions = new Vector3[activeCards.Count];
        Quaternion[] startRotations = new Quaternion[activeCards.Count];
        Quaternion[] targetRotations = new Quaternion[activeCards.Count];
        int hoveredIndex = hoveredCard == null ? -1 : activeCards.IndexOf(hoveredCard);
        float normalizedSpread = width > 0.0001f ? hoverSpreadDistance / width : 0f;

        for (int i = 0; i < activeCards.Count; i++)
        {
            startPositions[i] = activeCards[i].localPosition;
            startRotations[i] = activeCards[i].localRotation;
            float t = activeCards.Count <= 1 ? 0.5f : (float)i / (activeCards.Count - 1);

            if (hoveredIndex >= 0 && i != hoveredIndex)
                t += Mathf.Sign(i - hoveredIndex) * normalizedSpread;

            GetTargetAtNormalizedPosition(t, out targetPositions[i], out targetRotations[i]);

            if (hoveredIndex >= 0 && i != hoveredIndex && width <= 0.0001f)
                targetPositions[i].x += Mathf.Sign(i - hoveredIndex) * hoverSpreadDistance;
        }

        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = hoverDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / hoverDuration);
            for (int i = 0; i < activeCards.Count; i++)
            {
                activeCards[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                activeCards[i].localRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
            }
            yield return null;
        }

        for (int i = 0; i < activeCards.Count; i++)
        {
            activeCards[i].localPosition = targetPositions[i];
            activeCards[i].localRotation = targetRotations[i];
        }
        hoverRoutine = null;
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

        isUnfolding = true;

        // All cards begin as one stack. It travels through every target in one
        // continuous spline motion; a card stays behind when its target is passed.
        Vector3 stackPosition = activeCards[0].localPosition;
        Quaternion stackRotation = activeCards[0].localRotation;
        for (int i = 0; i < activeCards.Count; i++)
        {
            GetTarget(i, activeCards.Count, out targetPositions[i], out targetRotations[i]);
        }

        if (startAtFirstCardFanAngle)
        {
            stackPosition = targetPositions[0];
            stackRotation = targetRotations[0];
        }

        for (int i = 0; i < activeCards.Count; i++)
        {
            Transform card = activeCards[i];
            card.localPosition = stackPosition;
            card.localRotation = stackRotation;
            SetDrawOrder(card, i);
        }

        if (unfoldStartDelay > 0f)
            yield return new WaitForSeconds(unfoldStartDelay);

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
        isUnfolding = false;
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
        for (int i = spawnedCards.Count - 1; i >= 0; i--)
        {
            Transform card = spawnedCards[i];
            if (card == null)
            {
                spawnedCards.RemoveAt(i);
                continue;
            }

            if (card.gameObject.activeSelf)
                result.Add(card);
        }
        result.Reverse();
        return result;
    }

    private void ClearSpawnedCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
                Destroy(spawnedCards[i].gameObject);
        }
        spawnedCards.Clear();
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
        GetTargetAtNormalizedPosition(t, out position, out rotation);
    }

    private void GetTargetAtNormalizedPosition(float t, out Vector3 position, out Quaternion rotation)
    {
        float x = Mathf.LerpUnclamped(-width * 0.5f, width * 0.5f, t);
        // The middle cards sit highest, giving the row a gentle upward arc.
        float y = arcHeight * (1f - 4f * Mathf.Pow(t - 0.5f, 2f));
        float zAngle = Mathf.LerpUnclamped(fanAngle * 0.5f, -fanAngle * 0.5f, t);

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
