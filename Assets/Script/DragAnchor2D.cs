using System;
using UnityEngine;

/// <summary>
/// Marks a 2D object as draggable by DragManager2D.
/// Attach it to the same GameObject as the object's Collider2D.
/// </summary>
[DefaultExecutionOrder(-50)]
public class DragAnchor2D : MonoBehaviour
{
    [Header("Drag")]
    [SerializeField, Tooltip("Keeps the distance between the click position and this anchor while dragging. Disable to snap the anchor to the click position.")]
    private bool preserveClickOffset = true;

    [Header("Movement Bounds")]
    [SerializeField] private bool useMovementBounds;
    [SerializeField] private Vector2 movementBoundsMinOffset = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 movementBoundsMaxOffset = new Vector2(10f, 10f);

    private bool isDragging;
    private bool isReturning;
    private Vector3 dragOffset;
    private Rigidbody2D attachedBody;
    private Vector3 dragTargetPosition;
    private Vector3 movementBoundsOriginLocal;
    private Vector3 returnStartLocalPosition;
    private float returnElapsed;
    private float activeReturnDuration;
    private float activeReturnAccelerationPower = 1f;
    private float activeReturnBrakeStart = 0.85f;
    private float returnProgress;
    private float savedGravityScale;

    public bool IsDragging => isDragging;
    public bool IsReturning => isReturning;
    public float ReturnProgress => returnProgress;
    public event Action<DragAnchor2D> ReturnCompleted;

    private void Awake()
    {
        attachedBody = GetComponent<Rigidbody2D>();
        movementBoundsOriginLocal = transform.localPosition;
    }

    public void BeginDrag(Vector3 mouseWorldPosition)
    {
        isReturning = false;
        returnElapsed = 0f;
        returnProgress = 0f;

        if (attachedBody == null)
            attachedBody = GetComponent<Rigidbody2D>();

        isDragging = true;
        dragOffset = preserveClickOffset
            ? transform.position - mouseWorldPosition
            : Vector3.zero;
        dragTargetPosition = transform.position;

        if (attachedBody != null)
        {
            savedGravityScale = attachedBody.gravityScale;
            attachedBody.gravityScale = 0f;
            attachedBody.linearVelocity = Vector2.zero;
        }
    }

    public void DragTo(Vector3 mouseWorldPosition)
    {
        if (isDragging)
        {
            Vector3 targetPosition = mouseWorldPosition + dragOffset;

            if (useMovementBounds)
            {
                Transform parent = transform.parent;
                Vector3 targetLocalPosition = parent != null
                    ? parent.InverseTransformPoint(targetPosition)
                    : targetPosition;

                targetLocalPosition.x = Mathf.Clamp(
                    targetLocalPosition.x,
                    movementBoundsOriginLocal.x + movementBoundsMinOffset.x,
                    movementBoundsOriginLocal.x + movementBoundsMaxOffset.x);
                targetLocalPosition.y = Mathf.Clamp(
                    targetLocalPosition.y,
                    movementBoundsOriginLocal.y + movementBoundsMinOffset.y,
                    movementBoundsOriginLocal.y + movementBoundsMaxOffset.y);

                targetPosition = parent != null
                    ? parent.TransformPoint(targetLocalPosition)
                    : targetLocalPosition;
            }

            dragTargetPosition = targetPosition;
        }
    }

    public void EndDrag()
    {
        isDragging = false;
        if (attachedBody != null)
            attachedBody.gravityScale = savedGravityScale;
    }

    public void ReturnToOriginalPosition(
        float duration,
        float accelerationPower,
        float brakeStart)
    {
        isDragging = false;
        isReturning = true;
        returnStartLocalPosition = transform.localPosition;
        returnElapsed = 0f;
        returnProgress = 0f;
        activeReturnDuration = Mathf.Max(0f, duration);
        activeReturnAccelerationPower = Mathf.Max(1f, accelerationPower);
        activeReturnBrakeStart = Mathf.Clamp(brakeStart, 0.5f, 0.95f);

        if (attachedBody != null)
            attachedBody.linearVelocity = Vector2.zero;

        if (activeReturnDuration <= 0f)
            CompleteReturn();
    }

    private void FixedUpdate()
    {
        if (isReturning)
        {
            returnElapsed += Time.fixedDeltaTime;
            float t = activeReturnDuration <= 0f
                ? 1f
                : Mathf.Clamp01(returnElapsed / activeReturnDuration);
            float easedT = EvaluateReturnProgress(t);
            returnProgress = easedT;
            MoveToLocalPosition(Vector3.Lerp(
                returnStartLocalPosition,
                movementBoundsOriginLocal,
                easedT));

            if (t >= 1f)
                CompleteReturn();

            return;
        }

        if (!isDragging) return;

        if (attachedBody != null)
            attachedBody.MovePosition(dragTargetPosition);
        else
            transform.position = dragTargetPosition;
    }

    private void CompleteReturn()
    {
        if (activeReturnDuration <= 0f)
            MoveToLocalPosition(movementBoundsOriginLocal);

        dragTargetPosition = transform.position;
        returnProgress = 1f;
        isReturning = false;
        ReturnCompleted?.Invoke(this);
    }

    private float EvaluateReturnProgress(float t)
    {
        if (t <= activeReturnBrakeStart)
            return Mathf.Pow(t, activeReturnAccelerationPower);

        float brakeDuration = 1f - activeReturnBrakeStart;
        float brakeT = (t - activeReturnBrakeStart) / brakeDuration;
        float brakeStartPosition = Mathf.Pow(
            activeReturnBrakeStart,
            activeReturnAccelerationPower);
        float brakeStartSlope = activeReturnAccelerationPower * Mathf.Pow(
            activeReturnBrakeStart,
            activeReturnAccelerationPower - 1f);
        float brakeStartTangent = brakeStartSlope * brakeDuration;

        float brakeT2 = brakeT * brakeT;
        float brakeT3 = brakeT2 * brakeT;
        float startPositionWeight = 2f * brakeT3 - 3f * brakeT2 + 1f;
        float startTangentWeight = brakeT3 - 2f * brakeT2 + brakeT;
        float endPositionWeight = -2f * brakeT3 + 3f * brakeT2;

        return startPositionWeight * brakeStartPosition +
               startTangentWeight * brakeStartTangent +
               endPositionWeight;
    }

    private void MoveToLocalPosition(Vector3 localPosition)
    {
        Transform parent = transform.parent;
        Vector3 worldPosition = parent != null
            ? parent.TransformPoint(localPosition)
            : localPosition;

        if (attachedBody != null)
            attachedBody.position = worldPosition;
        else
            transform.position = worldPosition;
    }
}
