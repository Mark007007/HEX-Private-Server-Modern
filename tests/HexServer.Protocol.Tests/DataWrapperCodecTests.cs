using System.Text;
using HexServer.Protocol.DataWrapper;
using Xunit;

namespace HexServer.Protocol.Tests;

public sealed class DataWrapperCodecTests
{
    [Fact]
    public void EncodeMatchesKnownGolden()
    {
        var bytes = DataWrapperCodec.Encode(
            1,
            2127,
            Encoding.UTF8.GetBytes("test body payload 42"),
            0,
            Guid.Empty);

        var expectedBase64 =
            "OzA7MDs1O1JlcXVlc3RJZDsxOzE7MDswMTAwMDAwMDAwMDAwMDAwO0RhdGFUeXBlOzI7MjswOzRmMDgwMDAwO0J5dGVzOzM7MzswOwAAABR0ZXN0IGJvZHkgcGF5bG9hZCA0MlJlcXVlc3RIYW5kbGVyU2Vzc2lvbklkOzQ7NDswOzM2OzAwMDAwMDAwLTAwMDAwLTAwMDAwLTAwMDAwMDAwMDAwMENvbXA7NTs1OzA7MDA7R2FtZS5TaGFyZWQuTmV0d29yay5EYXRhV3JhcHBlcjtTeXN0ZW0uSW50NjQ7U3lzdGVtLkludDMyO1N5c3RlbS5CeXRlW107U3lzdGVtLkd1aWQ7U3lzdGVtLkJ5dGUKMTgzOzMzOzI0OzM2OzY5OzE0";

        Assert.Equal(
            Convert.FromBase64String(expectedBase64),
            bytes);
    }

    [Fact]
    public void DecodeGoldenRoundTripsFields()
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
}
