using UnityEngine;

/// <summary>
/// Saves a selection made in this scene, then starts its transition video.
/// Fortune results are resolved in the result scene.
/// </summary>
public sealed class SelectionFlow : MonoBehaviour
{
    private enum SelectionStage
    {
        Rope,
        Card
    }

    [Header("Selection")]
    [SerializeField] private SelectionStage selectionStage;
    [SerializeField] private SceneVideoController sceneVideoController;

    private bool hasHandledSelection;

    /// <summary>
    /// Connect this to DragManager2D.On Anchor Confirmed for a rope-selection scene.
    /// </summary>
    public void HandleRopeSelected()
    {
        if (selectionStage != SelectionStage.Rope)
        {
            Debug.LogError("HandleRopeSelected can only be used for the Rope selection stage.", this);
            return;
        }

        CompleteSelection();
    }

    /// <summary>
    /// Call this when any card is selected in a card-selection scene.
    /// </summary>
    public void HandleCardSelected()
    {
        if (selectionStage != SelectionStage.Card)
        {
            Debug.LogError("HandleCardSelected can only be used for the Card selection stage.", this);
            return;
        }

        CompleteSelection();
    }

    public void CompleteSelection()
    {
        if (hasHandledSelection)
            return;

        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError("SelectionFlow needs an active PlayerFortuneState.", this);
            return;
        }

        if (sceneVideoController == null)
        {
            Debug.LogError("SelectionFlow needs a SceneVideoController.", this);
            return;
        }

        int selectedId;
        if (selectionStage == SelectionStage.Rope)
        {
            state.SelectRandomRope();
            selectedId = state.RopeId;
        }
        else
        {
            state.SelectRandomCard();
            selectedId = state.CardId;
        }

        hasHandledSelection = true;
        sceneVideoController.VideoPlay(selectedId);
    }

}
