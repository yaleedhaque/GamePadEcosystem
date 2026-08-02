using System.Buffers.Binary;

namespace GamePadEcosystem.Server.Core;

/// <summary>
/// Shared protocol constants and packet definitions.
/// Binary wire format for ultra-low-latency UDP controller input.
/// Must match the Android client exactly.
/// </summary>
public static class Protocol
{
    public const int UdpPort = 9876;
    public const int TcpPort = 9877;
    public const int DiscoveryPort = 9878;
    public const int MaxPlayers = 8;
    public const int DisconnectionTimeoutMs = 2000;
    public const int PacketMagic = 0x47504144; // "GPAD" in ASCII

    public static readonly byte[] DiscoveryMagic = "GPAD_DISCOVER_V1"u8.ToArray();

    public enum PacketType : byte
    {
        Input     = 0x01,
        Heartbeat = 0x02,
        GyroData  = 0x03,
        Config    = 0x04,
    }

    [Flags]
    public enum ButtonFlag : uint
    {
        None         = 0,
        A            = 1 << 0,
        B            = 1 << 1,
        X            = 1 << 2,
        Y            = 1 << 3,
        LeftBumper   = 1 << 4,
        RightBumper  = 1 << 5,
        Back         = 1 << 6,
        Start        = 1 << 7,
        LeftStick    = 1 << 8,
        RightStick   = 1 << 9,
        DPadUp       = 1 << 10,
        DPadDown     = 1 << 11,
        DPadLeft     = 1 << 12,
        DPadRight    = 1 << 13,
        Guide        = 1 << 14,
    }
}

/// <summary>
/// Compact binary input packet — 34 bytes total.
/// Designed for sub-5ms UDP transmission over local WiFi hotspot.
/// 
/// Layout:
///   [0..3]   Magic (int32)       — validates packet authenticity
///   [4]      PacketType (byte)   — input, heartbeat, gyro, config
///   [5]      DeviceId (byte)     — assigned by server on connection
///   [6..7]   Sequence (uint16)   — monotonic packet counter
///   [8..11]  Buttons (uint32)    — bitmask of all digital inputs
///   [12..13] LeftX (int16)       — left stick horizontal
///   [14..15] LeftY (int16)       — left stick vertical
///   [16..17] RightX (int16)      — right stick horizontal
///   [18..19] RightY (int16)      — right stick vertical
///   [20]     LeftTrigger (byte)  — left analog trigger 0-255
///   [21]     RightTrigger (byte) — right analog trigger 0-255
///   [22..25] GyroX (float32)     — gyroscope X (rad/s)
///   [26..29] GyroY (float32)     — gyroscope Y (rad/s)
///   [30..33] GyroZ (float32)     — gyroscope Z (rad/s)
/// </summary>
public struct InputPacket
{
    public int Magic;
    public byte PacketType;
    public byte DeviceId;
    public ushort Sequence;
    public uint Buttons;
    public short LeftX;
    public short LeftY;
    public short RightX;
    public short RightY;
    public byte LeftTrigger;
    public byte RightTrigger;
    public float GyroX;
    public float GyroY;
    public float GyroZ;

    public const int Size = 34;

    public byte[] Serialize()
    {
        var buf = new byte[Size];
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0), Magic);
        buf[4] = PacketType;
        buf[5] = DeviceId;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6), Sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8), Buttons);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(12), LeftX);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(14), LeftY);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(16), RightX);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(18), RightY);
        buf[20] = LeftTrigger;
        buf[21] = RightTrigger;
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(22), GyroX);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(26), GyroY);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(30), GyroZ);
        return buf;
    }

    public static InputPacket Deserialize(byte[] data)
    {
        if (data.Length < Size)
            throw new ArgumentException($"Packet too small: {data.Length} < {Size}");

        return new InputPacket
        {
            Magic         = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0)),
            PacketType    = data[4],
            DeviceId      = data[5],
            Sequence      = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)),
            Buttons       = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8)),
            LeftX         = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(12)),
            LeftY         = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(14)),
            RightX        = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(16)),
            RightY        = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(18)),
            LeftTrigger   = data[20],
            RightTrigger  = data[21],
            GyroX         = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(22)),
            GyroY         = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(26)),
            GyroZ         = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(30)),
        };
    }

    public bool IsValid() => Magic == Protocol.PacketMagic;
}

/// <summary>
/// Server-to-client assignment packet sent after discovery.
/// Communicates the assigned player slot and server identity.
/// </summary>
public struct AssignmentPacket
{
    public byte PlayerSlot;
    public string ServerName;

    public byte[] Serialize()
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(ServerName);
        var buf = new byte[2 + nameBytes.Length];
        buf[0] = 0xAA; // assignment magic
        buf[1] = PlayerSlot;
        Buffer.BlockCopy(nameBytes, 0, buf, 2, nameBytes.Length);
        return buf;
    }
}
