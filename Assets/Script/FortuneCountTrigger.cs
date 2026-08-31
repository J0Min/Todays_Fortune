using UnityEngine;

public sealed class FortuneCountTrigger : MonoBehaviour
{
    private void Start()
    {
        if (!FortuneCountStorage.IncreaseTodayCount())
        {
            Debug.LogError("[FortuneCountTrigger] Failed to record the fortune count.", this);
        }
    }
}
