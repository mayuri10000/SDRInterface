using System.Runtime.InteropServices;

namespace SDRInterface.RtlSdr;

[Registry("rtlsdr")]
public unsafe partial class RtlSdrDevice : Device
{
    private const int DefaultBufferLength = 16 * 32 * 512;
    private const int DefaultNumBuffers = 15;
    private const int BytesPerSample = 2;
    
    private enum RxFormat
    {
        Float32, Int16, Int8
    }
    
    private int _deviceId = -1;
    private IntPtr _dev = IntPtr.Zero;
    private GCHandle _gcHandle;

    private RxFormat _rxFormat = RxFormat.Float32;
    private RtlSdrTunerType _tunerType = RtlSdrTunerType.R820T;
    private uint _sampleRate = 2048000;
    private uint _centerFrequency = 100000000;
    private uint _bandwidth = 0;
    private int _ppm = 0;
    private int _directSamplingMode = 0;
    private uint _numBuffers = DefaultNumBuffers;
    private uint _bufferLength = DefaultBufferLength;
    private uint _asyncBuffs = 0;
    private bool _iqSwap = false;
    private bool _gainMode = false;
    private bool _offsetMode = false;
    private bool _digitalAgc = false;
    private bool _testMode = false;
    private bool _biasTee = false;
    private double[] _ifGain = new double[6];
    private double _tunerGain;
    private long _ticks;

    private double _gainMin = 0.0;
    private double _gainMax = 0.0;

    public RtlSdrDevice(IDictionary<string, string> args)
    {
        if (args.ContainsKey("label"))
            Logger.LogF(LogLevel.Info, "Opening {0}...", args["label"]);

        // if a serial is not present, then FindRTLSDR had zero devices enumerated
        if (!args.ContainsKey("serial"))
            throw new Exception("No RTL-SDR devices found!");

        var serial = args["serial"];
        _deviceId = RtlApi.GetIndexBySerial(serial);
        if (_deviceId < 0)
            throw new Exception($"RtlApi.GetIndexBySerial({serial}) returned {_deviceId}");

        if (args.ContainsKey("tuner")) _tunerType = StringToTuner(args["tuner"]);
        Logger.LogF(LogLevel.Debug, "RTL-SDR Tuner type: {0}", TunerToString(_tunerType));
        
        Logger.LogF(LogLevel.Debug, "RTL-SDR opening device {0}", _deviceId);
        if (RtlApi.Open(out _dev, (uint)_deviceId) != 0)
            throw new Exception("Failed to open RTL-SDR device");

        // extract min/max overall gain range
        var num_gains = RtlApi.GetTunerGains(_dev, null);
        if (num_gains > 0)
        {
            var gains = new int[num_gains];
            RtlApi.GetTunerGains(_dev, gains);
            _gainMin = gains.Min() / 10.0;
            _gainMax = gains.Max() / 10.0;
        }
        
        _gcHandle = GCHandle.Alloc(this);
    }

    public override void Dispose()
    {
        // cleanup device handles
        RtlApi.Close(_dev);

        // free gc handle
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();
        GC.SuppressFinalize(this);
    }

    #region Registration

    [FindFunction]
    public static IList<IDictionary<string, string>> FindRtlSdr(IDictionary<string, string> args)
    {
        var results = new List<IDictionary<string, string>>();

        var count = RtlApi.GetDeviceCount();

        for (var i = 0u; i < count; i++)
        {
            if (RtlApi.GetDeviceUsbStrings(i, out var manufact, out var product, out var serial) != 0)
            {
                Logger.LogF(LogLevel.Error, "rtlsdr_get_device_usb_strings({0}) failed", i);
                continue;
            }
            Logger.LogF(LogLevel.Debug, "Manufacturer: {0}, Product name: {0}, Serial: {0}", manufact, product, serial);

            var devInfo = new Dictionary<string, string>();
            devInfo["label"] = RtlApi.GetDeviceName(i) + " :: " + serial;
            devInfo["product"] = product;
            devInfo["serial"] = serial;
            devInfo["manufacturer"] = manufact;
            devInfo["tuner"] = GetTuner(serial, i);
            
            if (args.ContainsKey("serial") && args["serial"] != serial) continue;
            
            results.Add(devInfo);
        }

        return results;
    }

    [MakeFunction]
    public static Device MakeRtlSdr(IDictionary<string, string> args)
    {
        return new RtlSdrDevice(args);
    }

    private static object _staticMutex = new object();
    private static Dictionary<string, string> _cache = new Dictionary<string, string>();
    
