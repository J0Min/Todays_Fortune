using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ReturnToStartButton : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    public void ReturnToStart()
    {
        if (PlayerFortuneState.Instance == null)
        {
            Debug.LogError("[ReturnToStartButton] PlayerFortuneState 인스턴스를 찾을 수 없습니다.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogError("[ReturnToStartButton] 최초 화면 Scene 이름이 비어 있습니다.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError(
                $"[ReturnToStartButton] '{startSceneName}' Scene을 로드할 수 없습니다. " +
                "Scene 이름과 Build Settings 등록 여부를 확인해주세요.",
                this);
            return;
        }

        PlayerFortuneState.Instance.ResetData();
        SceneManager.LoadScene(startSceneName);
    }
}
