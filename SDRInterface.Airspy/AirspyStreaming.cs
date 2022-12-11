using System.Runtime.InteropServices;

namespace SDRInterface.Airspy;

public unsafe partial class AirspyDevice
{
    public override IList<string> GetStreamFormats(Direction direction, uint channel)
        => new List<string>() { StreamFormat.ComplexInt16, StreamFormat.ComplexFloat32 };

    public override string GetNativeStreamFormat(Direction direction, uint channel, out double fullScale)
    {
        fullScale = 32767;
        return StreamFormat.ComplexInt16;
    }

    private static int RxCallbackStatic(AirspyTransfer* transfer)
    {
        var gcHandle = GCHandle.FromIntPtr(transfer->ctx);
        if (!gcHandle.IsAllocated)
            return 0;
        var target = (AirspyDevice) gcHandle.Target;
        return target.RxCallback(transfer);
    }

    private int RxCallback(AirspyTransfer* transfer)
    {
        if (_sampleRateChanged == 1)
            return 1;

        if (_bufCount == _numBuffers)
        {
            Interlocked.Exchange(ref _overflowEvent, 1);
            return 0;
        }

        var buff = _buffs[_bufTail];
        buff.length = transfer->sampleCount * _bytesPerSample;
        Buffer.MemoryCopy(transfer->samples, buff.data, _bufferLength * _bytesPerSample,
            transfer->sampleCount * _bytesPerSample);

        _bufTail = (_bufTail + 1) % _numBuffers;

        lock (_bufMutex)
        {
            Interlocked.Increment(ref _bufCount);
            Monitor.Pulse(_bufMutex);
        }

        return 0;
    }

    protected override StreamHandle SetupStream(Direction direction, string format, IList<uint> channels = null, IDictionary<string, string> args = null)
    {
        if (channels.Count > 1 || (channels.Count > 0 && channels[0] != 0))
            throw new Exception("Invalid channel selection");

        var asFormat = AirspySampleType.Int16IQ;

        if (format == StreamFormat.ComplexFloat32)
        {
            Logger.Log(LogLevel.Info, "Using format CF32");
            asFormat = AirspySampleType.Float32IQ;
        }
        else if (format == StreamFormat.ComplexInt16)
        {
            Logger.Log(LogLevel.Info, "Using format CS16");
            asFormat = AirspySampleType.Int16IQ;
        }
        else
        {
            throw new ArgumentException("Invalid format: " + format + ", Airspy module supports CS16 and CF32");
        }

        AirspyApi.SetSampleType(_dev, asFormat);
        Interlocked.Exchange(ref _sampleRateChanged, 1);

        _bytesPerSample = StreamFormat.FormatToSize(format);

        _bufferLength = DefaultBufferBytes / 4;

        _bufTail = 0;
        _bufCount = 0;
        _bufHead = 0;

        _buffs = new Buff[_numBuffers];
        for (var i = 0; i < _numBuffers; i++)
        {
            _buffs[i].data = (byte*)Marshal.AllocHGlobal((int)_bufferLength * _bytesPerSample);
            _buffs[i].length = (int)_bufferLength * _bytesPerSample;
        }

        return new StreamHandle()
        {
            Channels = channels.ToArray(),
            Format = format,
            Index = 1
        };
    }

    protected override void CloseStream(StreamHandle stream)
    {
        for (var i = 0; i < _numBuffers; i++)
            Marshal.FreeHGlobal((IntPtr) _buffs[i].data);
        _buffs = null;
    }

    protected override ulong GetStreamMTU(StreamHandle stream)
    {
        return _bufferLength;
    }

    protected override ErrorCode ActivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0, uint numElems = 0)
    {
        if (flags != 0)
            return ErrorCode.NotSupported;

        _resetBuffer = true;
        _bufferedElems = 0;

        if (_sampleRateChanged == 1)
        {
            AirspyApi.SetSampleRate(_dev, _sampleRate);
            Interlocked.Exchange(ref _sampleRateChanged, 0);
        }

        AirspyApi.StartRx(_dev, RxCallbackStatic, (IntPtr) _gcHandle);
        
        _streamActive = true;
        
        return ErrorCode.None;
    }

    protected override ErrorCode DeactivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0)
    {
        if (flags != 0) return ErrorCode.NotSupported;

        AirspyApi.StopRx(_dev);

        _streamActive = false;

        return ErrorCode.None;
    }

    protected override StreamResult ReadStream(StreamHandle stream, void*[] buffs, uint numElems, long timeoutUs = 100000)
    {
        if (AirspyApi.IsStreaming(_dev) != AirspyError.True)
            return StreamResult.Success(0);

        if (_sampleRateChanged == 1)
        {
            AirspyApi.StopRx(_dev);
            AirspyApi.SetSampleRate(_dev, _sampleRate);
            AirspyApi.StartRx(_dev, RxCallbackStatic, (IntPtr) _gcHandle);
            Interlocked.Exchange(ref _sampleRateChanged, 0);
        }

        var buff = buffs[0];
        var flags = StreamFlags.None;

        if (_bufferedElems == 0)
        {
            var buf = new void*[1];
            var ret = AcquireReadBuffer(stream, ref _currentHandle, buf, timeoutUs);
            _currentBuff = (byte*) buf[0];
            if (ret.Status < 0) return ret;
            _bufferedElems = (uint) ret.NumSamples;
        }

        var returnedElems = (int) Math.Min(_bufferedElems, numElems);
        Buffer.MemoryCopy(_currentBuff, buff, returnedElems * _bytesPerSample, returnedElems * _bytesPerSample);

        _bufferedElems -= (uint) returnedElems;
        _currentBuff += returnedElems * _bytesPerSample;

        if (_bufferedElems != 0) flags |= StreamFlags.MoreFragments;
        else ReleaseReadBuffer(stream, _currentHandle);
        return StreamResult.Success(returnedElems);
    }

    public override uint GetNumDirectAccessBuffers(StreamHandle stream)
    {
        return (uint) _buffs.Length;
    }

    public override ErrorCode GetDirectAccessBufferAddrs(StreamHandle stream, int index, void*[] buffs)
    {
        buffs[0] = _buffs[index].data;
        return ErrorCode.None;
    }

    public override StreamResult AcquireReadBuffer(StreamHandle stream, ref int index, void*[] buffs, long timeoutUs = 100000)
    {
        if (_resetBuffer)
        {
            _bufHead = (_bufHead + Interlocked.Exchange(ref _bufCount, 0)) % _numBuffers;
            _resetBuffer = false;
            Interlocked.Exchange(ref _overflowEvent, 0);
        }

        if (_overflowEvent == 1)
        {
            _bufHead = (_bufHead + Interlocked.Exchange(ref _bufCount, 0)) % _numBuffers;
            Interlocked.Exchange(ref _overflowEvent, 0);
            Logger.Log(LogLevel.SSI, "O");
            return StreamResult.Error(ErrorCode.Overflow);
        }

        if (_bufCount == 0)
        {
            lock (_bufMutex)
            {
                Monitor.Wait(_bufMutex, (int) timeoutUs);
                if (_bufCount == 0) return StreamResult.Error(ErrorCode.Timeout);
            }
        }

        index = (int) _bufHead;
        _bufHead = (_bufHead + 1) & _numBuffers;
        buffs[0] = _buffs[index].data;
        
        return StreamResult.Success(_buffs[index].length / _bytesPerSample);
    }

    public override void ReleaseReadBuffer(StreamHandle stream, int index)
    {
        Interlocked.Decrement(ref _bufCount);
    }
}