    private static string GetTuner(string serial, uint deviceIndex)
    {
        if (_cache.ContainsKey(serial)) return _cache[serial];

        var devTest = IntPtr.Zero;
        if (RtlApi.Open(out devTest, deviceIndex) != 0) return "unavailable";
        var tuner = TunerToString(RtlApi.GetTunerType(devTest));
        RtlApi.Close(devTest);
        _cache[serial] = tuner;
        return tuner;
    }

    #endregion

    #region Identification API
    public override string DriverKey => "RTLSDR";

    public override string HardwareKey
    {
        get
        {
            switch (RtlApi.GetTunerType(_dev))
            {
                case RtlSdrTunerType.Unknown:
                    return "UNKNOWN";
                case RtlSdrTunerType.E4000:
                    return "E4000";
                case RtlSdrTunerType.FC0012:
                    return "FC0012";
                case RtlSdrTunerType.FC0013:
                    return "FC0013";
                case RtlSdrTunerType.FC2580:
                    return "FC2580";
                case RtlSdrTunerType.R820T:
                    return "R820T";
                case RtlSdrTunerType.R828D:
                    return "R828D";
                default:
                    return "OTHER";
            }
        }
    }

    public override IDictionary<string, string> HardwareInfo => new Dictionary<string, string>()
    {
        // key/value pairs for any useful information
        // this also gets printed in --probe
        { "origin", "Mayuri is my waifu" }, // TODO: Github Url
        { "index", _deviceId.ToString() }
    };
    #endregion

    #region Channels API

    public override int GetNumChannels(Direction direction) => direction == Direction.Rx ? 1 : 0;

    public override bool GetFullDuplex(Direction direction, uint channel) => false;

    #endregion

    #region Antenna API

    public override IList<string> ListAntennas(Direction direction, uint channel) => new List<string>() { "RX" };

    public override void SetAntenna(Direction direction, uint channel, string name)
    {
        if (direction != Direction.Rx)
            throw new Exception("RTL-SDR only support RX");
    }

    public override string GetAntenna(Direction direction, uint channel) => "RX";

    #endregion

    #region Frontend corrections API

    public override bool HasDCOffsetMode(Direction direction, uint channel) => false;

    public override bool HasFrequencyCorrection(Direction direction, uint channel) => true;

    public override void SetFrequencyCorrection(Direction direction, uint channel, double value)
    {
        var r = RtlApi.SetFreqCorrection(_dev, (int)value);
        if (r == -2) 
            return; // CORR didn't actually change, we are done
        if (r != 0)
            throw new Exception("SetFrequencyCorrection failed");

        _ppm = RtlApi.GetFreqCorrection(_dev);
    }

    public override double GetFrequencyCorrection(Direction direction, uint channel) => (double)_ppm;

    #endregion

    #region Gain API

    public override IList<string> ListGains(Direction direction, uint channel)
    {
        // list available gain elements,
        // the functions below have a "name" parameter
        var result = new List<string>();

        if (_tunerType == RtlSdrTunerType.E4000)
        {
            result.Add("IF1");
            result.Add("IF2");
            result.Add("IF3");
            result.Add("IF4");
            result.Add("IF5");
            result.Add("IF6");
        }
        result.Add("TUNER");

        return result;
    }

    public override bool HasGainMode(Direction direction, uint channel) => true;

    public override void SetGainMode(Direction direction, uint channel, bool automatic)
    {
        Logger.LogF(LogLevel.Debug, "Setting RTL-SDR gain mode: {0}", automatic ? "Automatic" : "Manual");
        if (RtlApi.SetTunerGainMode(_dev, automatic ? 0 : 1) != 0)
            throw new Exception("SetGainMode failed");
        _gainMode = automatic;
    }

    public override bool GetGainMode(Direction direction, uint channel) => _gainMode;

    public override void SetGain(Direction direction, uint channel, string name, double value)
    {
        if ((name.Length >= 2) && name.StartsWith("IF"))
        {
            var stage = 1;
            if (name.Length > 2)
            {
                var stageIn = name[2] - '0';
                if (stageIn < 1 || stageIn > 6)
                    throw new ArgumentException("Invalid IF stage, 1 or 1-6 for E4000");
            }

            if (_tunerType == RtlSdrTunerType.E4000)
                _ifGain[stage - 1] = GetE4000Gain(stage, (int) value);

            else
                _ifGain[stage - 1] = value;

            RtlApi.SetTunerIFGain(_dev, stage, (int) (_ifGain[stage - 1] * 10.0));
        }

        if (name == "TUNER")
        {
            _tunerGain = value;
            RtlApi.SetTunerGain(_dev, (int) (_tunerGain * 10.0 ));
        }
    }

