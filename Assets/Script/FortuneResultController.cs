using TMPro;
using UnityEngine;

public sealed class FortuneResultController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private FortuneDataReader fortuneDataReader;

    [Header("Result Text")]
    [SerializeField] private TMP_Text ropeText;
    [SerializeField] private TMP_Text cardText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    private void Start()
    {
        RefreshResult();
    }

    public void RefreshResult()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state == null)
        {
            Debug.LogError(
                "[FortuneResultController] PlayerFortuneState.Instance is missing.",
                this);
            return;
        }

        if (fortuneDataReader == null)
        {
            Debug.LogError(
                "[FortuneResultController] FortuneDataReader is not assigned.",
                this);
            return;
        }

        if (!fortuneDataReader.TryGetFortune(
                state.RopeId,
                state.CardId,
                out FortuneDataReader.FortuneData fortune))
        {
            return;
        }

        state.SetFortuneResult(fortune);

        SetText(ropeText, fortune.Rope);
        SetText(cardText, fortune.Card);
        SetText(titleText, fortune.Title);
        SetText(descriptionText, fortune.Description);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
