namespace SDRInterface;

public static class Time
{
    public static long TicksToTimeNs(long ticks, double rate)
    {
        var ratel = (long)rate;
        var full = (long)(ticks / ratel);
        var err = ticks - (full * ratel);
        var part = full * (rate - ratel);
        var frac = ((err - part) * 1000000000) / rate;
        return (full * 1000000000) + llround(frac);
    }

    public static long TimeNsToTicks(long timeNs, double rate)
    {
        var ratel = (long)rate;
        var full = (long)(timeNs / 1000000000);
        var err = timeNs - (full * 1000000000);
        var part = full * (rate - ratel);
        var frac = part + ((err * rate) / 1000000000);
        return (full * ratel) + llround(frac);
    }

    private static long llround(double x)
    {
        return (long)(x < 0.0 ? (x - 0.5) : (x + 0.5));
    }
} 