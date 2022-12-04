namespace SDRInterface;

public enum SampleFormat
{
    CF64,
    CF32,
    CU32,
    CS16,
    CU16,
    CS12,
    CU12,
    CS8,
    CU8,
    CS4,
    CU4,
    F64,
    F32,
    S32,
    U32,
    S16,
    U16,
    S8,
    U8
}

public static class SampleFormatExt
{
    public static int FormatToSize(this SampleFormat format)
    {
        var size = 0;
        var isComplex = false;
        var formatStr = format.ToString();
        for (var i = 0; i < formatStr.Length; i++)
        {
            var ch = formatStr[i];
            if (ch == 'C') isComplex = true;
            if (char.IsDigit(ch)) size = size * 10 + (ch - '0');
        }

        if (isComplex) size *= 2;
        return size / 8;
    }
}