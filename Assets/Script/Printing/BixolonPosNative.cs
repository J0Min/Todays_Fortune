using System.Runtime.InteropServices;

internal static class BixolonPosNative
{
    internal const int Success = 0;

    internal const int InterfaceUsb = 2;

    internal const int AlignmentLeft = 0;
    internal const int AlignmentCenter = 1;
    internal const int AlignmentRight = 2;

    internal const int WidthFull = -1;
    internal const int WidthOriginal = -2;

    internal const int StatusPaperEmpty = 1;
    internal const int StatusCoverOpen = 2;
    internal const int StatusPaperNearEnd = 4;
    internal const int StatusAutoCutterError = 8;
    internal const int StatusOffline = 32;
    internal const int StatusNotOpen = 64;
    internal const int StatusPaperToBeTaken = 256;
    internal const int StatusStackerFull = 16384;
    internal const int StatusStackerError = 32768;

    private const string LibraryName = "BXLPAPI_x64";

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int ConnectUsb();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PrinterClose();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int InitializePrinter();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int GetPrinterCurrentStatus();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int TransactionStart();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int TransactionEnd(
        [MarshalAs(UnmanagedType.Bool)] bool sendCompletedCheck,
        int timeoutMilliseconds);

    [DllImport(
        LibraryName,
        CallingConvention = CallingConvention.StdCall,
        CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    internal static extern int PrintBitmapW(
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        int width,
        int alignment,
        int level,
        [MarshalAs(UnmanagedType.Bool)] bool dithering);

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int LineFeed(int feed);

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int CutPaper();

    [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PaperEject(int option);
}
