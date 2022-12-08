using System.Runtime.InteropServices;

namespace SDRInterface.HackRF;

public unsafe partial class HackRfDevice
{
    private static HackRfSampleBlockCallback _rxCallback = new HackRfSampleBlockCallback(RxCallback);
    private static HackRfSampleBlockCallback _txCallback = new HackRfSampleBlockCallback(TxCallback);
    
    private static unsafe int RxCallback(HackRfTransfer* transfer)
    {
        var gcHandle = GCHandle.FromIntPtr(transfer->rxCtx);
        if (!gcHandle.IsAllocated)
            return -1;
        var target = (HackRfDevice) gcHandle.Target;
        return target.RxCallback(transfer->buffer, transfer->validLength);
    }
    
    private static unsafe int TxCallback(HackRfTransfer* transfer)
    {
        var gcHandle = GCHandle.FromIntPtr(transfer->txCtx);
        if (!gcHandle.IsAllocated)
            return -1;
        var target = (HackRfDevice) gcHandle.Target;
        return target.TxCallback(transfer->buffer, transfer->validLength);
    }

    private int RxCallback(byte* buffer, int length)
    {
        lock (_bufMutex)
        {
            _rxStream.bufTail = (_txStream.bufHead + _rxStream.bufCount) % _rxStream.bufNum;
            Buffer.MemoryCopy(buffer, _rxStream.buf[_rxStream.bufTail], length, length);

            if (_rxStream.bufCount == _rxStream.bufNum)
            {
                _rxStream.overflow = true;
                _rxStream.bufHead = (_rxStream.bufHead + 1) % _rxStream.bufNum;
            }
            else
            {
                _rxStream.bufCount++;
            }
            
            Monitor.Pulse(_bufMutex);

            return 0;
        }
    }

    private int TxCallback(byte* buffer, int length)
    {
        lock (_bufMutex)
        {
            if (_txStream.bufCount == 0)
            {
                for (var i = 0; i < length; i++) buffer[i] = 0;
                _txStream.underflow = true;
            }
            else
            {
                Buffer.MemoryCopy(_txStream.buf[_txStream.bufTail], buffer, length, length);
                _txStream.bufTail = (_txStream.bufTail + 1) % _txStream.bufNum;

                _txStream.bufCount--;

                if (_txStream.burstEnd)
                {
                    _txStream.burstSamps -= length / (int) BytesPerSample;
                    if (_txStream.burstSamps < 0)
                    {
                        _txStream.burstEnd = false;
                        _txStream.burstSamps = 0;
                        return -1;
                    }
                }
            }
            
            Monitor.Pulse(_bufMutex);

            return 0;
        }
    }

    public override IList<string> GetStreamFormats(Direction direction, uint channel)
        => new List<string>()
        {
            StreamFormat.ComplexInt8,
            StreamFormat.ComplexInt16,
            StreamFormat.ComplexFloat32,
            StreamFormat.ComplexFloat64
        };

    public override string GetNativeStreamFormat(Direction direction, uint channel, out double fullScale)
    {
        fullScale = 128;
        return StreamFormat.ComplexInt8;
    }

    public override IList<ArgInfo> GetStreamArgsInfo(Direction direction, uint channel) =>
        new List<ArgInfo>()
        {
            new ArgInfo()
            {
                Key = "buffers",
                Value = BufferNum.ToString(),
                Name = "Buffer Count",
                Description = "Number of buffers per read.",
                Units = "buffers",
                Type = ArgType.Int
            }
        };

