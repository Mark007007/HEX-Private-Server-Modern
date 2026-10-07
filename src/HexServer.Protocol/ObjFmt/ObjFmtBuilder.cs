using System.Buffers.Binary;
using System.Text;

namespace HexServer.Protocol.ObjFmt;

/// <summary>
/// Behavioural implementation of the reconstructed HEX CUSTOM ObjFmt writer.
/// The wire layout is based on observed client IL and byte-for-byte fixtures.
/// </summary>
public sealed class ObjFmtBuilder
{
    private readonly MemoryStream _body = new();
    private readonly List<long> _sizes = new() { 0 };
    private readonly List<string> _types = new();
    private readonly int _rootTypeIndex;

    public ObjFmtBuilder(string rootType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootType);
        _rootTypeIndex = AddType(rootType);
    }

    public IReadOnlyList<string> Types => _types;
    public IReadOnlyList<long> Sizes => _sizes;

    public void FieldInt(string name, int value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Int32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldUInt(string name, uint value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.UInt32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldLong(string name, long value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Int64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldULong(string name, ulong value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.UInt64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldByte(string name, byte value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Byte", 0);
        WriteText(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        SetSize(index, start);
    }

    public void FieldBool(string name, bool value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Boolean", 0);
        WriteText(value ? "1" : "0");
        SetSize(index, start);
    }

    public void FieldString(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.String", 0);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _body.Write(bytes);
        SetSize(index, start);
    }

    public void FieldGuid(string name, Guid value) => FieldGuid(name, value.ToString());

    public void FieldGuid(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Guid", 0);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _body.Write(bytes);
        SetSize(index, start);
    }

    public void FieldDateTime(string name, string invariantText)
    {
        ArgumentNullException.ThrowIfNull(invariantText);
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.DateTime", 0);
        var bytes = Encoding.UTF8.GetBytes(invariantText);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _body.Write(bytes);
        SetSize(index, start);
    }

    public void FieldBytes(string name, ReadOnlySpan<byte> data)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Byte[]", 0);

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        _body.Write(length);
        _body.Write(data);

        SetSize(index, start);
    }

    public void FieldEnum(string name, string enumTypeName, int value)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, enumTypeName, 1);

        var subStart = _body.Position;
        var subIndex = PushSize();
        WriteFieldHeader("value__", subIndex, "System.Int32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public void FieldUid(string name, ulong uid64)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "Game.Shared.UID", 1);

        var subStart = _body.Position;
        var subIndex = PushSize();
        WriteFieldHeader("m_UID64", subIndex, "System.UInt64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(uid64)));
        Separator();
        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public void FieldResourceId(
        string name,
        string guidText,
        bool memberNameIsGuid = true)
    {
        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "Game.Shared.ResourceId", 1);

        var subStart = _body.Position;
        var subIndex = PushSize();
        WriteFieldHeader(
            memberNameIsGuid ? "guid" : "m_Guid",
            subIndex,
            "System.Guid",
            0);

        var bytes = Encoding.UTF8.GetBytes(guidText);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _body.Write(bytes);

        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public ListScope BeginList(string name, string listTypeName, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var start = _body.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, listTypeName, 0);
        WriteText(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();

        return new ListScope(this, index, start);
    }

    public void ListItemUInt64(int itemIndex, ulong value)
    {
        var start = _body.Position;
        var index = PushSize();

        WriteText(itemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        WriteText(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        WriteText(AddType("System.UInt64").ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        WriteText("0");
        Separator();
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();

        SetSize(index, start);
    }

    public byte[] Finish(int rootPropertyCount)
    {
        if (rootPropertyCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rootPropertyCount));

        var body = _body.ToArray();
        var rootHeader = Encoding.UTF8.GetBytes(
            $";0;{_rootTypeIndex};{rootPropertyCount};");

        _sizes[0] = rootHeader.Length + body.Length;

        using var output = new MemoryStream();
        output.Write(rootHeader);
        output.Write(body);

        WriteText(output, string.Join(";", _types));
        output.WriteByte((byte)'\n');
        WriteText(output, string.Join(";", _sizes));

        return output.ToArray();
    }

    private int AddType(string typeName)
    {
        var index = _types.IndexOf(typeName);
        if (index >= 0)
            return index;

        _types.Add(typeName);
        return _types.Count - 1;
    }

    private int PushSize()
    {
        _sizes.Add(0);
        return _sizes.Count - 1;
    }

    private void SetSize(int index, long start)
        => _sizes[index] = _body.Position - start;

    private void WriteFieldHeader(
        string name,
        int sizeIndex,
        string typeName,
        int propertyCount)
    {
        WriteText(name);
        Separator();
        WriteText(sizeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        WriteText(AddType(typeName).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        WriteText(propertyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
    }

    private void WriteText(string text) => WriteText(_body, text);

    private static void WriteText(Stream stream, string text)
        => stream.Write(Encoding.UTF8.GetBytes(text));

    private void Separator() => _body.WriteByte((byte)';');

    public readonly struct ListScope
    {
        private readonly ObjFmtBuilder _owner;
        private readonly int _sizeIndex;
        private readonly long _start;

        internal ListScope(ObjFmtBuilder owner, int sizeIndex, long start)
        {
            _owner = owner;
            _sizeIndex = sizeIndex;
            _start = start;
        }

        public void Finish() => _owner.SetSize(_sizeIndex, _start);
    }
}
