using System.Globalization;
using System.Runtime.InteropServices;

namespace SDRInterface.Airspy;

[Registry("airspy")]
public unsafe partial class AirspyDevice : Device
{
    private const uint DefaultBufferBytes = 262144;
    private const uint DefaultNumBuffers = 8;
    
    private ulong _serial;
    private IntPtr _dev;

    private uint _sampleRate = 3000000;
    private uint _centerFrequency = 0;
    private uint _bufferLength;
    private uint _numBuffers = DefaultNumBuffers;
    private bool _agcMode;
    private bool _streamActive;
    private bool _rfBias;
    private bool _bitPack;
    private int _sampleRateChanged;
    private int _bytesPerSample;
    private byte _lnaGain;
    private byte _mixerGain;
    private byte _vgaGain;

    private struct Buff
    {
        public byte* data;
        public int length;
    }
    
    private object _bufMutex = new object();
    private Buff[] _buffs;
    private uint _bufHead;
    private uint _bufTail;
    private uint _bufCount;
    private byte* _currentBuff;
    private int _overflowEvent;
    private uint _bufferedElems;
    private int _currentHandle;
    private bool _resetBuffer;

    private GCHandle _gcHandle;

    private static AirspySampleBlockCallback _rxCallback = new AirspySampleBlockCallback(RxCallbackStatic);

    public AirspyDevice(IDictionary<string, string> args)
    {
        if (args.ContainsKey("serial"))
        {
            if (!ulong.TryParse(args["serial"], NumberStyles.HexNumber, NumberFormatInfo.CurrentInfo, out _serial))
            {
                throw new Exception("Invalid serial number");
            }

            if (AirspyApi.OpenSerial(out _dev, _serial) != AirspyError.Success)
            {
                throw new Exception("Cannot open Airspy device with serial " + args["serial"]);
            }
            
            Logger.LogF(LogLevel.Debug, "Found Airspy device with serial {0:X8}", _serial);
        }
        else
        {
            if (AirspyApi.Open(out _dev) != AirspyError.Success)
                throw new Exception("Cannot open Airspy device");
        }

        foreach (var info in SettingInfo)
        {
            if (args.ContainsKey(info.Key))
                WriteSettingString(info.Key, args[info.Key]);
        }
        
        _gcHandle = GCHandle.Alloc(this);
    }

    public override void Dispose()
    {
        AirspyApi.Close(_dev);
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();
        GC.SuppressFinalize(this);
    }

    ~AirspyDevice()
    {
        Dispose();
    }

    public override string DriverKey => "Airspy";

    public override string HardwareKey => "Airspy R2/Mini";

    public override IDictionary<string, string> HardwareInfo => new Dictionary<string, string>()
    {
        { "serial", _serial.ToString("x8") }
    };

    #region Channels API

    public override int GetNumChannels(Direction direction) => direction == Direction.Rx ? 1 : 0;

    #endregion

    #region Antenna API

    public override IList<string> ListAntennas(Direction direction, uint channel) => new List<string>() { "RX" };

    public override void SetAntenna(Direction direction, uint channel, string name)
    {
    }

    public override string GetAntenna(Direction direction, uint channel) => "RX";

    #endregion

    #region Frontend corrections API

    public override bool HasDCOffsetMode(Direction direction, uint channel) => false;

    #endregion

    #region Gain API

    public override IList<string> ListGains(Direction direction, uint channel)
        => new List<string>() { "LNA", "MIX", "VGA" };

    public override bool HasGainMode(Direction direction, uint channel) => true;

    public override void SetGainMode(Direction direction, uint channel, bool automatic)
    {
        _agcMode = automatic;

        AirspyApi.SetLnaAgc(_dev, _agcMode ? (byte) 1 : (byte) 0);
        AirspyApi.SetMixerAgc(_dev, _agcMode ? (byte) 1 : (byte) 0);
        
        Logger.LogF(LogLevel.Debug, "Setting AGC to {0}", automatic ? "Automatic" : "Manual");
    }

    public override bool GetGainMode(Direction direction, uint channel) => _agcMode;

