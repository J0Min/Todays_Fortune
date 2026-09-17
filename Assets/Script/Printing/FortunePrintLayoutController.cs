using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FortunePrintLayoutController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform printLayout;
    [SerializeField] private RawImage resultImage;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text recommendationText;
    [SerializeField, Tooltip("기본 이미지의 가로세로 비율에 맞춰 Print Layout 높이를 자동으로 변경합니다.")]
    private bool matchLayoutHeightToResultImage = true;

    [Header("Result ID Mapping")]
    [SerializeField, Tooltip("ID 1은 배열 0번에 대응합니다. 비어 있으면 Result Image에 현재 지정된 이미지를 유지합니다.")]
    private Texture[] resultImages;
    [SerializeField, Tooltip("최종 ID를 추천 개수로 순환 배정합니다. ID 1은 배열 0번에 대응합니다.")]
    private string[] recommendations;

    [Header("Text")]
    [SerializeField] private string dateFormat = "yyyy년 MM월 dd일";
    [SerializeField, Tooltip("{0} 위치에 추천 메뉴 이름이 들어갑니다.")]
    private string recommendationFormat = "{0}";
    [SerializeField] private string defaultRecommendation = "추천 메뉴";

    private bool hasRecommendationOverride;
    private string recommendationOverride;

    private void Start()
    {
        RefreshForPrint();
    }

    public void RefreshForPrint()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        int resultId = state != null ? state.ID : 0;

        ApplyResultImage(resultId);
        MatchLayoutHeightToResultImage();
        ApplyDate();
        ApplyRecommendation(resultId);
        Canvas.ForceUpdateCanvases();
    }

    public void SetResultTexture(Texture texture)
    {
        if (resultImage != null)
        {
            resultImage.texture = texture;
            MatchLayoutHeightToResultImage();
        }
    }

    public void SetRecommendation(string value)
    {
        hasRecommendationOverride = true;
        recommendationOverride = value ?? string.Empty;
        ApplyRecommendation(PlayerFortuneState.Instance != null
            ? PlayerFortuneState.Instance.ID
            : 0);
    }

    public void ClearRecommendationOverride()
    {
        hasRecommendationOverride = false;
        recommendationOverride = string.Empty;
    }

    private void ApplyResultImage(int resultId)
    {
        int index = resultId - 1;
        if (resultImage != null &&
            resultImages != null &&
            index >= 0 &&
            index < resultImages.Length &&
            resultImages[index] != null)
        {
            resultImage.texture = resultImages[index];
        }
    }

    private void ApplyDate()
    {
        if (dateText == null)
        {
            return;
        }

        try
        {
            dateText.text = DateTime.Now.ToString(
                dateFormat,
                CultureInfo.GetCultureInfo("ko-KR"));
        }
        catch (FormatException)
        {
            dateText.text = DateTime.Now.ToString("yyyy년 MM월 dd일");
            Debug.LogWarning(
                "[FortunePrintLayoutController] 날짜 형식이 잘못되어 기본 형식을 사용합니다.",
                this);
        }
    }

    private void MatchLayoutHeightToResultImage()
    {
        if (!matchLayoutHeightToResultImage || resultImage == null || resultImage.texture == null)
        {
            return;
        }

        RectTransform targetLayout = printLayout != null
            ? printLayout
            : resultImage.rectTransform.parent as RectTransform;
        if (targetLayout == null)
        {
            return;
        }

        float layoutWidth = targetLayout.rect.width;
        if (layoutWidth <= 0f)
        {
            layoutWidth = targetLayout.sizeDelta.x;
        }

        if (layoutWidth <= 0f || resultImage.texture.width <= 0)
        {
            return;
        }

        float targetHeight = layoutWidth * resultImage.texture.height / resultImage.texture.width;
        targetLayout.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private void ApplyRecommendation(int resultId)
    {
        if (recommendationText == null)
        {
            return;
        }

        if (hasRecommendationOverride)
        {
            SetRecommendationText(recommendationOverride);
            return;
        }

        int recommendationCount = recommendations != null ? recommendations.Length : 0;
        int index = resultId > 0 && recommendationCount > 0
            ? (resultId - 1) % recommendationCount
            : -1;
        string value = index >= 0 ? recommendations[index] : string.Empty;

        SetRecommendationText(string.IsNullOrWhiteSpace(value)
            ? defaultRecommendation
            : value);
    }

    private void SetRecommendationText(string menuName)
    {
        try
        {
            recommendationText.text = string.Format(recommendationFormat, menuName);
        }
        catch (FormatException)
        {
            recommendationText.text = menuName;
            Debug.LogWarning(
                "[FortunePrintLayoutController] 추천 메뉴 형식이 잘못되어 메뉴 이름만 사용합니다.",
                this);
        }
    }
}
