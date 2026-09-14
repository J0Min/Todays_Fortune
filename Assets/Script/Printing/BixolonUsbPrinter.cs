using System;
using System.IO;
using UnityEngine;

public enum BixolonPrintImageSource
{
    AssignedImage = 0,
    FortuneRenderer = 1
}

public sealed class BixolonUsbPrinter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField, Tooltip("Assigned Image는 Inspector에 지정한 이미지를, Fortune Renderer는 기존 PrintLayout을 출력합니다.")]
    private BixolonPrintImageSource imageSource = BixolonPrintImageSource.AssignedImage;
    [SerializeField, Tooltip("출력할 PNG/JPG Texture 에셋을 지정합니다. Read/Write 옵션을 켤 필요는 없습니다.")]
    private Texture2D assignedImage;
    [SerializeField, Min(1), Tooltip("지정 이미지의 출력 폭(dot)입니다. BK3-31 최대 인쇄 폭은 576dot입니다.")]
    private int assignedImageOutputWidth = 576;
    [SerializeField] private FortunePrintRenderer printRenderer;

    [Header("Connection")]
    [SerializeField] private bool connectOnEnable;
    [SerializeField] private bool initializeAfterConnect = true;

    [Header("Print")]
    [SerializeField, Range(-96, 96), Tooltip("양수는 인쇄 내용을 오른쪽으로 이동합니다. 단위는 203dpi 프린터의 dot입니다.")]
    private int horizontalOffsetDots;
    [SerializeField, Range(0, 100)] private int brightness = 50;
    [SerializeField, Tooltip("SDK는 프린터 드라이버에 맡기고, 나머지는 Unity에서 BMP 생성 전에 흑백 처리합니다.")]
    private BixolonDitheringMode ditheringMode = BixolonDitheringMode.Sdk;
    [SerializeField, Range(0, 255), Tooltip("Unity 디더링 방식에서 사용합니다. 높을수록 검정 영역이 늘어납니다.")]
    private int blackWhiteThreshold = 128;
    [SerializeField, Range(0, 20)] private int lineFeedsAfterImage = 3;
    [SerializeField] private bool cutAfterPrint = true;
    [SerializeField, Min(100)] private int completionTimeoutMilliseconds = 5000;

    public bool IsConnected { get; private set; }
    public bool IsPrinting { get; private set; }
    public int LastResult { get; private set; }

    private readonly object printerLock = new object();

    private void OnEnable()
    {
        if (connectOnEnable)
        {
            ConnectUsb();
        }
    }

    private void OnDisable()
    {
        Disconnect();
    }

    public bool ConnectUsb()
    {
        lock (printerLock)
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                LastResult = BixolonPosNative.ConnectUsb();
                IsConnected = LastResult == BixolonPosNative.Success;

                if (IsConnected && initializeAfterConnect)
                {
                    LastResult = BixolonPosNative.InitializePrinter();
                    if (LastResult != BixolonPosNative.Success)
                    {
                        BixolonPosNative.PrinterClose();
                        IsConnected = false;
                    }
                }
            }
            catch (Exception exception) when (IsNativePluginException(exception))
            {
                IsConnected = false;
                LastResult = int.MinValue;
                Debug.LogError($"[BixolonUsbPrinter] SDK DLL을 불러오지 못했습니다: {exception.Message}", this);
                return false;
            }

            if (!IsConnected)
            {
                Debug.LogError($"[BixolonUsbPrinter] USB 연결 실패: {DescribeResult(LastResult)}", this);
            }

            return IsConnected;
        }
    }

    public void Disconnect()
    {
        lock (printerLock)
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                LastResult = BixolonPosNative.PrinterClose();
            }
            catch (Exception exception) when (IsNativePluginException(exception))
            {
                Debug.LogError($"[BixolonUsbPrinter] 연결 해제 실패: {exception.Message}", this);
            }
            finally
            {
                IsConnected = false;
            }
        }
    }

    public void PrintImage()
    {
        if (IsPrinting)
        {
            Debug.LogWarning("[BixolonUsbPrinter] 이전 인쇄가 아직 진행 중입니다.", this);
            return;
        }

        Texture2D texture = CreatePrintTexture();
        if (texture == null)
        {
            return;
        }

        string bitmapPath = Path.Combine(
            Application.temporaryCachePath,
            $"bixolon_print_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp");

        try
        {
            File.WriteAllBytes(
                bitmapPath,
                Bmp24Encoder.Encode(
                    texture,
                    horizontalOffsetDots,
                    ditheringMode,
                    blackWhiteThreshold));
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException)
        {
            Debug.LogError($"[BixolonUsbPrinter] 임시 BMP 생성 실패: {exception.Message}", this);
            return;
        }
        finally
        {
            Destroy(texture);
        }

        IsPrinting = true;
        try
        {
            LastResult = PrintBitmapFile(bitmapPath);
            if (LastResult == BixolonPosNative.Success)
            {
                Debug.Log("[BixolonUsbPrinter] 인쇄 명령을 완료했습니다.", this);
            }
            else
            {
                Debug.LogError($"[BixolonUsbPrinter] 인쇄 실패: {DescribeResult(LastResult)}", this);
            }
        }
        finally
        {
            IsPrinting = false;
            TryDelete(bitmapPath);
        }
    }

    public void SetAssignedImage(Texture2D image)
    {
        assignedImage = image;
        imageSource = BixolonPrintImageSource.AssignedImage;
    }

    public void PrintTexture(Texture2D image)
    {
        if (image == null)
        {
            Debug.LogError("[BixolonUsbPrinter] 출력할 이미지가 null입니다.", this);
            return;
        }

        SetAssignedImage(image);
        PrintImage();
    }

    // 기존 버튼이나 스크립트 연결을 깨지 않기 위한 호환용 메서드입니다.
    public void PrintFortune()
    {
        PrintImage();
    }

    public int GetPrinterStatus()
    {
        lock (printerLock)
        {
            if (!IsConnected)
            {
                return BixolonPosNative.StatusNotOpen;
            }

            try
            {
                return BixolonPosNative.GetPrinterCurrentStatus();
            }
            catch (Exception exception) when (IsNativePluginException(exception))
            {
                Debug.LogError($"[BixolonUsbPrinter] 상태 확인 실패: {exception.Message}", this);
                return BixolonPosNative.StatusNotOpen;
            }
        }
    }

    private int PrintBitmapFile(string bitmapPath)
    {
        lock (printerLock)
        {
            if (!IsConnected && !ConnectUsb())
            {
                return LastResult;
            }

            int result = BixolonPosNative.TransactionStart();
            if (result != BixolonPosNative.Success)
            {
                return result;
            }

            result = BixolonPosNative.PrintBitmapW(
                bitmapPath,
                BixolonPosNative.WidthFull,
                BixolonPosNative.AlignmentCenter,
                brightness,
                ditheringMode == BixolonDitheringMode.Sdk);

            if (result == BixolonPosNative.Success && lineFeedsAfterImage > 0)
            {
                result = BixolonPosNative.LineFeed(lineFeedsAfterImage);
            }

            if (result == BixolonPosNative.Success && cutAfterPrint)
            {
                result = BixolonPosNative.CutPaper();
            }

            int endResult = BixolonPosNative.TransactionEnd(
                result == BixolonPosNative.Success,
                completionTimeoutMilliseconds);

            return result != BixolonPosNative.Success ? result : endResult;
        }
    }

    private Texture2D CreatePrintTexture()
    {
        if (imageSource == BixolonPrintImageSource.FortuneRenderer)
        {
            if (printRenderer == null)
            {
                Debug.LogError("[BixolonUsbPrinter] Fortune Renderer가 지정되지 않았습니다.", this);
                return null;
            }

            return printRenderer.RenderToTexture();
        }

        if (assignedImage == null)
        {
            Debug.LogError("[BixolonUsbPrinter] Assigned Image에 출력할 이미지가 지정되지 않았습니다.", this);
            return null;
        }

        int outputWidth = Mathf.Clamp(assignedImageOutputWidth, 1, SystemInfo.maxTextureSize);
        int outputHeight = Mathf.Max(
            1,
            Mathf.RoundToInt(assignedImage.height * (outputWidth / (float)assignedImage.width)));

        if (outputHeight > SystemInfo.maxTextureSize)
        {
            float scale = SystemInfo.maxTextureSize / (float)outputHeight;
            outputWidth = Mathf.Max(1, Mathf.RoundToInt(outputWidth * scale));
            outputHeight = SystemInfo.maxTextureSize;
        }

        RenderTexture temporary = RenderTexture.GetTemporary(
            outputWidth,
            outputHeight,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        RenderTexture previous = RenderTexture.active;
        Texture2D readableCopy = null;

        try
        {
            Graphics.Blit(assignedImage, temporary);
            RenderTexture.active = temporary;

            readableCopy = new Texture2D(
                outputWidth,
                outputHeight,
                TextureFormat.RGBA32,
                false);
            readableCopy.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
            readableCopy.Apply(false, false);
            return readableCopy;
        }
        catch (Exception exception)
        {
            if (readableCopy != null)
            {
                Destroy(readableCopy);
            }

            Debug.LogError($"[BixolonUsbPrinter] 지정 이미지 변환 실패: {exception.Message}", this);
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    public static string DescribeResult(int result)
    {
        switch (result)
        {
            case 0: return "성공";
            case -100: return "프린터 연결 실패";
            case -101: return "프린터가 연결되어 있지 않음";
            case -107: return "지원하지 않는 함수";
            case -108: return "잘못된 파라미터";
            case -110: return "인쇄 데이터 없음";
            case -111: return "트랜잭션 완료 응답 실패";
            case -112: return "트랜잭션이 시작되지 않음";
            case -120: return "문자 인코딩 실패";
            case -300: return "데이터 전송 실패";
            case -301: return "데이터 수신 실패";
            case -400: return "비트맵 파일 로드 실패";
            case -401: return "비트맵 버퍼 크기 오류";
            default: return $"알 수 없는 결과 코드 ({result})";
        }
    }

    private static bool IsNativePluginException(Exception exception)
    {
        return exception is DllNotFoundException ||
               exception is EntryPointNotFoundException ||
               exception is BadImageFormatException;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
