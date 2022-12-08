using System.Xml;

namespace SDRInterface;

/// <summary>
/// The base class for representing transmit or receive streams. This
/// class will never be used itself. See TxStream and RxStream.
/// </summary>
public class Stream : IDisposable
{
    internal Device _device = null;
    internal StreamHandle _streamHandle;
    protected bool _active = false;
    protected bool _isClosed = false;

    /// <summary>
    /// The underlying stream format. See Pothosware.SoapySDR.StreamFormat.
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// The device channels used in this stream.
    /// </summary>
    public uint[] Channels { get; }

    /// <summary>
    /// The arguments used in creating this stream.
    /// </summary>
    public IDictionary<string, string> StreamArgs { get; }

    /// <summary>
    /// Whether or not the stream is active.
    /// </summary>
    public bool Active => _active;

    // We already used these parameters to create the stream,
    // this is just for the sake of getters.
    internal Stream(
        Device device,
        string format,
        uint[] channels,
        IDictionary<string, string> kwargs)
    {
        _device = device;

        Format = format;
        Channels = (uint[])channels.Clone();
        StreamArgs = kwargs;
    }

    ~Stream()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_active) Deactivate();
        if (!_isClosed) Close();
    }

    /// <summary>
    /// The number of elements per channel this stream can handle in a single read/write call.
    /// </summary>
    public ulong MTU => _device.GetStreamMTU(_streamHandle);

    /// <summary>
    /// Activate the stream to prepare it for read/write operations.
    /// </summary>
    /// <param name="flags">Optional stream flags.</param>
    /// <param name="timeNs">Optional activation time in nanoseconds. Only valid when flags includes Pothosware.SoapySDR.StreamFlags.HasTime.</param>
    /// <param name="numElems">Optional element count for burst control.</param>
    /// <returns>An error code for the stream activation.</returns>
    public ErrorCode Activate(
        StreamFlags flags = StreamFlags.None,
        long timeNs = 0,
        uint numElems = 0)
    {
        ErrorCode ret;
        if (!_isClosed)
        {
            if (!_active)
            {
                ret = _device.ActivateStream(
                    _streamHandle,
                    flags,
                    timeNs,
                    numElems);

                if (ret == ErrorCode.None) _active = true;
            }
            else throw new InvalidOperationException("Stream is already active");
        }
        else throw new InvalidOperationException("Stream is closed");

        return ret;
    }

    /// <summary>
    /// Deactivate the stream to end read/write operations.
    /// </summary>
    /// <param name="flags">Optional stream flags.</param>
    /// <param name="timeNs">Optional activation time in nanoseconds. Only valid when flags includes Pothosware.SoapySDR.StreamFlags.HasTime.</param>
    /// <param name="numElems">Optional element count for burst control.</param>
    /// <returns>An error code for the stream deactivation.</returns>
    public ErrorCode Deactivate(
        StreamFlags flags = StreamFlags.None,
        long timeNs = 0)
    {
        ErrorCode ret;
        if (_isClosed)
        {
            if (!_active)
            {
                ret = _device.DeactivateStream(
                    _streamHandle,
                    flags,
                    timeNs);

                if (ret == ErrorCode.None) _active = false;
            }
            else throw new InvalidOperationException("Stream is already inactive");
        }
        else throw new InvalidOperationException("Stream is closed");

        return ret;
    }

    /// <summary>
    /// Close the underlying stream.
    /// </summary>
    public void Close()
    {
        if (_isClosed)
        {
            _device.CloseStream(_streamHandle);
            _isClosed = true;
        }
        else throw new InvalidOperationException("Stream is already closed");
    }

    //
    // Utility
    //

    protected void ValidateSpan<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        var numChannels = _streamHandle.Channels.Length;
        var format = _streamHandle.Format;
        var complexFormatString = Utility.GetFormatString<T>();

        if (numChannels != 1)
            throw new ArgumentException("Stream is configured for a single channel. Cannot accept multiple buffers.");
        else if (!format.Equals(complexFormatString))
            throw new ArgumentException(string.Format("Stream format \"{0}\" is incompatible with buffer type {1}.",
                format, typeof(T)));
        else if ((span.Length % 2) != 0)
            throw new ArgumentException("For complex interleaved streams, input buffers must be of an even size.");
    }

    protected void ValidateSpan<T>(Span<T> span) where T : unmanaged => ValidateSpan((ReadOnlySpan<T>)span);

    protected void ValidateMemory<T>(ReadOnlyMemory<T>[] mems) where T : unmanaged
    {
        var numChannels = _streamHandle.Channels.Length;
        var format = _streamHandle.Format;

        var complexFormatString = Utility.GetFormatString<T>();

        if (mems == null)
            throw new ArgumentNullException("mems");
        else if (numChannels != mems.Length)
            throw new ArgumentException(string.Format(
                "Stream is configured for {0} channel(s). Cannot accept {1} buffer(s).", numChannels, mems.Length));
        else if (!format.Equals(complexFormatString))
            throw new ArgumentException(string.Format("Stream format \"{0}\" is incompatible with buffer type {1}.",
                format, typeof(T)));
        else if (mems.Select(buff => buff.Length).Distinct().Count() > 1)
            throw new ArgumentException("All buffers must be of the same length");
        else if ((mems[0].Length % 2) != 0)
            throw new ArgumentException("For complex interleaved streams, input buffers must be of an even size.");
    }

    protected void ValidateMemory<T>(Memory<T>[] mems) where T : unmanaged =>
        ValidateMemory(mems.Select(mem => (ReadOnlyMemory<T>)mem).ToArray());

    protected void ValidateIntPtrArray(IntPtr[] intPtrs)
    {
        var numChannels = _streamHandle.Channels.Length;
        if (intPtrs == null)
            throw new ArgumentNullException("mems");
        else if (intPtrs.Length != numChannels)
            throw new ArgumentException(string.Format(
                "Stream is configured for {0} channel(s). Cannot accept {1} buffer(s).", numChannels, intPtrs.Length));
        else if (intPtrs.Any(x => x == null))
            throw new ArgumentNullException("intPtrs");
    }

    //
    // Object overrides
    //

    // For completeness, but a stream is only ever equal to itself
    public override bool Equals(object other) => ReferenceEquals(this, other);

    public override int GetHashCode() => HashCodeBuilder.Create()
        .AddValue(GetType())
        .AddValue(_device)
        .AddValue(Format)
        .AddValue(Channels)
        .AddValue(StreamArgs);
}

