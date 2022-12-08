namespace SDRInterface.HackRF;

public class HackRfSession : IDisposable
{
    private static object _sessionMutex = new object();
    private static int _sessionCount = 0;

    public HackRfSession()
    {
        lock (_sessionMutex)
        {
            if (_sessionCount == 0)
            {
                var ret = HackRfApi.Init();
                if (ret != HackRfError.Success)
                {
                    Logger.Log(LogLevel.Error, "hackrf_init() failed -- " + HackRfApi.ErrorName(ret));
                }
            }

            _sessionCount++;
        }
    }

    ~HackRfSession()
    {
        Dispose();
    }

    public void Dispose()
    {
        lock (_sessionMutex)
        {
            _sessionCount--;
            if (_sessionCount == 0)
            {
                var ret = HackRfApi.Exit();
                if (ret != HackRfError.Success)
                {
                    Logger.Log(LogLevel.Error, "hackrf_exit() failed -- " + HackRfApi.ErrorName(ret));
                }
            }
        }
    }
}