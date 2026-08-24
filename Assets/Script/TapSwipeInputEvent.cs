using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Raises one event when an enabled tap or swipe is completed.
/// Enable it from CardFan's On Card Selection Finished event after the card move ends.
/// </summary>
public sealed class TapSwipeInputEvent : MonoBehaviour
{
    [Header("Input State")]
    [SerializeField, Tooltip("Only receives input while this value is enabled.")]
    private bool inputEnabled;

    [Header("Accepted Input")]
    [SerializeField] private bool acceptTap = true;
    [SerializeField] private bool acceptSwipe = true;
    [SerializeField, Min(1f)] private float swipeDistance = 60f;

    [Header("Event")]
    [SerializeField] private UnityEvent onInputReceived;

    private Vector2 pointerStartPosition;
    private bool isPointerDown;

    /// <summary>Turns tap/swipe detection on or off.</summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        isPointerDown = false;
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        Pointer pointer = Pointer.current;
        if (pointer == null)
            return;

        if (pointer.press.wasPressedThisFrame)
        {
            pointerStartPosition = pointer.position.ReadValue();
            isPointerDown = true;
        }

        if (isPointerDown && pointer.press.wasReleasedThisFrame)
            TryReceiveInput(pointer.position.ReadValue());
    }

    private void TryReceiveInput(Vector2 pointerEndPosition)
    {
        isPointerDown = false;
        float movedDistance = Vector2.Distance(pointerStartPosition, pointerEndPosition);
        bool isSwipe = movedDistance >= swipeDistance;
        bool isTap = !isSwipe;

        if ((isTap && !acceptTap) || (isSwipe && !acceptSwipe))
            return;

        inputEnabled = false;
        onInputReceived?.Invoke();
    }
}
