using System.Configuration;

namespace SDRInterface;

public static class Module
{
    public static IList<string> ListSearchPaths()
    {
        var ret = new List<string>();
        
        ret.Add(".");
        ret.Add("./Modules");

        // TODO: Read from config
        return ret;
    }
}