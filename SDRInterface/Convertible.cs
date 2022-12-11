namespace SDRInterface;

public class Convertible : IConvertible
{
    private string _value;

    public Convertible(object value)
    {
        _value = value.ToString();
    }


    public TypeCode GetTypeCode() => TypeCode.Object;

    public bool ToBoolean(IFormatProvider? provider) => bool.Parse(_value);

    public byte ToByte(IFormatProvider? provider) => byte.Parse(_value);

    public char ToChar(IFormatProvider? provider) => char.Parse(_value);

    public DateTime ToDateTime(IFormatProvider? provider) => DateTime.Parse(_value);

    public decimal ToDecimal(IFormatProvider? provider) => Decimal.Parse(_value);

    public double ToDouble(IFormatProvider? provider) => Double.Parse(_value);

    public short ToInt16(IFormatProvider? provider) => short.Parse(_value);

    public int ToInt32(IFormatProvider? provider) => int.Parse(_value);

    public long ToInt64(IFormatProvider? provider) => long.Parse(_value);

    public sbyte ToSByte(IFormatProvider? provider) => SByte.Parse(_value);

    public float ToSingle(IFormatProvider? provider) => float.Parse(_value);

    public string ToString(IFormatProvider? provider) => _value;

    public object ToType(Type conversionType, IFormatProvider? provider)
    {
        if (conversionType.Equals(typeof(string))) return ToString(provider);
        if (conversionType.Equals(typeof(bool))) return ToBoolean(provider);
        if (conversionType.Equals(typeof(sbyte))) return ToSByte(provider);
        if (conversionType.Equals(typeof(short))) return ToInt16(provider);
        if (conversionType.Equals(typeof(int))) return ToInt32(provider);
        if (conversionType.Equals(typeof(long))) return ToInt64(provider);
        if (conversionType.Equals(typeof(byte))) return ToByte(provider);
        if (conversionType.Equals(typeof(ushort))) return ToUInt16(provider);
        if (conversionType.Equals(typeof(uint))) return ToUInt32(provider);
        if (conversionType.Equals(typeof(ulong))) return ToUInt64(provider);
        if (conversionType.Equals(typeof(float))) return ToSingle(provider);
        if (conversionType.Equals(typeof(double))) return ToDouble(provider);
        if (conversionType.Equals(typeof(decimal))) return ToDecimal(provider);

        throw new NotImplementedException(conversionType.FullName);
    }

    public ushort ToUInt16(IFormatProvider? provider) => ushort.Parse(_value);

    public uint ToUInt32(IFormatProvider? provider) => uint.Parse(_value);

    public ulong ToUInt64(IFormatProvider? provider) => UInt64.Parse(_value);

    public override string ToString() => _value;
}