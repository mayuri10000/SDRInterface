using System.Runtime.InteropServices;
using System.Text;

namespace SDRInterface.RtlSdr;

internal enum RtlSdrTunerType
{
    Unknown,
    E4000,
    FC0012,
    FC0013,
    FC2580,
    R820T,
    R828D,
}

internal enum RtlSdrDirectSampMode
{
    IQ, I, Q, I_Below, Q_Below
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void RtlSdrReadAsyncCallback(byte* buf, uint len, IntPtr ctx);

internal static unsafe class RtlApi
{
    private const string LibraryName = "rtlsdr";
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_device_count")]
    public static extern uint GetDeviceCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_device_name")]
    private static extern IntPtr GetDeviceNameInternal(uint index);

    public static string GetDeviceName(uint index) => Marshal.PtrToStringAnsi(GetDeviceNameInternal(index));

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_device_usb_strings")]
    private static extern int GetDeviceUsbStringsInternal(
        uint index,
        sbyte* manufact,
        sbyte* product,
        sbyte* serial);

    public static int GetDeviceUsbStrings(uint index, out string manufact, out string product, out string serial)
    {
        var m = stackalloc sbyte[256];
        var p = stackalloc sbyte[256];
        var s = stackalloc sbyte[256];
        var ret = GetDeviceUsbStringsInternal(index, m, p, s);
        manufact = new string(m);
        product = new string(p);
        serial = new string(s);
        return ret;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_index_by_serial", CharSet = CharSet.Ansi)]
    public static extern int GetIndexBySerial(string serial);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_open")]
    public static extern int Open(out IntPtr dev, uint index);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_close")]
    public static extern int Close(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_write_eeprom")]
    private static extern int WriteEEPROMInternal(IntPtr dev, byte* data, byte offset, ushort len);

    public static int WriteEEPROM(IntPtr dev, byte[] data, byte offset)
    {
        fixed (byte* p = data)
            return WriteEEPROMInternal(dev, p, offset, (ushort)data.Length);
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_center_freq")]
    public static extern int SetCenterFreq(IntPtr dev, uint freq);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_center_freq")]
    public static extern uint GetCenterFreq(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_freq_correction")]
    public static extern int SetFreqCorrection(IntPtr dev, int ppm);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_freq_correction")]
    public static extern int GetFreqCorrection(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_tuner_gains")]
    public static extern int GetTunerGains(IntPtr dev, [In, Out] int[] gains);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_tuner_type")]
    public static extern RtlSdrTunerType GetTunerType(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_tuner_gain")]
    public static extern int SetTunerGain(IntPtr dev, int gain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_tuner_bandwidth")]
    public static extern int SetTunerBandwidth(IntPtr dev, uint bw);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_tuner_gain")]
    public static extern int GetTunerGain(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_tuner_if_gain")]
    public static extern int SetTunerIFGain(IntPtr dev, int stage, int gain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_tuner_gain_mode")]
    public static extern int SetTunerGainMode(IntPtr dev, int manual);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_sample_rate")]
    public static extern int SetSampleRate(IntPtr dev, uint rate);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_get_sample_rate")]
    public static extern uint GetSampleRate(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_test_mode")]
    public static extern int SetTestMode(IntPtr dev, int on);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_agc_mode")]
    public static extern int SetAgcMode(IntPtr dev, int on);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_direct_sampling")]
    public static extern int SetDirectSampling(IntPtr dev, int on);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_offset_tuning")]
    public static extern int SetOffsetTuning(IntPtr dev, int on);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_reset_buffer")]
    public static extern int ResetBuffer(IntPtr dev);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_read_async")]
    public static extern int ReadAsync(
        IntPtr dev,
        RtlSdrReadAsyncCallback cb,
        IntPtr ctx,
        uint bufNum,
        uint bufLen);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_cancel_async")]
    public static extern int CancelAsync(IntPtr dev);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_bias_tee")]
    public static extern int SetBiasTee(IntPtr dev, int on);
}