namespace SDRInterface;

using System;

/// <summary>
/// SoapySDR's logging interface.
///
/// Through this, messages can be logged to console or file by registering C# delegates.
/// The logging API is designed to match System.String.Format().
/// </summary>
public class Logger
{
    /// <summary>
    /// The signature of a function to use for logging.
    /// </summary>
    /// <param name="logLevel">The message's level of importance.</param>
    /// <param name="message">The message text.</param>
    public delegate void LoggerDelegate(LogLevel logLevel, string message);

    private static LoggerDelegate _registeredLogHandler;
    private static LogLevel _registeredLogLevel;

    // TODO: add read after getter implemented

    static Logger()
    {
        _registeredLogHandler = DefaultLogHandler;
        _registeredLogLevel = GetDefaultLogLevel();
    }

    private static void DefaultLogHandler(LogLevel logLevel, string message)
    {
        switch (logLevel)
        {
            case LogLevel.Fatal:
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Error.WriteLine("[FATAL] " + message);
                break;
            case LogLevel.Critical:
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Error.WriteLine("[CRITICAL] " + message);
                break;
            case LogLevel.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("[ERROR] " + message);
                break;
            case LogLevel.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine("[WARNING] " + message);
                break;
            case LogLevel.Notice:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Error.WriteLine("[NOTICE] " + message);
                break;
            case LogLevel.Info:
                Console.Error.WriteLine("[INFO] " + message);
                break;
            case LogLevel.Debug:
                Console.Error.WriteLine("[DEBUG] " + message);
                break;
            case LogLevel.Trace:
                Console.Error.WriteLine("[TRACE] " + message);
                break;
            case LogLevel.SSI:
                Console.Error.Write(message);
                break;
        }
        
        Console.ResetColor();
    }

    private static LogLevel GetDefaultLogLevel()
    {
        return LogLevel.Info;
    }

    /// <summary>
    /// The log level threshold. Messages with lower priorities are dropped.
    /// </summary>
    public static LogLevel LogLevel
    {
        get => _registeredLogLevel;
        set => _registeredLogLevel = value;
    }

    /// <summary>
    /// Register a custom logging function to be used for all SoapySDR logging calls.
    /// </summary>
    /// <param name="del">A logging function, or null for the default logger (prints to stderr).</param>
    public static void RegisterLogHandler(LoggerDelegate del)
    {
        if (del != null)
        {
            _registeredLogHandler = del;
        }
        else
        {
            _registeredLogHandler = DefaultLogHandler;
        }
    }

    /// <summary>
    /// Log a message with a given level and string.
    /// </summary>
    /// <param name="logLevel">The message's priority</param>
    /// <param name="message">The message string</param>
    public static void Log(LogLevel logLevel, string message)
    {
        if (logLevel > _registeredLogLevel && logLevel != LogLevel.SSI) return;
        _registeredLogHandler(logLevel, message);
    }

    /// <summary>
    /// Log a message with a given level and string, formatted with System.String.Format().
    /// </summary>
    /// <param name="logLevel">The message's priority</param>
    /// <param name="format">The message format</param>
    public static void LogF(LogLevel logLevel, string format, object arg) =>
        Log(logLevel, string.Format(format, arg));

    /// <summary>
    /// Log a message with a given level and string, formatted with System.String.Format().
    /// </summary>
    /// <param name="logLevel">The message's priority</param>
    /// <param name="format">The message format</param>
    public static void LogF(LogLevel logLevel, string format, params object[] args) =>
        Log(logLevel, string.Format(format, args));

    /// <summary>
    /// Log a message with a given level and string, formatted with System.String.Format().
    /// </summary>
    /// <param name="logLevel">The message's priority</param>
    /// <param name="format">The message format</param>
    public static void LogF(LogLevel logLevel, IFormatProvider formatProvider, string format, object arg) =>
        Log(logLevel, string.Format(formatProvider, format, arg));

    /// <summary>
    /// Log a message with a given level and string, formatted with System.String.Format().
    /// </summary>
    /// <param name="logLevel">The message's priority</param>
    /// <param name="format">The message format</param>
    public static void LogF(LogLevel logLevel, IFormatProvider formatProvider, string format, params object[] args) =>
        Log(logLevel, string.Format(formatProvider, format, args));
}
