using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Presents the already selected rope and card before continuing to the loading scene.
/// </summary>
public sealed class SelectionCombinationController : MonoBehaviour
{
    private const int MinimumRopeId = 1;
    private const int MaximumRopeId = 5;
    private const int MinimumCardId = 1;
    private const int MaximumCardId = 12;

    [Header("Presentation Assets")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite titleSprite;
    [Tooltip("Elements 0-4 map to Rope IDs 1-5.")]
    [SerializeField] private Sprite[] ropeSprites = new Sprite[5];
    [Tooltip("Elements 0-11 map to Card IDs 1-12.")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[12];

    [Header("Direct Scene Preview")]
    [Tooltip("Uses the IDs below only when this scene is played without a completed selection.")]
    [SerializeField] private bool usePreviewIdsWhenSelectionMissing = true;
    [Range(MinimumRopeId, MaximumRopeId)]
    [SerializeField] private int previewRopeId = 1;
    [Range(MinimumCardId, MaximumCardId)]
    [SerializeField] private int previewCardId = 1;

    [Header("Layout (1920 x 1080 Reference)")]
    [SerializeField] private Vector2 ropeStartPosition = new Vector2(-470f, -126f);
    [SerializeField] private Vector2 ropeEndPosition = new Vector2(-115f, -126f);
    [SerializeField] private Vector2 ropeSize = new Vector2(600f, 678f);
    [SerializeField] private Vector2 cardStartPosition = new Vector2(470f, -126f);
    [SerializeField] private Vector2 cardEndPosition = new Vector2(115f, -126f);
    [SerializeField] private Vector2 cardSize = new Vector2(600f, 678f);
    [SerializeField] private Vector2 titlePosition = new Vector2(0f, 380f);
    [SerializeField] private Vector2 titleSize = new Vector2(765f, 185f);

    [Header("Animation")]
    [Min(0f)]
    [SerializeField] private float initialHoldDuration = 1.5f;
    [Min(0f)]
    [SerializeField] private float idleFloatDistance = 8f;
    [Min(0.01f)]
    [SerializeField] private float idleFloatSpeed = 2f;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 2.5f;
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartProgress = 0.33f;
    [Range(0f, 1f)]
    [SerializeField] private float fullyTransparentProgress = 0.95f;
    [Range(0.01f, 1f)]
    [SerializeField] private float finalScale = 0.5f;

    [Header("Scene Transition")]
    [SerializeField] private string loadingSceneName = "LoadingEnding";

    private RectTransform ropeRectTransform;
    private RectTransform cardRectTransform;
    private Image ropeImage;
    private Image cardImage;
    private InactivityTimer inactivityTimer;
    private bool isTransitioning;

    private void Awake()
    {
        CreatePresentation();
    }

    private void OnEnable()
    {
        inactivityTimer = FindAnyObjectByType<InactivityTimer>();
        inactivityTimer?.Pause(this);
    }

    private void OnDisable()
    {
        inactivityTimer?.Resume(this);
    }

    private void Start()
    {
        if (!TryApplySelection())
        {
            return;
        }

        StartCoroutine(PlayCombinationAnimation());
    }

    private bool TryApplySelection()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        int ropeId = state != null ? state.RopeId : 0;
        int cardId = state != null ? state.CardId : 0;

        if (usePreviewIdsWhenSelectionMissing &&
            (ropeId < MinimumRopeId || ropeId > MaximumRopeId ||
             cardId < MinimumCardId || cardId > MaximumCardId))
        {
            ropeId = previewRopeId;
            cardId = previewCardId;
            Debug.LogWarning(
                $"[SelectionCombination] Selection is missing. Using preview RopeId={ropeId}, CardId={cardId}.",
                this);
        }

        if (ropeId < MinimumRopeId || ropeId > MaximumRopeId)
        {
            Debug.LogError($"[SelectionCombination] RopeId={ropeId} is outside the valid range (1-5).", this);
            return false;
        }

        if (cardId < MinimumCardId || cardId > MaximumCardId)
        {
            Debug.LogError($"[SelectionCombination] CardId={cardId} is outside the valid range (1-12).", this);
            return false;
        }

        int ropeIndex = ropeId - MinimumRopeId;
        int cardIndex = cardId - MinimumCardId;
        if (ropeSprites == null || ropeIndex >= ropeSprites.Length || ropeSprites[ropeIndex] == null ||
            cardSprites == null || cardIndex >= cardSprites.Length || cardSprites[cardIndex] == null)
        {
            Debug.LogError($"[SelectionCombination] Display sprite is missing for RopeId={ropeId}, CardId={cardId}.", this);
            return false;
        }

        ropeImage.sprite = ropeSprites[ropeIndex];
        cardImage.sprite = cardSprites[cardIndex];
        return true;
    }

    private IEnumerator PlayCombinationAnimation()
    {
        float elapsed = 0f;
        SetPresentationProgress(0f);

        while (elapsed < initialHoldDuration)
        {
            elapsed += Time.deltaTime;
            ApplyIdleMotion(elapsed, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, initialHoldDuration)));
            yield return null;
        }

