using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public class DragAnchorConfirmedEvent : UnityEvent<DragAnchor2D> { }

/// <summary>
/// Central mouse-input controller for every DragAnchor2D in the scene.
/// It selects the clicked Collider2D's DragAnchor2D and moves only that one.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DragManager2D : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask draggableLayers = ~0;
    [Header("Selection")]
    [SerializeField, Tooltip("A released rope is confirmed only when its Collider2D overlaps a collider on one of these layers.")]
    private LayerMask selectionZoneLayers;
    [SerializeField, Tooltip("Prevents dragging another rope after one has been confirmed.")]
    private bool lockAfterSelection = true;
    [SerializeField] private DragAnchorConfirmedEvent onAnchorConfirmed;

    private DragAnchor2D activeAnchor;
    private DragAnchor2D confirmedAnchor;
    private bool activeDragUsesTouch;
    private readonly Collider2D[] overlapResults = new Collider2D[8];

    public DragAnchor2D ConfirmedAnchor => confirmedAnchor;
    public bool HasConfirmedAnchor => confirmedAnchor != null;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (targetCamera == null) return;
        if (lockAfterSelection && HasConfirmedAnchor) return;

        if (activeAnchor == null && TryGetPrimaryInputPressedThisFrame(
                out Vector2 pressedScreenPosition, out bool usesTouch))
            TryBeginDrag(pressedScreenPosition, usesTouch);

        if (activeAnchor != null && IsActiveDragInputPressed())
            activeAnchor.DragTo(GetScreenWorldPosition(
                GetActiveDragScreenPosition(), activeAnchor.transform.position.z));

        if (activeAnchor != null && WasActiveDragInputReleasedThisFrame())
        {
            DragAnchor2D releasedAnchor = activeAnchor;
            releasedAnchor.EndDrag();
            activeAnchor = null;
            TryConfirmAnchor(releasedAnchor);
        }
    }

    private void TryConfirmAnchor(DragAnchor2D anchor)
    {
        if (selectionZoneLayers.value == 0) return;

        Collider2D anchorCollider = anchor.GetComponent<Collider2D>();
        if (anchorCollider == null) return;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(selectionZoneLayers);

        if (anchorCollider.Overlap(filter, overlapResults) == 0) return;

        confirmedAnchor = anchor;
        Debug.Log($"Rope selected: {anchor.name}", anchor);
        onAnchorConfirmed?.Invoke(anchor);
    }

    private void TryBeginDrag(Vector2 screenPosition, bool usesTouch)
    {
        Vector2 worldPosition = GetScreenWorldPosition(screenPosition, 0f);
        Collider2D hit = Physics2D.OverlapPoint(worldPosition, draggableLayers);
        if (hit == null) return;

        DragAnchor2D anchor = hit.GetComponentInParent<DragAnchor2D>();
        if (anchor == null) return;

        activeAnchor = anchor;
        activeDragUsesTouch = usesTouch;
        activeAnchor.BeginDrag(GetScreenWorldPosition(
            screenPosition, activeAnchor.transform.position.z));
    }

    private Vector3 GetScreenWorldPosition(Vector2 screenPosition, float worldZ)
    {
        Vector3 cameraScreenPosition = screenPosition;
        cameraScreenPosition.z = Mathf.Abs(targetCamera.transform.position.z - worldZ);
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(cameraScreenPosition);
        worldPosition.z = worldZ;
        return worldPosition;
    }

    private static bool TryGetPrimaryInputPressedThisFrame(out Vector2 screenPosition, out bool usesTouch)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            usesTouch = false;
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            usesTouch = true;
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            usesTouch = false;
            return true;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
            usesTouch = true;
            return true;
        }
#endif
        screenPosition = default;
        usesTouch = false;
        return false;
    }

    private bool IsActiveDragInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch
            ? Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed
            : Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return activeDragUsesTouch
            ? Input.touchCount > 0 && Input.GetTouch(0).phase != TouchPhase.Ended &&
              Input.GetTouch(0).phase != TouchPhase.Canceled
            : Input.GetMouseButton(0);
#endif
    }

    private bool WasActiveDragInputReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch
            ? Touchscreen.current == null || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame
            : Mouse.current == null || Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return activeDragUsesTouch
            ? Input.touchCount == 0 || Input.GetTouch(0).phase == TouchPhase.Ended ||
              Input.GetTouch(0).phase == TouchPhase.Canceled
            : Input.GetMouseButtonUp(0);
#endif
    }

    private Vector2 GetActiveDragScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch && Touchscreen.current != null
            ? Touchscreen.current.primaryTouch.position.ReadValue()
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return activeDragUsesTouch && Input.touchCount > 0
            ? Input.GetTouch(0).position
            : Input.mousePosition;
#endif
    }
}
