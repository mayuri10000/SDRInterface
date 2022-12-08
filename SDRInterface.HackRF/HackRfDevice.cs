using System.Runtime.InteropServices;

namespace SDRInterface.HackRF;

[Registry("hackrf")]
public unsafe partial class HackRfDevice : Device
{
    private const uint BufferLength = 262144;
    private const uint BufferNum = 15;
    private const uint BytesPerSample = 2;
    private const uint HackRfRxVgaMaxDb = 62;
    private const uint HackRfTxVgaMaxDb = 47;
    private const uint HackRfRxLnaMaxDb = 40;
    private const byte HackRfAmpMaxDb = 14;

    private const int TxStreamId = 1;
    private const int RxStreamId = 2;

    private enum HackRfFormat
    {
        Float32, Int16, Int8, Float64
    }

    private enum TransceiverMode
    {
        Off, Rx, Tx
    }
    
    private class Stream
    {
        public bool opened = false;
        public uint bufNum = BufferNum;
        public uint bufLen = BufferLength;
        public sbyte** buf = null;
        public uint bufHead = 0;
        public uint bufTail = 0;
        public uint bufCount = 0;

        public int reminderHandle = -1;
        public uint reminderSamps = 0;
        public uint reminderOffset = 0;
        public sbyte* reminderBuff = null;
        public HackRfFormat format = HackRfFormat.Int8;

        public void AllocateBuffers()
        {
            buf = (sbyte**)Marshal.AllocHGlobal((int) bufNum * sizeof(byte*));
            if (buf != null)
            {
                for (var i = 0; i < bufNum; i++)
                    buf[i] = (sbyte*)Marshal.AllocHGlobal((int)bufLen);
            }
        }

        public void ClearBuffers()
        {
            if (buf != null)
            {
                for (var i = 0; i < bufNum; i++)
                {
                    if (buf[i] != null) 
                        Marshal.FreeHGlobal((IntPtr) buf[i]);
                }
                Marshal.FreeHGlobal((IntPtr) buf);
                buf = null;
            }

            bufCount = 0;
            bufTail = 0;
            bufHead = 0;
            reminderSamps = 0;
            reminderOffset = 0;
            reminderBuff = null;
            reminderHandle = -1;
        }
    }

    private class RxStream : Stream
    {
        public uint vgaGain = 16;
        public uint lnaGain = 16;
        public byte ampGain = 0;
        public double samplerate = 0;
        public uint bandwidth = 0;
        public ulong frequency = 0;

        public bool overflow = false;
    }

    private class TxStream : Stream
    {
        public uint vgaGain = 0;
        public byte ampGain = 0;
        public double samplerate = 0;
        public uint bandwidth = 0;
        public ulong frequency = 0;
        public bool bias;

        public bool underflow = false;

        public bool burstEnd;
        public int burstSamps;
    }

    private RxStream _rxStream = new RxStream();
    private TxStream _txStream = new TxStream();

    private bool _autoBandwidth;

    private IntPtr _dev;
    private string _serial;

    private ulong _currentFrequency;

    private double _currentSamplerate;

    private uint _currentBandwidth;

    private byte _currentAmp;

    private object _deviceMutex = new object();
    private object _bufMutex = new object();
    private TransceiverMode _currentMode = TransceiverMode.Off;
    private HackRfSession _sess;

    private GCHandle _gcHandle;
    
    public HackRfDevice(IDictionary<string, string> args)
    {
        _sess = new HackRfSession();
        
        if (args.ContainsKey("label"))
            Logger.LogF(LogLevel.Info, "Opening {0}...", args["label"]);

        if (!args.ContainsKey("serial"))
            throw new Exception("no hackrf device matches");
        _serial = args["serial"];

        var ret = HackRfApi.OpenBySerial(_serial, out _dev);
        if (ret != HackRfError.Success)
        {
            Logger.Log(LogLevel.Error, "Cannot open HackRF device");
            throw new Exception("Cannot open HackRF device");
        }

        _claimedSerials.Add(_serial);
        
        _gcHandle = GCHandle.Alloc(this);
    }

