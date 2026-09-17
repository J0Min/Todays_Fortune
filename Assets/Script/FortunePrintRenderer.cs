using System.Collections;
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
    [SerializeField] private FortunePrintLayoutController printLayoutController;

    [Header("Output")]
    [SerializeField, Min(1)] private int outputWidth = 576;
    [SerializeField, Tooltip("Use {0:...} to insert the current date and time. Example: fortune_{0:yyyyMMdd_HHmmss}.png")]
    private string pngFileNameFormat = DefaultPngFileNameFormat;
    [SerializeField, Tooltip("Full folder path for saved PNGs. Leave blank to use Application.persistentDataPath.")]
    private string outputDirectoryPath;
    [SerializeField] private bool autoSaveOnStart;

    public string LastSavedPath { get; private set; }
    private RenderTexture printRenderTexture;

    private void OnEnable()
    {
        if (ValidateReferences(false) && TryGetOutputSize(out int width, out int height))
        {
            EnsureRenderTarget(width, height);
        }
    }

    private void OnDisable()
    {
        ReleaseRenderTarget();
    }

    private IEnumerator Start()
    {
        if (!autoSaveOnStart)
        {
            yield break;
        }

        yield return null;
        SavePng();
    }

    public Texture2D RenderToTexture()
    {
        if (!ValidateReferences())
        {
            return null;
        }

        if (printLayoutController != null)
        {
            printLayoutController.RefreshForPrint();
        }

        if (!TryGetOutputSize(out int renderWidth, out int renderHeight))
        {
            return null;
        }

        EnsureRenderTarget(renderWidth, renderHeight);

        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Canvas.ForceUpdateCanvases();
            printCanvas.GetComponent<RectTransform>().ForceUpdateRectTransforms();
            printLayout.ForceUpdateRectTransforms();
            printCamera.Render();

            RenderTexture.active = printRenderTexture;
            Texture2D texture = new Texture2D(
                renderWidth,
                renderHeight,
                TextureFormat.RGBA32,
                false);
            texture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            texture.Apply();
            return texture;
        }
        finally
        {
            RenderTexture.active = previousActive;
        }
    }

    private bool TryGetOutputSize(out int width, out int height)
    {
        width = 0;
        height = 0;

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
            return false;
        }

        width = outputWidth;
        height = Mathf.CeilToInt(layoutHeight * outputWidth / layoutWidth);
        if (width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
        {
            Debug.LogError(
                $"[FortunePrintRenderer] Output size {width}x{height} exceeds " +
                $"the maximum texture size of {SystemInfo.maxTextureSize}.",
                this);
            return false;
        }

        return true;
    }

    private void EnsureRenderTarget(int width, int height)
    {
        if (printRenderTexture != null &&
            printRenderTexture.width == width &&
            printRenderTexture.height == height)
        {
            return;
        }

        ReleaseRenderTarget();
        printRenderTexture = new RenderTexture(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32)
        {
            name = "Fortune Print Render Texture"
        };
        printRenderTexture.Create();

        printCamera.targetTexture = printRenderTexture;
        printCanvas.worldCamera = printCamera;
        printCanvas.enabled = false;
        printCanvas.enabled = true;
        Canvas.ForceUpdateCanvases();
    }

    private void ReleaseRenderTarget()
    {
        if (printRenderTexture == null)
        {
            return;
        }

        if (printCamera != null && printCamera.targetTexture == printRenderTexture)
        {
            printCamera.targetTexture = null;
        }

        printRenderTexture.Release();
        if (Application.isPlaying)
        {
            Destroy(printRenderTexture);
        }
        else
        {
            DestroyImmediate(printRenderTexture);
        }

        printRenderTexture = null;
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

    private bool ValidateReferences(bool logErrors = true)
    {
        if (printCamera == null)
        {
            if (logErrors)
            {
                Debug.LogError("[FortunePrintRenderer] Print Camera is not assigned.", this);
            }
            return false;
        }

        if (printCanvas == null)
        {
            if (logErrors)
            {
                Debug.LogError("[FortunePrintRenderer] Print Canvas is not assigned.", this);
            }
            return false;
        }

        if (printCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (logErrors)
            {
                Debug.LogError(
                    "[FortunePrintRenderer] Print Canvas must use Screen Space - Camera or World Space.",
                    this);
            }
            return false;
        }

        if (printLayout == null)
        {
            if (logErrors)
            {
                Debug.LogError("[FortunePrintRenderer] Print Layout is not assigned.", this);
            }
            return false;
        }

        return true;
    }
}
