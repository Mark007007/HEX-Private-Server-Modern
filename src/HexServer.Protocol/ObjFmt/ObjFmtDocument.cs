using System.Text;

namespace HexServer.Protocol.ObjFmt;

public sealed class ObjFmtDocument
{
    private ObjFmtDocument(
        byte[] data,
        long rootSize,
        IReadOnlyList<string> types,
        IReadOnlyList<long> sizes,
        IReadOnlyList<ObjFmtField> fields)
    {
        Data = data;
        RootSize = rootSize;
        Types = types;
        Sizes = sizes;
        Fields = fields;
    }

    public byte[] Data { get; }
    public long RootSize { get; }
    public IReadOnlyList<string> Types { get; }
    public IReadOnlyList<long> Sizes { get; }
    public IReadOnlyList<ObjFmtField> Fields { get; }

    public static ObjFmtDocument Parse(ReadOnlySpan<byte> data)
    {
        var bytes = data.ToArray();

        // The final LF separates the type table from the size table. Object
        // payloads are allowed to contain arbitrary bytes, so parse backwards.
        var lf = Array.LastIndexOf(bytes, (byte)'\n');
        if (lf < 0)
            throw new InvalidDataException("ObjFmt size-table separator not found.");

        var sizeText = Encoding.UTF8.GetString(bytes, lf + 1, bytes.Length - lf - 1);
        var sizes = ParseNumbers(sizeText, "size table");
        if (sizes.Count == 0)
            throw new InvalidDataException("ObjFmt size table is empty.");

        var rootSize = sizes[0];
        if (rootSize < 4 || rootSize > lf)
            throw new InvalidDataException(
                $"Invalid ObjFmt root size {rootSize} for payload boundary {lf}.");

        var typeBytesLength = checked(lf - (int)rootSize);
        var typeText = Encoding.UTF8.GetString(bytes, (int)rootSize, typeBytesLength);
        var types = typeText.Length == 0
            ? Array.Empty<string>()
            : typeText.Split(';', StringSplitOptions.None);

        var cursor = new Cursor(bytes, 0, checked((int)rootSize));
        var rootName = cursor.ReadToken();
        if (rootName.Length != 0)
            throw new InvalidDataException("ObjFmt root name must be empty.");

        _ = cursor.ReadIntToken("root size index");
        var rootTypeIndex = cursor.ReadIntToken("root type index");
        var rootPropertyCount = cursor.ReadIntToken("root property count");

        if (rootTypeIndex < 0 || rootTypeIndex >= types.Length)
            throw new InvalidDataException("ObjFmt root type index is outside type table.");

        var fields = ReadFields(
            bytes,
            cursor,
            checked((int)rootSize),
            sizes,
            rootPropertyCount);

        return new ObjFmtDocument(bytes, rootSize, types, sizes, fields);
    }

    public static IReadOnlyList<ObjFmtField> ReadNestedFields(
        ObjFmtField field,
        IReadOnlyList<long> sizes)
    {
        var cursor = new Cursor(field.Payload, 0, field.Payload.Length);
        return ReadFields(field.Payload, cursor, field.Payload.Length, sizes, field.PropertyCount);
    }

    public static IReadOnlyList<ObjFmtElement> ReadListElements(
        ObjFmtField field,
        IReadOnlyList<long> sizes)
    {
        var cursor = new Cursor(field.Payload, 0, field.Payload.Length);
        var count = cursor.ReadIntToken("collection count");
        if (count < 0)
            throw new InvalidDataException("Negative ObjFmt collection count.");

        var result = new List<ObjFmtElement>(count);
        for (var i = 0; i < count; i++)
        {
            var start = cursor.Position;
            var itemIndex = cursor.ReadIntToken("collection item index");
            var sizeIndex = cursor.ReadIntToken("collection item size index");
            var typeIndex = cursor.ReadIntToken("collection item type index");
            var propertyCount = cursor.ReadIntToken("collection item property count");

            ValidateSizeIndex(sizes, sizeIndex);
            var end = checked(start + (int)sizes[sizeIndex]);
            if (end > cursor.End)
                throw new InvalidDataException("ObjFmt collection item exceeds field boundary.");

            result.Add(new ObjFmtElement(
                itemIndex,
                sizeIndex,
                typeIndex,
                propertyCount,
                field.Payload.AsMemory(cursor.Position, end - cursor.Position).ToArray()));

            cursor.Position = end;
        }

        return result;
    }

