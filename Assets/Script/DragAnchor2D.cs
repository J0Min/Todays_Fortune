using UnityEngine;

/// <summary>
/// Marks a 2D object as draggable by DragManager2D.
/// Attach it to the same GameObject as the object's Collider2D.
/// </summary>
public class DragAnchor2D : MonoBehaviour
{
    private bool isDragging;
    private Vector3 dragOffset;
    private Rigidbody2D attachedBody;
    private Vector3 dragTargetPosition;
    private float savedGravityScale;

    public bool IsDragging => isDragging;

    private void Awake()
    {
        attachedBody = GetComponent<Rigidbody2D>();
    }

    public void BeginDrag(Vector3 mouseWorldPosition)
    {
        if (attachedBody == null)
            attachedBody = GetComponent<Rigidbody2D>();

        isDragging = true;
        dragOffset = transform.position - mouseWorldPosition;
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
            dragTargetPosition = mouseWorldPosition + dragOffset;
    }

    public void EndDrag()
    {
        isDragging = false;
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