    public override void Dispose()
    {
        _claimedSerials.Remove(_serial);
        if (_dev != IntPtr.Zero)
        {
            HackRfApi.Close(_dev);
        }
        _sess.Dispose();
        
        // free gc handle
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();
        GC.SuppressFinalize(this);
    }

    ~HackRfDevice()
    {
        Dispose();
    }

    public override string DriverKey => "HackRF";

    public override string HardwareKey
    {
        get
        {
            HackRfApi.ReadBoardId(_dev, out var value);
            return HackRfApi.BoardIdName(value);
        }
    }

    public override IDictionary<string, string> HardwareInfo
    {
        get
        {
            lock (_deviceMutex)
            {
                var info = new Dictionary<string, string>();
                var versionStr = stackalloc sbyte[100];
                
                HackRfApi.ReadVersionString(_dev, versionStr, 100);
                info["version"] = new string(versionStr);

                HackRfApi.ReadBoardPartIdAndSerialNo(_dev, out var readPartidSerialno);
                info["part_id"] = $"{readPartidSerialno.partId[0]:X8}{readPartidSerialno.partId[1]:X8}";

                var serial =
                    $"{readPartidSerialno.SerialNo[0]:x8}{readPartidSerialno.SerialNo[1]:x8}{readPartidSerialno.SerialNo[2]:x8}{readPartidSerialno.SerialNo[3]:x8}";
                info["serial"] = serial;

                HackRfApi.ReadSI5351C(_dev, 0, out var clock);
                info["clock source"] = clock == 0x51 ? "internal" : "external";

                return info;
            }
        }
    }

    #region Channels Api

    public override int GetNumChannels(Direction direction) => 1;

    public override bool GetFullDuplex(Direction direction, uint channel) => false;

    #endregion

    #region Settings Api

    public override IList<ArgInfo> SettingInfo => new List<ArgInfo>()
    {
        new ArgInfo()
        {
            Key = "bias_tx",
            Value = "false",
            Name = "Antenna Bias",
            Description = "Antenna port power control.",
            Type = ArgType.Bool
        }
    };

    public override void WriteSettingString(string key, string value)
    {
        if (key == "bias_tx")
        {
            lock (_deviceMutex)
            {
                _txStream.bias = value == "true";
                var ret = HackRfApi.SetAntennaEnable(_dev, _txStream.bias ? 1u : 0u);
                if (ret != HackRfError.Success)
                {
                    Logger.Log(LogLevel.Warning, "Failed to apply antenna bias voltage");
                }
            }
        }
    }

    public override string ReadSettingString(string key)
    {
        if (key == "bias_tx")
        {
            return _txStream.bias ? "true" : "false";
        }

        return "";
    }

    #endregion

    #region Antenna Api

    public override IList<string> ListAntennas(Direction direction, uint channel) => new List<string>()
    {
        "TX/RX"
    };

    public override string GetAntenna(Direction direction, uint channel) => "TX/RX";

    public override void SetAntenna(Direction direction, uint channel, string name)
    {
    }

    #endregion

    #region Frontend corrections API

    public override bool HasDCOffsetMode(Direction direction, uint channel) => false;

    #endregion

    #region Gain API

    public override IList<string> ListGains(Direction direction, uint channel) => 
        direction == Direction.Rx ? new List<string>() { "LNA", "AMP", "VGA" }
        : new List<string>() { "VGA", "AMP" };

    public override bool HasGainMode(Direction direction, uint channel) => false;

