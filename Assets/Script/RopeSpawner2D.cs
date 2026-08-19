using System.Collections.Generic;
using UnityEngine;

public sealed class RopeSpawner2D : MonoBehaviour
{
    [SerializeField] private GameObject ropePrefab;
    [SerializeField, Min(1)] private int ropeCount = 5;
    [SerializeField, Min(0f)] private float horizontalSpacing = 3.31f;

    private readonly List<GameObject> spawnedRopes = new();

    private void OnEnable()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state != null)
        {
            ropeCount = state.RopeIdCount;
        }
    }

    private void Start()
    {
        SpawnRopes();
    }

    public void SpawnRopes()
    {
        ClearSpawnedRopes();

        if (ropePrefab == null)
        {
            Debug.LogError("[RopeSpawner2D] Rope Prefab을 등록해주세요.", this);
            return;
        }

        for (int index = 0; index < ropeCount; index++)
        {
            GameObject rope = Instantiate(ropePrefab, transform);
            rope.name = $"Rope {index + 1}";
            rope.transform.localPosition = Vector3.right * (horizontalSpacing * index);
            spawnedRopes.Add(rope);
        }
    }

    private void ClearSpawnedRopes()
    {
        foreach (GameObject rope in spawnedRopes)
        {
            if (rope != null)
            {
                Destroy(rope);
            }
        }

        spawnedRopes.Clear();
    }
}
