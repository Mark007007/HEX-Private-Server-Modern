namespace HexServer.Core.Network;

public readonly record struct HcpFrame(byte[] Header, byte[] Body)
{
    public const string MagicText = "~HCP~";
    public static ReadOnlySpan<byte> Magic => "~HCP~"u8;
    public uint ContentSize => checked((uint)(8 + Header.Length + Body.Length));
    public int TotalLength => checked(9 + (int)ContentSize);
}
