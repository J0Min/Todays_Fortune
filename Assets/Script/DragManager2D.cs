using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Central mouse-input controller for every DragAnchor2D in the scene.
/// It selects the clicked Collider2D's DragAnchor2D and moves only that one.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DragManager2D : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask draggableLayers = ~0;

    private DragAnchor2D activeAnchor;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (targetCamera == null) return;

        if (WasLeftMousePressedThisFrame())
            TryBeginDrag();

        if (activeAnchor != null && IsLeftMousePressed())
            activeAnchor.DragTo(GetMouseWorldPosition(activeAnchor.transform.position.z));

        if (activeAnchor != null && WasLeftMouseReleasedThisFrame())
        {
            activeAnchor.EndDrag();
            activeAnchor = null;
        }
    }

    private void TryBeginDrag()
    {
        Vector2 mouseWorldPosition = GetMouseWorldPosition(0f);
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPosition, draggableLayers);
        if (hit == null) return;

        DragAnchor2D anchor = hit.GetComponentInParent<DragAnchor2D>();
        if (anchor == null) return;

        activeAnchor = anchor;
        activeAnchor.BeginDrag(GetMouseWorldPosition(activeAnchor.transform.position.z));
    }

    private Vector3 GetMouseWorldPosition(float worldZ)
    {
        Vector3 screenPosition = GetMouseScreenPosition();
        screenPosition.z = Mathf.Abs(targetCamera.transform.position.z - worldZ);
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = worldZ;
        return worldPosition;
    }

    private static bool WasLeftMousePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool IsLeftMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private static bool WasLeftMouseReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current == null || Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    private static Vector3 GetMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }
}