    public override void SetGain(Direction direction, uint channel, string name, double value)
    {
        if (name == "LNA")
        {
            _lnaGain = (byte)value;
            AirspyApi.SetLnaGain(_dev, _lnaGain);
        }
        else if (name == "MIX")
        {
            _mixerGain = (byte)value;
            AirspyApi.SetMixerGain(_dev, _mixerGain);
        }
        else if (name == "VGA")
        {
            _vgaGain = (byte)value;
            AirspyApi.SetVgaGain(_dev, _vgaGain);
        }
    }

    public override double GetGain(Direction direction, uint channel, string name)
    {
        if (name == "LNA")
        {
            return _lnaGain;
        }
        else if (name == "MIX")
        {
            return _mixerGain;
        }
        else if (name == "VGA")
        {
            return _vgaGain;
        }

        return 0;
    }

    public override Range GetGainRange(Direction direction, uint channel, string name)
    {
        if (name == "LNA" || name == "MIX" || name == "VGA")
            return new Range(0, 15);
        return new Range(0, 0);
    }

    #endregion

    #region Frequency API

    public override void SetFrequency(Direction direction, uint channel, string name, double frequency, IDictionary<string, string> kwargs)
    {
        if (name == "RF")
        {
            _centerFrequency = (uint)frequency;
            _resetBuffer = true;
            Logger.LogF(LogLevel.Debug, "Setting center freq: {0}", _centerFrequency);
            AirspyApi.SetFreq(_dev, _centerFrequency);
        }
    }

    public override double GetFrequency(Direction direction, uint channel, string name)
    {
        if (name == "RF") return _centerFrequency;
        return 0;
    }

    public override IList<string> ListFrequencies(Direction direction, uint channel)
        => new List<string>() { "RF" };

    public override IList<Range> GetFrequencyRange(Direction direction, uint channel, string name)
    {
        var result = new List<Range>();
        if (name == "RF") result.Add(new Range(24000000, 1800000000));
        return result;
    }

    public override IList<ArgInfo> GetFrequencyArgsInfo(Direction direction, uint channel) => new List<ArgInfo>();

    #endregion

    #region Sample Rate API

    public override void SetSampleRate(Direction direction, uint channel, double rate)
    {
        Logger.LogF(LogLevel.Debug, "Setting sample rate: {0}", _sampleRate);

        if (_sampleRate != (uint) rate)
        {
            _sampleRate = (uint)rate;
            _resetBuffer = true;
            Interlocked.Exchange(ref _sampleRateChanged, 1);
        }
    }

    public override double GetSampleRate(Direction direction, uint channel)
    {
        return _sampleRate;
    }

    public override IList<double> ListSampleRates(Direction direction, uint channel)
    {
        var results = new List<double>();

        var numRates = 0u;
        AirspyApi.GetSampleRates(_dev, &numRates, 0);

        var sampleRates = new uint[numRates];
        fixed (uint* pSampleRates = sampleRates)
            AirspyApi.GetSampleRates(_dev, pSampleRates, numRates);

        foreach (var i in sampleRates)
        {
            results.Add(i);
        }

        return results;
    }

    public override IList<double> ListBandwidths(Direction direction, uint channel)
        => new List<double>();

    #endregion

    #region Settings API

    public override IList<ArgInfo> SettingInfo => new List<ArgInfo>()
    {
        new ArgInfo()
        {
            Key = "biastee",
            Value = "false",
            Name = "Bias tee",
            Description = "Enable the 4.5v DC Bias tee to power SpyVerter / LNA / etc. via antenna connection.",
            Type = ArgType.Bool
        },
        new ArgInfo()
        {
            Key = "bitpack",
            Value = "false",
            Name = "Bit Pack",
            Description = "Enable packing 4 12-bit samples into 3 16-bit words for 25% less USB trafic.",
            Type = ArgType.Bool
        }
    };

    public override void WriteSettingString(string key, string value)
    {
        if (key == "biastee")
        {
            var enable = value == "true";
            _rfBias = enable;

            AirspyApi.SetRfBias(_dev, (byte)(enable ? 1 : 0));
        }

        if (key == "bitpack")
        {
            var enable = value == "true";
            _bitPack = enable;

            AirspyApi.SetPacking(_dev, (byte)(enable ? 1 : 0));
        }
    }

    public override string ReadSettingString(string key)
    {
        if (key == "biastee")
            return _rfBias.ToString();
        if (key == "bitpack")
            return _bitPack.ToString();

        return "";
    }

    #endregion
}