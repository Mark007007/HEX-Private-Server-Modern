using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using HexServer.Protocol.ObjFmt;

namespace HexServer.Protocol.DataWrapper;

public static class DataWrapperCodec
{
    private const string RootType = "Game.Shared.Network.DataWrapper";
    private const string ZeroGuid = "00000000-0000-0000-0000-000000000000";

    public static byte[] Encode(
        long requestId,
        int dataType,
        ReadOnlySpan<byte> payload,
        byte compression,
        Guid requestHandlerSessionId)
    {
        var builder = new ObjFmtBuilder(RootType);
        builder.FieldLong("RequestId", requestId);
        builder.FieldInt("DataType", dataType);
        builder.FieldBytes("Bytes", payload);
        builder.FieldGuid("RequestHandlerSessionId", requestHandlerSessionId);
        builder.FieldByte("Comp", compression);
        return builder.Finish(5);
    }

    public static DataWrapperEnvelope Decode(ReadOnlySpan<byte> input)
    {
        var cursor = new Cursor(input);

        cursor.Expect(";0;0;5;");

        cursor.ReadFieldHeader("RequestId", out _, out _);
        var requestId = cursor.ReadInt64Hex();

        cursor.ReadFieldHeader("DataType", out _, out _);
        var dataType = cursor.ReadInt32Hex();

        cursor.ReadFieldHeader("Bytes", out _, out _);
        var payload = cursor.ReadLengthPrefixedBytes();

        cursor.ReadFieldHeader("RequestHandlerSessionId", out _, out _);
        var guidText = cursor.ReadUtf8LengthPrefixedString();
        if (!Guid.TryParse(guidText, out var requestHandlerSessionId))
            throw new InvalidDataException($"Invalid RequestHandlerSessionId: {guidText}");

        cursor.ReadFieldHeader("Comp", out _, out _);
        var compression = cursor.ReadByteHex();

        // The ObjFmt type table follows a newline after the root object body.
        if (!cursor.FindByte((byte)'\n'))
            throw new InvalidDataException("DataWrapper type table terminator was not found.");

        return new DataWrapperEnvelope(
            requestId,
            dataType,
            payload,
            requestHandlerSessionId,
            compression);
    }

    public static byte[] EncodePayload(ReadOnlySpan<byte> payload, byte compression)
        => compression switch
        {
            0 => payload.ToArray(),
            1 => Gzip(payload),
            _ => throw new NotSupportedException($"HEX compression mode {compression} is not implemented.")
        };

    public static byte[] DecodePayload(ReadOnlySpan<byte> payload, byte compression)
        => compression switch
        {
            0 => payload.ToArray(),
            1 => Gunzip(payload),
            _ => throw new NotSupportedException($"HEX compression mode {compression} is not implemented.")
        };

    private static byte[] Gzip(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(data);
        return output.ToArray();
    }

    private static byte[] Gunzip(ReadOnlySpan<byte> data)
    {
        using var input = new MemoryStream(data.ToArray());
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private ref struct Cursor
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        public Cursor(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public void Expect(string ascii)
        {
            var bytes = Encoding.ASCII.GetBytes(ascii);
            if (_offset + bytes.Length > _data.Length ||
                !_data.Slice(_offset, bytes.Length).SequenceEqual(bytes))
                throw new InvalidDataException($"Unexpected DataWrapper prefix at offset {_offset}.");
            _offset += bytes.Length;
        }

        public void ReadFieldHeader(
            string expectedName,
            out int sizeIndex,
            out int typeIndex)
        {
            var name = ReadToken();
            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Expected DataWrapper field {expectedName}, got {name}.");

            if (!int.TryParse(ReadToken(), out sizeIndex))
                throw new InvalidDataException("Invalid DataWrapper size index.");

            if (!int.TryParse(ReadToken(), out typeIndex))
                throw new InvalidDataException("Invalid DataWrapper type index.");

            if (!int.TryParse(ReadToken(), out _))
                throw new InvalidDataException("Invalid DataWrapper property count.");
        }

        public long ReadInt64Hex()
        {
            var token = ReadUntilSeparator();
            var bytes = Convert.FromHexString(token);
            if (bytes.Length != 8)
                throw new InvalidDataException("Expected 8-byte Int64 payload.");
            return BitConverter.ToInt64(bytes, 0);
        }

        public int ReadInt32Hex()
        {
            var token = ReadUntilSeparator();
            var bytes = Convert.FromHexString(token);
            if (bytes.Length != 4)
                throw new InvalidDataException("Expected 4-byte Int32 payload.");
            return BitConverter.ToInt32(bytes, 0);
        }

        public byte ReadByteHex()
        {
            var token = ReadUntilSeparator();
            var bytes = Convert.FromHexString(token);
            if (bytes.Length != 1)
                throw new InvalidDataException("Expected one-byte payload.");
            return bytes[0];
        }

        public byte[] ReadLengthPrefixedBytes()
        {
            Ensure(4);
            var length = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(_offset, 4));
            _offset += 4;
            if (length > int.MaxValue || _offset + (long)length > _data.Length)
                throw new InvalidDataException("Invalid DataWrapper byte-array length.");

            var result = _data.Slice(_offset, (int)length).ToArray();
            _offset += (int)length;
            return result;
        }

        public string ReadUtf8LengthPrefixedString()
        {
            var lengthToken = ReadToken();
            if (!int.TryParse(lengthToken, out var length) || length < 0)
                throw new InvalidDataException("Invalid UTF-8 length prefix.");

            Ensure(length);
            var value = Encoding.UTF8.GetString(_data.Slice(_offset, length));
            _offset += length;
            return value;
        }

        public bool FindByte(byte needle)
        {
            for (var i = _offset; i < _data.Length; i++)
            {
                if (_data[i] == needle)
                {
                    _offset = i + 1;
                    return true;
                }
            }
            return false;
        }

        private string ReadToken()
        {
            var start = _offset;
            while (_offset < _data.Length && _data[_offset] != (byte)';')
                _offset++;

            if (_offset >= _data.Length)
                throw new EndOfStreamException("Unexpected end of DataWrapper token.");

            var token = Encoding.UTF8.GetString(_data.Slice(start, _offset - start));
            _offset++;
            return token;
        }

        private string ReadUntilSeparator() => ReadToken();

        private void Ensure(int count)
        {
            if (count < 0 || _offset + (long)count > _data.Length)
                throw new EndOfStreamException("Unexpected end of DataWrapper payload.");
        }
    }
}
