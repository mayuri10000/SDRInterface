using System.Configuration;
using System.Reflection;

namespace SDRInterface;

public static class Module
{
    private static IDictionary<string, Assembly> _moduleAssemblies = new Dictionary<string, Assembly>();
    private static string _moduleLoading = "";

    private static string _currentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

    public static IList<string> ListSearchPaths()
    {
        var ret = new List<string>();
        
        ret.Add(".");
        ret.Add("./Modules");

        // TODO: Read from config
        return ret;
    }

    public static IList<string> ListModules(string path)
    {
        var modules = new List<string>();
        if (Directory.Exists(path))
        {
            var dir = new DirectoryInfo(path);
            foreach (var file in dir.GetFiles())
            {
                if (file.Name.StartsWith(_currentAssemblyName) && file.Extension == ".dll" && file.Name != _currentAssemblyName + ".dll")
                {
                    modules.Add(file.FullName);
                }
            }
        }

        return modules;
    }

    public static IList<string> ListModules()
    {
        var modules = new List<string>();
        foreach (var searchPath in ListSearchPaths())
        {
            modules.AddRange(ListModules(searchPath));
        }
        
        return modules;
    }

    public static void LoadModule(string modulePath)
    {
        var assembly = Assembly.LoadFrom(modulePath);
        _moduleAssemblies[modulePath] = assembly;
        
        Registry.RegisterAssembly(assembly);
        Logger.LogF(LogLevel.Debug, "Loaded module: " + modulePath);
    }

    public static void LoadModules()
    {
        foreach (var path in ListModules())
        {
            LoadModule(path);
        }
    }
}