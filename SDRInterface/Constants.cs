namespace SDRInterface;

public enum Direction
{
    Tx,
    Rx
}

public enum ErrorCode
{
    None,
    Timeout = -1,
    StreamError = -2,
    Corruption = -3,
    Overflow = -4,
    NotSupported = -5,
    TimeError = -6,
    Underflow = -7
}

public enum LogLevel
{
    Fatal = 1,
    Critical = 2,
    Error = 3,
    Warning = 4,
    Notice = 5,
    Info = 6,
    Debug = 7,
    Trace = 8,
    SSI = 9
}