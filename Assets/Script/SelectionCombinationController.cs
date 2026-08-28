using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Presents the already selected rope and card before continuing to the loading scene.
/// </summary>
public sealed class SelectionCombinationController : MonoBehaviour
{
    [Header("Presentation Assets")]
    [Tooltip("Displayed at the start, then faded out before the second background is shown.")]
    [SerializeField] private Sprite firstBackgroundSprite;
    [Tooltip("Displayed after the first background has faded out.")]
    [SerializeField] private Sprite secondBackgroundSprite;
    [SerializeField] private Sprite titleSprite;
    [SerializeField] private Camera canvasCamera;
    [Tooltip("Element 0 maps to Rope ID 1.")]
    [SerializeField] private Sprite[] ropeSprites = new Sprite[PlayerFortuneState.DefaultRopeIdCount];
    [Tooltip("Element 0 maps to Card ID 1.")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[PlayerFortuneState.DefaultCardIdCount];

    [Header("Direct Scene Preview")]
    [Tooltip("Uses the IDs below only when this scene is played without a completed selection.")]
    [SerializeField] private bool usePreviewIdsWhenSelectionMissing = true;
    [Min(PlayerFortuneState.MinimumSelectionId)]
    [SerializeField] private int previewRopeId = 1;
    [Min(PlayerFortuneState.MinimumSelectionId)]
    [SerializeField] private int previewCardId = 1;

    [Header("Layout (1920 x 1080 Reference)")]
    [SerializeField] private Vector2 ropeStartPosition = new Vector2(390f, -96f);
    [SerializeField] private Vector2 ropeEndPosition = new Vector2(95f, -96f);
    [SerializeField] private Vector2 ropeSize = new Vector2(600f, 678f);
    [SerializeField] private Vector2 cardStartPosition = new Vector2(-390f, -96f);
    [SerializeField] private Vector2 cardEndPosition = new Vector2(-95f, -96f);
    [SerializeField] private Vector2 cardSize = new Vector2(600f, 678f);
    [SerializeField] private Vector2 titlePosition = new Vector2(0f, 380f);
    [SerializeField] private Vector2 titleSize = new Vector2(765f, 185f);

    [Header("Animation")]
    [Min(0.01f)]
    [SerializeField] private float backgroundFadeDuration = 1.25f;
    [Min(0.01f)]
    [SerializeField] private float titleFadeDuration = 1.25f;
    [Min(0.01f)]
    [SerializeField] private float introFadeDuration = 1.25f;
    [Min(0.01f)]
    [SerializeField] private float secondImageFadeDuration = 1.25f;
    [Min(0f)]
    [SerializeField] private float initialHoldDuration = 3.5f;
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
    [Min(0f)]
    [SerializeField] private float postFadeHoldDuration = 0.5f;

    [Header("After Combination")]
    [Tooltip("Shown after the rope and card have finished fading out. The title is attached to this object when it is shown.")]
    [SerializeField] private GameObject objectToShowAfterCombination;
    [Min(0f)]
    [SerializeField] private float objectHoldDuration = 2f;
    [Min(0f)]
    [SerializeField] private float objectFadeOutDuration = 1f;
    [SerializeField] private UnityEvent onCombinationFinished;

    [Header("Scene Transition")]
    [Tooltip("Optionally preloads the next scene on start. The scene is not activated until Continue To Next Scene is called.")]
    [SerializeField] private SceneVideoController sceneVideoController;

    private RectTransform ropeRectTransform;
    private RectTransform cardRectTransform;
    private Image firstBackgroundImage;
    private Image secondBackgroundImage;
    private Image titleImage;
    private Image ropeImage;
    private Image cardImage;
    private CanvasGroup objectToShowCanvasGroup;
    private InactivityTimer inactivityTimer;
    private bool isTransitioning;

    private void Awake()
    {
        CreatePresentation();
        if (objectToShowAfterCombination != null)
        {
            objectToShowCanvasGroup = objectToShowAfterCombination.GetComponent<CanvasGroup>();
            if (objectToShowCanvasGroup == null)
            {
                objectToShowCanvasGroup = objectToShowAfterCombination.AddComponent<CanvasGroup>();
            }

            objectToShowCanvasGroup.alpha = 1f;
            objectToShowAfterCombination.SetActive(false);
        }
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
        float backgroundTransitionDuration = secondBackgroundSprite != null ? backgroundFadeDuration : 0f;
        float presentationDuration = backgroundTransitionDuration + titleFadeDuration + introFadeDuration + secondImageFadeDuration +
            initialHoldDuration + animationDuration + postFadeHoldDuration;
        AmbientAudioManager.Instance?.FadeThroughSelection(presentationDuration);

        sceneVideoController?.PreloadNextScene();

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
        int ropeIdCount = state != null ? state.RopeIdCount : PlayerFortuneState.DefaultRopeIdCount;
        int cardIdCount = state != null ? state.CardIdCount : PlayerFortuneState.DefaultCardIdCount;

        if (usePreviewIdsWhenSelectionMissing &&
            (!PlayerFortuneState.IsValidSelectionId(ropeId, ropeIdCount) ||
             !PlayerFortuneState.IsValidSelectionId(cardId, cardIdCount)))
        {
            ropeId = previewRopeId;
            cardId = previewCardId;
            Debug.LogWarning(
                $"[SelectionCombination] Selection is missing. Using preview RopeId={ropeId}, CardId={cardId}.",
                this);
        }

        if (!PlayerFortuneState.IsValidSelectionId(ropeId, ropeIdCount))
        {
            Debug.LogError(
                $"[SelectionCombination] RopeId={ropeId} is outside the valid range " +
                $"({PlayerFortuneState.MinimumSelectionId}-{ropeIdCount}).",
                this);
            return false;
        }

        if (!PlayerFortuneState.IsValidSelectionId(cardId, cardIdCount))
        {
            Debug.LogError(
                $"[SelectionCombination] CardId={cardId} is outside the valid range " +
                $"({PlayerFortuneState.MinimumSelectionId}-{cardIdCount}).",
                this);
            return false;
        }

        int ropeIndex = ropeId - PlayerFortuneState.MinimumSelectionId;
        int cardIndex = cardId - PlayerFortuneState.MinimumSelectionId;
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
        SetImageAlpha(cardImage, 0f);
        SetImageAlpha(ropeImage, 0f);
        SetImageAlpha(titleImage, 0f);

        yield return PlayBackgroundTransition();

        elapsed = 0f;
        while (elapsed < titleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float titleAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / titleFadeDuration));
            SetImageAlpha(titleImage, titleAlpha);
            yield return null;
        }

