using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public static class HcpCodec
{
    private const int PrefixSize = 9;
    private const int HeaderPrefixSize = 4;
    private const int MaxHeaderBytes = 64 * 1024;
    private const int MaxBodyBytes = 16 * 1024 * 1024;

    public static byte[] Encode(HcpFrame frame)
    {
        Validate(frame.Header.Length, frame.Body.Length);
        var output = new byte[frame.TotalLength];
        HcpFrame.Magic.CopyTo(output.AsSpan(0, 5));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(5, 4), frame.ContentSize);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(9, 4), (uint)frame.Header.Length);
        frame.Header.CopyTo(output, 13);
        var bodySizeOffset = checked(13 + frame.Header.Length);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(bodySizeOffset, 4), (uint)frame.Body.Length);
        frame.Body.CopyTo(output, bodySizeOffset + 4);
        return output;
    }

    public static bool TryDecode(ReadOnlySequence<byte> buffer, out HcpFrame frame, out SequencePosition consumed)
    {
        frame = default;
        consumed = buffer.GetPosition(0);
        if (buffer.Length < PrefixSize)
            return false;

        Span<byte> prefix = stackalloc byte[9];
        buffer.Slice(0, 9).CopyTo(prefix);
        if (!prefix[..5].SequenceEqual(HcpFrame.Magic))
            throw new InvalidDataException("Invalid HCP magic.");

        var contentSize = BinaryPrimitives.ReadUInt32BigEndian(prefix[5..9]);
        if (contentSize < 8 || contentSize > 8u + MaxHeaderBytes + MaxBodyBytes)
            throw new InvalidDataException($"Invalid HCP content size: {contentSize}.");

        var totalLength = checked(9L + contentSize);
        if (buffer.Length < totalLength)
            return false;

        Span<byte> u32 = stackalloc byte[4];
        buffer.Slice(9, 4).CopyTo(u32);
        var headerLength = BinaryPrimitives.ReadUInt32BigEndian(u32);
        if (headerLength > MaxHeaderBytes)
            throw new InvalidDataException($"HCP header too large: {headerLength}.");

        var bodySizeOffset = checked(13L + headerLength);
        if (bodySizeOffset + HeaderPrefixSize > totalLength)
            throw new InvalidDataException("HCP header exceeds frame boundary.");

        buffer.Slice(bodySizeOffset, 4).CopyTo(u32);
        var bodyLength = BinaryPrimitives.ReadUInt32BigEndian(u32);
        if (bodyLength > MaxBodyBytes)
            throw new InvalidDataException($"HCP body too large: {bodyLength}.");

        var expectedContentSize = checked(8UL + headerLength + bodyLength);
        if (expectedContentSize != contentSize)
            throw new InvalidDataException($"HCP size mismatch: content={contentSize}, expected={expectedContentSize}.");

        frame = new HcpFrame(
            buffer.Slice(13, headerLength).ToArray(),
            buffer.Slice(bodySizeOffset + 4, bodyLength).ToArray());
        consumed = buffer.GetPosition(totalLength);
        return true;
    }

    public static string DecodeHeaderUtf8(ReadOnlySpan<byte> header) => Encoding.UTF8.GetString(header);

    private static void Validate(int headerLength, int bodyLength)
    {
        if ((uint)headerLength > MaxHeaderBytes)
            throw new ArgumentOutOfRangeException(nameof(headerLength));
        if ((uint)bodyLength > MaxBodyBytes)
            throw new ArgumentOutOfRangeException(nameof(bodyLength));
    }
}
