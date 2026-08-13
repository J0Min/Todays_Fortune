using UnityEngine;

/// <summary>
/// Added automatically to cards spawned by TarotCardFan.
/// </summary>
public class TarotCard2D : MonoBehaviour
{
    private TarotCardFan cardFan;

    public void Initialize(TarotCardFan owner)
    {
        cardFan = owner;
    }

    private void OnMouseUpAsButton()
    {
        if (cardFan != null)
            cardFan.SelectCard(transform);
    }
}
