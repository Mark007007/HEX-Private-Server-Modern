namespace HexServer.Protocol.ObjFmt;

public static class HexNumberCodec
{
    public static string Encode(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            byte v => ToHex(stackalloc byte[] { v }),
            sbyte v => ToHex(stackalloc byte[] { unchecked((byte)v) }),
            short v => ToHex(BitConverter.GetBytes(v)),
            ushort v => ToHex(BitConverter.GetBytes(v)),
            int v => ToHex(BitConverter.GetBytes(v)),
            uint v => ToHex(BitConverter.GetBytes(v)),
            long v => ToHex(BitConverter.GetBytes(v)),
            ulong v => ToHex(BitConverter.GetBytes(v)),
            float v => ToHex(BitConverter.GetBytes(v)),
            double v => ToHex(BitConverter.GetBytes(v)),
            decimal v => EncodeDecimal(v),
            _ => throw new ArgumentException(
                $"Unsupported numeric type: {value.GetType().FullName}",
                nameof(value))
        };
    }

    private static string EncodeDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        Span<byte> bytes = stackalloc byte[16];

        for (var i = 0; i < bits.Length; i++)
            BitConverter.TryWriteBytes(bytes.Slice(i * 4, 4), bits[i]);

        return ToHex(bytes);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}
