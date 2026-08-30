using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Moves this object to a target position whenever it becomes active.
/// </summary>
public sealed class MoveToTargetOnEnable : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float delayTime;
    [SerializeField, Min(0f)] private float moveDuration = 1f;

    [Header("Easing")]
    [SerializeField] private AnimationCurve moveEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Delayed Event")]
    [Tooltip("Invoked once when Delay Time has elapsed, immediately before movement begins.")]
    [SerializeField] private UnityEvent onDelayCompleted;
    [Tooltip("Invoked once after this object reaches the cached target position.")]
    [SerializeField] private UnityEvent onMoveCompleted;

    [Header("Disappear (Optional)")]
    [SerializeField] private bool disappearOnEnable;
    [SerializeField, Min(0f)] private float disappearDelayTime;
    [SerializeField, Min(0f)] private float disappearDuration = 1f;
    [SerializeField] private AnimationCurve disappearEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine moveRoutine;
    private Coroutine disappearRoutine;
    private Vector3 destinationPosition;
    private bool hasDestination;
    private CanvasGroup canvasGroup;
    private Graphic[] graphics;
    private SpriteRenderer[] spriteRenderers;
    private float initialCanvasGroupAlpha;
    private Color[] initialGraphicColors;
    private Color[] initialSpriteColors;

    private void Awake()
    {
        CacheDisappearTargets();
    }

    private void OnEnable()
    {
        hasDestination = target != null;
        if (!hasDestination)
        {
            Debug.LogWarning("[MoveToTargetOnEnable] Target is not assigned.", this);
        }
        else
        {
            // Cache once because a child target moves together with this object.
            destinationPosition = target.position;
        }

        moveRoutine = StartCoroutine(MoveToTarget());

        if (disappearOnEnable)
        {
            RestoreInitialAlpha();
            disappearRoutine = StartCoroutine(Disappear());
        }
    }

    private void OnDisable()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (disappearRoutine != null)
        {
            StopCoroutine(disappearRoutine);
            disappearRoutine = null;
        }
    }

    private IEnumerator MoveToTarget()
    {
        if (delayTime > 0f)
        {
            yield return new WaitForSeconds(delayTime);
        }

        onDelayCompleted?.Invoke();

        if (!hasDestination)
        {
            moveRoutine = null;
            yield break;
        }

        Vector3 startPosition = transform.position;

        if (moveDuration <= 0f)
        {
            transform.position = destinationPosition;
            onMoveCompleted?.Invoke();
            moveRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / moveDuration);
            float easedTime = moveEase != null
                ? moveEase.Evaluate(normalizedTime)
                : normalizedTime;

            transform.position = Vector3.LerpUnclamped(
                startPosition,
                destinationPosition,
                easedTime);

            yield return null;
        }

        transform.position = destinationPosition;
        onMoveCompleted?.Invoke();
        moveRoutine = null;
    }

    private IEnumerator Disappear()
    {
        if (!HasDisappearTarget())
        {
            Debug.LogWarning(
                "[MoveToTargetOnEnable] No CanvasGroup, UI Graphic, or SpriteRenderer was found to fade.",
                this);
            disappearRoutine = null;
            yield break;
        }

        if (disappearDelayTime > 0f)
        {
            yield return new WaitForSeconds(disappearDelayTime);
        }

        if (disappearDuration <= 0f)
        {
            SetAlphaMultiplier(0f);
            disappearRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / disappearDuration);
            float easedTime = disappearEase != null
                ? disappearEase.Evaluate(normalizedTime)
                : normalizedTime;

            SetAlphaMultiplier(1f - easedTime);
            yield return null;
        }

        SetAlphaMultiplier(0f);
        disappearRoutine = null;
    }

    private void CacheDisappearTargets()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        graphics = canvasGroup == null
            ? GetComponentsInChildren<Graphic>(true)
            : System.Array.Empty<Graphic>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        initialCanvasGroupAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        initialGraphicColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
        {
            initialGraphicColors[i] = graphics[i].color;
        }

        initialSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            initialSpriteColors[i] = spriteRenderers[i].color;
        }
    }

    private bool HasDisappearTarget()
    {
        return canvasGroup != null || graphics.Length > 0 || spriteRenderers.Length > 0;
    }

    private void RestoreInitialAlpha()
    {
        SetAlphaMultiplier(1f);
    }

    private void SetAlphaMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp01(multiplier);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = initialCanvasGroupAlpha * multiplier;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
            {
                continue;
            }

            Color color = initialGraphicColors[i];
            color.a *= multiplier;
            graphics[i].color = color;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color color = initialSpriteColors[i];
            color.a *= multiplier;
            spriteRenderers[i].color = color;
        }
    }
}
