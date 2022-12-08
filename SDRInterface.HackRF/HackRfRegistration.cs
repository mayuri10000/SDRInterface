namespace SDRInterface.HackRF;

public unsafe partial class HackRfDevice
{
    private static IDictionary<string, IDictionary<string, string>> _cachedResults =
        new Dictionary<string, IDictionary<string, string>>();

    private static ISet<string> _claimedSerials = new HashSet<string>();

    [FindFunction]
    public static IList<IDictionary<string, string>> FindHackRf(IDictionary<string, string> args)
    {
        using (new HackRfSession())
        {
            var results = new List<IDictionary<string, string>>();

            var list = HackRfApi.DeviceList();

            if (list->deviceCount > 0)
            {
                for (var i = 0; i < list->deviceCount; i++)
                {
                    HackRfApi.DeviceListOpen(list, i, out IntPtr device);
                    var versionStr = stackalloc sbyte[100];
                    var options = new Dictionary<string, string>();
                    if (device != IntPtr.Zero)
                    {
                        HackRfApi.ReadBoardId(device, out var boardId);
                        options["device"] = HackRfApi.BoardIdName(boardId);

                        HackRfApi.ReadVersionString(device, versionStr, 100);
                        options["version"] = new string(versionStr);

                        HackRfApi.ReadBoardPartIdAndSerialNo(device, out var readPartidSerialno);
                        options["part_id"] = $"{readPartidSerialno.partId[0]:X8}{readPartidSerialno.partId[1]:X8}";

                        var serial =
                            $"{readPartidSerialno.SerialNo[0]:x8}{readPartidSerialno.SerialNo[1]:x8}{readPartidSerialno.SerialNo[2]:x8}{readPartidSerialno.SerialNo[3]:x8}";
                        options["serial"] = serial;

                        var ofs = 0;
                        while (ofs < serial.Length && serial[ofs] == '0') ofs++;
                        var label = $"{options["device"]} #{i} {serial.Substring(ofs)}";
                        options["label"] = label;

                        var serialMatch = !args.ContainsKey("serial") || args["serial"] == options["serial"];
                        var idxMatch = !args.ContainsKey("hackrf") || int.Parse(args["hackrf"]) == i;
                        if (serialMatch && idxMatch)
                        {
                            results.Add(options);
                            _cachedResults[serial] = options;
                        }

                        HackRfApi.Close(device);
                    }
                }
            }
            
            HackRfApi.DeviceListFree(list);

            foreach (var serial in _claimedSerials)
            {
                if (!_cachedResults.ContainsKey(serial)) continue;
                if (args.ContainsKey("serial") && args["serial"] != serial) continue;
                results.Add(_cachedResults[serial]);
            }

            return results;
        }
    }

    [MakeFunction]
    public static Device MakeHackRf(IDictionary<string, string> args)
    {
        return new HackRfDevice(args);
    }
}