    protected override StreamHandle SetupStream(Direction direction, string format, IList<uint> channels = null, IDictionary<string, string> args = null)
    {
        lock (_deviceMutex)
        {
            if (channels.Count > 1 || (channels.Count > 0 && channels[0] != 0))
            {
                throw new Exception("Invalid channel selection");
            }

            if (direction == Direction.Rx)
            {
                if (_rxStream.opened)
                    throw new Exception("RX stream already opened");

                if (format == StreamFormat.ComplexInt8)
                {
                    Logger.Log(LogLevel.Debug, "Using format CS8");
                    _rxStream.format = HackRfFormat.Int8;
                } 
                else if (format == StreamFormat.ComplexInt16)
                {
                    Logger.Log(LogLevel.Debug, "Using format CS16");
                    _rxStream.format = HackRfFormat.Int16;
                }
                else if (format == StreamFormat.ComplexFloat32)
                {
                    Logger.Log(LogLevel.Debug, "Using format CF32");
                    _rxStream.format = HackRfFormat.Float32;
                }
                else if (format == StreamFormat.ComplexFloat64)
                {
                    Logger.Log(LogLevel.Debug, "Using format CF64");
                    _rxStream.format = HackRfFormat.Float64;
                }
                else
                    throw new Exception("Invalid format " + format);

                _rxStream.bufNum = BufferNum;

                if (args.ContainsKey("buffers") && uint.TryParse(args["buffers"], out var buffers))
                {
                    _rxStream.bufNum = buffers;
                }
                
                _rxStream.AllocateBuffers();

                _rxStream.opened = true;

                return new StreamHandle()
                {
                    Index = RxStreamId,
                    Channels = channels.ToArray(),
                    Format = format
                };
            } 
            else if (direction == Direction.Tx)
            {
                if (_txStream.opened)
                    throw new Exception("TX stream already opened");

                if (format == StreamFormat.ComplexInt8)
                {
                    Logger.Log(LogLevel.Debug, "Using format CS8");
                    _txStream.format = HackRfFormat.Int8;
                } 
                else if (format == StreamFormat.ComplexInt16)
                {
                    Logger.Log(LogLevel.Debug, "Using format CS16");
                    _txStream.format = HackRfFormat.Int16;
                }
                else if (format == StreamFormat.ComplexFloat32)
                {
                    Logger.Log(LogLevel.Debug, "Using format CF32");
                    _txStream.format = HackRfFormat.Float32;
                }
                else if (format == StreamFormat.ComplexFloat64)
                {
                    Logger.Log(LogLevel.Debug, "Using format CF64");
                    _txStream.format = HackRfFormat.Float64;
                }
                else
                    throw new Exception("Invalid format " + format);
                
                _txStream.bufNum = BufferNum;

                if (args.ContainsKey("buffers") && uint.TryParse(args["buffers"], out var buffers))
                {
                    _txStream.bufNum = buffers;
                }
                
                _txStream.AllocateBuffers();

                _txStream.opened = true;

                return new StreamHandle()
                {
                    Index = TxStreamId,
                    Channels = channels.ToArray(),
                    Format = format
                };
            }
            else
            {
                throw new Exception("Invalid direction");
            }
        }
    }

    protected override void CloseStream(StreamHandle stream)
    {
        DeactivateStream(stream);
        lock (_deviceMutex)
        {
            if (stream.Index == RxStreamId)
            {
                _rxStream.ClearBuffers();
                _rxStream.opened = false;
            } 
            else if (stream.Index == TxStreamId)
            {
                _txStream.ClearBuffers();
                _txStream.opened = false;
            }
        }
    }

    protected override ulong GetStreamMTU(StreamHandle stream)
    {
        if (stream.Index == RxStreamId)
            return _rxStream.bufLen / BytesPerSample;
        else if (stream.Index == TxStreamId)
            return _txStream.bufLen / BytesPerSample;
        else
            throw new Exception("Invalid stream handle");
    }

