using System.Buffers;
using System.Text;
using HexServer.Core.Network;
using HexServer.Protocol.HConnect;

namespace HexServer.Protocol.Tests;

public sealed class HcpCodecTests
{
    [Fact]
    public void RoundTripPreservesHeaderAndBody()
    {
        var frame = new HcpFrame(
            Encoding.UTF8.GetBytes("{\"target\":\"newsession\"}"),
            new byte[] { 0x01, 0x02, 0xA5, 0xFF });

        var wire = HcpCodec.Encode(frame);

        Assert.Equal("~HCP~", Encoding.ASCII.GetString(wire, 0, 5));
        Assert.True(HcpCodec.TryDecode(new ReadOnlySequence<byte>(wire), out var decoded, out var consumed));
        Assert.Equal(wire.Length, consumed.GetIntegerOffset());
        Assert.Equal(frame.Header, decoded.Header);
        Assert.Equal(frame.Body, decoded.Body);
    }

    [Fact]
    public void RejectsBadMagic()
    {
        var wire = Encoding.UTF8.GetBytes("XXXXX" + "\0\0\0\x08\0\0\0\0\0\0\0\0");
        Assert.Throws<InvalidDataException>(() =>
            HcpCodec.TryDecode(new ReadOnlySequence<byte>(wire), out _, out _));
    }

    [Fact]
    public void WaitsForCompleteFrame()
    {
        var frame = new HcpFrame(Encoding.UTF8.GetBytes("{}"), new byte[] { 1, 2, 3 });
        var wire = HcpCodec.Encode(frame);

        var partial = new ReadOnlySequence<byte>(wire.AsMemory(0, wire.Length - 1));
        Assert.False(HcpCodec.TryDecode(partial, out _, out _));
    }
}

internal static class SequencePositionExtensions
{
    public static long GetIntegerOffset(this SequencePosition position)
    {
        return position.GetInteger();
    }
}
