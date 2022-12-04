using System.Runtime.InteropServices;

namespace SDRInterface.RtlSdr;

public unsafe partial class RtlSdrDevice
{
    private IList<(float, float)> _lut_32f = new List<(float, float)>();
    private IList<(float, float)> _lut_swap_32f = new List<(float, float)>();
    private IList<(short, short)> _lut_16i = new List<(short, short)>();
    private IList<(short, short)> _lut_swap_16i = new List<(short, short)>();

    private struct RtlBuffer
    {
        public long tick;
        public byte* data;
        public uint size;
    }

    private Thread _rxAsyncThread;
    private object _bufMutex = new object();

    private RtlBuffer[] _buffs;
    private uint _bufHead = 0;
    private uint _bufTail = 0;
    private uint _bufCount = 0;
    private byte* _currentBuf;
    private int _currentHandle;
    private int _overflowEvent;
    private uint _bufferedElems;
    private long _bufTicks;
    private int _resetBuffer;

    private static RtlSdrReadAsyncCallback _callback = new RtlSdrReadAsyncCallback(RtlRxCallback);

    public override IList<string> GetStreamFormats(Direction direction, uint channel)
        => new List<string>() { StreamFormat.ComplexInt8, StreamFormat.ComplexInt16, StreamFormat.ComplexFloat32 };

    public override string GetNativeStreamFormat(Direction direction, uint channel, ref double fullScale)
    {
        //check that direction RX
        if (direction != Direction.Rx)
            throw new Exception("RTL-SDR is RX only, use Direction.Rx");

        fullScale = 128;
        return StreamFormat.ComplexInt8;
    }

    public override IList<ArgInfo> GetStreamArgsInfo(Direction direction, uint channel)
    {
        if (direction != Direction.Rx)
            throw new Exception("RTL-SDR is RX only");

        var streamArgs = new List<ArgInfo>();

        var bufflenArg = new ArgInfo()
        {
            Key = "bufflen",
            Value = DefaultBufferLength.ToString(),
            Name = "Buffer Size",
            Description = "Number of bytes per buffer, multiples of 512 only.",
            Units = "bytes",
            Type = ArgType.Int
        };
        
        streamArgs.Add(bufflenArg);

        var buffersArg = new ArgInfo()
        {
            Key = "buffers",
            Value = DefaultNumBuffers.ToString(),
            Name = "Ring buffers",
            Description = "Number of buffers in the ring.",
            Units = "buffers",
            Type = ArgType.Int
        };
        
        streamArgs.Add(buffersArg);

        var asyncBuffsArg = new ArgInfo()
        {
            Key = "asyncBuffs",
            Value = "0",
            Name = "Async buffers",
            Description = "Number of async usb buffers (advanced).",
            Units = "buffers",
            Type = ArgType.Int
        };
        
        streamArgs.Add(asyncBuffsArg);

        return streamArgs;
    }

    private static unsafe void RtlRxCallback(byte* buf, uint len, IntPtr ctx)
    {
        // Console.WriteLine("RtlRxCallback");
        var gcHandle = GCHandle.FromIntPtr(ctx);
        if (!gcHandle.IsAllocated)
            return;
        var target = (RtlSdrDevice) gcHandle.Target;
        target.RxCallback(buf, len);
    }

    private void RxAsyncOperation()
    {
        RtlApi.ReadAsync(_dev, _callback, (IntPtr)_gcHandle, _asyncBuffs, _bufferLength);
    }

    private unsafe void RxCallback(byte* buf, uint len)
    {
        var tick = Interlocked.Add(ref _ticks, len);

        if (_bufCount == _numBuffers)
        {
            Interlocked.Exchange(ref _overflowEvent, 1);
            return;
        }

        ref var buff = ref _buffs[_bufTail];
        buff.tick = tick;
        buff.size = len;
        Buffer.MemoryCopy(buf, buff.data, _bufferLength, len);

        _bufTail = (_bufTail + 1) % _numBuffers;

        lock (_bufMutex)
        {
            Interlocked.Increment(ref _bufCount);
        }

        lock (_bufMutex)
            Monitor.Pulse(_bufMutex);
    }