    protected override ErrorCode ActivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0, uint numElems = 0)
    {
        if (stream.Index == RxStreamId)
        {
            lock (_deviceMutex)
            {
                if (_currentMode == TransceiverMode.Rx) return ErrorCode.None;

                if (_currentMode == TransceiverMode.Tx)
                {
                    if (_txStream.burstEnd)
                    {
                        while (HackRfApi.IsStreaming(_dev) == HackRfError.True)
                            Thread.Sleep(10);
                    }

                    HackRfApi.StopTx(_dev);

                    if (_currentSamplerate != _rxStream.samplerate)
                    {
                        _currentSamplerate = _rxStream.frequency;
                        Logger.LogF(LogLevel.Debug, "ActiveStream - Set RX samplerate to {0}", _currentSamplerate);
                        HackRfApi.SetSampleRate(_dev, _currentSamplerate);
                    }

                    if (_currentFrequency != _rxStream.frequency)
                    {
                        _currentFrequency = _rxStream.frequency;
                        Logger.LogF(LogLevel.Debug, "ActivateStream - Set RX frequency to {0}", _currentFrequency);
                        HackRfApi.SetFreq(_dev, _currentFrequency);
                    }

                    if (_currentAmp != _rxStream.ampGain)
                    {
                        _currentAmp = _rxStream.ampGain;
                        Logger.LogF(LogLevel.Debug, "ActivateStream - Set RX amp gain to {0}", _currentAmp);
                        HackRfApi.SetAmpEnable(_dev, (byte)(_currentAmp > 0 ? 0 : 1));
                    }

                    if (_currentBandwidth != _rxStream.bandwidth)
                    {
                        _currentBandwidth = _rxStream.bandwidth;
                        Logger.LogF(LogLevel.Debug, "ActivateStream - Set RX bandwidth to {0}", _currentBandwidth);
                        HackRfApi.SetBasebandFilterBandwidth(_dev, _currentBandwidth);
                    }
                }

                Logger.Log(LogLevel.Debug, "Start RX");

                    _rxStream.bufCount = 0;
                    _rxStream.bufHead = 0;
                    _rxStream.bufTail = 0;

                    var ret = HackRfApi.StartRx(_dev, _rxCallback, (IntPtr) _gcHandle);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_set_rx() failed -- {0}", HackRfApi.ErrorName(ret));
                    }

                    ret = HackRfApi.IsStreaming(_dev);

                    if (ret == HackRfError.StreamingExitCalled)
                    {
                        HackRfApi.Close(_dev);
                        HackRfApi.OpenBySerial(_serial, out _dev);
                        _currentFrequency = _rxStream.frequency;
                        HackRfApi.SetFreq(_dev, _currentFrequency);
                        _currentSamplerate = _rxStream.samplerate;
                        HackRfApi.SetSampleRate(_dev, _currentSamplerate);
                        _currentBandwidth = _rxStream.bandwidth;
                        HackRfApi.SetBasebandFilterBandwidth(_dev, _currentBandwidth);
                        _currentAmp = _rxStream.ampGain;
                        HackRfApi.SetAmpEnable(_dev, (byte)(_currentAmp > 0 ? 0 : 1));
                        HackRfApi.SetLnaGain(_dev, _rxStream.lnaGain);
                        HackRfApi.SetVgaGain(_dev, _rxStream.vgaGain);
                        HackRfApi.StartRx(_dev, _rxCallback, (IntPtr)_gcHandle);
                        ret = HackRfApi.IsStreaming(_dev);
                    }

                    if (ret != HackRfError.True)
                    {
                        Logger.Log(LogLevel.Error, "Activate RX stream failed");
                        return ErrorCode.StreamError;
                    }

                    _currentMode = TransceiverMode.Rx;
                
            }
        }
        else if (stream.Index == TxStreamId)
        {
            lock (_deviceMutex)
            {
                if ((flags & StreamFlags.EndBurst) != StreamFlags.None && numElems != 0)
                {
                    if (_currentMode == TransceiverMode.Tx) return ErrorCode.None;

                    if (_currentMode == TransceiverMode.Rx)
                    {
                        HackRfApi.StopRx(_dev);

                        if (_currentSamplerate != _txStream.samplerate)
                        {
                            _currentSamplerate = _txStream.frequency;
                            Logger.LogF(LogLevel.Debug, "ActiveStream - Set TX samplerate to {0}", _currentSamplerate);
                            HackRfApi.SetSampleRate(_dev, _currentSamplerate);
                        }

                        if (_currentFrequency != _txStream.frequency)
                        {
                            _currentFrequency = _txStream.frequency;
                            Logger.LogF(LogLevel.Debug, "ActivateStream - Set TX frequency to {0}", _currentFrequency);
                            HackRfApi.SetFreq(_dev, _currentFrequency);
                        }

                        if (_currentAmp != _txStream.ampGain)
                        {
                            _currentAmp = _txStream.ampGain;
                            Logger.LogF(LogLevel.Debug, "ActivateStream - Set TX amp gain to {0}", _currentAmp);
                            HackRfApi.SetAmpEnable(_dev, (byte)(_currentAmp > 0 ? 0 : 1));
                        }

                        if (_currentBandwidth != _txStream.bandwidth)
                        {
                            _currentBandwidth = _txStream.bandwidth;
                            Logger.LogF(LogLevel.Debug, "ActivateStream - Set TX bandwidth to {0}", _currentBandwidth);
                            HackRfApi.SetBasebandFilterBandwidth(_dev, _currentBandwidth);
                        }
                    }
                    
                    Logger.Log(LogLevel.Debug, "Start TX");

                    var ret = HackRfApi.StartTx(_dev, _txCallback, (IntPtr)_gcHandle);
                    if (ret != HackRfError.Success)
                    {
                        Logger.LogF(LogLevel.Error, "hackrf_start_tx() failed -- {0}", HackRfApi.ErrorName(ret));
                    }

                    ret = HackRfApi.IsStreaming(_dev);

                    if (ret == HackRfError.StreamingExitCalled)
                    {
                        HackRfApi.Close(_dev);
                        HackRfApi.OpenBySerial(_serial, out _dev);
                        _currentFrequency = _txStream.frequency;
                        HackRfApi.SetFreq(_dev, _currentFrequency);
                        _currentSamplerate = _txStream.samplerate;
                        HackRfApi.SetSampleRate(_dev, _currentSamplerate);
                        _currentBandwidth = _txStream.bandwidth;
                        HackRfApi.SetBasebandFilterBandwidth(_dev, _currentBandwidth);
                        _currentAmp = _txStream.ampGain;
                        HackRfApi.SetAmpEnable(_dev, (byte)(_currentAmp > 0 ? 0 : 1));
                        HackRfApi.SetTxVgaGain(_dev, _txStream.vgaGain);
                        HackRfApi.StartTx(_dev, _txCallback, (IntPtr)_gcHandle);
                        ret = HackRfApi.IsStreaming(_dev);
                    }

                    if (ret != HackRfError.True)
                    {
                        Logger.LogF(LogLevel.Error, "Activate TX stream failed");
                        return ErrorCode.StreamError;
                    }

                    _currentMode = TransceiverMode.Tx;
                }
            }    
        }

        return ErrorCode.None;
    }

    protected override ErrorCode DeactivateStream(StreamHandle stream, StreamFlags flags = StreamFlags.None, long timeNs = 0)
    {
        if (stream.Index == RxStreamId)
        {
            lock (_deviceMutex)
            {
                if (_currentMode == TransceiverMode.Rx)
                {
                    var ret = HackRfApi.StopRx(_dev);
                    if (ret != HackRfError.Success) 
                        Logger.LogF(LogLevel.Error, "hackrf_stop_rx() failed -- {0}", HackRfApi.ErrorName(ret));
                    _currentMode = TransceiverMode.Off;
                }
            }
        }
        else if (stream.Index == TxStreamId)
        {
            lock (_deviceMutex)
            {
                if (_currentMode == TransceiverMode.Tx)
                {
                    var ret = HackRfApi.StopTx(_dev);
                    if (ret != HackRfError.Success)
                        Logger.LogF(LogLevel.Error, "hackrf_stop_tx() failed -- {0}", HackRfApi.ErrorName(ret));
                    _currentMode = TransceiverMode.Off;
                }
                
            }
        }

        return ErrorCode.None;
    }

    private static void ReadBuff(sbyte* src, void* dst, uint len, HackRfFormat format, uint offset)
    {
        if (format == HackRfFormat.Int8)
        {
            var samples = (sbyte*)dst + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                samples[i * BytesPerSample] = src[i * BytesPerSample];
                samples[i * BytesPerSample + 1] = src[i * BytesPerSample + 1];
            }
        }
        else if (format == HackRfFormat.Int16)
        {
            var samples = (short*)dst + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                samples[i * BytesPerSample] = (short)(src[i * BytesPerSample] << 8);
                samples[i * BytesPerSample + 1] = (short)(src[i * BytesPerSample + 1] << 8);
            }
        }
        else if (format == HackRfFormat.Float32)
        {
            var samples = (float*)dst + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                samples[i * BytesPerSample] = (float)(src[i * BytesPerSample] / 127.0);
                samples[i * BytesPerSample + 1] = (float)(src[i * BytesPerSample + 1] / 127.0);
            }
        }
        else if (format == HackRfFormat.Float64)
        {
            var samples = (double*)dst + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                samples[i * BytesPerSample] = (double)(src[i * BytesPerSample] / 127.0);
                samples[i * BytesPerSample + 1] = (double)(src[i * BytesPerSample] / 127.0);
            }
        }
        else
        {
            Logger.LogF(LogLevel.Error, "Read format not supported");
        }
    }

    private static void WriteBuff(void* src, sbyte* dst, uint len, HackRfFormat format, uint offset)
    {
        if (format == HackRfFormat.Int8)
        {
            var samples = (sbyte*)src + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                dst[i * BytesPerSample] = samples[i * BytesPerSample];
                dst[i * BytesPerSample + 1] = samples[i * BytesPerSample + 1];
            }
        }
        else if (format == HackRfFormat.Int16)
        {
            var samples = (short*)src + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                dst[i * BytesPerSample] = (sbyte) (samples[i * BytesPerSample] >> 8);
                dst[i * BytesPerSample + 1] = (sbyte) (samples[i * BytesPerSample + 1] >> 8);
            }
        }
        else if (format == HackRfFormat.Float32)
        {
            var samples = (float*)src + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                dst[i * BytesPerSample] = (sbyte) (samples[i * BytesPerSample] * 127.0);
                dst[i * BytesPerSample + 1] = (sbyte) (samples[i * BytesPerSample + 1] * 127.0);
            }
        }
        else if (format == HackRfFormat.Float64)
        {
            var samples = (double*)src + offset * BytesPerSample;
            for (var i = 0; i < len; ++i)
            {
                dst[i * BytesPerSample] = (sbyte) (samples[i * BytesPerSample] * 127.0);
                dst[i * BytesPerSample + 1] = (sbyte) (samples[i * BytesPerSample + 1] * 127.0);
            }
        }
    }

    protected override StreamResult ReadStream(StreamHandle stream, void*[] buffs, uint numElems, long timeoutUs = 100000)
    {
        if (stream.Index != RxStreamId)
            return StreamResult.Error(ErrorCode.NotSupported);

        var returnedElems = Math.Min(numElems, (uint) GetStreamMTU(stream));
        var sampAvaliable = 0u;

        if (_rxStream.reminderHandle >= 0)
        {
            var n = Math.Min(_rxStream.reminderSamps, returnedElems);

            if (n < returnedElems)
                sampAvaliable = n;

            ReadBuff(_rxStream.reminderBuff + _rxStream.reminderOffset * BytesPerSample, buffs[0], n, _rxStream.format,
                0);

            _rxStream.reminderOffset += n;
            _rxStream.reminderSamps -= n;

            if (_rxStream.reminderSamps == 0)
            {
                ReleaseReadBuffer(stream, _rxStream.reminderHandle);
                _rxStream.reminderHandle = -1;
                _rxStream.reminderOffset = 0;
            }

            if (n == returnedElems)
                return StreamResult.Success((int)returnedElems);
        }

        var handle = -1;
        var buf = new void*[1];
        var ret = AcquireReadBuffer(stream, ref handle, buf, timeoutUs);
        _rxStream.reminderBuff = (sbyte*) buf[0];

        if (ret.Status != ErrorCode.None)
        {
            if ((ret.Status == ErrorCode.Timeout) && sampAvaliable > 0)
                return StreamResult.Success((int) sampAvaliable);
            return StreamResult.Error(ret.Status);
        }

        _rxStream.reminderHandle = handle;
        _rxStream.reminderSamps = (uint) ret.NumSamples;

        if (_rxStream.reminderSamps == 0)
        {
            ReleaseReadBuffer(stream, _rxStream.reminderHandle);
            _rxStream.reminderHandle = -1;
            _rxStream.reminderOffset = 0;
        }

        return StreamResult.Success((int)returnedElems);
    }

    protected override StreamResult WriteStream(StreamHandle stream, void*[] buffs, uint numElems, StreamFlags flags, long timeNs,
        long timeoutUs = 100000)
    {
        if (stream.Index != TxStreamId)
            return StreamResult.Error(ErrorCode.NotSupported);

        var returnedElems = Math.Min(numElems, (uint)GetStreamMTU(stream));

        var sampAvail = 0u;

        if (_rxStream.reminderHandle >= 0)
        {
            var n = Math.Min(_rxStream.reminderSamps, returnedElems);

            if (n < returnedElems)
                sampAvail = n;
            
            WriteBuff(buffs[0], _txStream.reminderBuff + _txStream.reminderOffset * BytesPerSample, n, _txStream.format, 0);
            _txStream.reminderSamps -= n;
            _txStream.reminderOffset += n;

            if (_txStream.reminderSamps == 0)
            {
                ReleaseWriteBuffer(stream, _txStream.reminderHandle, _txStream.reminderOffset, ref flags, timeNs);
                _txStream.reminderHandle = -1;
                _txStream.reminderOffset = 0;
            }
            
            if (n == returnedElems)
                return StreamResult.Success((int) returnedElems, flags, timeNs);
        }

        var index = -1;
        var buf = new void*[1];
        var ret = AcquireWriteBuffer(stream, ref index, buf, timeoutUs);
        _txStream.reminderBuff = (sbyte*)buf[0];
        if (ret.Status < 0)
        {
            if ((ret.Status == ErrorCode.Timeout) && sampAvail > 0)
            {
                return StreamResult.Success((int) sampAvail, flags, timeNs);
            }
            return ret;
        }

        _txStream.reminderHandle = index;
        _txStream.reminderSamps = (uint) ret.NumSamples;

        var len = Math.Min(returnedElems - sampAvail, _txStream.reminderSamps);
        
        WriteBuff(buffs[0], _rxStream.reminderBuff, len, _txStream.format, sampAvail);
        _txStream.reminderSamps -= len;
        _txStream.reminderOffset += len;

        if (_txStream.reminderSamps == 0)
        {
            ReleaseWriteBuffer(stream, _txStream.reminderHandle, _txStream.reminderOffset, ref flags, timeNs);
            _txStream.reminderHandle = -1;
            _txStream.reminderOffset = 0;
        }
        
        return StreamResult.Success((int) returnedElems, flags, timeNs);
    }

    protected override StreamResult ReadStreamStatus(StreamHandle stream, long timeoutUs = 100000)
    {
        if (stream.Index != TxStreamId)
            return StreamResult.Error(ErrorCode.NotSupported);

        var timeout = TimeSpan.FromMilliseconds(timeoutUs);
        var exitTime = DateTime.Now + timeout;

        while (true)
        {
            if (_txStream.underflow)
            {
                _txStream.underflow = false;
                Logger.Log(LogLevel.SSI, "U");
                return StreamResult.Error(ErrorCode.Underflow);
            }

            var sleepTime = Math.Min(1000, timeoutUs / 10);
            Thread.Sleep((int) sleepTime);

            var timeNow = DateTime.Now;
            if (exitTime < timeNow) return StreamResult.Error(ErrorCode.Timeout);
        }
    }

    public override StreamResult AcquireReadBuffer(StreamHandle stream, ref int index, void*[] buffs, long timeoutUs = 100000)
    {
        if (stream.Index != RxStreamId)
            return StreamResult.Error(ErrorCode.NotSupported);

        if (_currentMode != TransceiverMode.Rx)
        {
            lock (_bufMutex)
            {
                if (_txStream.bufCount == 0 && !Monitor.Wait(_bufMutex, (int) timeoutUs))
                    return StreamResult.Error(ErrorCode.Timeout);
            }

            var ret = ActivateStream(stream);
            if (ret < ErrorCode.None) return StreamResult.Error(ret);
        }

        lock (_bufMutex)
        {
            while (_rxStream.bufCount == 0)
            {
                Monitor.Wait(_bufMutex, (int)timeoutUs);
                if (_rxStream.bufCount == 0) return StreamResult.Error(ErrorCode.Timeout);
            }

            if (_rxStream.overflow)
            {
                _rxStream.overflow = false;
                Logger.Log(LogLevel.SSI, "O");
                return StreamResult.Error(ErrorCode.Overflow, StreamFlags.EndAbrupt);
            }

            index = (int) _rxStream.bufHead;
            _rxStream.bufHead = (_rxStream.bufHead + 1) % _rxStream.bufNum;
            GetDirectAccessBufferAddrs(stream, index, buffs);
            
            return StreamResult.Success((int) GetStreamMTU(stream));
        }
    }

    public override void ReleaseReadBuffer(StreamHandle stream, int index)
    {
        if (stream.Index != RxStreamId)
            throw new Exception("Invalid stream");

        lock (_bufMutex)
            _rxStream.bufCount--;
    }

    public override StreamResult AcquireWriteBuffer(StreamHandle stream, ref int index, void*[] buffs, long timeoutUs = 100000)
    {
        if (stream.Index != TxStreamId)
            return StreamResult.Error(ErrorCode.NotSupported);

        if (_currentMode != TransceiverMode.Tx)
        {
            var ret = ActivateStream(stream);
            if (ret < ErrorCode.None) return StreamResult.Error(ret);
        }

        lock (_bufMutex)
        {
            while (_txStream.bufCount == _txStream.bufNum)
            {
                Monitor.Wait(_bufMutex, (int)timeoutUs);
                if (_txStream.bufCount == _txStream.bufNum) return StreamResult.Error(ErrorCode.Timeout);
            }

            index = (int) _txStream.bufHead;
            _txStream.bufHead = (_txStream.bufHead + 1) % _txStream.bufNum;

            GetDirectAccessBufferAddrs(stream, index, buffs);

            if (_txStream.burstEnd)
            {
                if (_txStream.burstSamps - (int)GetStreamMTU(stream) < 0)
                {
                    for (var i = 0; i < (int)GetStreamMTU(stream); i++) ((byte*)buffs[0])[i] = 0;
                    return StreamResult.Success(_txStream.burstSamps);
                }
            }
            
            return StreamResult.Success((int) GetStreamMTU(stream));
        }
    }

    public override void ReleaseWriteBuffer(StreamHandle stream, int index, uint numElems, ref StreamFlags flags, long timeNs = 0)
    {
        if (stream.Index == TxStreamId)
        {
            lock (_bufMutex)
            {
                _txStream.bufCount++;
            }
        }
        else
        {
            throw new Exception("Invalid stream");
        }
    }


    public override uint GetNumDirectAccessBuffers(StreamHandle stream)
    {
        if (stream.Index == RxStreamId)
            return _rxStream.bufNum;
        else if (stream.Index == TxStreamId)
            return _txStream.bufNum;
        else
            throw new Exception("Invalid stream");
    }

    public override ErrorCode GetDirectAccessBufferAddrs(StreamHandle stream, int index, void*[] buffs)
    {
        if (stream.Index == RxStreamId)
            buffs[0] = (void*)_rxStream.buf[index];
        else if (stream.Index == TxStreamId)
            buffs[0] = (void*)_txStream.buf[index];
        else
            throw new Exception("Invalid stream");
        return ErrorCode.None;
    }
}