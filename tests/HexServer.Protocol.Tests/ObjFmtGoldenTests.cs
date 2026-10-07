using System.Text;
using HexServer.Protocol.ObjFmt;
using Xunit;

namespace HexServer.Protocol.Tests;

public sealed class ObjFmtGoldenTests
{
    [Fact]
    public void SimpleIntFieldMatchesReconstructedWire()
    {
        var builder = new ObjFmtBuilder("Test.Basic");
        builder.FieldInt("Value", 1);

        var bytes = builder.Finish(1);

        Assert.Equal(
            ";0;0;01;Value;1;1;0;01000000;Test.Basic;System.Int32\n29;21",
            Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void ResourceIdUsesNestedGuidMember()
    {
        var builder = new ObjFmtBuilder("Test.Resource");
        builder.FieldResourceId("TemplateID", "00000000-0000-0000-0000-000000000000");

        var text = Encoding.UTF8.GetString(builder.Finish(1));
        Assert.Contains("TemplateID;", text);
        Assert.Contains("guid;", text);
        Assert.Contains("System.Guid;", text);
    }
}