    protected override StreamHandle SetupStream(Direction direction, string format, IList<uint> channels = null, IDictionary<string, string> args = null)
    {
        if (direction != Direction.Rx)
        {
            throw new Exception("RTL-SDR is RX only");
        }

        if (channels.Count > 1 || (channels.Count > 0 && channels[0] != 0))
        {
            throw new Exception("setupStream invalid channel selection");
        }

        if (format == StreamFormat.ComplexFloat32)
        {
            Logger.Log(LogLevel.Info, "Using format CF32.");
            _rxFormat = RxFormat.Float32;
        }
        else if (format == StreamFormat.ComplexInt16)
        {
            Logger.Log(LogLevel.Info, "Using format CS16.");
            _rxFormat = RxFormat.Int16;
        }
        else if (format == StreamFormat.ComplexInt8)
        {
            Logger.Log(LogLevel.Info, "Using format CS8.");
            _rxFormat = RxFormat.Int8;
        }
        else
        {
            throw new Exception("setupStream invalid format '" + format
                                                               + "' -- Only CS8, CS16 and CF32 are supported by RTLSDR module.");
        }

        if (_rxFormat != RxFormat.Int8 && _lut_32f.Count == 0)
        {
            Logger.Log(LogLevel.Debug, "Generate RTL-SDR lookup tables");
            for (var i = 0; i <= 0xffff; i++)
            {
                float re = ((i & 0xff) - 127.4f) * (1.0f / 128.0f);
                float im = ((i >> 8) - 127.4f) * (1.0f / 128.0f);
                
                _lut_32f.Add((re, im));
                _lut_swap_32f.Add((im, re));

                var i16re = (short)((float)short.MaxValue * re);
                var i16im = (short)((float)short.MaxValue * im);
                
                _lut_16i.Add((i16re, i16im));
                _lut_swap_16i.Add((i16im, i16re));
            }
        }

        _bufferLength = DefaultBufferLength;
        if (args.ContainsKey("buffers"))
        {
            if (!int.TryParse(args["buffers"], out int numBuffers) && numBuffers > 0)
                _numBuffers = (uint) numBuffers;
        }
        Logger.LogF(LogLevel.Debug, "RTL-SDR Using {0} buffers", _numBuffers);

        _asyncBuffs = 0;
        if (args.ContainsKey("asyncBuffs"))
        {
            if (!int.TryParse(args["asyncBuffs"], out int asyncBuffs) && asyncBuffs > 0)
                _asyncBuffs = (uint) asyncBuffs;
        }

        if (_tunerType == RtlSdrTunerType.E4000)
        {
            _ifGain[0] = 6;
            _ifGain[1] = 9;
            _ifGain[2] = 3;
            _ifGain[3] = 2;
            _ifGain[4] = 3;
            _ifGain[5] = 2;
        }

        _tunerGain = RtlApi.GetTunerGain(_dev) / 10.0;

        _bufTail = 0;
        _bufCount = 0;
        _bufHead = 0;
        
        _buffs = new RtlBuffer[_numBuffers];
        for (var i = 0; i < _numBuffers; i++)
        {
            _buffs[i].data = (byte*)Marshal.AllocHGlobal((int) _bufferLength);
            _buffs[i].size = _bufferLength;
        }

        return new StreamHandle()
        {
            Index = 1,
            Channels = (uint[])channels.ToArray(),
            Format = format
        };
    }

    protected override void CloseStream(StreamHandle stream)
    {
        DeactivateStream(stream, StreamFlags.None, 0);
        foreach (var buff in _buffs)
        {
            Marshal.FreeHGlobal((IntPtr) buff.data);
        }

        _buffs = new RtlBuffer[0];
    }

    protected override ulong GetStreamMTU(StreamHandle stream)
    {
        return _bufferLength / BytesPerSample;
    }

