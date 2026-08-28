using System.Collections;
using UnityEngine;

/// <summary>
/// Moves this transform to the first card position of a CardFan and back.
/// </summary>
public sealed class HandMovingCardFan : MonoBehaviour
{
    [SerializeField] private CardFan cardFan;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float moveToFirstCardDuration = 0.5f;
    [SerializeField, Min(0f)] private float holdAtFirstCardDuration = 0.5f;
    [SerializeField, Min(0f)] private float moveBetweenCardPositionsDuration = 0.5f;
    [SerializeField, Min(0f)] private float returnToStartDuration = 0.5f;
    [SerializeField] private Vector3 returnPositionOffset = Vector3.zero;
    [SerializeField] private AnimationCurve movementEase = null;

    private Coroutine movementRoutine;
    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void OnEnable()
    {
        MoveToFirstCardPosition();
    }

    private void Reset()
    {
        movementEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public void MoveToFirstCardPosition()
    {
        if (cardFan == null)
        {
            Debug.LogError("HandMovingCardFan needs a CardFan reference.", this);
            return;
        }

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }

        movementRoutine = StartCoroutine(MoveToFirstCardAndReturn());
    }

    public void ReturnToStartPosition()
    {
        StartMovement(startPosition + returnPositionOffset, returnToStartDuration);
    }

    private void StartMovement(Vector3 targetPosition, float duration)
    {
        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
        }

        movementRoutine = StartCoroutine(MoveToPositionAndFinish(targetPosition, duration));
    }

    private IEnumerator MoveToFirstCardAndReturn()
    {
        yield return MoveToPosition(cardFan.GetFirstCardTargetPosition(), moveToFirstCardDuration);
        cardFan.Unfold();

        if (holdAtFirstCardDuration > 0f)
        {
            yield return new WaitForSeconds(holdAtFirstCardDuration);
        }

        int targetCount = cardFan.GetCardTargetCount();
        for (int i = 1; i < targetCount; i++)
        {
            yield return MoveToPosition(
                cardFan.GetCardTargetPosition(i),
                moveBetweenCardPositionsDuration);
        }

        yield return MoveToPosition(startPosition + returnPositionOffset, returnToStartDuration);
        movementRoutine = null;
    }

    private IEnumerator MoveToPositionAndFinish(Vector3 targetPosition, float duration)
    {
        yield return MoveToPosition(targetPosition, duration);
        movementRoutine = null;
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition, float duration)
    {
        Vector3 initialPosition = transform.position;
        if (duration <= 0f)
        {
            transform.position = targetPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = movementEase == null
                ? Mathf.SmoothStep(0f, 1f, progress)
                : movementEase.Evaluate(progress);
            transform.position = Vector3.LerpUnclamped(initialPosition, targetPosition, easedProgress);
            yield return null;
        }

        transform.position = targetPosition;
    }
}
