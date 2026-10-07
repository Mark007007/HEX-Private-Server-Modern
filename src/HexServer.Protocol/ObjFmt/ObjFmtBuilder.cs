using System.Buffers.Binary;
using System.Text;

namespace HexServer.Protocol.ObjFmt;

/// <summary>
/// Behavioural port of the HEX CUSTOM ObjFmt writer.
/// This class intentionally implements only primitive/struct/list patterns
/// already reconstructed from the original client/server code. More complex
/// generated contracts should be added only after golden-byte verification.
/// </summary>
public sealed class ObjFmtBuilder
{
    private readonly MemoryStream _buffer = new();
    private readonly List<long> _sizes = new();
    private readonly List<string> _types = new();
    private readonly long _rootPropsPosition;
    private readonly int _rootTypeIndex;

    public ObjFmtBuilder(string rootType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootType);

        _rootTypeIndex = AddType(rootType);
        WriteText("");
        Separator();
        WriteText("0");
        Separator();
        WriteText(_rootTypeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();

        _rootPropsPosition = _buffer.Position;
        WriteText("00");
        Separator();

        _sizes.Add(0);
    }

    public IReadOnlyList<string> Types => _types;
    public IReadOnlyList<long> Sizes => _sizes;

    public void SetRootPropertyCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var save = _buffer.Position;
        _buffer.Position = _rootPropsPosition;

        var text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (text.Length < 2)
            text = "0" + text;

        WriteText(text);
        Separator();
        _buffer.Position = save;
    }

    public void FieldInt(string name, int value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Int32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldUInt(string name, uint value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.UInt32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldLong(string name, long value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Int64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldULong(string name, ulong value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.UInt64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(index, start);
    }

    public void FieldByte(string name, byte value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Byte", 0);
        WriteText(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        SetSize(index, start);
    }

    public void FieldBool(string name, bool value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Boolean", 0);
        WriteText(value ? "1" : "0");
        SetSize(index, start);
    }

    public void FieldString(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.String", 0);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _buffer.Write(bytes);
        SetSize(index, start);
    }

    public void FieldGuid(string name, Guid value)
        => FieldGuid(name, value.ToString());

    public void FieldGuid(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Guid", 0);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _buffer.Write(bytes);
        SetSize(index, start);
    }

    public void FieldDateTime(string name, string invariantText)
    {
        ArgumentNullException.ThrowIfNull(invariantText);

        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.DateTime", 0);
        var bytes = Encoding.UTF8.GetBytes(invariantText);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _buffer.Write(bytes);
        SetSize(index, start);
    }

    public void FieldBytes(string name, ReadOnlySpan<byte> data)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "System.Byte[]", 0);

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        _buffer.Write(length);
        _buffer.Write(data);

        SetSize(index, start);
    }

    public void FieldEnum(string name, string enumTypeName, int value)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, enumTypeName, 1);

        var subStart = _buffer.Position;
        var subIndex = PushSize();
        WriteFieldHeader("value__", subIndex, "System.Int32", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(value)));
        Separator();
        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public void FieldUid(string name, ulong uid64)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "Game.Shared.UID", 1);

        var subStart = _buffer.Position;
        var subIndex = PushSize();
        WriteFieldHeader("m_UID64", subIndex, "System.UInt64", 0);
        WriteText(Convert.ToHexString(BitConverter.GetBytes(uid64)));
        Separator();
        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public void FieldResourceId(string name, string guidText)
    {
        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, "Game.Shared.ResourceId", 1);

        var subStart = _buffer.Position;
        var subIndex = PushSize();
        WriteFieldHeader("guid", subIndex, "System.Guid", 0);
        var bytes = Encoding.UTF8.GetBytes(guidText);
        WriteText(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();
        _buffer.Write(bytes);
        SetSize(subIndex, subStart);
        SetSize(index, start);
    }

    public ListScope BeginList(string name, string listTypeName, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var start = _buffer.Position;
        var index = PushSize();
        WriteFieldHeader(name, index, listTypeName, 0);
        WriteText(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Separator();

        return new ListScope(this, index, start);
    }

    public void ListItemUInt64(int itemIndex, ulong value)
    {
        var start = _buffer.Position;
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

    public byte[] Finish(int? rootPropertyCount = null)
    {
        if (rootPropertyCount.HasValue)
            SetRootPropertyCount(rootPropertyCount.Value);

        _sizes[0] = _buffer.Position;
        WriteText(string.Join(";", _types));
        _buffer.WriteByte((byte)'\n');
        WriteText(string.Join(";", _sizes));

        return _buffer.ToArray();
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
        => _sizes[index] = _buffer.Position - start;

    private void WriteFieldHeader(string name, int sizeIndex, string typeName, int propertyCount)
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

    private void WriteText(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _buffer.Write(bytes);
    }

    private void Separator() => _buffer.WriteByte((byte)';');

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
