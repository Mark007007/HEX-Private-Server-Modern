namespace HexServer.Protocol.ObjFmt;

public static class HexNumberCodec
{
    public static string Encode(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            byte v => Convert.ToHexString(BitConverter.GetBytes(v)),
            sbyte v => Convert.ToHexString(BitConverter.GetBytes(v)),
            short v => Convert.ToHexString(BitConverter.GetBytes(v)),
            ushort v => Convert.ToHexString(BitConverter.GetBytes(v)),
            int v => Convert.ToHexString(BitConverter.GetBytes(v)),
            uint v => Convert.ToHexString(BitConverter.GetBytes(v)),
            long v => Convert.ToHexString(BitConverter.GetBytes(v)),
            ulong v => Convert.ToHexString(BitConverter.GetBytes(v)),
            float v => Convert.ToHexString(BitConverter.GetBytes(v)),
            double v => Convert.ToHexString(BitConverter.GetBytes(v)),
            decimal v => EncodeDecimal(v),
            _ => throw new ArgumentException($"Unsupported numeric type: {value.GetType().FullName}", nameof(value))
        };
    }

    private static string EncodeDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < bits.Length; i++)
            BitConverter.TryWriteBytes(bytes.Slice(i * 4, 4), bits[i]);
        return Convert.ToHexString(bytes);
    }
}
