using System.Numerics;

namespace SDRInterface;


/// <summary>
/// Abstraction for an SDR transceiver device - configuration and streaming.
/// </summary>
public abstract partial class Device : IDisposable
{
    /// <summary>
    /// 
    /// </summary>
    public virtual void Dispose()
    {
    }
    
    #region Identification API
    
    /// <summary>
    /// A key that uniquely identifies the device driver.
    /// This key identifies the underlying implementation.
    /// Several variants of a product may share a driver.
    /// </summary>
    public virtual string DriverKey => "";

    /// <summary>
    /// A key that uniquely identifies the hardware.
    /// This key should be meaningful to the user
    /// to optimize for the underlying hardware.
    /// </summary>
    public virtual string HardwareKey => "";

    /// <summary>
    /// Query a dictionary of available device information.
    /// This dictionary can any number of values like
    /// vendor name, product name, revisions, serials...
    /// This information can be displayed to the user
    /// to help identify the instantiated device.
    /// </summary>
    public virtual IDictionary<string, string> HardwareInfo => new Dictionary<string, string>();
    
    #endregion
    
    #region Channels API

    /// <summary>
    /// Set the frontend mapping of available DSP units to RF frontends.
    /// This mapping controls channel mapping and channel availability.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="mapping">a vendor-specific mapping string</param>
    public virtual void SetFrontendMapping(Direction direction, string mapping) { }

    /// <summary>
    /// Get the mapping configuration string.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <returns>the vendor-specific mapping string</returns>
    public virtual string GetFrontendMapping(Direction direction) => "";

    /// <summary>
    /// Get a number of channels given the streaming direction
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public virtual int GetNumChannels(Direction direction) => 0;

    /// <summary>
    /// Query a dictionary of available channel information.
    /// This dictionary can any number of values like
    /// decoder type, version, available functions...
    /// This information can be displayed to the user
    /// to help identify the instantiated channel.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="channel">an available channel on the device</param>
    /// <returns>channel information</returns>
    public virtual IDictionary<string, string> GetChannelInfo(Direction direction, uint channel) =>
        new Dictionary<string, string>();
    
    /// <summary>
    /// Find out if the specified channel is full or half duplex.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="channel">an available channel on the device</param>
    /// <returns>true for full duplex, false for half duplex</returns>
    public virtual bool GetFullDuplex(Direction direction, uint channel)
    {
        var numRxChs = GetNumChannels(Direction.Rx);
        var numTxChs = GetNumChannels(Direction.Tx);
        if (numRxChs > 0 && numTxChs > 0) return true;
        return false;
    }
    
    #endregion
    
    #region Stream API

    /// <summary>
    /// Query a list of the available stream formats.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="channel">an available channel on the device</param>
    /// <returns>a list of allowed format strings (see StreamFormat)</returns>
    public virtual IList<string> GetStreamFormats(Direction direction, uint channel) => new List<string>();
    
    /// <summary>
    /// Get the hardware's native stream format for this channel.
    /// This is the format used by the underlying transport layer,
    /// and the direct buffer access API calls (when available).
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="channel">an available channel on the device</param>
    /// <param name="fullScale">the maximum possible value</param>
    /// <returns>the native stream buffer format string</returns>
    public virtual string GetNativeStreamFormat(Direction direction, uint channel, ref double fullScale)
    {
        fullScale = (double)(1 << 15);
        return StreamFormat.ComplexInt16;
    }

    /// <summary>
    /// Query the argument info description for stream args.
    /// </summary>
    /// <param name="direction">the channel direction RX or TX</param>
    /// <param name="channel">an available channel on the device</param>
    /// <returns>a list of argument info structures</returns>
    public virtual IList<ArgInfo> GetStreamArgsInfo(Direction direction, uint channel) => new List<ArgInfo>();
    
