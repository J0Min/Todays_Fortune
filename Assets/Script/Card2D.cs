using UnityEngine;

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

    private void OnMouseUpAsButton()
    {
        if (cardFan != null)
            cardFan.SelectCard(transform);
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
