using System.Text;
using HexServer.Core.Network;
using HexServer.Protocol.DataWrapper;
using HexServer.Protocol.HConnect;
using Xunit;

namespace HexServer.Protocol.Tests;

public sealed class HcpServiceMessageTests
{
    [Fact]
    public void DecodesGameSessionMethodFromDataType()
    {
        var body = DataWrapperCodec.Encode(
            2,
            3005,
            Encoding.UTF8.GetBytes(""),
            0,
            Guid.Empty);

        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["target"] = "ServiceGameSession",
            ["instance"] = "Shared",
            ["reqid"] = 2,
            ["c"] = 0,
            ["conh"] = 0,
            ["sid"] = 10001L
        });

        var message = new HcpMessage(HcpHeaderCodec.Decode(header), body);

        Assert.True(HcpServiceMessage.TryDecode(message, out var request));
        Assert.Equal(246, request.ServiceId);
        Assert.Equal(3005, request.MethodId);
        Assert.Equal(2, request.RequestId);
        Assert.Equal((ulong)10001, request.SessionId);
    }
}
