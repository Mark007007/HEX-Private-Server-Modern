using System.Text;
using HexServer.Protocol.DataWrapper;
using HexServer.Protocol.HConnect;
using Xunit;

namespace HexServer.Protocol.Tests;

public sealed class HcpServiceMessageTests
{
    [Fact]
    public void DecodesLoadBalancerFindSessionDataType()
    {
        var body = DataWrapperCodec.Encode(
            2,
            22015,
            Encoding.UTF8.GetBytes(""),
            0,
            Guid.Empty);

        var header = HcpHeaderCodec.Encode(new Dictionary<string, object?>
        {
            ["target"] = "ServiceLoadBalancer",
            ["instance"] = "Shared",
            ["reqid"] = 2,
            ["c"] = 0,
            ["conh"] = 0,
            ["sid"] = 10001L,
            ["ccnt"] = 7L
        });

        var message = new HcpMessage(
            HcpHeaderCodec.Decode(header),
            body);

        Assert.True(
            HcpServiceMessage.TryDecode(
                message,
                out var request));

        Assert.Equal(254, request.ServiceId);
        Assert.Equal(22015, request.DataType);
        Assert.Equal(2, request.RequestId);
        Assert.Equal((ulong)10001, request.SessionId);
        Assert.Equal(7, request.ClientCounter);
    }
}
