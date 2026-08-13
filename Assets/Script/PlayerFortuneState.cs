using UnityEngine;

public sealed class PlayerFortuneState : MonoBehaviour
{
    [SerializeField] private bool runResetTestOnStart = true;

    public int RopeId { get; private set; }
    public int CardId { get; private set; }
    public FortuneDataReader.FortuneData FortuneResult { get; private set; }

    public void SaveSelection(int ropeId, int cardId)
    {
        RopeId = ropeId;
        CardId = cardId;
    }

    public void SetFortuneResult(FortuneDataReader.FortuneData fortuneResult)
    {
        FortuneResult = fortuneResult;
    }

    public void ResetData()
    {
        RopeId = 0;
        CardId = 0;
        FortuneResult = null;
    }

    private void Start()
    {
        if (runResetTestOnStart)
        {
            RunResetTest();
        }
    }

    private void RunResetTest()
    {
        SaveSelection(1, 3);
        SetFortuneResult(new FortuneDataReader.FortuneData(
            1,
            "테스트 동아줄",
            3,
            "테스트 카드",
            "테스트 운세",
            "초기화 확인용 임시 데이터"));

        ResetData();

        string fortuneResultText = FortuneResult == null ? "null" : FortuneResult.Title;
        Debug.Log(
            $"[PlayerFortuneState Reset Test]\n" +
            $"rope_id = {RopeId}\n" +
            $"card_id = {CardId}\n" +
            $"fortune result = {fortuneResultText}",
            this);
    }
}