    /// <summary>
    /// Initialize a transmit stream given a list of channels and stream arguments.
    ///
    /// The implementation may change switches or power-up components.
    /// All stream API calls should be usable with the new stream object
    /// after setupStream() is complete, regardless of the activity state.
    ///
    /// The API allows any number of simultaneous TX and RX streams, but many dual-channel
    /// devices are limited to one stream in each direction, using either one or both channels.
    /// This call will throw an exception if an unsupported combination is requested,
    /// or if a requested channel in this direction is already in use by another stream.
    ///
    /// When multiple channels are added to a stream, they are typically expected to have
    /// the same sample rate. See SetSampleRate().
    /// </summary>
    ///
    /// <param name="format">The stream's sample type (see StreamFormat).</param>
    /// <param name="channels">A list of channels to use for the stream.</param>
    /// <param name="args">Stream arguments or empty for defaults.</param>
    /// <returns>
    /// A transmit stream created with the given settings.
    ///
    /// The returned stream is not required to have internal locking, and may not be used
    /// concurrently from multiple threads.
    /// </returns>
    public virtual TxStream SetupTxStream(
        string format,
        uint[] channels,
        IDictionary<string, string> kwargs)
    {
        return new TxStream(this, format, channels, kwargs);
    }

    /// <summary>
    /// Initialize a transmit stream given a list of channels and stream arguments.
    ///
    /// The implementation may change switches or power-up components.
    /// All stream API calls should be usable with the new stream object
    /// after setupStream() is complete, regardless of the activity state.
    ///
    /// The API allows any number of simultaneous TX and RX streams, but many dual-channel
    /// devices are limited to one stream in each direction, using either one or both channels.
    /// This call will throw an exception if an unsupported combination is requested,
    /// or if a requested channel in this direction is already in use by another stream.
    ///
    /// When multiple channels are added to a stream, they are typically expected to have
    /// the same sample rate. See SetSampleRate().
    /// </summary>
    ///
    /// <param name="format">The stream's sample type (see StreamFormat).</param>
    /// <param name="channels">A list of channels to use for the stream.</param>
    /// <param name="args">Stream arguments or empty for defaults (markup format: "keyA=valA, keyB=valB").</param>
    /// <returns>
    /// A transmit stream created with the given settings.
    ///
    /// The returned stream is not required to have internal locking, and may not be used
    /// concurrently from multiple threads.
    /// </returns>
    public TxStream SetupTxStream(
        string format,
        uint[] channels,
        string args) => SetupTxStream(format, channels, Utility.StringToKwargs(args));

    /// <summary>
    /// Initialize a receive stream given a list of channels and stream arguments.
    ///
    /// The implementation may change switches or power-up components.
    /// All stream API calls should be usable with the new stream object
    /// after setupStream() is complete, regardless of the activity state.
    ///
    /// The API allows any number of simultaneous TX and RX streams, but many dual-channel
    /// devices are limited to one stream in each direction, using either one or both channels.
    /// This call will throw an exception if an unsupported combination is requested,
    /// or if a requested channel in this direction is already in use by another stream.
    ///
    /// When multiple channels are added to a stream, they are typically expected to have
    /// the same sample rate. See SetSampleRate().
    /// </summary>
    ///
    /// <param name="format">The stream's sample type (see StreamFormat).</param>
    /// <param name="channels">A list of channels to use for the stream.</param>
    /// <param name="args">Stream arguments or empty for defaults.</param>
    /// <returns>
    /// A receive stream created with the given settings.
    ///
    /// The returned stream is not required to have internal locking, and may not be used
    /// concurrently from multiple threads.
    /// </returns>
    public virtual RxStream SetupRxStream(
        string format,
        uint[] channels,
        IDictionary<string, string> kwargs)
    {
        return new RxStream(this, format, channels, kwargs);
    }

    /// <summary>
    /// Initialize a receive stream given a list of channels and stream arguments.
    ///
    /// The implementation may change switches or power-up components.
    /// All stream API calls should be usable with the new stream object
    /// after setupStream() is complete, regardless of the activity state.
    ///
    /// The API allows any number of simultaneous TX and RX streams, but many dual-channel
    /// devices are limited to one stream in each direction, using either one or both channels.
    /// This call will throw an exception if an unsupported combination is requested,
    /// or if a requested channel in this direction is already in use by another stream.
    ///
    /// When multiple channels are added to a stream, they are typically expected to have
    /// the same sample rate. See SetSampleRate().
    /// </summary>
    ///
    /// <param name="format">The stream's sample type (see StreamFormat).</param>
    /// <param name="channels">A list of channels to use for the stream.</param>
    /// <param name="args">Stream arguments or empty for defaults (markup format: "keyA=valA, keyB=valB").</param>
    /// <returns>
    /// A receive stream created with the given settings.
    ///
    /// The returned stream is not required to have internal locking, and may not be used
    /// concurrently from multiple threads.
    /// </returns>
    public RxStream SetupRxStream(
        string format,
        uint[] channels,
        string args = "") => SetupRxStream(format, channels, Utility.StringToKwargs(args));

