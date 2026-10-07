using System.Text;
using HexServer.Core.Network;
using HexServer.Protocol.DataWrapper;
using HexServer.Protocol.HConnect;
using HexServer.Protocol.ObjFmt;

var tests = new (string Name, Action Run)[]
{
    ("HCP round-trip", TestHcpRoundTrip),
    ("HCP bad magic", TestHcpBadMagic),
    ("HCP partial frame", TestHcpPartialFrame),
    ("HEX numeric encoding", TestHexNumbers),
    ("DataWrapper exact golden", TestDataWrapperGolden),
    ("DataWrapper round-trip", TestDataWrapperRoundTrip),
    ("DataWrapper gzip", TestDataWrapperGzip),
    ("ServiceLoadBalancer decode", TestServiceDecode),
    ("ObjFmt ResourceId", TestResourceId)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Tests: {tests.Length}; Passed: {tests.Length - failures.Count}; Failed: {failures.Count}");

if (failures.Count != 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

return 0;

static void TestHcpRoundTrip()
{
    var frame = new HcpFrame(
        Encoding.UTF8.GetBytes("{\"target\":\"newsession\"}"),
        new byte[] { 1, 2, 0xA5, 0xFF });

    var wire = HcpCodec.Encode(frame);

    Check(
        Encoding.ASCII.GetString(wire, 0, 5) == "~HCP~",
        "magic");

    Check(
        HcpCodec.TryDecode(
            new System.Buffers.ReadOnlySequence<byte>(wire),
            out var decoded,
            out _),
        "decode");

    Check(frame.Header.SequenceEqual(decoded.Header), "header");
    Check(frame.Body.SequenceEqual(decoded.Body), "body");
}

static void TestHcpBadMagic()
{
    var wire = Encoding.UTF8.GetBytes(
        "XXXXX" + "\0\0\0\x08\0\0\0\0\0\0\0\0");

    try
    {
        HcpCodec.TryDecode(
            new System.Buffers.ReadOnlySequence<byte>(wire),
            out _,
            out _);
    }
    catch (InvalidDataException)
    {
        return;
    }

    throw new Exception("bad magic was accepted");
}

static void TestHcpPartialFrame()
{
    var frame = new HcpFrame(
        Encoding.UTF8.GetBytes("{}"),
        new byte[] { 1, 2, 3 });

    var wire = HcpCodec.Encode(frame);

    Check(
        !HcpCodec.TryDecode(
            new System.Buffers.ReadOnlySequence<byte>(
                wire.AsMemory(0, wire.Length - 1)),
            out _,
            out _),
        "partial frame should wait");
}

static void TestHexNumbers()
{
    Check(HexNumberCodec.Encode(1) == "01000000", "int32");
    Check(HexNumberCodec.Encode(0x12345678) == "78563412", "int32 bytes");
    Check(HexNumberCodec.Encode((byte)0xAB) == "ab", "byte");
    Check(HexNumberCodec.Encode((sbyte)-1) == "ff", "sbyte");
    Check(HexNumberCodec.Encode(12.5m).Length == 32, "decimal");
}

static void TestDataWrapperGolden()
{
    var actual = DataWrapperCodec.Encode(
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
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
        payloadLength,
        20);

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

    Check(actual.SequenceEqual(expected.ToArray()), "golden bytes");
}

static void TestDataWrapperRoundTrip()
{
    var actual = DataWrapperCodec.Encode(
        42,
        3005,
        Encoding.UTF8.GetBytes("payload"),
        0,
        Guid.Empty);

    var decoded = DataWrapperCodec.Decode(actual);

    Check(decoded.RequestId == 42, "request id");
    Check(decoded.DataType == 3005, "data type");
    Check(decoded.Compression == 0, "compression");
    Check(decoded.RequestHandlerSessionId == Guid.Empty, "session guid");
    Check(
        Encoding.UTF8.GetString(decoded.Bytes) == "payload",
        "payload");
}

static void TestDataWrapperGzip()
{
    var original = Encoding.UTF8.GetBytes("repeatable HEX payload");
    var compressed = DataWrapperCodec.EncodePayload(original, 1);
    var restored = DataWrapperCodec.DecodePayload(compressed, 1);

    Check(original.SequenceEqual(restored), "gzip round-trip");
}

static void TestServiceDecode()
{
    var body = DataWrapperCodec.Encode(
        2,
        22015,
        Array.Empty<byte>(),
        0,
        Guid.Empty);

    var header = HcpHeaderCodec.Encode(
        new Dictionary<string, object?>
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

    Check(
        HcpServiceMessage.TryDecode(message, out var request),
        "service decode");

    Check(request.ServiceId == 254, "LoadBalancer service id");
    Check(request.DataType == 22015, "FindSession data type");
    Check(request.RequestId == 2, "request id");
    Check(request.SessionId == 10001UL, "session id");
    Check(request.ClientCounter == 7, "client counter");
}

static void TestResourceId()
{
    var builder = new ObjFmtBuilder("Test.Resource");
    builder.FieldResourceId(
        "TemplateID",
        "00000000-0000-0000-0000-000000000000");

    var text = Encoding.UTF8.GetString(builder.Finish(1));

    Check(text.Contains("TemplateID;"), "resource field");
    Check(text.Contains("m_Guid;"), "resource guid member");
    Check(text.Contains("System.Guid;"), "guid type");
}

static void WriteText(Stream stream, string value)
    => stream.Write(Encoding.UTF8.GetBytes(value));

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}
