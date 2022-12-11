namespace SDRInterface.Airspy;

public unsafe partial class AirspyDevice
{
    public const int MaxDevices = 32;
    
    [FindFunction]
    public static IList<IDictionary<string, string>> FindAirspy(IDictionary<string, string> args)
    {
        var results = new List<IDictionary<string, string>>();
        
        AirspyApi.LibVersion(out var asVersion);
        Logger.LogF(LogLevel.Debug, "Airspy libray v{0}.{1} rev {2}", asVersion.majorVersion, asVersion.minorVersion,
            asVersion.reversion);

        var serials = stackalloc ulong[MaxDevices];
        var count = AirspyApi.ListDevices(serials, MaxDevices);
        if (count < 0)
        {
            Logger.LogF(LogLevel.Error, "airspy_list_devices failed with: {0}", (AirspyError) count);
            return results;
        }
        
        Logger.LogF(LogLevel.Debug, "{0} Airspy devices found", count);

        for (var i = 0; i < count; i++)
        {
            var serialStr = serials[i].ToString("X8");
            
            Logger.LogF(LogLevel.Debug, "Airspy device serial {0}", serialStr);

            var info = new Dictionary<string, string>();
            info["label"] = "Airspy R2/Mini [" + serialStr + "]";
            info["serial"] = serialStr;

            if (args.ContainsKey("serial"))
            {
                if (args["serial"] != serialStr)
                    continue;
                Logger.LogF(LogLevel.Debug, "Found device by serial {0}", serialStr);
            }
            
            results.Add(info);
        }

        return results;
    }

    [MakeFunction]
    public static Device MakeAirspy(IDictionary<string, string> args)
    {
        return new AirspyDevice(args);
    }
}