    /// <summary>
    /// Initialize a stream given a list of channels and stream arguments.
    /// The implementation may change switches or power-up components.
    /// All stream API calls should be usable with the new stream handle
    /// after setupStream() is complete, regardless of the activity state.
    ///
    /// The API allows any number of simultaneous TX and RX streams, but many dual-channel
    /// devices are limited to one stream in each direction, using either one or both channels.
    /// This call will throw an exception if an unsupported combination is requested,
    /// or if a requested channel in this direction is already in use by another stream.
    ///
    /// When multiple channels are added to a stream, they are typically expected to have
    /// the same sample rate. See setSampleRate().
    /// </summary>
    /// <param name="direction">the channel direction</param>
    /// <param name="format">The stream's sample type (see StreamFormat).</param>
    /// <param name="channels">A list of channels to use for the stream</param>
    /// <param name="args">Stream arguments</param>
    /// <returns>A stream handle</returns>
    protected internal virtual StreamHandle SetupStream(Direction direction, string format, IList<uint> channels = null,
        IDictionary<string, string> args = null) => default;

    /// <summary>
    /// Close an open stream created by setupStream
    /// The implementation may change switches or power-down components.
    /// </summary>
    /// <param name="stream">The stream handle</param>
    protected internal virtual void CloseStream(StreamHandle stream) { }

    /// <summary>
    /// Get the stream's maximum transmission unit (MTU) in number of elements.
    /// The MTU specifies the maximum payload transfer in a stream operation.
    /// This value can be used as a stream buffer allocation size that can
    /// best optimize throughput given the underlying stream implementation.
    /// </summary>
    /// <param name="stream">The stream handle</param>
    /// <returns>the MTU in number of stream elements (never zero)</returns>
    protected internal virtual ulong GetStreamMTU(StreamHandle stream) => 1024;

