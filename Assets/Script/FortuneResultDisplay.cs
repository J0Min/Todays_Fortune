using UnityEngine;

public sealed class FortuneResultDisplay : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private FortuneDataReader fortuneDataReader;

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
                "[FortuneResultDisplay] PlayerFortuneState.Instance is missing. " +
                "Open the result screen after the selection flow, or add a PlayerFortuneState for testing.",
                this);
            return;
        }

        FortuneDataReader.FortuneData fortune = state.FortuneResult;
        if (fortune == null)
        {
            if (fortuneDataReader == null)
            {
                Debug.LogError(
                    "[FortuneResultDisplay] FortuneResult is empty and Fortune Data Reader is not assigned.",
                    this);
                return;
            }

            if (state.RopeId < 1 || state.RopeId > 5)
            {
                Debug.LogError(
                    $"[FortuneResultDisplay] RopeId={state.RopeId} is outside the valid range (1-5).",
                    this);
                return;
            }

            if (state.CardId < 1 || state.CardId > 12)
            {
                Debug.LogError(
                    $"[FortuneResultDisplay] CardId={state.CardId} is outside the valid range (1-12).",
                    this);
                return;
            }

            if (!fortuneDataReader.TryGetFortune(state.RopeId, state.CardId, out fortune))
            {
                Debug.LogError(
                    $"[FortuneResultDisplay] No fortune data was found for " +
                    $"RopeId={state.RopeId}, CardId={state.CardId}.",
                    this);
                return;
            }

            state.SetFortuneResult(fortune);
        }

    }
}
