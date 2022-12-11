// See https://aka.ms/new-console-template for more information

using System.Numerics;
using System.Reflection;
using SDRInterface;

class Program
{
    
    public static void Main(String[] args)
    {
        Logger.LogLevel = LogLevel.Trace;
        var e = Device.Enumerate("");
        var d = Device.Make(e[0]);
        var f = d.GetFrequency(Direction.Rx, 0);
        var sr = d.GetSampleRate(Direction.Rx, 0);
        
        d.WriteSetting("biastee", true);
        
        var s = d.SetupRxStream(StreamFormat.ComplexFloat32, new uint[] { 0 });
        var r = s.Activate();
        var buf = new float[(int)s.MTU * 2];
        while (true)
        {
            r = s.Read(ref buf, 100000, out var res);
           // Console.WriteLine(r);
        }
    }
}