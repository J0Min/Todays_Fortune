using UnityEngine;
using UnityEngine.UI;

public sealed class SelectedFortuneImageDisplay : MonoBehaviour
{
    private const int RopeIdMinimum = 1;
    private const int RopeIdMaximum = 5;
    private const int CardIdMinimum = 1;
    private const int CardIdMaximum = 12;

    [Header("Display Images")]
    [SerializeField] private Image ropeDisplayImage;
    [SerializeField] private Image cardDisplayImage;

    [Header("Selection Sprites (ID Order)")]
    [Tooltip("Register 5 sprites in RopeId order: elements 0-4 correspond to IDs 1-5.")]
    [SerializeField] private Sprite[] ropeSprites = new Sprite[5];

    [Tooltip("Register 12 sprites in CardId order: elements 0-11 correspond to IDs 1-12.")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[12];

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
            RopeIdMinimum,
            RopeIdMaximum,
            ropeSprites,
            ropeDisplayImage,
            "RopeId");

        SetSpriteForId(
            state.CardId,
            CardIdMinimum,
            CardIdMaximum,
            cardSprites,
            cardDisplayImage,
            "CardId");
    }

    private void SetSpriteForId(
        int id,
        int minimumId,
        int maximumId,
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

        if (id < minimumId || id > maximumId)
        {
            Debug.LogError(
                $"[SelectedFortuneImageDisplay] {idName}={id} is outside the valid range " +
                $"({minimumId}-{maximumId}).",
                this);
            return;
        }

        int spriteIndex = id - minimumId;
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
