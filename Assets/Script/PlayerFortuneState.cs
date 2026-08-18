using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerFortuneState : MonoBehaviour
{
    [Header("Inactivity Reset")]
    [SerializeField] private string startSceneName = "StartScene";

    [SerializeField] private bool runResetTestOnStart = true;
    [Header("Random Selection")]
    [SerializeField, Min(1)] private int ropeIdCount = 5;
    [SerializeField, Min(1)] private int cardIdCount = 12;

    public static PlayerFortuneState Instance { get; private set; }

    public int RopeId { get; private set; }
    public int CardId { get; private set; }
    public FortuneDataReader.FortuneData FortuneResult { get; private set; }
    public int RopeIdCount => ropeIdCount;

    private InactivityTimer inactivityTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inactivityTimer = GetComponent<InactivityTimer>();
    }

    private void OnEnable()
    {
        if (Instance != this || inactivityTimer == null)
        {
            return;
        }

        inactivityTimer.TimedOut += ReturnToStartAfterInactivity;
    }

    private void OnDisable()
    {
        if (inactivityTimer != null)
        {
            inactivityTimer.TimedOut -= ReturnToStartAfterInactivity;
        }
    }

    public void SelectRandomRope()
    {
        RopeId = Random.Range(1, ropeIdCount + 1);
    }

    public void SelectRandomCard()
    {
        CardId = Random.Range(1, cardIdCount + 1);
    }

    public void SelectRopeCard(int cardId, int ropeId)
    {
        CardId=cardId;
        RopeId=ropeId;
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

    private void ReturnToStartAfterInactivity()
    {
        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError(
                $"[PlayerFortuneState] '{startSceneName}' Scene을 로드할 수 없습니다. " +
                "Scene 이름과 Build Settings 등록 여부를 확인해주세요.",
                this);
            return;
        }

        ResetData();
        SceneManager.LoadScene(startSceneName);
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
        SelectRandomRope();
        SelectRandomCard();
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
