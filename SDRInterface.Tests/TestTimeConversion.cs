namespace SDRInterface.Tests;

public class TestTimeConversion
{
    readonly System.Random rng = new System.Random();
    readonly double[] Rates = { 1e9, 52e6, 61.44e6, 100e6 / 3 };

    // https://docs.microsoft.com/en-us/dotnet/api/system.random?view=net-5.0#Long
    internal long RandomLong() => (long)(rng.NextDouble() * long.MaxValue);

    [Repeat(100)]
    [Test]
    public void Test_TimeToTicks()
    {
        var timeNs = RandomLong();
        foreach(var rate in Rates)
        {
            var ticks = Time.TimeNsToTicks(timeNs, rate);
            var timeNsOut = Time.TicksToTimeNs(ticks, rate);

            // We expect an error because timeNs specifies a subtick.
            Assert.Less(System.Math.Abs(timeNs - timeNsOut) / 1e9, rate);
        }
    }

    [Repeat(100)]
    [Test]
    public void Test_TicksToTime()
    {
        // Room for max rate
        var ticks = RandomLong() >> 8;
        foreach(var rate in Rates)
        {
            var timeNs = Time.TicksToTimeNs(ticks, rate);
            var ticksOut = Time.TimeNsToTicks(timeNs, rate);
            Assert.AreEqual(ticks, ticksOut);
        }
    }
}