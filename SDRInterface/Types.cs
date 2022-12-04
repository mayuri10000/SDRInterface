namespace SDRInterface;

public struct Range
{
    public double Minimum { get; }
    public double Maximum { get; }
    public double Step { get; }

    public Range()
    {
        Minimum = 0.0;
        Maximum = 0.0;
        Step = 0.0;
    }

    public Range(double minimum, double maximum, double step = 0.0)
    {
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }
    
    //
    // Object overrides
    //

    public override string ToString()
    {
        return $"Range: min={Minimum}, max={Maximum}, step={Step}";
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Range)) return false;
        var objAsRange = (Range) obj;
        return Minimum.Equals(objAsRange.Minimum) && Maximum.Equals(objAsRange.Maximum) && Step.Equals(objAsRange.Step);
    }

    public override int GetHashCode() => HashCodeBuilder.Create()
        .AddValue(GetType())
        .AddValue(Minimum)
        .AddValue(Maximum)
        .AddValue(Step);
}

/// <summary>
/// The data type of arguments.
/// </summary>
public enum ArgType
{
    /// <summary>
    /// String argument
    /// </summary>
    String,
    /// <summary>
    /// Boolean argument
    /// </summary>
    Bool,
    /// <summary>
    /// Integral argument
    /// </summary>
    Int,
    /// <summary>
    /// Floating-point argument
    /// </summary>
    Float
}

/// <summary>
/// Representation of a key/value argument.
/// </summary>
public struct ArgInfo
{
    /// <summary>
    /// The key used to identify the argument.
    /// </summary>
    public string Key;

    public string Value;

    public string Name;

    public string Description;

    public string Units;

    public ArgType Type;

    public Range Range;

    public IEnumerable<string> Options;

    public IEnumerable<string> OptionNames;
    
    public override string ToString()
    {
        return string.Format("{0} ({1})", Name, Type);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is ArgInfo)) return false;
        var objAsArgInfo = (ArgInfo) obj ;
        return Key.Equals(objAsArgInfo.Key) && Type.Equals(objAsArgInfo.Type);
    }

    public override int GetHashCode() => HashCodeBuilder.Create()
        .AddValue(GetType())
        .AddValue(Key)
        .AddValue(Type);
}