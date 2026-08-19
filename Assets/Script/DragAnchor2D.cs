using UnityEngine;

/// <summary>
/// Marks a 2D object as draggable by DragManager2D.
/// Attach it to the same GameObject as the object's Collider2D.
/// </summary>
public class DragAnchor2D : MonoBehaviour
{
    private const float MinimumDragDistance = 0.05f;

    [Header("Drag")]
    [SerializeField, Tooltip("Keeps the distance between the click position and this anchor while dragging. Disable to snap the anchor to the click position.")]
    private bool preserveClickOffset = true;

    [Header("Movement Bounds")]
    [SerializeField] private bool useMovementBounds;
    [SerializeField] private Vector2 movementBoundsMinOffset = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 movementBoundsMaxOffset = new Vector2(10f, 10f);

    private bool isDragging;
    private Vector3 dragOffset;
    private Rigidbody2D attachedBody;
    private Vector3 dragTargetPosition;
    private Vector3 dragStartMousePosition;
    private Vector3 currentMouseWorldPosition;
    private Vector3 movementBoundsOriginLocal;
    private float savedGravityScale;
    private bool hasMovedSinceBeginDrag;

    public bool IsDragging => isDragging;
    public bool HasMovedSinceBeginDrag => hasMovedSinceBeginDrag;
    public Vector3 CurrentMouseWorldPosition => currentMouseWorldPosition;

    private void Awake()
    {
        attachedBody = GetComponent<Rigidbody2D>();
        movementBoundsOriginLocal = transform.localPosition;
    }

    public void BeginDrag(Vector3 mouseWorldPosition)
    {
        if (attachedBody == null)
            attachedBody = GetComponent<Rigidbody2D>();

        isDragging = true;
        hasMovedSinceBeginDrag = false;
        dragStartMousePosition = mouseWorldPosition;
        currentMouseWorldPosition = mouseWorldPosition;
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
            currentMouseWorldPosition = mouseWorldPosition;

            if ((mouseWorldPosition - dragStartMousePosition).sqrMagnitude >=
                MinimumDragDistance * MinimumDragDistance)
                hasMovedSinceBeginDrag = true;

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
        hasMovedSinceBeginDrag = false;
        if (attachedBody != null)
            attachedBody.gravityScale = savedGravityScale;
    }

    private void FixedUpdate()
    {
        if (!isDragging) return;

        if (attachedBody != null)
            attachedBody.MovePosition(dragTargetPosition);
        else
            transform.position = dragTargetPosition;
    }
}