    public override double GetGain(Direction direction, uint channel, string name)
    {
        if ((name.Length >= 2) && name.StartsWith("IF"))
        {
            var stage = 1;
            if (name.Length > 2)
            {
                var stageIn = name[2] - '0';
                if (stageIn < 1 || stageIn > 6)
                    throw new ArgumentException("Invalid IF stage, 1 or 1-6 for E4000");
                else
                    stage = stageIn;
            }

            if (_tunerType == RtlSdrTunerType.E4000)
                return GetE4000Gain(stage, (int) _ifGain[stage - 1]);

            return _ifGain[stage - 1];
        }

        if (name == "TUNER")
            return _tunerGain;

        return 0;
    }

    public override Range GetGainRange(Direction direction, uint channel, string name)
    {
        if (_tunerType == RtlSdrTunerType.E4000 && name != "TUNER")
        {
            if (name == "IF1") return new Range(-3, 6);
            if (name == "IF2" || name == "IF3") return new Range(0, 9);
            if (name == "IF4") return new Range(0, 2);
            if (name == "IF5" || name == "IF6") return new Range(3, 15);
        }

        return new Range(_gainMin, _gainMax);
    }

    #endregion

    #region Frequency API

    public override void SetFrequency(Direction direction, uint channel, string name, double frequency, IDictionary<string, string> kwargs)
    {
        if (name == "RF")
        {
            var r = RtlApi.SetCenterFreq(_dev, (uint)frequency);
            if (r != 0)
            {
                throw new Exception("RTLSDR: Set RF frequency failed");
            }

            _centerFrequency = RtlApi.GetCenterFreq(_dev);
        }

        if (name == "CORR")
        {
            var r = RtlApi.SetFreqCorrection(_dev, (int)frequency);
            if (r == -2)
            {
                return;  // CORR didn't actually change, we are done
            }

            if (r != 0)
            {
                throw new Exception("RTLSDR: Set frequency correction failed");
            }

            _ppm = RtlApi.GetFreqCorrection(_dev);
        }
    }

    public override double GetFrequency(Direction direction, uint channel, string name)
    {
        if (name == "RF") return _centerFrequency;
        if (name == "CORR") return _ppm;

        return 0;
    }

    public override IList<string> ListFrequencies(Direction direction, uint channel) => new List<string>() { "RF", "CORR" };

    public override IList<Range> GetFrequencyRange(Direction direction, uint channel, string name)
    {
        var results = new List<Range>();
        if (name == "RF")
        {
            if (_tunerType == RtlSdrTunerType.E4000) 
                results.Add(new Range(52000000, 2200000000));
            else if (_tunerType == RtlSdrTunerType.FC0012)
                results.Add(new Range(22000000, 1100000000));
            else if (_tunerType == RtlSdrTunerType.FC0013)
                results.Add(new Range(22000000, 948600000));
            else
                results.Add(new Range(24000000, 1764000000));
        }

        if (name == "CORR")
        {
            results.Add(new Range(-1000, 1000));
        }

        return results;
    }

    #endregion

    #region Sample Rate API

    public override void SetSampleRate(Direction direction, uint channel, double rate)
    {
        var ns = Time.TicksToTimeNs(_ticks, _sampleRate);
        _sampleRate = (uint) rate;
        Interlocked.Exchange(ref _resetBuffer, 1);
        Logger.LogF(LogLevel.Debug, "Setting sample rate: {0}", _sampleRate);
        var r = RtlApi.SetSampleRate(_dev, _sampleRate);
        if (r == -22)
            throw new ArgumentException("setSampleRate failed: RTL-SDR does not support this sample rate");
        if (r != 0)
            throw new Exception("setSampleRate failed");
        _sampleRate = RtlApi.GetSampleRate(_dev);
        Interlocked.Exchange(ref _ticks, Time.TimeNsToTicks(ns, _sampleRate));
    }

    public override double GetSampleRate(Direction direction, uint channel) => _sampleRate;

    public override IList<double> ListSampleRates(Direction direction, uint channel)=> new List<double>()
    {
        250000,
        1024000,
        1536000,
        1792000,
        1920000,
        2048000,
        2160000,
        2560000,
        2880000,
        3200000,
    };

    public override IList<Range> GetSampleRateRange(Direction direction, uint channel) => new List<Range>()
    {
        new Range(225001, 300000),
        new Range(900001, 3200000)
    };