    public override void SetGain(Direction direction, uint channel, string name, double value)
    {
        lock (_deviceMutex)
        {
            Logger.LogF(LogLevel.Debug, "SetGain {0} {1}, channel {2}, gain {3}", name, direction, channel, value);
            if (name == "AMP")
            {
                _currentAmp = value > byte.MaxValue ? HackRfAmpMaxDb : (byte) value;

                if (direction == Direction.Rx)
                    _rxStream.ampGain = _currentAmp;
                else if (direction == Direction.Tx)
                    _txStream.ampGain = _currentAmp;

                if (_dev != IntPtr.Zero)
                {
                    var ret = HackRfApi.SetAmpEnable(_dev, (byte) (_currentAmp > 0 ? 1 : 0));
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_ser_amp_enable({0}) returned {1}", _currentAmp,
                            HackRfApi.ErrorName(ret));
                    }
                }
            }
            else if (direction == Direction.Rx && name == "LNA")
            {
                _rxStream.lnaGain = (uint)value;
                if (_dev != IntPtr.Zero)
                {
                    var ret = HackRfApi.SetLnaGain(_dev, _rxStream.lnaGain);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_set_lna_gain({0}) returned {1}", value,
                            HackRfApi.ErrorName(ret));
                    }
                }
            }
            else if (direction == Direction.Rx && name == "VGA")
            {
                _rxStream.vgaGain = (uint) value;
                if (_dev != IntPtr.Zero)
                {
                    var ret = HackRfApi.SetVgaGain(_dev, _rxStream.vgaGain);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_set_vga_gain({0}) returned {1}", value,
                            HackRfApi.ErrorName(ret));
                    }
                }
            }
            else if (direction == Direction.Tx && name == "VGA")
            {
                _txStream.vgaGain = (uint) value;
                if (_dev != IntPtr.Zero)
                {
                    var ret = HackRfApi.SetTxVgaGain(_dev, _txStream.vgaGain);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_set_txvga_gain({0}) returned {1}", value,
                            HackRfApi.ErrorName(ret));
                    }
                }
            }
        }
    }

    public override double GetGain(Direction direction, uint channel, string name)
    {
        lock (_deviceMutex)
        {
            var gain = 0.0;
            if (direction == Direction.Rx && name == "AMP")
                gain = _rxStream.ampGain;
            else if (direction == Direction.Tx && name == "AMP")
                gain = _txStream.ampGain;
            else if (direction == Direction.Rx && name == "LNA")
                gain = _rxStream.lnaGain;
            else if (direction == Direction.Rx && name == "VGA")
                gain = _rxStream.vgaGain;
            else if (direction == Direction.Tx && name == "VGA")
                gain = _txStream.vgaGain;

            return gain;
        }
    }

    public override Range GetGainRange(Direction direction, uint channel, string name)
    {
        if (name == "AMP")
            return new Range(0, HackRfAmpMaxDb, HackRfAmpMaxDb);
        if (direction == Direction.Rx && name == "LNA")
            return new Range(0, HackRfRxLnaMaxDb, 8.0);
        if (direction == Direction.Rx && name == "VGA")
            return new Range(0, HackRfTxVgaMaxDb, 2.0);
        if (direction == Direction.Tx && name == "VGA")
            return new Range(0, HackRfTxVgaMaxDb, 1.0);
        return new Range(0, 0);
    }

    #endregion

    #region Frequency API

    public override void SetFrequency(Direction direction, uint channel, string name, double frequency, IDictionary<string, string> kwargs)
    {
        if (name == "BB") return;
        if (name != "RF") throw new KeyNotFoundException("SetFrequency(" + name + "): unknown name");

        lock (_deviceMutex)
        {
            _currentFrequency = (ulong) frequency;

            if (direction == Direction.Rx)
            {
                _rxStream.frequency = _currentFrequency;
            }

            if (direction == Direction.Tx)
            {
                _txStream.frequency = _currentFrequency;
            }

            if (_dev != IntPtr.Zero)
            {
                var ret = HackRfApi.SetFreq(_dev, _currentFrequency);
                if (ret != HackRfError.Success)
                {
                    Logger.LogF(LogLevel.Error, "hackrf_set_freq({0}) returned {1}", _currentFrequency,
                        HackRfApi.ErrorName(ret));
                }
            }
        }
    }

    public override double GetFrequency(Direction direction, uint channel, string name)
    {
        if (name == "BB") return 0;
        if (name != "RF") throw new KeyNotFoundException("GetFrequency(" + name + "): unknown name");

        lock (_deviceMutex)
        {
            var freq = 0.0;

            if (direction == Direction.Rx)
                freq = _rxStream.frequency;
            else if (direction == Direction.Tx)
                freq = _txStream.frequency;

            return freq;
        }
    }

    public override IList<ArgInfo> GetFrequencyArgsInfo(Direction direction, uint channel) => new List<ArgInfo>();

    public override IList<string> ListFrequencies(Direction direction, uint channel) => new List<string>() { "RF" };

    public override IList<Range> GetFrequencyRange(Direction direction, uint channel, string name)
    {
        if (name == "BB") return new List<Range>() { new Range(0, 0) };
        if (name != "RF") throw new KeyNotFoundException("GetFrequency(" + name + "): unknown name");

        return new List<Range>() { new Range(0, 7250000000) };
    }

    #endregion

    #region Sample Rate API

    public override void SetSampleRate(Direction direction, uint channel, double rate)
    {
        lock (_deviceMutex)
        {
            _currentSamplerate = rate;

            if (direction == Direction.Rx)
                _rxStream.samplerate = _currentSamplerate;
            if (direction == Direction.Tx)
                _txStream.samplerate = _currentSamplerate;

            if (_dev != IntPtr.Zero)
            {
                var ret = HackRfApi.SetSampleRate(_dev, _currentSamplerate);
                if (ret != HackRfError.Success)
                {
                    Logger.LogF(LogLevel.Error, "hackrf_set_sample_rate({0}) returned {1}", _currentSamplerate,
                        HackRfApi.ErrorName(ret));
                    throw new Exception("Cannot set sample rate: " + HackRfApi.ErrorName(ret));
                }
            }
        }
    }

    public override double GetSampleRate(Direction direction, uint channel)
    {
        lock (_deviceMutex)
        {
            var samp = 0.0;
            if (direction == Direction.Tx)
                samp = _txStream.samplerate;
            if (direction == Direction.Rx)
                samp = _rxStream.samplerate;

            return samp;
        }
    }

    public override IList<double> ListSampleRates(Direction direction, uint channel)
    {
        var ret = new List<double>();
        for (var r = 1e6; r <= 20e6; r += 1e6)
        {
            ret.Add(r);
        }

        return ret;
    }

    public override void SetBandwidth(Direction direction, uint channel, double bw)
    {
        lock (_deviceMutex)
        {
            _currentBandwidth = (uint) bw;

            if (direction == Direction.Rx)
                _rxStream.bandwidth = _currentBandwidth;
            if (direction == Direction.Tx)
                _txStream.bandwidth = _currentBandwidth;

            if (_currentBandwidth > 0)
            {
                _autoBandwidth = false;

                if (_dev != IntPtr.Zero)
                {
                    var ret = HackRfApi.SetBasebandFilterBandwidth(_dev, _currentBandwidth);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_set_baseband_filter_bandwidth({0}) returned {1}", _currentSamplerate,
                            HackRfApi.ErrorName(ret));
                        throw new Exception("Cannot set bandwidth: " + HackRfApi.ErrorName(ret));
                    }
                }
            }
            else
            {
                _autoBandwidth = true;
            }
        }
    }

    public override double GetBandwidth(Direction direction, uint channel)
    {
        lock (_deviceMutex)
        {
            var bw = 0.0;
            if (direction == Direction.Tx)
                bw = _txStream.bandwidth;
            if (direction == Direction.Rx)
                bw = _rxStream.bandwidth;

            return bw;
        }
    }

    public override IList<double> ListBandwidths(Direction direction, uint channel) => new List<double>()
    {
        1750000,
        2500000,
        3500000,
        5000000,
        5500000,
        6000000,
        7000000,
        8000000,
        9000000,
        10000000,
        12000000,
        14000000,
        15000000,
        20000000,
        24000000,
        28000000,
    };

    #endregion
}