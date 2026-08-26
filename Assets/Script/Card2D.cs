using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Added automatically to cards spawned by CardFan.
/// </summary>
public class Card2D : MonoBehaviour
{
    private CardFan cardFan;

    public void Initialize(CardFan owner)
    {
        cardFan = owner;
    }

    private void OnMouseDown()
    {
        BeginPointerHold();
    }

    private void Update()
    {
        if (Touchscreen.current == null ||
            !Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        RaycastHit2D hit = Physics2D.GetRayIntersection(
            mainCamera.ScreenPointToRay(Touchscreen.current.primaryTouch.position.ReadValue()));
        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            BeginPointerHold();
        }
    }

    private void BeginPointerHold()
    {
        if (Buttons.IsWorldInputBlocked || cardFan == null)
        {
            return;
        }

        cardFan.BeginCardPointerHold(transform);
    }

    private void OnMouseEnter()
    {
        if (cardFan != null)
            cardFan.HoverCard(transform);
    }

    private void OnMouseExit()
    {
        if (cardFan != null)
            cardFan.ClearHoverCard(transform);
    }
}
