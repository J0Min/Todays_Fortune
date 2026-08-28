using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerFortuneState : MonoBehaviour
{
    public const int MinimumSelectionId = 1;
    public const int DefaultRopeIdCount = 5;
    public const int DefaultCardIdCount = 12;

    [Header("Inactivity Reset")]
    [SerializeField] private string startSceneName = "StartScene";

    [SerializeField] private bool runResetTestOnStart = true;
    [Header("Random Selection")]
    [SerializeField, Min(1)] private int ropeIdCount = DefaultRopeIdCount;
    [SerializeField, Min(1)] private int cardIdCount = DefaultCardIdCount;

    [HideInInspector]
    [SerializeField] private string testSceneName;

    public static PlayerFortuneState Instance { get; private set; }

    public int RopeId { get; private set; }
    public int CardId { get; private set; }
    public int ID { get; private set; }
    public FortuneDataReader.FortuneData FortuneResult { get; private set; }
    public bool IsMuted { get; private set; }
    public int RopeIdCount => ropeIdCount;
    public int CardIdCount => cardIdCount;

    public bool IsValidRopeId(int ropeId) => IsValidSelectionId(ropeId, RopeIdCount);
    public bool IsValidCardId(int cardId) => IsValidSelectionId(cardId, CardIdCount);

    public static bool IsValidSelectionId(int id, int selectionCount)
    {
        return id >= MinimumSelectionId && id <= selectionCount;
    }

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
        Application.targetFrameRate = 60;
        if (Instance != this)
        {
            return;
        }

        if (inactivityTimer != null)
        {
            inactivityTimer.TimedOut += ReturnToStartAfterInactivity;
        }

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
        RopeId = Random.Range(MinimumSelectionId, ropeIdCount + 1);
    }

    public void SelectRandomCard()
    {
        CardId = Random.Range(MinimumSelectionId, cardIdCount + 1);
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

    public void SetID(int id)
    {
        ID = id;
    }

    public void SetMuted(bool isMuted)
    {
        IsMuted = isMuted;
    }

    public void ResetData()
    {
        RopeId = 0;
        CardId = 0;
        ID = 0;
        FortuneResult = null;
        IsMuted = false;
    }

    public void OpenTestScene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("OpenTestScene can only be used in Play Mode.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(testSceneName) || !Application.CanStreamedLevelBeLoaded(testSceneName))
        {
            Debug.LogWarning("Select a scene registered in Build Settings first.", this);
            return;
        }

        SceneManager.LoadScene(testSceneName);
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
        Buttons.ResetPauseState();
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

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(PlayerFortuneState))]
public sealed class PlayerFortuneStateEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "testSceneName");

        PlayerFortuneState state = (PlayerFortuneState)target;
        UnityEditor.SerializedProperty testSceneName = serializedObject.FindProperty("testSceneName");
        System.Collections.Generic.List<string> sceneNames = GetBuildSceneNames();
        int selectedIndex = Mathf.Max(0, sceneNames.IndexOf(testSceneName.stringValue));

        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Current Selection", UnityEditor.EditorStyles.boldLabel);

        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying))
        {
            UnityEditor.EditorGUI.BeginChangeCheck();
            int ropeId = Mathf.Clamp(
                UnityEditor.EditorGUILayout.IntField("Current Rope ID", state.RopeId),
                0,
                state.RopeIdCount);
            int cardId = Mathf.Clamp(
                UnityEditor.EditorGUILayout.IntField("Current Card ID", state.CardId),
                0,
                state.CardIdCount);

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                state.SelectRopeCard(cardId, ropeId);
                UnityEditor.EditorUtility.SetDirty(state);
            }

            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.IntField("ID", state.ID);
                UnityEditor.EditorGUILayout.Toggle("Muted", state.IsMuted);
            }
        }

        UnityEditor.EditorGUILayout.HelpBox(
            "0 means no result has been selected yet. Values can be changed in Play Mode.",
            UnityEditor.MessageType.None);

        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Test", UnityEditor.EditorStyles.boldLabel);

        UnityEditor.EditorGUI.BeginChangeCheck();
        selectedIndex = UnityEditor.EditorGUILayout.Popup(
            "Test Scene",
            selectedIndex,
            sceneNames.ToArray());

        if (UnityEditor.EditorGUI.EndChangeCheck())
        {
            testSceneName.stringValue = selectedIndex > 0 ? sceneNames[selectedIndex] : string.Empty;
        }

        serializedObject.ApplyModifiedProperties();

        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying || selectedIndex == 0))
        {
            if (GUILayout.Button("Open Test Scene"))
            {
                state.OpenTestScene();
            }
        }

        SceneVideoController sceneVideoController = FindAnyObjectByType<SceneVideoController>();
        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying || sceneVideoController == null))
        {
            if (GUILayout.Button("Skip Current Video"))
            {
                sceneVideoController.SkipVideo();
            }
        }

        if (!Application.isPlaying)
        {
            UnityEditor.EditorGUILayout.HelpBox(
                "This button is available in Play Mode only.",
                UnityEditor.MessageType.Info);
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private static System.Collections.Generic.List<string> GetBuildSceneNames()
    {
        var sceneNames = new System.Collections.Generic.List<string> { "Select Scene" };

        foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            sceneNames.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
        }

        return sceneNames;
    }
}
#endif
