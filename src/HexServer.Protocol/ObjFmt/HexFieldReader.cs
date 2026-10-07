using System.Buffers.Binary;
using System.Text;

namespace HexServer.Protocol.ObjFmt;

public static class HexFieldReader
{
    public static int ReadInt32(ObjFmtField field)
        => checked((int)ReadSignedHex(field.Payload, 4));

    public static uint ReadUInt32(ObjFmtField field)
        => checked((uint)ReadUnsignedHex(field.Payload, 4));

    public static long ReadInt64(ObjFmtField field)
        => ReadSignedHex(field.Payload, 8);

    public static ulong ReadUInt64(ObjFmtField field)
        => ReadUnsignedHex(field.Payload, 8);

    public static byte ReadByte(ObjFmtField field)
        => checked((byte)ReadUnsignedHex(field.Payload, 1));

    public static bool ReadBool(ObjFmtField field)
    {
        var text = Encoding.UTF8.GetString(field.Payload);
        return text switch
        {
            "1" => true,
            "0" => false,
            _ => throw new InvalidDataException($"Invalid HEX bool: {text}")
        };
    }

    public static string ReadString(ObjFmtField field)
        => ReadUtf8LengthPrefixed(field.Payload);

    public static Guid ReadGuid(ObjFmtField field)
    {
        var value = ReadString(field);
        if (!Guid.TryParse(value, out var guid))
            throw new InvalidDataException($"Invalid HEX Guid: {value}");
        return guid;
    }

    public static byte[] ReadBytes(ObjFmtField field)
    {
        if (field.Payload.Length < 4)
            throw new InvalidDataException("HEX byte[] field is shorter than its length prefix.");

        var length = BinaryPrimitives.ReadUInt32BigEndian(field.Payload.AsSpan(0, 4));
        if (length > int.MaxValue || 4L + length > field.Payload.Length)
            throw new InvalidDataException("HEX byte[] length exceeds its field boundary.");

        return field.Payload.AsSpan(4, checked((int)length)).ToArray();
    }

    public static ulong ReadUid64(
        ObjFmtField field,
        IReadOnlyList<long> sizes)
    {
        var nested = ObjFmtDocument.ReadNestedFields(field, sizes);
        var uid = nested.FirstOrDefault(x => x.Name == "m_UID64")
                  ?? throw new InvalidDataException("UID.m_UID64 field is missing.");
        return ReadUInt64(uid);
    }

    public static Guid ReadResourceGuid(
        ObjFmtField field,
        IReadOnlyList<long> sizes)
    {
        var nested = ObjFmtDocument.ReadNestedFields(field, sizes);
        var guid = nested.FirstOrDefault(x =>
            x.Name is "guid" or "m_Guid")
                   ?? throw new InvalidDataException("ResourceId GUID field is missing.");
        return ReadGuid(guid);
    }

    public static int ReadEnumValue(
        ObjFmtField field,
        IReadOnlyList<long> sizes)
    {
        var nested = ObjFmtDocument.ReadNestedFields(field, sizes);
        var value = nested.FirstOrDefault(x => x.Name == "value__")
                    ?? throw new InvalidDataException("Enum value__ field is missing.");
        return ReadInt32(value);
    }

    private static long ReadSignedHex(byte[] payload, int expectedBytes)
    {
        var token = ReadToken(payload);
        var bytes = Convert.FromHexString(token);
        if (bytes.Length != expectedBytes)
            throw new InvalidDataException(
                $"Expected {expectedBytes} byte(s) of primitive HEX data.");

        return expectedBytes switch
        {
            1 => unchecked((sbyte)bytes[0]),
            4 => BitConverter.ToInt32(bytes, 0),
            8 => BitConverter.ToInt64(bytes, 0),
            _ => throw new NotSupportedException()
        };
    }

    private static ulong ReadUnsignedHex(byte[] payload, int expectedBytes)
    {
        var token = ReadToken(payload);
        var bytes = Convert.FromHexString(token);
        if (bytes.Length != expectedBytes)
            throw new InvalidDataException(
                $"Expected {expectedBytes} byte(s) of primitive HEX data.");

        return expectedBytes switch
        {
            1 => bytes[0],
            4 => BitConverter.ToUInt32(bytes, 0),
            8 => BitConverter.ToUInt64(bytes, 0),
            _ => throw new NotSupportedException()
        };
    }

    private static string ReadUtf8LengthPrefixed(byte[] payload)
    {
        var separator = Array.IndexOf(payload, (byte)';');
        if (separator <= 0)
            throw new InvalidDataException("HEX UTF-8 field length prefix is missing.");

        var prefix = Encoding.UTF8.GetString(payload, 0, separator);
        if (!int.TryParse(
                prefix,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var length) ||
            length < 0 ||
            separator + 1L + length > payload.Length)
        {
            throw new InvalidDataException("Invalid HEX UTF-8 field length.");
        }

        return Encoding.UTF8.GetString(payload, separator + 1, length);
    }

    private static string ReadToken(byte[] payload)
    {
        var separator = Array.IndexOf(payload, (byte)';');
        var length = separator >= 0 ? separator : payload.Length;

        return Encoding.UTF8.GetString(payload, 0, length);
    }
}