    protected override ErrorCode ActivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0, uint numElems = 0)
    {
        if (flags != StreamFlags.None) return ErrorCode.NotSupported;
        Interlocked.Exchange(ref _resetBuffer, 1);
        _bufferedElems = 0;

        if (_rxAsyncThread == null || !_rxAsyncThread.IsAlive)
        {
            RtlApi.ResetBuffer(_dev);
            _rxAsyncThread = new Thread(RxAsyncOperation);
            _rxAsyncThread.Priority = ThreadPriority.Highest;
            _rxAsyncThread.Start();
        }

        return ErrorCode.None;
    }

    protected override ErrorCode DeactivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0)
    {
        if (flags != StreamFlags.None) return ErrorCode.NotSupported;
        if (_rxAsyncThread != null && _rxAsyncThread.IsAlive)
        {
            RtlApi.CancelAsync(_dev);
            _rxAsyncThread.Join();
            _rxAsyncThread = null;
        }

        return ErrorCode.None;
    }

    protected override StreamResultPairInternal ReadStream(StreamHandle stream, IList<UIntPtr> buffs, uint numElems, long timeoutUs = 100000)
    {
        if (_resetBuffer == 1 && _bufferedElems != 0)
        {
            _bufferedElems = 0;
            ReleaseReadBuffer(stream, _currentHandle);
        }

        var buff = (void*)buffs[0];
        var flags = StreamFlags.None;
        var timeNs = 0l;

        if (_bufferedElems == 0)
        {
            var ret = AcquireReadBuffer(stream, ref _currentHandle, new List<UIntPtr>() { (UIntPtr)_currentBuf }, timeoutUs);
            if (ret.Code < 0) return ret;
            _bufferedElems = (uint)ret.Code;
        }
        else
        {
            flags |= StreamFlags.HasTime;
            timeNs = Time.TicksToTimeNs(_bufTicks, _sampleRate);
        }

        var returnedElems = Math.Min(_bufferedElems, numElems);

        if (_rxFormat == RxFormat.Float32)
        {
            var ftarget = (float*)buff;
            if (_iqSwap)
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    var tmp = _lut_swap_32f[*(ushort*)_currentBuf[2 * i]];
                    ftarget[i * 2] = tmp.Item1;
                    ftarget[i * 2 + 1] = tmp.Item2;
                }
            }
            else
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    var tmp = _lut_32f[*(ushort*)_currentBuf[2 * i]];
                    ftarget[i * 2] = tmp.Item1;
                    ftarget[i * 2 + 1] = tmp.Item2;
                }
            }
        }
        else if (_rxFormat == RxFormat.Int16)
        {
            var itarget = (short*)buff;
            if (_iqSwap)
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    var tmp = _lut_swap_16i[*(ushort*)_currentBuf[2 * i]];
                    itarget[i * 2] = tmp.Item1;
                    itarget[i * 2 + 1] = tmp.Item2;
                }
            }
            else
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    var tmp = _lut_16i[*(ushort*)_currentBuf[2 * i]];
                    itarget[i * 2] = tmp.Item1;
                    itarget[i * 2 + 1] = tmp.Item2;
                }
            }
        }
        else if (_rxFormat == RxFormat.Int8)
        {
            var itarget = (sbyte*)buff;
            if (_iqSwap)
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    itarget[i * 2] = (sbyte) (_currentBuf[i * 2 + 1]-128);
                    itarget[i * 2 + 1] = (sbyte) (_currentBuf[i * 2]-128);
                }
            }
            else
            {
                for (var i = 0; i < returnedElems; i++)
                {
                    itarget[i * 2] = (sbyte) (_currentBuf[i * 2]-128);
                    itarget[i * 2 + 1] = (sbyte) (_currentBuf[i * 2 + 1]-128);
                }
            }
        }

        _bufferedElems -= returnedElems;
        _currentBuf += returnedElems * BytesPerSample;
        _bufTicks += returnedElems;

        if (_bufferedElems != 0) flags |= StreamFlags.MoreFragments;
        else ReleaseReadBuffer(stream, _currentHandle);
        
        return new StreamResultPairInternal()
        {
            Code = ErrorCode.None,
            Result = new StreamResult()
            {
                NumSamples = (int) returnedElems,
                Flags = flags,
                TimeNs = timeNs
            }
        };
    }

    public override uint GetNumDirectAccessBuffers(StreamHandle stream)
    {
        return (uint) _buffs.Length;
    }

    public override ErrorCode GetDirectAccessBufferAddrs(StreamHandle stream, int index, IList<UIntPtr> buffs)
    {
        buffs.Clear();
        buffs.Add((UIntPtr) _buffs[index].data);
        return 0;
    }

    public override StreamResultPairInternal AcquireReadBuffer(StreamHandle stream, ref int index, IList<UIntPtr> buffs, long timeoutUs = 100000)
    {
        if (_resetBuffer == 1)
        {
            _bufHead = (_bufHead + Interlocked.Exchange(ref _bufCount, 0)) % _numBuffers;
            Interlocked.Exchange(ref _resetBuffer, 0);
            Interlocked.Exchange(ref _overflowEvent, 0);
        }

        if (_overflowEvent == 1)
        {
            _bufHead = (_bufHead + Interlocked.Exchange(ref _bufCount, 0)) % _numBuffers;
            Interlocked.Exchange(ref _overflowEvent, 0);
            Logger.Log(LogLevel.SSI, "O");
            return new StreamResultPairInternal()
            {
                Code = ErrorCode.Overflow,
            };
        }

        if (_bufCount == 0)
        {
            lock (_bufMutex)
            {
                Monitor.Wait(_bufMutex, TimeSpan.FromMilliseconds(timeoutUs));
                if (_bufCount == 0)
                    return new StreamResultPairInternal()
                    {
                        Code = ErrorCode.Timeout
                    };
            }
        }

        index = (int) _bufHead;
        _bufHead = (_bufHead + 1) % _numBuffers;
        _bufTicks = _buffs[index].tick;
        var timeNs = Time.TicksToTimeNs(_buffs[index].tick, _sampleRate);
        buffs.Clear();
        buffs.Add((UIntPtr) _buffs[index].data);
        var flags = StreamFlags.HasTime;

        return new StreamResultPairInternal()
        {
            Code = ErrorCode.None,
            Result = new StreamResult()
            {
                Flags = flags,
                TimeNs = timeNs,
                NumSamples = (int)_buffs[index].size / BytesPerSample,
            }
        };
    }

    public override void ReleaseReadBuffer(StreamHandle stream, int index)
    {
        Interlocked.Decrement(ref _bufCount);
    }
}