[Flags]
public enum StreamFlags
{
    None          = 0,
    EndBurst      = (1 << 1),
    HasTime       = (1 << 2),
    EndAbrupt     = (1 << 3),
    OnePacket     = (1 << 4),
    MoreFragments = (1 << 5),
    WaitTrigger   = (1 << 6),
    UserFlag0     = (1 << 16),
    UserFlag1     = (1 << 17),
    UserFlag2     = (1 << 18),
    UserFlag3     = (1 << 19),
    UserFlag4     = (1 << 20)
}

public static class StreamFormat
{
    public const string ComplexFloat64 = "CF64";
    public const string ComplexFloat32 = "CF32";
    public const string ComplexInt32 = "CS32";
    public const string ComplexUInt32 = "CU32";
    public const string ComplexInt16 = "CS16";
    public const string ComplexUInt16 = "CU16";
    public const string ComplexInt12 = "CS12";
    public const string ComplexUInt12 = "CU12";
    public const string ComplexInt8 = "CS8";
    public const string ComplexUInt8 = "CU8";
    public const string ComplexInt4 = "CS4";
    public const string ComplexUInt4 = "CU4";
    public const string RealFloat64 = "F64";
    public const string RealFloat32 = "F32";
    public const string RealInt32 = "S32";
    public const string RealUInt32 = "U32";
    public const string RealInt16 = "S16";
    public const string RealUInt16 = "U16";
    public const string RealInt8 = "S8";
    public const string RealUInt8 = "U8";

    public static int FormatToSize(string format)
    {
        var size = 0;
        var isComplex = false;
        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (ch == 'C') isComplex = true;
            if (char.IsDigit(ch)) size = size * 10 + (ch - '0');
        }

        if (isComplex) size *= 2;
        return size / 8;
    }
}

public struct StreamHandle
{
    public int Index;
    public uint[] Channels;
    public string Format;
}

public struct StreamResult
{
    public ErrorCode Status;
    public int NumSamples;
    public StreamFlags Flags;
    public long TimeNs;
    public int ChanMask;

    public static StreamResult Error(ErrorCode code, StreamFlags flags = StreamFlags.None)
        => new StreamResult() { Status = code, Flags = flags};

    public static StreamResult Success(int samples, StreamFlags flags = StreamFlags.None, long timeNs = 0)
        => new StreamResult() { Status = ErrorCode.None, NumSamples = samples, Flags = flags, TimeNs = timeNs };
}