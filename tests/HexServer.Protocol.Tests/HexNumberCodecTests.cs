using HexServer.Protocol.ObjFmt;

namespace HexServer.Protocol.Tests;

public sealed class HexNumberCodecTests
{
    [Fact]
    public void Int32UsesLittleEndianPrimitiveBytesBeforeHex()
    {
        Assert.Equal("01000000", HexNumberCodec.Encode(1));
        Assert.Equal("78563412", HexNumberCodec.Encode(0x12345678));
    }

    [Fact]
    public void DecimalUsesFourInt32Parts()
    {
        var value = 12.5m;
        Assert.Equal(32, HexNumberCodec.Encode(value).Length);
    }
}
