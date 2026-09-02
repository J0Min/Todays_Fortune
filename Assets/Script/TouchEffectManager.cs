using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TouchEffectManager : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private RectTransform effectRoot;
    [SerializeField] private TouchEffectPlayer playerPrefab;
    [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float framesPerSecond = 30f;

    [Header("Pool")]
    [SerializeField, Min(0)] private int initialPoolSize = 5;
    [SerializeField] private bool expandPool = true;

    private readonly Stack<TouchEffectPlayer> availablePlayers = new Stack<TouchEffectPlayer>();
    private Canvas effectCanvas;

    private void Awake()
    {
        if (effectRoot != null)
        {
            effectCanvas = effectRoot.GetComponentInParent<Canvas>();
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            TouchEffectPlayer player = CreatePlayer();
            if (player != null)
            {
                availablePlayers.Push(player);
            }
        }
    }

    private void OnEnable()
    {
        SfxAudioManager.PrimaryPressed += Play;
    }

    private void OnDisable()
    {
        SfxAudioManager.PrimaryPressed -= Play;
    }

    public void Play(Vector2 screenPosition)
    {
        if (effectRoot == null || playerPrefab == null || frames == null || frames.Length == 0)
        {
            return;
        }

        Camera eventCamera = effectCanvas != null && effectCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? effectCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectRoot,
                screenPosition,
                eventCamera,
                out Vector2 localPosition))
        {
            return;
        }

        TouchEffectPlayer player = GetPlayer();
        if (player == null)
        {
            return;
        }

        player.transform.SetAsLastSibling();
        player.Play(frames, framesPerSecond, localPosition, ReturnToPool);
    }

    private TouchEffectPlayer GetPlayer()
    {
        while (availablePlayers.Count > 0)
        {
            TouchEffectPlayer player = availablePlayers.Pop();
            if (player != null)
            {
                return player;
            }
        }

        return expandPool ? CreatePlayer() : null;
    }

    private TouchEffectPlayer CreatePlayer()
    {
        if (playerPrefab == null || effectRoot == null)
        {
            return null;
        }

        TouchEffectPlayer player = Instantiate(playerPrefab, effectRoot);
        player.gameObject.SetActive(false);
        return player;
    }

    private void ReturnToPool(TouchEffectPlayer player)
    {
        if (player == null)
        {
            return;
        }

        player.gameObject.SetActive(false);
        availablePlayers.Push(player);
    }
}
