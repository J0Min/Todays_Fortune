using System;
using System.IO;
using UnityEngine;

public sealed class BixolonUsbPrinter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private FortunePrintRenderer printRenderer;

    [Header("Connection")]
    [SerializeField] private bool connectOnEnable;
    [SerializeField] private bool initializeAfterConnect = true;

    [Header("Print")]
    [SerializeField, Range(-96, 96), Tooltip("양수는 인쇄 내용을 오른쪽으로 이동합니다. 단위는 203dpi 프린터의 dot입니다.")]
    private int horizontalOffsetDots;
    [SerializeField, Range(0, 100)] private int brightness = 50;
    [SerializeField] private bool dithering = true;
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

    public void PrintFortune()
    {
        if (IsPrinting)
        {
            Debug.LogWarning("[BixolonUsbPrinter] 이전 인쇄가 아직 진행 중입니다.", this);
            return;
        }

        if (printRenderer == null)
        {
            Debug.LogError("[BixolonUsbPrinter] FortunePrintRenderer가 지정되지 않았습니다.", this);
            return;
        }

        Texture2D texture = printRenderer.RenderToTexture();
        if (texture == null)
        {
            return;
        }

        string bitmapPath = Path.Combine(
            Application.temporaryCachePath,
            $"bixolon_fortune_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp");

        try
        {
            File.WriteAllBytes(bitmapPath, Bmp24Encoder.Encode(texture, horizontalOffsetDots));
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
                dithering);

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
