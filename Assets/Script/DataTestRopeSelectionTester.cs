using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class DataTestRopeSelectionTester : MonoBehaviour
{
    [Header("Test Selection")]
    [Tooltip("Enter a RopeId configured in PlayerFortuneState.")]
    [SerializeField] private int testRopeId = 1;

    [Tooltip("Enter a CardId configured in PlayerFortuneState.")]
    [SerializeField] private int testCardId = 1;

    private void Start()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError(
                "[DataTestRopeSelectionTester] PlayerFortuneState.Instance is missing.",
                this);
            return;
        }

        if (!ValidateTestIds(state))
        {
            return;
        }

        state.SelectRopeCard(testCardId, testRopeId);

        Debug.Log(
            $"[DataTestRopeSelectionTester] Test selection saved: " +
            $"RopeId={testRopeId}, CardId={testCardId}.",
            this);
    }

    private bool ValidateTestIds(PlayerFortuneState state)
    {
        bool isValid = true;

        if (!state.IsValidRopeId(testRopeId))
        {
            Debug.LogError(
                $"[DataTestRopeSelectionTester] Test Rope Id={testRopeId} is invalid. " +
                $"Enter a value from {PlayerFortuneState.MinimumSelectionId} to {state.RopeIdCount}.",
                this);
            isValid = false;
        }

        if (!state.IsValidCardId(testCardId))
        {
            Debug.LogError(
                $"[DataTestRopeSelectionTester] Test Card Id={testCardId} is invalid. " +
                $"Enter a value from {PlayerFortuneState.MinimumSelectionId} to {state.CardIdCount}.",
                this);
            isValid = false;
        }

        return isValid;
    }
}
