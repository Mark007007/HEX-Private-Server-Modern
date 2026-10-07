using System.Buffers.Binary;
using System.Text;
using HexServer.Protocol.DataWrapper;
using Xunit;

namespace HexServer.Protocol.Tests;

public sealed class DataWrapperCodecTests
{
    [Fact]
    public void EncodeMatchesKnownGoldenByteForByte()
    {
        var bytes = DataWrapperCodec.Encode(
            1,
            2127,
            Encoding.UTF8.GetBytes("test body payload 42"),
            0,
            Guid.Empty);

        using var expected = new MemoryStream();

        WriteText(expected, ";0;0;5;");
        WriteText(expected, "RequestId;1;1;0;0100000000000000;");
        WriteText(expected, "DataType;2;2;0;4f080000;");
        WriteText(expected, "Bytes;3;3;0;");

        Span<byte> payloadLength = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(payloadLength, 20);
        expected.Write(payloadLength);
        expected.Write(Encoding.UTF8.GetBytes("test body payload 42"));

        WriteText(
            expected,
            "RequestHandlerSessionId;4;4;0;36;" +
            "00000000-0000-0000-0000-000000000000");

        WriteText(expected, "Comp;5;5;0;00;");

        WriteText(
            expected,
            "Game.Shared.Network.DataWrapper;" +
            "System.Int64;" +
            "System.Int32;" +
            "System.Byte[];" +
            "System.Guid;" +
            "System.Byte");
        expected.WriteByte((byte)'\n');
        WriteText(expected, "183;33;24;36;69;14");

        Assert.Equal(expected.ToArray(), bytes);
    }

    [Fact]
    public void DecodeRoundTripsFields()
    {
        var bytes = DataWrapperCodec.Encode(
            42,
            3005,
            Encoding.UTF8.GetBytes("payload"),
            0,
            Guid.Empty);

        var decoded = DataWrapperCodec.Decode(bytes);

        Assert.Equal(42, decoded.RequestId);
        Assert.Equal(3005, decoded.DataType);
        Assert.Equal(Guid.Empty, decoded.RequestHandlerSessionId);
        Assert.Equal((byte)0, decoded.Compression);
        Assert.Equal("payload", Encoding.UTF8.GetString(decoded.Bytes));
    }

    [Fact]
    public void GzipPayloadRoundTrips()
    {
        var original = Encoding.UTF8.GetBytes("repeatable HEX payload");
        var compressed = DataWrapperCodec.EncodePayload(original, 1);
        var restored = DataWrapperCodec.DecodePayload(compressed, 1);

        Assert.Equal(original, restored);
    }

    private static void WriteText(Stream stream, string value)
        => stream.Write(Encoding.UTF8.GetBytes(value));
}
