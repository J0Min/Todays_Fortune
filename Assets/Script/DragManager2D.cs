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
    [Header("UI")]
    [SerializeField, Tooltip("Hidden as soon as the first rope drag begins.")]
    private GameObject initialInstructionUi;
    [SerializeField, Tooltip("Background hidden together with the initial instruction.")]
    private GameObject initialInstructionBlankUi;
    [Header("Failed Drag Return")]
    [SerializeField, Min(0f), Tooltip("Time in seconds for an unconfirmed rope end to return to its starting position.")]
    private float failedReturnDuration = 2.2f;
    [SerializeField, Min(1f), Tooltip("Power-curve exponent for failed returns. Higher values start slower and accelerate more sharply near the end.")]
    private float failedReturnAccelerationPower = 3.5f;
    [SerializeField, Range(0.5f, 0.95f), Tooltip("Normalized return time at which the rope starts braking toward zero arrival speed.")]
    private float failedReturnBrakeStart = 0.85f;
    [SerializeField] private DragAnchorConfirmedEvent onAnchorConfirmed;

    private DragAnchor2D activeAnchor;
    private DragAnchor2D lockedAnchor;
    private DragAnchor2D confirmedAnchor;
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

        if (WasLeftMousePressedThisFrame())
            TryBeginDrag();

        if (activeAnchor != null && IsLeftMousePressed())
            activeAnchor.DragTo(GetMouseWorldPosition(activeAnchor.transform.position.z));

        if (activeAnchor != null && WasLeftMouseReleasedThisFrame())
        {
            DragAnchor2D releasedAnchor = activeAnchor;
            releasedAnchor.EndDrag();
            activeAnchor = null;
            if (!TryConfirmAnchor(releasedAnchor))
                releasedAnchor.ReturnToOriginalPosition(
                    failedReturnDuration,
                    failedReturnAccelerationPower,
                    failedReturnBrakeStart);
        }
    }

    private bool TryConfirmAnchor(DragAnchor2D anchor)
    {
        if (selectionZoneLayers.value == 0) return false;

        Collider2D anchorCollider = anchor.GetComponent<Collider2D>();
        if (anchorCollider == null) return false;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(selectionZoneLayers);

        if (anchorCollider.Overlap(filter, overlapResults) == 0) return false;

        confirmedAnchor = anchor;
        Debug.Log($"Rope selected: {anchor.name}", anchor);
        onAnchorConfirmed?.Invoke(anchor);
        return true;
    }

    private void TryBeginDrag()
    {
        Vector2 mouseWorldPosition = GetMouseWorldPosition(0f);
        Collider2D hit = Physics2D.OverlapPoint(mouseWorldPosition, draggableLayers);
        if (hit == null) return;

        DragAnchor2D anchor = hit.GetComponentInParent<DragAnchor2D>();
        if (anchor == null) return;
        if (lockedAnchor != null && anchor != lockedAnchor) return;

        LockAnchor(anchor);

        if (initialInstructionUi != null)
            initialInstructionUi.SetActive(false);
        if (initialInstructionBlankUi != null)
            initialInstructionBlankUi.SetActive(false);

        activeAnchor = anchor;
        activeAnchor.BeginDrag(GetMouseWorldPosition(activeAnchor.transform.position.z));
    }

    private void LockAnchor(DragAnchor2D anchor)
    {
        if (lockedAnchor == anchor) return;

        if (lockedAnchor != null)
            lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;

        lockedAnchor = anchor;
        lockedAnchor.ReturnCompleted += HandleLockedAnchorReturnCompleted;
    }

    private void HandleLockedAnchorReturnCompleted(DragAnchor2D anchor)
    {
        if (anchor != lockedAnchor) return;

        lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;
        lockedAnchor = null;
    }

    private void OnDestroy()
    {
        if (lockedAnchor != null)
            lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;
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
