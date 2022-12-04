namespace SDRInterface;

public partial class Device
{
    private static object _factoryMutex = new object();
    private static IDictionary<string, Device> _deviceTable = new Dictionary<string, Device>();
    private static IDictionary<Device, uint> _deviceCounts = new Dictionary<Device, uint>();


    public static IList<IDictionary<string, string>> Enumerate() => Enumerate("");

    public static IList<IDictionary<string, string>> Enumerate(IDictionary<string, string> args)
    {
        var futures = new Dictionary<string, Task<IList<IDictionary<string, string>>>>();
        foreach (var reg in Registry.Registries)
        {
            var specifiedDriver = args.ContainsKey("driver");
            if (specifiedDriver && args["driver"] != reg.Name) continue;

            var future = new Task<IList<IDictionary<string, string>>>(() => reg.Find(args));
            future.Start();
            futures[reg.Name] = future;
        }

        var results = new List<IDictionary<string, string>>();
        foreach (var it in futures)
        {
            it.Value.Wait();
            if (it.Value.IsCompletedSuccessfully)
            {
                foreach (var handle in it.Value.Result)
                {
                    handle["driver"] = it.Key;
                    results.Add(handle);
                }
            }
            else if (it.Value.IsFaulted)
            {
                Logger.LogF(LogLevel.Error, "Device.Enumerate({0}) {1}", it.Key, it.Value.Exception.Message);
            }
        }

        return results;
    }

    public static IList<IDictionary<string, string>> Enumerate(string args)
    {
        return Enumerate(Utility.StringToKwargs(args));
    }

    private static Device GetDeviceFromTable(IDictionary<string, string> args)
    {
        if (args.Count == 0) return null;
        var key = Utility.KwargsToString(args);
        if (!_deviceTable.ContainsKey(key)) return null;
        var device = _deviceTable[key];
        if (device == null)
            throw new Exception("Device deletion in-progress");
        _deviceCounts[device]++;
        return device;
    }

    public static Device Make(IDictionary<string, string> args)
    {
        Monitor.Enter(_factoryMutex);

        var device = GetDeviceFromTable(args);
        if (device != null)
        {
            Monitor.Exit(_factoryMutex);
            return device;
        }

        IDictionary<string, string> discoveredArgs = new Dictionary<string, string>();
        Monitor.Exit(_factoryMutex);
        var results = Enumerate(args);
        if (results.Count > 0) discoveredArgs = results[0];
        Monitor.Enter(_factoryMutex);

        device = GetDeviceFromTable(discoveredArgs);
        if (device != null)
        {
            Monitor.Exit(_factoryMutex);
            return device;
        }

        var hybridArgs = new Dictionary<string, string>(discoveredArgs);
        foreach (var it in args)
        {
            if (!hybridArgs.ContainsKey(it.Key)) hybridArgs[it.Key] = it.Value;
        }

        var specifiedDriver = hybridArgs.ContainsKey("driver");
        var registries = Registry.Registries;

        if (!specifiedDriver && registries.Count > 2)
        {
            Monitor.Exit(_factoryMutex);
            throw new Exception("No driver specified and no enumeration results");
        }

        var cache = new Dictionary<string, Task<Device>>();
        Task<Device> deviceFuture = null;
        var key = Utility.KwargsToString(discoveredArgs);
        foreach (var it in registries)
        {
            if (!specifiedDriver && it.Name == "null") continue;
            if (specifiedDriver && hybridArgs["driver"] != it.Name) continue;
            if (cache.ContainsKey(key)) deviceFuture = cache[key];
            else
            {
                deviceFuture = new Task<Device>(() => it.Make(hybridArgs));
                cache[key] = deviceFuture;
            }
            break;
        }

        if (deviceFuture == null) throw new Exception("No matching found");
        
        Monitor.Exit(_factoryMutex);
        deviceFuture.Start();
        deviceFuture.Wait();
        Monitor.Enter(_factoryMutex);

        cache.Remove(key);

        device = deviceFuture.Result;
        _deviceTable[key] = device;
        if (_deviceCounts.ContainsKey(device))
            _deviceCounts[device]++;
        else
            _deviceCounts[device] = 1;

        Monitor.Exit(_factoryMutex);
        return device;
    }

    public static Device Make(string args)
    {
        return Make(Utility.StringToKwargs(args));
    }

    public static void Unmake(Device device)
    {
        if (device == null) return;
        
        Monitor.Enter(_factoryMutex);

        if (!_deviceCounts.ContainsKey(device))
        {
            Monitor.Exit(_factoryMutex);
            throw new Exception("Unknown device");
        }

        if (--_deviceCounts[device] != 0)
        {
            Monitor.Exit(_factoryMutex);
            return;
        }

        _deviceCounts.Remove(device);

        var argsList = new List<string>();
        foreach (var it in _deviceTable)
        {
            if (it.Value != device) continue;
            argsList.Add(it.Key);
            _deviceTable[it.Key] = null;
        }
        
        Monitor.Exit(_factoryMutex);
        device.Dispose();
        Monitor.Enter(_factoryMutex);

        foreach (var args in argsList)
        {
            _deviceTable.Remove(args);
        }
        
        Monitor.Exit(_factoryMutex);
    }

}