    public override void SetBandwidth(Direction direction, uint channel, double bw)
    {
        var r = RtlApi.SetTunerBandwidth(_dev, (uint) bw);
        if (r != 0)
            throw new Exception("RTLSDR: Failed to set bandwidth");

        _bandwidth = (uint) bw;
    }

    public override double GetBandwidth(Direction direction, uint channel) => _bandwidth == 0 ? _sampleRate : _bandwidth; // auto / full bandwidth

    public override IList<Range> GetBandwidthRange(Direction direction, uint channel) => new List<Range>()
    {
        // stub, not sure what the sensible ranges for different tuners are.
        new Range(0, 8000000)
    };

    #endregion

    #region Time API

    public override IList<string> TimeSources => new List<string>() { "sw_ticks" };

    public override string TimeSource
    {
        get => "sw_ticks";
        set { }
    }

    public override bool HasHardwareTime(string what = "") => what == "" || what == "sw_ticks";

    public override long GetHardwareTime(string what = "")
    {
        return Time.TicksToTimeNs(_ticks, _sampleRate);
    }

    public override void SetHardwareTime(long timeNs, string what = "")
    {
        Interlocked.Exchange(ref _ticks, Time.TimeNsToTicks(timeNs, _sampleRate));
    }

    #endregion

    #region Settings API

    public override IList<ArgInfo> SettingInfo
    {
        get
        {
            var setArgs = new List<ArgInfo>();

            var directSampArg = new ArgInfo()
            {
                Key = "direct_samp",
                Value = "0",
                Name = "Direct Sampling",
                Description = "RTL-SDR Direct Sampling Mode",
                Type = ArgType.String,
                Options = new[] { "0", "1", "2" },
                OptionNames = new[] { "Off", "I-ADC", "Q-ADC" },
            };
            
            setArgs.Add(directSampArg);

            var offsetTuneArg = new ArgInfo()
            {
                Key = "offset_tune",
                Value = "false",
                Name = "Offset Tune",
                Description = "RTL-SDR Offset Tuning Mode",
                Type = ArgType.Bool
            };
            
            setArgs.Add(offsetTuneArg);

            var iqSwapArg = new ArgInfo()
            {
                Key = "iq_swap",
                Value = "false",
                Name = "I/Q Swap",
                Description = "RTL-SDR I/Q Swap Mode",
                Type = ArgType.Bool
            };
            
            setArgs.Add(iqSwapArg);

            var digitalAgcArg = new ArgInfo()
            {
                Key = "digital_agc",
                Value = "false",
                Name = "Digital AGC",
                Description = "RTL-SDR digital AGC mode",
                Type = ArgType.Bool
            };
            
            setArgs.Add(digitalAgcArg);

            var testModeArg = new ArgInfo()
            {
                Key = "testmode",
                Value = "false",
                Name = "Test Mode",
                Description = "RTL-SDR Test Mode",
                Type = ArgType.Bool
            };
            
            setArgs.Add(testModeArg);

            var biasTeeArg = new ArgInfo()
            {
                Key = "biastee",
                Value = "false",
                Name = "Bias Tee",
                Description = "RTL-SDR Blog V.3 Bias-Tee Mode",
                Type = ArgType.Bool
            };
            
            setArgs.Add(biasTeeArg);

            return setArgs;
        }
    }

    public override void WriteSettingString(string key, string value)
    {
        if (key == "direct_samp")
        {
            if (!int.TryParse(value, out _directSamplingMode) || _directSamplingMode < 0 || _directSamplingMode > 2)
            {
                Logger.LogF(LogLevel.Error, "RTL-SDR invalid direct sampline mode '{0}', [0:Off, 1:I-ADC, 2:Q-ADC]", value);
                _directSamplingMode = 0;
            }
            Logger.LogF(LogLevel.Debug, "RTL-SDR direct sampling mode: {0}", _directSamplingMode);
            RtlApi.SetDirectSampling(_dev, _directSamplingMode);
        }
        else if (key == "iq_swap")
        {
            _iqSwap = value == "true";
            Logger.LogF(LogLevel.Debug, "RTL-SDR I/Q Swap: {0}", _iqSwap);
        }
        else if (key == "offset_tune")
        {
            _offsetMode = value == "true";
            Logger.LogF(LogLevel.Debug, "RTL-SDR offset_tune mode: {0}", _offsetMode);
            RtlApi.SetOffsetTuning(_dev, _offsetMode ? 1 : 0);
        }
        else if (key == "digital_agc")
        {
            _digitalAgc = value == "true";
            Logger.LogF(LogLevel.Debug, "RTL-SDR digital AGC mode: {0}", _digitalAgc);
            RtlApi.SetAgcMode(_dev, _digitalAgc ? 1 : 0);
        }
        else if (key == "testmode")
        {
            _testMode = value == "true";
            Logger.LogF(LogLevel.Debug, "RTL-SDR test mode: {0}", _testMode);
            RtlApi.SetTestMode(_dev, _testMode ? 1 : 0);
        }
        else if (key == "biastee")
        {
            _biasTee = value == "true";
            Logger.LogF(LogLevel.Debug, "RTL-SDR bias tee mode: {0}", _biasTee);
            RtlApi.SetBiasTee(_dev, _biasTee ? 1 : 0);
        }
    }

