using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class DataTestRopeSelectionTester : MonoBehaviour
{
    private const int MinimumRopeId = 1;
    private const int MaximumRopeId = 5;
    private const int MinimumCardId = 1;
    private const int MaximumCardId = 12;

    [Header("Test Selection")]
    [Tooltip("Enter a RopeId from 1 to 5.")]
    [SerializeField] private int testRopeId = 1;

    [Tooltip("Enter a CardId from 1 to 12.")]
    [SerializeField] private int testCardId = 1;

    private void Start()
    {
        if (!ValidateTestIds())
        {
            return;
        }

        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError(
                "[DataTestRopeSelectionTester] PlayerFortuneState.Instance is missing.",
                this);
            return;
        }

        state.SaveSelection(testRopeId, testCardId);

        Debug.Log(
            $"[DataTestRopeSelectionTester] Test selection saved: " +
            $"RopeId={testRopeId}, CardId={testCardId}.",
            this);
    }

    private bool ValidateTestIds()
    {
        bool isValid = true;

        if (testRopeId < MinimumRopeId || testRopeId > MaximumRopeId)
        {
            Debug.LogError(
                $"[DataTestRopeSelectionTester] Test Rope Id={testRopeId} is invalid. " +
                $"Enter a value from {MinimumRopeId} to {MaximumRopeId}.",
                this);
            isValid = false;
        }

        if (testCardId < MinimumCardId || testCardId > MaximumCardId)
        {
            Debug.LogError(
                $"[DataTestRopeSelectionTester] Test Card Id={testCardId} is invalid. " +
                $"Enter a value from {MinimumCardId} to {MaximumCardId}.",
                this);
            isValid = false;
        }

        return isValid;
    }
}
