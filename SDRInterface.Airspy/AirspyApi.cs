using System.Runtime.InteropServices;

namespace SDRInterface.Airspy;

internal enum AirspyError
{
    Success = 0,
    True = 1,
    InvalidParam = -2,
    NotFound = -5,
    Busy = -6,
    NoMemory = -11,
    Libusb = -1000,
    Thread = -1001,
    StreamingThreadErr = -1002,
    StreamingStopped = -1003,
    Other = -9999
}
internal enum AirspySampleType
{
    Float32IQ = 0,
    Float32Real = 1,
    Int16IQ = 2,
    Int16Real = 3,
    UInt16Real = 4,
    Raw = 5, 
    End = 6
}

internal unsafe struct AirspyTransfer
{
    public IntPtr device;
    public IntPtr ctx;
    public void* samples;
    public int sampleCount;
    public ulong droppedSamples;
    public AirspySampleType sampleType;
}
internal struct AirspyLibVersion
{
    public uint majorVersion;
    public uint minorVersion;
    public uint reversion;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate int AirspySampleBlockCallback(AirspyTransfer* transfer);

internal static unsafe class AirspyApi
{
    private const string LibraryName = "airspy";

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_lib_version")]
    public static extern void LibVersion(out AirspyLibVersion libVersion);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_init")]
    public static extern AirspyError Init();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_exit")]
    public static extern AirspyError Exit();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_list_devices")]
    public static extern int ListDevices(ulong* serials, int count);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_open_sn")]
    public static extern AirspyError OpenSerial(out IntPtr device, ulong serialNumber);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_open")]
    public static extern AirspyError Open(out IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_close")]
    public static extern AirspyError Close(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_get_samplerates")]
    public static extern AirspyError GetSampleRates(IntPtr device, uint* buffer, uint len);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_samplerate")]
    public static extern AirspyError SetSampleRate(IntPtr device, uint sampleRate);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_start_rx")]
    public static extern AirspyError StartRx(IntPtr device, AirspySampleBlockCallback callback, IntPtr ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_stop_rx")]
    public static extern AirspyError StopRx(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_is_streaming")]
    public static extern AirspyError IsStreaming(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_sample_type")]
    public static extern AirspyError SetSampleType(IntPtr device, AirspySampleType sampleType);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_freq")]
    public static extern AirspyError SetFreq(IntPtr device, uint freqHz);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_lna_gain")]
    public static extern AirspyError SetLnaGain(IntPtr device, byte value);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_mixer_gain")]
    public static extern AirspyError SetMixerGain(IntPtr device, byte value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_vga_gain")]
    public static extern AirspyError SetVgaGain(IntPtr device, byte value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_lna_agc")]
    public static extern AirspyError SetLnaAgc(IntPtr device, byte value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_mixer_agc")]
    public static extern AirspyError SetMixerAgc(IntPtr device, byte value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_rf_bias")]
    public static extern AirspyError SetRfBias(IntPtr device, byte value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "airspy_set_packing")]
    public static extern AirspyError SetPacking(IntPtr device, byte value);
}
