namespace SDRInterface;

[Registry("null")]
public class NullDevice : Device
{
    public override string DriverKey => "null";
    public override string HardwareKey => "null";

    [FindFunction]
    public static IList<IDictionary<string, string>> FindNullDevice(IDictionary<string, string> args)
    {
        var result = new List<IDictionary<string, string>>();

        if (!args.ContainsKey("type")) return result;
        if (args["type"] != "null") return result;

        var nullArgs = new Dictionary<string, string>()
        {
            { "type", "null" }
        };
        result.Add(nullArgs);

        return result;
    }

    [MakeFunction]
    public static Device MakeNullDevice(IDictionary<string, string> kwargs)
    {
        return new NullDevice();
    }
}