    private static IReadOnlyList<ObjFmtField> ReadFields(
        byte[] data,
        Cursor cursor,
        int boundary,
        IReadOnlyList<long> sizes,
        int propertyCount)
    {
        if (propertyCount < 0 || propertyCount > 10000)
            throw new InvalidDataException($"Invalid ObjFmt property count: {propertyCount}.");

        var fields = new List<ObjFmtField>(propertyCount);

        for (var i = 0; i < propertyCount; i++)
        {
            var fieldStart = cursor.Position;
            var name = cursor.ReadToken();
            var sizeIndex = cursor.ReadIntToken("field size index");
            var typeIndex = cursor.ReadIntToken("field type index");
            var nestedCount = cursor.ReadIntToken("field property count");

            ValidateSizeIndex(sizes, sizeIndex);
            var declaredSize = sizes[sizeIndex];
            if (declaredSize < 0 || declaredSize > int.MaxValue)
                throw new InvalidDataException("Invalid ObjFmt field size.");

            var end = checked(fieldStart + (int)declaredSize);
            if (end < cursor.Position || end > boundary)
                throw new InvalidDataException(
                    $"ObjFmt field '{name}' exceeds its containing object.");

            var payloadStart = cursor.Position;
            var payload = data.AsMemory(payloadStart, end - payloadStart).ToArray();

            fields.Add(new ObjFmtField(
                name,
                sizeIndex,
                typeIndex,
                nestedCount,
                payload));

            cursor.Position = end;
        }

        if (cursor.Position != boundary)
            throw new InvalidDataException(
                $"ObjFmt object ended at {cursor.Position}, expected {boundary}.");

        return fields;
    }

    private static void ValidateSizeIndex(IReadOnlyList<long> sizes, int index)
    {
        if ((uint)index >= (uint)sizes.Count)
            throw new InvalidDataException($"ObjFmt size index {index} is outside the size table.");
    }

    private static List<long> ParseNumbers(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"ObjFmt {label} is empty.");

        var values = new List<long>();
        foreach (var token in text.Split(';', StringSplitOptions.None))
        {
            if (!long.TryParse(
                    token,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                throw new InvalidDataException($"Invalid ObjFmt {label} value: '{token}'.");
            }

            values.Add(value);
        }

        return values;
    }

    private sealed class Cursor
    {
        private readonly byte[] _data;

        public Cursor(byte[] data, int start, int end)
        {
            _data = data;
            Position = start;
            End = end;
        }

        public Cursor(ReadOnlyMemory<byte> data, int start, int end)
            : this(data.ToArray(), start, end)
        {
        }

        public int Position { get; set; }
        public int End { get; }

        public string ReadToken()
        {
            var start = Position;
            while (Position < End && _data[Position] != (byte)';')
                Position++;

            if (Position >= End)
                throw new EndOfStreamException("ObjFmt token missing separator.");

            var token = Encoding.UTF8.GetString(_data, start, Position - start);
            Position++;
            return token;
        }

        public int ReadIntToken(string label)
        {
            var token = ReadToken();
            if (!int.TryParse(
                    token,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                throw new InvalidDataException($"Invalid ObjFmt {label}: '{token}'.");
            }

            return value;
        }
    }
}

public sealed record ObjFmtField(
    string Name,
    int SizeIndex,
    int TypeIndex,
    int PropertyCount,
    byte[] Payload);

public sealed record ObjFmtElement(
    int Index,
    int SizeIndex,
    int TypeIndex,
    int PropertyCount,
    byte[] Payload);