    public override string ReadSettingString(string key)
    {
        if (key == "direct_samp") return _directSamplingMode.ToString();
        else if (key == "iq_swap") return _iqSwap.ToString();
        else if (key == "offset_tune") return _offsetMode.ToString();
        else if (key == "digital_agc") return _digitalAgc.ToString();
        else if (key == "testmode") return _testMode.ToString();
        else if (key == "biastee") return _biasTee.ToString();
        
        Logger.LogF(LogLevel.Warning, "Unknown setting '{0}'", key);
        return "";
    }

    #endregion
    
    
    private static string TunerToString(RtlSdrTunerType tunerType)
    {
        string deviceTuner;
        switch (tunerType)
        {
            case RtlSdrTunerType.Unknown:
                deviceTuner = "Unknown";
                break;
            case RtlSdrTunerType.E4000:
                deviceTuner = "Elonics E4000";
                break;
            case RtlSdrTunerType.FC0012:
                deviceTuner = "Fitipower FC0012";
                break;
            case RtlSdrTunerType.FC0013:
                deviceTuner = "Fitipower FC0013";
                break;
            case RtlSdrTunerType.FC2580:
                deviceTuner = "Fitipower FC2580";
                break;
            case RtlSdrTunerType.R820T:
                deviceTuner = "Rafael Micro R820T";
                break;
            case RtlSdrTunerType.R828D:
                deviceTuner = "Rafael Micro R828D";
                break;
            default:
                deviceTuner = "Unknown";
                break;
        }
        return deviceTuner;
    }
    
    private static readonly sbyte[] if_stage1_gain = { -3, 6 };
    private static readonly sbyte[] if_stage23_gain = { 0, 3, 6, 9 };
    private static readonly sbyte[] if_stage4_gain = { 0, 1, 2 };
    private static readonly sbyte[] if_stage56_gain = { 3, 6, 9, 12, 15 };
    private int GetE4000Gain(int stage, int gain)
    {
        var if_stage = new sbyte[0];

        if (stage == 1)
            if_stage = if_stage1_gain;
        else if (stage == 2 || stage == 3)
            if_stage = if_stage23_gain;
        else if (stage == 4)
            if_stage = if_stage4_gain;
        else if (stage == 5 || stage == 6)
            if_stage = if_stage56_gain;
        else
            return gain;

        var gainMin = if_stage[0];
        var gainMax = if_stage[^1];

        if (gain > gainMax) gain = gainMax;
        if (gain < gainMin) gain = gainMin;

        for (var i = 0; i < if_stage.Length - 1; i++)
        {
            if (gain >= if_stage[i] && gain <= if_stage[i + 1])
            {
                gain = ((gain - if_stage[i]) < (if_stage[i + 1] - gain)) ? if_stage[i] : if_stage[i + 1];
            }
        }

        return gain;
    }
    
    private static RtlSdrTunerType StringToTuner(string tunerType)
    {
        RtlSdrTunerType deviceTuner = RtlSdrTunerType.Unknown;

        if (tunerType == "Elonics E4000")
            deviceTuner = RtlSdrTunerType.E4000;
        if (tunerType == "Fitipower FC0012")
            deviceTuner = RtlSdrTunerType.FC0012;
        if (tunerType == "Fitipower FC0013")
            deviceTuner = RtlSdrTunerType.FC0013;
        if (tunerType == "Fitipower FC2580")
            deviceTuner = RtlSdrTunerType.FC2580;
        if (tunerType == "Rafael Micro R820T")
            deviceTuner = RtlSdrTunerType.R820T;
        if (tunerType == "Rafael Micro R828D")
            deviceTuner = RtlSdrTunerType.R828D;

        return deviceTuner;
    }
}