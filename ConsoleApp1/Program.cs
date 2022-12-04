// See https://aka.ms/new-console-template for more information

using System.Reflection;
using SDRInterface;

class Program
{
    
    public static void Main(String[] args)
    {
        Logger.LogLevel = LogLevel.Trace;
        var assembly = Assembly.LoadFile(@"C:\Users\qq276\RiderProjects\SDRInterface\ConsoleApp1\bin\Debug\net6.0\SDRInterface.RtlSdr.dll");
        Registry.RegisterAssembly(assembly);
        var e = Device.Enumerate("");
        var d = Device.Make(e[0]);
        var f = d.GetFrequency(Direction.Rx, 0);
        var sr = d.GetSampleRate(Direction.Rx, 0);
        
        var s = d.SetupRxStream(StreamFormat.ComplexFloat32, new uint[] { 0 });
        var r = s.Activate();
        while (true)
        {
            var buf = new float[s.MTU];
            r = s.Read(ref buf, 10000, out var _);
            Console.WriteLine(r);
        }
    }
}