package com.gamepad.controller.network

import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Shared protocol constants — must match the Windows server exactly.
 */
object Protocol {
    const val UDP_PORT = 9876
    const val TCP_PORT = 9877
    const val DISCOVERY_PORT = 9878
    const val MAX_PLAYERS = 8
    const val DISCONNECTION_TIMEOUT_MS = 2000L
    const val PACKET_MAGIC = 0x47504144 // "GPAD"

    val DISCOVERY_MAGIC = "GPAD_DISCOVER_V1".toByteArray()

    object PacketType {
        const val INPUT: Byte = 0x01
        const val HEARTBEAT: Byte = 0x02
        const val GYRO: Byte = 0x03
        const val CONFIG: Byte = 0x04
    }

    object ButtonFlag {
        val A            = 1u
        val B            = 1u shl 1
        val X            = 1u shl 2
        val Y            = 1u shl 3
        val LEFT_BUMPER  = 1u shl 4
        val RIGHT_BUMPER = 1u shl 5
        val BACK         = 1u shl 6
        val START        = 1u shl 7
        val LEFT_STICK   = 1u shl 8
        val RIGHT_STICK  = 1u shl 9
        val DPAD_UP      = 1u shl 10
        val DPAD_DOWN    = 1u shl 11
        val DPAD_LEFT    = 1u shl 12
        val DPAD_RIGHT   = 1u shl 13
        val GUIDE        = 1u shl 14
    }
}

/**
 * Compact 34-byte binary input packet for sub-5ms UDP transmission.
 */
data class InputPacket(
    var packetType: Byte = Protocol.PacketType.INPUT,
    var deviceId: Byte = 0,
    var sequence: UShort = 0u,
    var buttons: UInt = 0u,
    var leftX: Short = 0,
    var leftY: Short = 0,
    var rightX: Short = 0,
    var rightY: Short = 0,
    var leftTrigger: Byte = 0,
    var rightTrigger: Byte = 0,
    var gyroX: Float = 0f,
    var gyroY: Float = 0f,
    var gyroZ: Float = 0f
) {
    fun serialize(): ByteArray {
        val buf = ByteBuffer.allocate(SIZE).order(ByteOrder.LITTLE_ENDIAN)
        buf.putInt(Protocol.PACKET_MAGIC)
        buf.put(packetType)
        buf.put(deviceId)
        buf.putShort(sequence.toShort())
        buf.putInt(buttons.toInt())
        buf.putShort(leftX)
        buf.putShort(leftY)
        buf.putShort(rightX)
        buf.putShort(rightY)
        buf.put(leftTrigger)
        buf.put(rightTrigger)
        buf.putFloat(gyroX)
        buf.putFloat(gyroY)
        buf.putFloat(gyroZ)
        return buf.array()
    }

    companion object {
        const val SIZE = 34

        fun deserialize(data: ByteArray): InputPacket? {
            if (data.size < SIZE) return null
            val buf = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
            if (buf.int != Protocol.PACKET_MAGIC) return null
            return InputPacket(
                packetType = buf.get(),
                deviceId = buf.get(),
                sequence = buf.short.toUShort(),
                buttons = buf.int.toUInt(),
                leftX = buf.short,
                leftY = buf.short,
                rightX = buf.short,
                rightY = buf.short,
                leftTrigger = buf.get(),
                rightTrigger = buf.get(),
                gyroX = buf.float,
                gyroY = buf.float,
                gyroZ = buf.float
            )
        }
    }
}
