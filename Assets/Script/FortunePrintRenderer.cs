using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class FortunePrintRenderer : MonoBehaviour
{
    private const string DefaultPngFileNameFormat = "fortune_{0:yyyyMMdd_HHmmss}.png";

    [Header("Print Layout")]
    [SerializeField] private Camera printCamera;
    [SerializeField] private Canvas printCanvas;
    [SerializeField] private RectTransform printLayout;

    [Header("Output")]
    [SerializeField, Min(1)] private int outputWidth = 576;
    [SerializeField, Tooltip("Use {0:...} to insert the current date and time. Example: fortune_{0:yyyyMMdd_HHmmss}.png")]
    private string pngFileNameFormat = DefaultPngFileNameFormat;
    [SerializeField, Tooltip("Full folder path for saved PNGs. Leave blank to use Application.persistentDataPath.")]
    private string outputDirectoryPath;

    public string LastSavedPath { get; private set; }

    public Texture2D RenderToTexture()
    {
        if (!ValidateReferences())
        {
            return null;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(printLayout);
        Canvas.ForceUpdateCanvases();

        float layoutWidth = printLayout.rect.width;
        float layoutHeight = Mathf.Max(
            printLayout.rect.height,
            LayoutUtility.GetPreferredHeight(printLayout));

        if (layoutWidth <= 0f || layoutHeight <= 0f)
        {
            Debug.LogError(
                "[FortunePrintRenderer] Print layout width and height must be greater than zero.",
                this);
            return null;
        }

        int outputHeight = Mathf.CeilToInt(layoutHeight * outputWidth / layoutWidth);
        if (outputWidth > SystemInfo.maxTextureSize || outputHeight > SystemInfo.maxTextureSize)
        {
            Debug.LogError(
                $"[FortunePrintRenderer] Output size {outputWidth}x{outputHeight} exceeds " +
                $"the maximum texture size of {SystemInfo.maxTextureSize}.",
                this);
            return null;
        }

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            outputWidth,
            outputHeight,
            24,
            RenderTextureFormat.ARGB32);

        RenderTexture previousCameraTarget = printCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Camera previousCanvasCamera = printCanvas.worldCamera;

        try
        {
            printCamera.targetTexture = renderTexture;
            printCanvas.worldCamera = printCamera;

            Canvas.ForceUpdateCanvases();
            printCamera.Render();

            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(
                outputWidth,
                outputHeight,
                TextureFormat.RGBA32,
                false);
            texture.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
            texture.Apply();
            return texture;
        }
        finally
        {
            printCamera.targetTexture = previousCameraTarget;
            printCanvas.worldCamera = previousCanvasCamera;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    public void SavePng()
    {
        Texture2D texture = RenderToTexture();
        if (texture == null)
        {
            return;
        }

        string safeFileName = Path.GetFileName(GetFormattedFileName());
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = string.Format(DefaultPngFileNameFormat, System.DateTime.Now);
        }

        if (!safeFileName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            safeFileName += ".png";
        }

        string outputDirectory = string.IsNullOrWhiteSpace(outputDirectoryPath)
            ? Application.persistentDataPath
            : outputDirectoryPath.Trim();

        try
        {
            Directory.CreateDirectory(outputDirectory);
            LastSavedPath = Path.Combine(outputDirectory, safeFileName);
            File.WriteAllBytes(LastSavedPath, texture.EncodeToPNG());
        }
        catch (System.Exception exception) when (
            exception is System.ArgumentException ||
            exception is System.IO.IOException ||
            exception is System.UnauthorizedAccessException)
        {
            Debug.LogError(
                $"[FortunePrintRenderer] PNG 저장에 실패했습니다: {exception.Message}",
                this);
            Destroy(texture);
            return;
        }

        Destroy(texture);

        Debug.Log($"[FortunePrintRenderer] PNG saved: {LastSavedPath}", this);
    }

    private string GetFormattedFileName()
    {
        try
        {
            return string.Format(pngFileNameFormat, System.DateTime.Now);
        }
        catch (System.FormatException)
        {
            Debug.LogWarning(
                "[FortunePrintRenderer] Invalid PNG File Name Format. " +
                "Using the default filename format instead.",
                this);
            return string.Format(DefaultPngFileNameFormat, System.DateTime.Now);
        }
    }

    private bool ValidateReferences()
    {
        if (printCamera == null)
        {
            Debug.LogError("[FortunePrintRenderer] Print Camera is not assigned.", this);
            return false;
        }

        if (printCanvas == null)
        {
            Debug.LogError("[FortunePrintRenderer] Print Canvas is not assigned.", this);
            return false;
        }

        if (printCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogError(
                "[FortunePrintRenderer] Print Canvas must use Screen Space - Camera or World Space.",
                this);
            return false;
        }

        if (printLayout == null)
        {
            Debug.LogError("[FortunePrintRenderer] Print Layout is not assigned.", this);
            return false;
        }

        return true;
    }
}
