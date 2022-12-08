using System.Runtime.InteropServices;

namespace SDRInterface.HackRF;

internal enum HackRfError : int
{
    Success = 0,
    True = 1,
    InvalidParam = -2,
    NotFound = -5,
    Busy = -6,
    NoMem = -11,
    LibUsb = -1000,
    Thread = -1001,
    StreamingThreadErr = -1002,
    StreamingStopped = -1003,
    StreamingExitCalled = -1004,
    UsbApiVersion = -1005,
    NotLastDevice = -2000,
    Other = -9999,
}

internal enum HackRfBoardId : byte
{
    Jellybean = 0,
    Jawbreaker = 1,
    HackRfOne = 2,
    Rad1O = 3,
    Invalid = 0xFF,
}

internal enum HackRfUsbBoardId : ushort
{
    Jawbreaker = 0x604b,
    HackRfOne = 0x6089,
    Rad1O = 0xcc15,
    Invalid = 0xffff
}

internal enum RfPathFilter
{
    Bypass = 0,
    LowPass = 1,
    HighPass = 2
}

internal unsafe struct HackRfTransfer
{
    public IntPtr device;
    public byte* buffer;
    public int bufferLength;
    public int validLength;
    public IntPtr rxCtx;
    public IntPtr txCtx;
}

internal unsafe struct ReadPartIdAndSerialNoRes
{
    public fixed uint partId[2];
    public fixed uint SerialNo[4];
}

internal unsafe struct HackRfDeviceList
{
    public sbyte** serialNumbers;
    public HackRfUsbBoardId* usbBoardIds;
    public int* usbDeviceIndex;
    public int deviceCount;

    public void** usbDevices;
    public int usbDeviceCount;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate int HackRfSampleBlockCallback(HackRfTransfer* transfer);

internal static unsafe class HackRfApi
{
    private const string LibraryName = "hackrf";
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_init")]
    public static extern HackRfError Init();

    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_exit")]
    public static extern HackRfError Exit();

    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_device_list")]
    public static extern HackRfDeviceList* DeviceList();
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_device_list_open")]
    public static extern HackRfError DeviceListOpen(HackRfDeviceList* list, int idx, out IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_device_list_free")]
    public static extern void DeviceListFree(HackRfDeviceList* list);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_open")]
    public static extern HackRfError Open(out IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_open_by_serial")]
    public static extern HackRfError OpenBySerial(string desiredSerialNumber, out IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_close")]
    public static extern HackRfError Close(IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_start_rx")]
    public static extern HackRfError StartRx(IntPtr device, HackRfSampleBlockCallback callback, IntPtr rxCtx);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_stop_rx")]
    public static extern HackRfError StopRx(IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_start_tx")]
    public static extern HackRfError StartTx(IntPtr device, HackRfSampleBlockCallback callback, IntPtr txCtx);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_stop_tx")]
    public static extern HackRfError StopTx(IntPtr device);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_is_streaming")]
    public static extern HackRfError IsStreaming(IntPtr dev);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_si5351c_read")]
    public static extern HackRfError ReadSI5351C(IntPtr dev, ushort registerNumber, out ushort value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_baseband_filter_bandwidth")]
    public static extern HackRfError SetBasebandFilterBandwidth(IntPtr device, uint bandwidthHz);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_board_id_read")]
    public static extern HackRfError ReadBoardId(IntPtr device, out HackRfBoardId value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_version_string_read")]
    public static extern HackRfError ReadVersionString(IntPtr device, sbyte* version, byte length);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_freq")]
    public static extern HackRfError SetFreq(IntPtr device, ulong freqHz);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_sample_rate")]
    public static extern HackRfError SetSampleRate(IntPtr device, double freqHz);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_amp_enable")]
    public static extern HackRfError SetAmpEnable(IntPtr device, byte value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_board_partid_serialno_read")]
    public static extern HackRfError ReadBoardPartIdAndSerialNo(IntPtr device, out ReadPartIdAndSerialNoRes result);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_lna_gain")]
    public static extern HackRfError SetLnaGain(IntPtr device, uint value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_vga_gain")]
    public static extern HackRfError SetVgaGain(IntPtr device, uint value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_txvga_gain")]
    public static extern HackRfError SetTxVgaGain(IntPtr device, uint value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_set_antenna_enable")]
    public static extern HackRfError SetAntennaEnable(IntPtr device, uint value);
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_error_name")]
    private static extern sbyte* ErrorNameInternal(HackRfError errcode);
    
    public static string ErrorName(HackRfError errcode) => new string(ErrorNameInternal(errcode));
    
    [DllImport(LibraryName, CallingConvention=CallingConvention.Cdecl, EntryPoint = "hackrf_board_id_name")]
    private static extern sbyte* BoardIdNameInternal(HackRfBoardId boardId);

    public static string BoardIdName(HackRfBoardId boardId) => new string(BoardIdNameInternal(boardId));
    
    
}