        SetImageAlpha(titleImage, 1f);

        elapsed = 0f;
        while (elapsed < introFadeDuration)
        {
            elapsed += Time.deltaTime;
            float cardAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / introFadeDuration));
            SetImageAlpha(cardImage, cardAlpha);
            yield return null;
        }

        SetImageAlpha(cardImage, 1f);

        elapsed = 0f;
        while (elapsed < secondImageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float ropeAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / secondImageFadeDuration));
            SetImageAlpha(ropeImage, ropeAlpha);
            yield return null;
        }

        SetImageAlpha(ropeImage, 1f);

        elapsed = 0f;
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

        if (postFadeHoldDuration > 0f)
        {
            yield return new WaitForSeconds(postFadeHoldDuration);
        }

        if (objectToShowAfterCombination != null)
        {
            objectToShowAfterCombination.SetActive(true);
            AttachTitleToAfterCombination();
        }

        onCombinationFinished?.Invoke();

        if (objectToShowCanvasGroup == null)
        {
            yield break;
        }

        if (objectHoldDuration > 0f)
        {
            yield return new WaitForSeconds(objectHoldDuration);
        }

        // Activate LoadingEnding at the start of this fade. Its intro plays
        // hidden and muted behind the outgoing scene until the fade completes.
        sceneVideoController?.PreActivateNextSceneWithoutVideo();

        elapsed = 0f;
        while (elapsed < objectFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            objectToShowCanvasGroup.alpha = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / Mathf.Max(0.01f, objectFadeOutDuration)));
            yield return null;
        }

        objectToShowCanvasGroup.alpha = 0f;
        ContinueToNextScene();
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

    private IEnumerator PlayBackgroundTransition()
    {
        if (firstBackgroundImage == null || secondBackgroundImage == null || secondBackgroundSprite == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < backgroundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / backgroundFadeDuration));
            SetImageAlpha(firstBackgroundImage, alpha);
            yield return null;
        }

        SetImageAlpha(firstBackgroundImage, 0f);
    }

    private void AttachTitleToAfterCombination()
    {
        if (titleImage == null || objectToShowAfterCombination == null)
        {
            return;
        }

        titleImage.rectTransform.SetParent(objectToShowAfterCombination.transform, false);
        titleImage.rectTransform.SetAsLastSibling();
    }

    /// <summary>
    /// Call this from the revealed object's animation event, button, or other
    /// completion signal when it is actually time to leave this scene.
    /// </summary>
    public void ContinueToNextScene()
    {
        if (isTransitioning)
        {
            return;
        }

        if (sceneVideoController == null)
        {
            Debug.LogError(
                "[SelectionCombination] Continue To Next Scene needs a SceneVideoController.",
                this);
            return;
        }

        isTransitioning = true;
        sceneVideoController.CompleteNextSceneTransitionWithoutVideo();
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
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = canvasCamera;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        secondBackgroundImage = CreateImage("Second Background", canvasObject.transform, secondBackgroundSprite, false);
        ConfigureBackgroundRect(secondBackgroundImage.rectTransform);

        firstBackgroundImage = CreateImage("First Background", canvasObject.transform, firstBackgroundSprite, false);
        ConfigureBackgroundRect(firstBackgroundImage.rectTransform);

        ropeImage = CreateImage("Selected Rope", canvasObject.transform, null, true);
        ropeRectTransform = ropeImage.rectTransform;
        ConfigureDisplayRect(ropeRectTransform, ropeStartPosition, ropeSize);

        cardImage = CreateImage("Selected Card", canvasObject.transform, null, true);
        cardRectTransform = cardImage.rectTransform;
        ConfigureDisplayRect(cardRectTransform, cardStartPosition, cardSize);

        titleImage = CreateImage("Combination Title", canvasObject.transform, titleSprite, true);
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

    private static void ConfigureBackgroundRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