        ropeRectTransform.anchoredPosition = ropeStartPosition;
        cardRectTransform.anchoredPosition = cardStartPosition;
        elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / animationDuration);
            SetPresentationProgress(progress);
            yield return null;
        }

        SetPresentationProgress(1f);
        OpenLoadingScene();
    }

    private void ApplyIdleMotion(float elapsed, float normalizedHoldTime)
    {
        float settleProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, normalizedHoldTime));
        float motionWeight = 1f - settleProgress;
        float verticalOffset = Mathf.Sin(elapsed * idleFloatSpeed) * idleFloatDistance * motionWeight;
        ropeRectTransform.anchoredPosition = ropeStartPosition + Vector2.up * verticalOffset;
        cardRectTransform.anchoredPosition = cardStartPosition + Vector2.up * verticalOffset;
    }

    private void SetPresentationProgress(float progress)
    {
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        ropeRectTransform.anchoredPosition = Vector2.LerpUnclamped(ropeStartPosition, ropeEndPosition, easedProgress);
        cardRectTransform.anchoredPosition = Vector2.LerpUnclamped(cardStartPosition, cardEndPosition, easedProgress);

        float scale = Mathf.Lerp(1f, finalScale, easedProgress);
        ropeRectTransform.localScale = Vector3.one * scale;
        cardRectTransform.localScale = Vector3.one * scale;

        float safeTransparentProgress = Mathf.Max(fadeStartProgress + 0.001f, fullyTransparentProgress);
        float alpha = 1f - Mathf.InverseLerp(fadeStartProgress, safeTransparentProgress, progress);
        SetImageAlpha(ropeImage, alpha);
        SetImageAlpha(cardImage, alpha);
    }

    private void OpenLoadingScene()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        if (string.IsNullOrWhiteSpace(loadingSceneName) || !Application.CanStreamedLevelBeLoaded(loadingSceneName))
        {
            Debug.LogError($"[SelectionCombination] Scene '{loadingSceneName}' is not available in Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(loadingSceneName);
    }

    private void CreatePresentation()
    {
        GameObject canvasObject = new GameObject(
            "Selection Combination Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image backgroundImage = CreateImage("Background", canvasObject.transform, backgroundSprite, false);
        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        ropeImage = CreateImage("Selected Rope", canvasObject.transform, null, true);
        ropeRectTransform = ropeImage.rectTransform;
        ConfigureDisplayRect(ropeRectTransform, ropeStartPosition, ropeSize);

        cardImage = CreateImage("Selected Card", canvasObject.transform, null, true);
        cardRectTransform = cardImage.rectTransform;
        ConfigureDisplayRect(cardRectTransform, cardStartPosition, cardSize);

        Image titleImage = CreateImage("Combination Title", canvasObject.transform, titleSprite, true);
        ConfigureDisplayRect(titleImage.rectTransform, titlePosition, titleSize);
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, bool preserveAspect)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private static void ConfigureDisplayRect(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
