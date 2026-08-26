using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public class DragAnchorConfirmedEvent : UnityEvent<DragAnchor2D> { }

/// <summary>
/// Central mouse-input controller for every DragAnchor2D in the scene.
/// It selects the clicked Collider2D's DragAnchor2D and moves only that one.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DragManager2D : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask draggableLayers = ~0;
    [Header("Selection")]
    [SerializeField, Tooltip("A released rope is confirmed only when its Collider2D overlaps a collider on one of these layers.")]
    private LayerMask selectionZoneLayers;
    [SerializeField, Tooltip("Prevents dragging another rope after one has been confirmed.")]
    private bool lockAfterSelection = true;
    [Header("UI")]
    [SerializeField, Tooltip("Hidden as soon as the first rope drag begins.")]
    private GameObject initialInstructionUi;
    [SerializeField, Tooltip("Background hidden together with the initial instruction.")]
    private GameObject initialInstructionBlankUi;
    [Header("Pull Gauge")]
    [SerializeField] private Collider2D selectionZoneCollider;
    [SerializeField] private Sprite pullGaugeBackgroundSprite;
    [SerializeField] private Sprite pullGaugeFillSprite;
    [SerializeField] private Vector2 pullGaugeSize = new Vector2(1299f, 135f);
    [SerializeField] private Vector2 pullGaugeAnchoredPosition = new Vector2(0f, 110f);
    [Header("Failed Drag Return")]
    [SerializeField, Min(0f), Tooltip("Time in seconds for an unconfirmed rope end to return to its starting position.")]
    private float failedReturnDuration = 2.2f;
    [SerializeField, Min(1f), Tooltip("Blends a non-zero starting speed into the failed-return acceleration curve. 1 uses the original curve; higher values start faster without a mid-return slowdown.")]
    private float failedReturnInitialSpeedMultiplier = 5f;
    [SerializeField, Min(1f), Tooltip("Smoothly increases return speed after the rope has covered its first third.")]
    private float failedReturnRemainingSpeedMultiplier = 2f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Speed multiplier applied near the final 15% of a failed return. 0.6 keeps 60% of the current speed before the existing arrival easing.")]
    private float failedReturnFinalSpeedMultiplier = 0.6f;
    [SerializeField, Min(1f), Tooltip("Power-curve exponent for failed returns. Higher values start slower and accelerate more sharply near the end.")]
    private float failedReturnAccelerationPower = 3.5f;
    [SerializeField, Range(0.5f, 0.95f), Tooltip("Normalized return time at which the rope starts braking toward zero arrival speed.")]
    private float failedReturnBrakeStart = 0.85f;
    [SerializeField] private DragAnchorConfirmedEvent onAnchorConfirmed;

    private DragAnchor2D activeAnchor;
    private DragAnchor2D lockedAnchor;
    private DragAnchor2D confirmedAnchor;
    private GameObject pullGaugeUi;
    private Image pullGaugeFillImage;
    private Collider2D gaugeAnchorCollider;
    private float gaugeStartY;
    private bool activeDragUsesTouch;
    private readonly Collider2D[] overlapResults = new Collider2D[8];

    public DragAnchor2D ConfirmedAnchor => confirmedAnchor;
    public bool HasConfirmedAnchor => confirmedAnchor != null;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        CreatePullGaugeUi();
    }

    private void Update()
    {
        UpdatePullGauge();

        if (targetCamera == null) return;
        if (lockAfterSelection && HasConfirmedAnchor) return;

        if (activeAnchor == null && TryGetPrimaryInputPressedThisFrame(
                out Vector2 pressedScreenPosition, out bool usesTouch))
            TryBeginDrag(pressedScreenPosition, usesTouch);

        if (activeAnchor != null && IsActiveDragInputPressed())
            activeAnchor.DragTo(GetScreenWorldPosition(
                GetActiveDragScreenPosition(), activeAnchor.transform.position.z));

        if (activeAnchor != null && WasActiveDragInputReleasedThisFrame())
        {
            DragAnchor2D releasedAnchor = activeAnchor;
            releasedAnchor.EndDrag();
            activeAnchor = null;
            if (!TryConfirmAnchor(releasedAnchor))
                releasedAnchor.ReturnToOriginalPosition(
                    failedReturnDuration,
                    failedReturnInitialSpeedMultiplier,
                    failedReturnRemainingSpeedMultiplier,
                    failedReturnFinalSpeedMultiplier,
                    failedReturnAccelerationPower,
                    failedReturnBrakeStart);
        }
    }

    private bool TryConfirmAnchor(DragAnchor2D anchor)
    {
        if (selectionZoneLayers.value == 0) return false;

        Collider2D anchorCollider = anchor.GetComponent<Collider2D>();
        if (anchorCollider == null) return false;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(selectionZoneLayers);

        if (anchorCollider.Overlap(filter, overlapResults) == 0) return false;

        confirmedAnchor = anchor;

        if (pullGaugeUi != null)
            pullGaugeUi.SetActive(false);

        Debug.Log($"Rope selected: {anchor.name}", anchor);
        onAnchorConfirmed?.Invoke(anchor);
        return true;
    }

    private void TryBeginDrag(Vector2 screenPosition, bool usesTouch)
    {
        Vector2 worldPosition = GetScreenWorldPosition(screenPosition, 0f);
        Collider2D hit = Physics2D.OverlapPoint(worldPosition, draggableLayers);
        if (hit == null) return;

        DragAnchor2D anchor = hit.GetComponentInParent<DragAnchor2D>();
        if (anchor == null) return;
        if (lockedAnchor != null && anchor != lockedAnchor) return;

        LockAnchor(anchor);

        if (initialInstructionUi != null)
            initialInstructionUi.SetActive(false);
        if (initialInstructionBlankUi != null)
            initialInstructionBlankUi.SetActive(false);
        if (pullGaugeUi != null)
            pullGaugeUi.SetActive(true);

        activeAnchor = anchor;
        activeDragUsesTouch = usesTouch;
        activeAnchor.BeginDrag(GetScreenWorldPosition(
            screenPosition, activeAnchor.transform.position.z));
    }

    private void LockAnchor(DragAnchor2D anchor)
    {
        if (lockedAnchor == anchor) return;

        if (lockedAnchor != null)
            lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;

        lockedAnchor = anchor;
        gaugeStartY = anchor.transform.position.y;
        gaugeAnchorCollider = anchor.GetComponent<Collider2D>();
        lockedAnchor.ReturnCompleted += HandleLockedAnchorReturnCompleted;
    }

    private void HandleLockedAnchorReturnCompleted(DragAnchor2D anchor)
    {
        if (anchor != lockedAnchor) return;

        lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;
        SetPullGaugeValue(0f);
        gaugeAnchorCollider = null;
        lockedAnchor = null;
    }

    private void CreatePullGaugeUi()
    {
        if (initialInstructionBlankUi == null ||
            pullGaugeBackgroundSprite == null || pullGaugeFillSprite == null)
            return;

        Canvas gaugeCanvas = initialInstructionBlankUi.GetComponentInParent<Canvas>();
        Transform gaugeParent = gaugeCanvas != null
            ? gaugeCanvas.transform
            : initialInstructionBlankUi.transform.parent;
        pullGaugeUi = new GameObject(
            "Gauge-bar-background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        RectTransform backgroundRect = pullGaugeUi.GetComponent<RectTransform>();
        backgroundRect.SetParent(gaugeParent, false);
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = pullGaugeSize;
        backgroundRect.anchoredPosition = pullGaugeAnchoredPosition;

        Image backgroundImage = pullGaugeUi.GetComponent<Image>();
        backgroundImage.sprite = pullGaugeBackgroundSprite;
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Full-gauge",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(backgroundRect, false);
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);

        Rect backgroundSpriteRect = pullGaugeBackgroundSprite.rect;
        Rect fillSpriteRect = pullGaugeFillSprite.rect;
        float horizontalScale = pullGaugeSize.x / backgroundSpriteRect.width;
        float verticalScale = pullGaugeSize.y / backgroundSpriteRect.height;
        fillRect.sizeDelta = new Vector2(
            fillSpriteRect.width * horizontalScale,
            fillSpriteRect.height * verticalScale);
        fillRect.anchoredPosition = new Vector2(
            (fillSpriteRect.xMin - backgroundSpriteRect.xMin) * horizontalScale,
            (fillSpriteRect.center.y - backgroundSpriteRect.center.y) * verticalScale);

        pullGaugeFillImage = fillObject.GetComponent<Image>();
        pullGaugeFillImage.sprite = pullGaugeFillSprite;
        pullGaugeFillImage.type = Image.Type.Filled;
        pullGaugeFillImage.fillMethod = Image.FillMethod.Horizontal;
        pullGaugeFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        pullGaugeFillImage.fillAmount = 0f;
        pullGaugeFillImage.raycastTarget = false;

        pullGaugeUi.SetActive(false);
    }

    private void UpdatePullGauge()
    {
        if (lockedAnchor == null || pullGaugeFillImage == null ||
            selectionZoneCollider == null)
            return;

        if (confirmedAnchor == lockedAnchor)
        {
            SetPullGaugeValue(1f);
            return;
        }

        float anchorBottomOffset = gaugeAnchorCollider != null
            ? gaugeAnchorCollider.bounds.min.y - lockedAnchor.transform.position.y
            : 0f;
        float fullGaugeY = selectionZoneCollider.bounds.max.y - anchorBottomOffset;
        float distanceToFull = gaugeStartY - fullGaugeY;
        float fillAmount = distanceToFull <= Mathf.Epsilon
            ? 0f
            : (gaugeStartY - lockedAnchor.transform.position.y) / distanceToFull;
        SetPullGaugeValue(fillAmount);
    }

    private void SetPullGaugeValue(float value)
    {
        if (pullGaugeFillImage != null)
            pullGaugeFillImage.fillAmount = Mathf.Clamp01(value);
    }

    private void OnDestroy()
    {
        if (lockedAnchor != null)
            lockedAnchor.ReturnCompleted -= HandleLockedAnchorReturnCompleted;
    }

    private Vector3 GetScreenWorldPosition(Vector2 screenPosition, float worldZ)
    {
        Vector3 cameraScreenPosition = screenPosition;
        cameraScreenPosition.z = Mathf.Abs(targetCamera.transform.position.z - worldZ);
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(cameraScreenPosition);
        worldPosition.z = worldZ;
        return worldPosition;
    }

    private static bool TryGetPrimaryInputPressedThisFrame(out Vector2 screenPosition, out bool usesTouch)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            usesTouch = false;
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            usesTouch = true;
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            usesTouch = false;
            return true;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
            usesTouch = true;
            return true;
        }
#endif
        screenPosition = default;
        usesTouch = false;
        return false;
    }

    private bool IsActiveDragInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch
            ? Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed
            : Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return activeDragUsesTouch
            ? Input.touchCount > 0 && Input.GetTouch(0).phase != TouchPhase.Ended &&
              Input.GetTouch(0).phase != TouchPhase.Canceled
            : Input.GetMouseButton(0);
#endif
    }

    private bool WasActiveDragInputReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch
            ? Touchscreen.current == null || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame
            : Mouse.current == null || Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return activeDragUsesTouch
            ? Input.touchCount == 0 || Input.GetTouch(0).phase == TouchPhase.Ended ||
              Input.GetTouch(0).phase == TouchPhase.Canceled
            : Input.GetMouseButtonUp(0);
#endif
    }

    private Vector2 GetActiveDragScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return activeDragUsesTouch && Touchscreen.current != null
            ? Touchscreen.current.primaryTouch.position.ReadValue()
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return activeDragUsesTouch && Input.touchCount > 0
            ? Input.GetTouch(0).position
            : Input.mousePosition;
#endif
    }
}
