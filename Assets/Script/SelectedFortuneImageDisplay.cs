using UnityEngine;
using UnityEngine.UI;

public sealed class SelectedFortuneImageDisplay : MonoBehaviour
{
    [Header("Display Images")]
    [SerializeField] private Image ropeDisplayImage;
    [SerializeField] private Image cardDisplayImage;

    [Header("Selection Sprites (ID Order)")]
    [Tooltip("Register sprites in RopeId order: element 0 corresponds to ID 1.")]
    [SerializeField] private Sprite[] ropeSprites = new Sprite[PlayerFortuneState.DefaultRopeIdCount];

    [Tooltip("Register sprites in CardId order: element 0 corresponds to ID 1.")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[PlayerFortuneState.DefaultCardIdCount];

    private void Start()
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError(
                "[SelectedFortuneImageDisplay] PlayerFortuneState.Instance is missing. " +
                "Make sure a PlayerFortuneState exists before opening the result screen.",
                this);
            return;
        }

        SetSpriteForId(
            state.RopeId,
            state.RopeIdCount,
            ropeSprites,
            ropeDisplayImage,
            "RopeId");

        SetSpriteForId(
            state.CardId,
            state.CardIdCount,
            cardSprites,
            cardDisplayImage,
            "CardId");
    }

    private void SetSpriteForId(
        int id,
        int expectedCount,
        Sprite[] sprites,
        Image displayImage,
        string idName)
    {
        if (displayImage == null)
        {
            Debug.LogError(
                $"[SelectedFortuneImageDisplay] The UI Image for {idName} is not assigned in the Inspector.",
                this);
            return;
        }

        if (!PlayerFortuneState.IsValidSelectionId(id, expectedCount))
        {
            Debug.LogError(
                $"[SelectedFortuneImageDisplay] {idName}={id} is outside the valid range " +
                $"({PlayerFortuneState.MinimumSelectionId}-{expectedCount}).",
                this);
            return;
        }

        int spriteIndex = id - PlayerFortuneState.MinimumSelectionId;
        if (sprites == null || spriteIndex >= sprites.Length)
        {
            int registeredCount = sprites == null ? 0 : sprites.Length;
            Debug.LogError(
                $"[SelectedFortuneImageDisplay] The sprite array for {idName} does not contain index " +
                $"{spriteIndex}. Registered sprite slots: {registeredCount}.",
                this);
            return;
        }

        Sprite selectedSprite = sprites[spriteIndex];
        if (selectedSprite == null)
        {
            Debug.LogError(
                $"[SelectedFortuneImageDisplay] No sprite is assigned for {idName}={id} " +
                $"(Inspector array element {spriteIndex}).",
                this);
            return;
        }

        displayImage.sprite = selectedSprite;
    }
}