    /// <summary>
    /// Activate a stream.
    /// Call activate to prepare a stream before using read/write().
    /// The implementation control switches or stimulate data flow.
    ///
    /// The timeNs is only valid when the flags have <see cref="StreamFlags.HasTime"/>.
    /// The numElems count can be used to request a finite burst size.
    /// The <see cref="StreamFlags.EndBurst"/> flag can signal end on the finite burst.
    /// Not all implementations will support the full range of options.
    /// In this case, the implementation returns <see cref="ErrorCode.NotSupported"/>.
    /// </summary>
    /// <param name="stream">The stream handle</param>
    /// <param name="flags">optional flag indicators about the stream</param>
    /// <param name="timeNs">optional activation time in nanoseconds</param>
    /// <param name="numElems">optional element count for burst control</param>
    /// <returns>None for success or error code on failure</returns>
    protected internal virtual ErrorCode ActivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None,
        long timeNs = 0,
        uint numElems = 0) => flags == StreamFlags.None ? ErrorCode.None : ErrorCode.NotSupported;

    protected internal virtual ErrorCode DeactivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0)
        => flags == StreamFlags.None ? ErrorCode.None : ErrorCode.NotSupported;

    protected internal virtual StreamResultPairInternal ReadStream(StreamHandle stream, IList<UIntPtr> buffs,
        uint numElems,
        long timeoutUs = 100000) => new StreamResultPairInternal() { Code = ErrorCode.NotSupported, Result = default };

    protected internal virtual StreamResultPairInternal WriteStream(StreamHandle stream, IList<UIntPtr> buffs, uint numElems,
        StreamFlags flags, long timeNs, long timeoutUs = 100000) => new StreamResultPairInternal() { Code = ErrorCode.NotSupported, Result = default };

    protected internal virtual StreamResultPairInternal ReadStreamStatus(StreamHandle stream, long timeoutUs = 100000)
        => new StreamResultPairInternal() { Code = ErrorCode.NotSupported, Result = default };
    
    #endregion
    
    #region Direct buffer access API

    public virtual uint GetNumDirectAccessBuffers(StreamHandle stream) => 0;

    public virtual ErrorCode GetDirectAccessBufferAddrs(StreamHandle stream, int index, IList<UIntPtr> buffs) =>
        ErrorCode.NotSupported;

    public virtual StreamResultPairInternal AcquireReadBuffer(StreamHandle stream, ref int index, IList<UIntPtr> buffs,
        long timeoutUs = 100000) => new StreamResultPairInternal() { Code = ErrorCode.NotSupported, Result = default };

    public virtual void ReleaseReadBuffer(StreamHandle stream, int index) { }

    public virtual ErrorCode AcquireWriteBuffer(StreamHandle stream, ref int index, IList<UIntPtr> buffs,
        long timeoutUs = 100000) => ErrorCode.NotSupported;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="index"></param>
    /// <param name="numElems"></param>
    /// <param name="flags"></param>
    /// <param name="timeNs"></param>
    public virtual void ReleaseWriteBuffer(StreamHandle stream, int index, uint numElems, ref StreamFlags flags,
        long timeNs = 0) { }

    #endregion
    
    #region Antenna API

    public virtual IList<string> ListAntennas(Direction direction, uint channel) => new List<string>();

    public virtual void SetAntenna(Direction direction, uint channel, string name) { }

    public virtual string GetAntenna(Direction direction, uint channel) => "";
    
    #endregion

    #region Frontend corrections API

    public virtual bool HasDCOffsetMode(Direction direction, uint channel) => false;

    public virtual void SetDCOffsetMode(Direction direction, uint channel, bool automatic) { }

    public virtual bool GetDCOffsetMode(Direction direction, uint channel) => false;

    public virtual bool HasDCOffset(Direction direction, uint channel) => false;

    public virtual void SetDCOffset(Direction direction, uint channel, Complex offset) { }

    public virtual Complex GetDCOffset(Direction direction, uint channel) => Complex.Zero;

    public virtual bool HasIQBalance(Direction direction, uint channel) => false;

    public virtual void SetIQBalance(Direction direction, uint channel, Complex balance) { }

    public virtual bool HasIQBalanceMode(Direction direction, uint channel) => false;

    public virtual void SetIQBalanceMode(Direction direction, uint channel, bool automatic) { }

    public virtual bool GetIQBalanceMode(Direction direction, uint channel) => false;

    public virtual bool HasFrequencyCorrection(Direction direction, uint channel) =>
        ListFrequencies(direction, channel).Contains("CORR");

    public virtual void SetFrequencyCorrection(Direction direction, uint channel, double value)
    {
        var componenets = ListFrequencies(direction, channel);
        if (componenets.Contains("CORR"))
        {
            SetFrequency(direction, channel, "CORR", value);
        }
    }

    public virtual double GetFrequencyCorrection(Direction direction, uint channel)
    {
        var components = ListFrequencies(direction, channel);
        if (components.Contains("CORR"))
            return GetFrequency(direction, channel, "CORR");
        return 0.0;
    }
    
    #endregion
    
    #region Gain API

    public virtual IList<string> ListGains(Direction direction, uint channel) => new List<string>();

    public virtual bool HasGainMode(Direction direction, uint channel) => false;

    public virtual void SetGainMode(Direction direction, uint channel, bool automatic) { }

    public virtual bool GetGainMode(Direction direction, uint channel) => false;

    public virtual void SetGain(Direction direction, uint channel, double value)
    {
        var names = ListGains(direction, channel);
        if (direction == Direction.Tx)
        {
            for (var i = names.Count - 1; i >= 0; i--)
            {
                var r = GetGainRange(direction, channel, names[i]);
                var g = Math.Min(value, r.Maximum - r.Minimum);
                SetGain(direction, channel, names[i], g + r.Minimum);
                value -= GetGain(direction, channel, names[i]) - r.Minimum;
            }
        }

        if (direction == Direction.Rx)
        {
            for (var i = 0; i < names.Count; i++)
            {
                var r = GetGainRange(direction, channel, names[i]);
                var g = Math.Min(value, r.Maximum - r.Minimum);
                SetGain(direction, channel, names[i], g + r.Minimum);
                value -= GetGain(direction, channel, names[i]) - r.Minimum;
            }
        }
    }

    public virtual void SetGain(Direction direction, uint channel, string name, double value) { }

    public virtual double GetGain(Direction direction, uint channel)
    {
        var gain = 0.0;
        foreach (var name in ListGains(direction, channel))
        {
            var r = GetGainRange(direction, channel, name);
            gain += GetGain(direction, channel, name) - r.Minimum;
        }

        return gain;
    }

    public virtual double GetGain(Direction direction, uint channel, string name) => 0.0;

    public virtual Range GetGainRange(Direction direction, uint channel)
    {
        var gain = 0.0;
        foreach (var name in ListGains(direction, channel))
        {
            var r = GetGainRange(direction, channel, name);
            gain += r.Maximum - r.Minimum;
        }

        return new Range(0.0, gain);
    }

    public virtual Range GetGainRange(Direction direction, uint channel, string name) => new Range(0.0, 0.0);
    
    #endregion

    #region Frequency API
    
    /// <summary>
    /// Set the center frequency of the chain.
    ///  - For RX, this specifies the down-conversion frequency.
    ///  - For TX, this specifies the up-conversion frequency.
    ///
    /// When no args are provided, setFrequency() will tune the "RF"
    /// component as close as possible to the requested center frequency.
    /// Tuning inaccuracies will be compensated for with the "BB" component.
    ///
    /// The args can be used to augment the tuning algorithm.
    ///  - Use "OFFSET" to specify an "RF" tuning offset,
    ///    usually with the intention of moving the LO out of the passband.
    ///    The offset will be compensated for using the "BB" component.
    ///  - Use the name of a component for the key and a frequency in Hz
    ///    as the value (any format) to enforce a specific frequency.
    ///    The other components will be tuned with compensation
    ///    to achieve the specified overall frequency.
    ///  - Use the name of a component for the key and the value "IGNORE"
    ///    so that the tuning algorithm will avoid altering the component.
    ///  - Vendor specific implementations can also use the same args to augment
    ///    tuning in other ways such as specifying fractional vs integer N tuning.
    /// </summary>
    /// <param name="direction">The channel direction (RX or TX)</param>
    /// <param name="channel">An available channel on the device</param>
    /// <param name="frequency">The center frequency in Hz</param>
    /// <param name="args">Optional tuner arguments</param>
    public void SetFrequency(Direction direction, uint channel, double frequency, string args = "") =>
        SetFrequency(direction, channel, frequency, Utility.StringToKwargs(args));

    /// <summary>
    /// Tune the center frequency of the specified element.
    ///  - For RX, this specifies the down-conversion frequency.
    ///  - For TX, this specifies the up-conversion frequency.
    ///
    /// When no args are provided, setFrequency() will tune the "RF"
    /// component as close as possible to the requested center frequency.
    /// Tuning inaccuracies will be compensated for with the "BB" component.
    ///
    /// The args can be used to augment the tuning algorithm.
    ///  - Use "OFFSET" to specify an "RF" tuning offset,
    ///    usually with the intention of moving the LO out of the passband.
    ///    The offset will be compensated for using the "BB" component.
    ///  - Use the name of a component for the key and a frequency in Hz
    ///    as the value (any format) to enforce a specific frequency.
    ///    The other components will be tuned with compensation
    ///    to achieve the specified overall frequency.
    ///  - Use the name of a component for the key and the value "IGNORE"
    ///    so that the tuning algorithm will avoid altering the component.
    ///  - Vendor specific implementations can also use the same args to augment
    ///    tuning in other ways such as specifying fractional vs integer N tuning.
    /// </summary>
    /// <param name="direction">The channel direction (RX or TX)</param>
    /// <param name="channel">An available channel on the device</param>
    /// <param name="name">The name of a tunable element</param>
    /// <param name="frequency">The center frequency in Hz</param>
    /// <param name="args">Optional tuner arguments</param>
    public void SetFrequency(Direction direction, uint channel, string name, double frequency, string args = "") =>
        SetFrequency(direction, channel, name, frequency, Utility.StringToKwargs(args));

    public virtual void SetFrequency(Direction direction, uint channel, double frequency,
        IDictionary<string, string> kwargs)
    {
        var comps = ListFrequencies(direction, channel);
        if (comps.Count == 0) return;

        var offset = kwargs.ContainsKey("OFFSET") ? double.Parse(kwargs["OFFSET"]) : 0.0;

        for (var comp_i = 0; comp_i < comps.Count; comp_i++)
        {
            var name = comps[comp_i];

            if (comp_i == 0) frequency += offset;

            if (kwargs.ContainsKey(name) && kwargs[name] == "IGNORE")
            {
                // do nothing, dont change component
            }
            else if (kwargs.ContainsKey(name) && kwargs[name] != "DEFAULT")
            {
                var f = double.Parse(kwargs[name]);
                SetFrequency(direction, channel, name, f, kwargs);
            }
            else
            {
                SetFrequency(direction, channel, name, frequency, kwargs);
            }

            frequency -= GetFrequency(direction, channel, name);

            if (comp_i == 0) frequency -= offset;
        }
    }
    
    public virtual void SetFrequency(Direction direction, uint channel, string name, double frequency,
        IDictionary<string, string> kwargs) { }

    public virtual double GetFrequency(Direction direction, uint channel)
    {
        var freq = 0.0;

        foreach (var comp in ListFrequencies(direction, channel))
        {
            freq += GetFrequency(direction, channel, comp);
        }

        return freq;
    }

    public virtual double GetFrequency(Direction direction, uint channel, string name) => 0.0;

    public virtual IList<string> ListFrequencies(Direction direction, uint channel) => new List<string>();

    public virtual IList<Range> GetFrequencyRange(Direction direction, uint channel)
    {
        var comps = ListFrequencies(direction, channel);
        if (comps.Count == 0) return new List<Range>();

        var ranges = GetFrequencyRange(direction, channel, comps.Last());

        var bw = GetBandwidth(direction, channel);

        for (var comp_i = 0; comp_i < comps.Count; comp_i++)
        {
            var subRange = GetFrequencyRange(direction, channel, comps[comp_i]);
            if (subRange.Count == 0) continue;

            var subRangeLow = subRange.Last().Minimum;
            if (bw > 0.0) subRangeLow = Math.Max(-bw / 2, subRangeLow);

            var subRangeHigh = subRange.First().Maximum;
            if (bw > 0.0) subRangeHigh = Math.Min(bw / 2, subRangeHigh);

            for (var range_i = 0; range_i < ranges.Count; range_i++)
            {
                ranges[range_i] = new Range(
                    ranges[range_i].Minimum + subRangeLow,
                    ranges[range_i].Maximum + subRangeHigh);
            }
        }

        return ranges;
    }

    public virtual IList<Range> GetFrequencyRange(Direction direction, uint channel, string name) => new List<Range>();

    public virtual IList<ArgInfo> GetFrequencyArgsInfo(Direction direction, uint channel)
    {
        var args = new List<ArgInfo>();

        var comps = ListFrequencies(direction, channel);

        if (comps.Count < 2) return args;

        var info = new ArgInfo()
        {
            Key = "OFFSET",
            Name = "LO Offset",
            Value = "0.0",
            Units = "Hz",
            Type = ArgType.Float,
            Description = "Tune the LO with an offset and compensate with the baseband CORDIC.",
            Range = GetFrequencyRange(direction, channel, comps[0]).FirstOrDefault(),
        };
        args.Add(info);

        for (var comp_i = 0; comp_i < comps.Count; comp_i++)
        {
            var info2 = new ArgInfo()
            {
                Key = comps[comp_i],
                Value = "DEFAULT",
                Units = "Hz",
                Type = ArgType.Float,
                Description = "Specify a specific value for this component or IGNORE to skip tuning it.",
                Options = new List<string>() { "DEFALUT", "IGNORE" },
                OptionNames = new List<string>() { "Default", "Ignore" },
                Range = GetFrequencyRange(direction, channel, comps[comp_i]).FirstOrDefault(),
            };
            args.Add(info2);
        } 

        return args;
    }
    
    #endregion

    #region Sample Rate API
    
    public virtual void SetSampleRate(Direction direction, uint channel, double rate) { }

    public virtual double GetSampleRate(Direction direction, uint channel) => 0.0;

    public virtual IList<double> ListSampleRates(Direction direction, uint channel) => new List<double>();

    public virtual IList<Range> GetSampleRateRange(Direction direction, uint channel)
    {
        var ranges = new List<Range>();

        foreach (var bw in ListSampleRates(direction, channel))
        {
            ranges.Add(new Range(bw, bw));
        }

        return ranges;
    }
    
    #endregion
    
    #region Bandwidth API

    public virtual void SetBandwidth(Direction direction, uint channel, double bw) { }

    public virtual double GetBandwidth(Direction direction, uint channel) => 0.0;

    public virtual IList<double> ListBandwidths(Direction direction, uint channel) => new List<double>();

    public virtual IList<Range> GetBandwidthRange(Direction direction, uint channel)
    {
        var ranges = new List<Range>();

        foreach (var bw in ListBandwidths(direction, channel))
        {
            ranges.Add(new Range(bw, bw));
        }

        return ranges;
    }
    
    #endregion
    
    #region Clocking API

    public virtual double MasterClockRate { get => 0.0; set { } }

    public virtual IList<Range> MasterClockRates => new List<Range>();

    public virtual double ReferenceClockRate { get => 0.0; set { } }

    public virtual IList<Range> ReferenceClockRates => new List<Range>();

    public virtual string ClockSource { get => ""; set { } }

    public virtual IList<string> ClockSources => new List<string>();
    
    #endregion
    
    #region Time API
    
    public virtual string TimeSource { get => ""; set { } }

    public virtual IList<string> TimeSources => new List<string>();

    public virtual bool HasHardwareTime(string what = "") => false;

    public virtual long GetHardwareTime(string what = "") => 0;

    public virtual void SetHardwareTime(long timeNs, string what = "")
    {
        if (what == "CMD") SetCommandTime(timeNs, what);
    }

    public long HardwareTime
    {
        get => GetHardwareTime();
        set => SetHardwareTime(value);
    }

    public virtual void SetCommandTime(long timeNs, string what = "") { }
    
    #endregion
    
    #region Sensor API

    public virtual IList<string> Sensors => new List<string>();

    public virtual ArgInfo GetSensorInfo(string key) => new ArgInfo();

    public virtual string ReadSensorAsString(string key) => "";

    public virtual ArgInfo GetSensorInfo(Direction direction, uint channel, string key) => new ArgInfo();

    public virtual string ReadSensorAsString(Direction direction, uint channel, string key) => "";
    
    /// <summary>
    /// Readback a global sensor given the name, typecasted to the given type.
    /// </summary>
    /// <typeparam name="T">The type to cast the sensor value</typeparam>
    /// <param name="key">The ID name of an available sensor</param>
    /// <returns>The current value of the sensor</returns>
    public T ReadSensor<T>(string key)
    {
        return (T)(new Convertible(ReadSensorAsString(key)).ToType(typeof(T), null));
    }

    /// <summary>
    /// Readback a channel sensor given the name, typecasted to the given type.
    /// </summary>
    /// <typeparam name="T">The type to cast the sensor value</typeparam>
    /// <param name="direction">The channel direction (RX or TX)</param>
    /// <param name="channel">An available channel on the device</param>
    /// <param name="key">The ID name of an available sensor</param>
    /// <returns>The current value of the sensor</returns>
    public T ReadSensor<T>(Direction direction, uint channel, string key)
    {
        return (T)(new Convertible(ReadSensorAsString(direction, channel, key)).ToType(typeof(T), null));
    }
    
    #endregion

    #region Register API

    public virtual IList<string> RegisterInterfaces => new List<string>();

    public virtual void WriteRegister(string name, uint addr, uint value) => WriteRegister(addr, value);

    public virtual uint ReadRegister(string name, uint addr) => ReadRegister(addr);

    public virtual void WriteRegister(uint addr, uint value) { }

    public virtual uint ReadRegister(uint addr) => 0;

    public virtual void WriteRegisters(string name, uint addr, IList<uint> value) { }

    public virtual IList<uint> ReadRegisters(string name, uint addr, uint length) => new uint[length];
    
    #endregion
    
    #region Settings API

    public virtual IList<ArgInfo> SettingInfo => new List<ArgInfo>();

    public virtual ArgInfo GetSettingInfo(string key)
    {
        var allArgInfos = SettingInfo;
        var res = from argInfo in allArgInfos where argInfo.Key == key select argInfo;

        return res.FirstOrDefault();
    }

    public virtual void WriteSettingString(string key, string value) { }

    public virtual string ReadSettingString(string key) => "";

    public virtual void WriteSettingString(Direction direction, uint channel, string key, string value) { }

    public virtual string ReadSettingString(Direction direction, uint channel, string key) => "";
    
    /// <summary>
    /// Read an arbitrary setting on the device, typecasted to the given type.
    ///
    /// This function will throw if T is not a string, bool, or numeric primitive.
    /// </summary>
    /// <typeparam name="T">The type to cast the setting</typeparam>
    /// <param name="key">The setting identifier</param>
    /// <returns>The setting value</returns>
    public T ReadSetting<T>(string key)
    {
        return (T)(new Convertible(ReadSettingString(key)).ToType(typeof(T), null));
    }

    /// <summary>
    /// Read an arbitrary channel setting on the device, typecasted to the given type.
    ///
    /// This function will throw if T is not a string, bool, or numeric primitive.
    /// </summary>
    /// <typeparam name="T">The type to cast the setting</typeparam>
    /// <param name="direction">The channel direction (RX or TX)</param>
    /// <param name="channel">An available channel on the device</param>
    /// <param name="key">The setting identifier</param>
    /// <returns>The setting value</returns>
    public T ReadSetting<T>(Direction direction, uint channel, string key)
    {
        return (T)(new Convertible(ReadSettingString(direction, channel, key)).ToType(typeof(T), null));
    }

    /// <summary>
    /// Write an arbitrary setting on the device, typecasted from the given type.
    ///
    /// For bools and primitive numeric types, SoapySDR's internal type conversion is used.
    /// Otherwise, the value of the input's ToString() will be passed in.
    /// </summary>
    /// <typeparam name="T">The type to cast the setting</typeparam>
    /// <param name="key">The setting identifier</param>
    /// <param name="value">The setting value</param>
    public void WriteSetting<T>(string key, T value)
    {
        WriteSettingString(key, new Convertible(value).ToString());
    }

    /// <summary>
    /// Write an arbitrary channel setting on the device, typecasted from the given type.
    ///
    /// For bools and primitive numeric types, SoapySDR's internal type conversion is used.
    /// Otherwise, the value of the input's ToString() will be passed in.
    /// </summary>
    /// <typeparam name="T">The type to cast the setting</typeparam>
    /// <param name="direction">The channel direction (RX or TX)</param>
    /// <param name="channel">An available channel on the device</param>
    /// <param name="key">The setting identifier</param>
    /// <param name="value">The setting value</param>
    public void WriteSetting<T>(Direction direction, uint channel, string key, T value)
    {
        WriteSettingString(direction, channel, key, new Convertible(value).ToString());
    }
    
    #endregion
    
    #region GPIO API

    public virtual IList<string> GPIOBanks => new List<string>();

    public virtual void WriteGPIO(string bank, uint value) { }

    public virtual void WriteGPIO(string bank, uint value, uint mask)
    {
        var readback = ReadGPIO(bank);
        var newValue = value | (readback & (~mask));
        WriteGPIO(bank, newValue);
    }

    public virtual uint ReadGPIO(string bank) => 0;

    public virtual void WriteGPIODir(string bank, uint dir) { }

    public virtual void WriteGPIODir(string bank, uint dir, uint mask)
    {
        var readback = ReadGPIODir(bank);
        var newValue = dir | (readback & ~mask);
        WriteGPIODir(bank, newValue);
    }

    public virtual uint ReadGPIODir(string bank) => 0;
    
    #endregion
    
    #region I2C API

    public virtual void WriteI2C(uint addr, string data) { }

    public virtual string readI2C(uint addr, uint numBytes) => "";
    
    #endregion
    
    #region SPI API

    public virtual uint TransactSPI(int addr, uint data, uint numBits) => 0;
    
    #endregion
    
    #region UART API

    public virtual IList<string> UARTs => new List<string>();

    public virtual void WriteUART(string which, string data) { }

    public virtual string ReadUART(string which, long timeoutUs = 100000) => "";
    
    #endregion

    #region Native Access API

    public virtual IntPtr NativeDeviceHandle => IntPtr.Zero;
    
    #endregion
    
    //
    // Object overrides
    //

    public override string ToString() => string.Format("{0}:{1}", DriverKey, HardwareKey);
}