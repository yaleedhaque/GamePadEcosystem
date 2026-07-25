# PROTOCOL REFERENCE — GamePadEcosystem v1.0
# ============================================
# This document defines the binary wire format shared between
# the Windows server and Android clients.
# Both implementations MUST use these exact byte offsets and types.
#
# Author:  GamePadEcosystem
# Version: 1.0.0
# License: MIT

## Ports
UDP_INPUT:      9876   # Controller input packets (34 bytes each)
UDP_DISCOVERY:  9878   # Auto-discovery broadcast / response
TCP_ASSIGN:     9877   # Reserved for future player assignment handshake

## Discovery Protocol (UDP broadcast)
Client sends:  "GPAD_DISCOVER_V1" + UTF8(deviceName)
Server replies: "GPAD_SERVER_V1|" + UTF8(hostName)

## Input Packet Layout (34 bytes, little-endian)
Offset  Type     Field           Description
------  ----     -----           -----------
[0..3]  int32    Magic           0x47504144 ("GPAD")
[4]     uint8    PacketType      1=Input, 2=Heartbeat, 3=Gyro, 4=Config
[5]     uint8    DeviceId        Assigned by server (0-7)
[6..7]  uint16   Sequence        Monotonic counter (wraps at 65535)
[8..11] uint32   Buttons         Bitmask — see Button Flags below
[12..13] int16   LeftStickX      -32768 to 32767
[14..15] int16   LeftStickY      -32768 to 32767
[16..17] int16   RightStickX     -32768 to 32767
[18..19] int16   RightStickY     -32768 to 32767
[20]    uint8    LeftTrigger     0-255
[21]    uint8    RightTrigger    0-255
[22..25] float32 GyroX           Gyroscope X axis (rad/s)
[26..29] float32 GyroY           Gyroscope Y axis (rad/s)
[30..33] float32 GyroZ           Gyroscope Z axis (rad/s)

## Button Flags (bitmask in Buttons field)
Bit 0  (0x0001)  A
Bit 1  (0x0002)  B
Bit 2  (0x0004)  X
Bit 3  (0x0008)  Y
Bit 4  (0x0010)  Left Bumper
Bit 5  (0x0020)  Right Bumper
Bit 6  (0x0040)  Back
Bit 7  (0x0080)  Start
Bit 8  (0x0100)  Left Stick Press
Bit 9  (0x0200)  Right Stick Press
Bit 10 (0x0400)  D-Pad Up
Bit 11 (0x0800)  D-Pad Down
Bit 12 (0x1000)  D-Pad Left
Bit 13 (0x2000)  D-Pad Right
Bit 14 (0x4000)  Guide / Home

## Disconnection Detection
If no packet received from a client for 2000ms:
  - Server zeros that controller's inputs (prevents stick drift)
  - Client is marked as disconnected in the HUD
  - Client can re-discover and reconnect at any time
