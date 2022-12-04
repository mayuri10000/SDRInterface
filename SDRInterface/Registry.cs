using System.Reflection;

namespace SDRInterface;

using Kwargs = IDictionary<string, string>;

public class Registry
{
    private MethodInfo _findFunction;
    private MethodInfo _makeFunction;
    public string Name { get; }

    public static List<Registry> Registries = new List<Registry>();

    private Registry(string name, MethodInfo findFunction, MethodInfo makeFunction)
    {
        Name = name;
        _findFunction = findFunction;
        _makeFunction = makeFunction;
    }

    static Registry()
    {
        RegisterAssembly(Assembly.GetCallingAssembly());
    }

    public IList<Kwargs> Find(Kwargs args)
    {
        return _findFunction.Invoke(null, new object?[] { args }) as IList<Kwargs>;
    }

    public Device Make(Kwargs args)
    {
        return _makeFunction.Invoke(null, new object?[] {args}) as Device;
    }

    public static void RegisterType(Type type)
    {
        var att = type.GetCustomAttribute<RegistryAttribute>();
        if (att == null) return;

        var name = att.Name;

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

        MethodInfo findFunc = null;
        MethodInfo makeFunc = null;

        foreach (var method in methods)
        {
            if (method.GetCustomAttribute<FindFunctionAttribute>() != null && findFunc == null)
            {
                if (!method.ReturnType.IsAssignableTo(typeof(IList<Kwargs>)))
                    throw new ArgumentException("Invalid return type of find function");
                
                if (method.GetParameters().Length != 1 || !method.GetParameters()[0].ParameterType.IsAssignableTo(typeof(Kwargs))) 
                    throw new ArgumentException("Invalid parameters of find function");

                findFunc = method;
            }

            if (method.GetCustomAttribute<MakeFunctionAttribute>() != null && makeFunc == null)
            {
                if (!method.ReturnType.IsAssignableTo(typeof(Device)))
                    throw new ArgumentException("Invalid return type of make function");
                
                if (method.GetParameters().Length != 1 || !method.GetParameters()[0].ParameterType.IsAssignableTo(typeof(Kwargs))) 
                    throw new ArgumentException("Invalid parameters of make function");

                makeFunc = method;
            }
        }

        if (findFunc == null) throw new Exception("Device " + name + " has no find function defined");
        if (makeFunc == null) throw new Exception("Device " + name + " has no make function defined");

        var reg = new Registry(name, findFunc, makeFunc);
        Registries.Add(reg);
    }

    public static void RegisterAssembly(Assembly assembly)
    {
        var types = assembly.DefinedTypes;
        foreach (var type in types)
        {
            RegisterType(type);
        }
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class RegistryAttribute : Attribute
{
    public string Name { get; }

    public RegistryAttribute(string name)
    {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class FindFunctionAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class MakeFunctionAttribute : Attribute
{
}