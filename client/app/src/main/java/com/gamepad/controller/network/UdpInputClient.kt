package com.gamepad.controller.network

import android.content.Context
import android.util.Log
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

class UdpInputClient {

    companion object {
        private const val TAG = "UdpInputClient"
        private const val TRAFFIC_CLASS_LOW_DELAY = 0x10
    }

    private var socket: DatagramSocket? = null
    private var serverAddress: InetAddress? = null
    private var serverPort: Int = Protocol.UDP_PORT
    private val isRunning = AtomicBoolean(false)
    private val sequenceCounter = AtomicInteger(0)

    var onConnectionStateChanged: ((Boolean) -> Unit)? = null
    var onPlayerAssigned: ((Int, String) -> Unit)? = null
    var onPacketsSent: ((Int) -> Unit)? = null

    fun start(serverAddress: InetAddress, serverPort: Int = Protocol.UDP_PORT, context: Context? = null) {
        if (isRunning.get()) stop()
        this.serverAddress = serverAddress
        this.serverPort = serverPort

        try {
            socket = DatagramSocket().apply {
                reuseAddress = true
                trafficClass = TRAFFIC_CLASS_LOW_DELAY
                soTimeout = 3000
            }
            if (context != null) {
                NetworkBinder.bindSocket(context, socket!!)
            }
            isRunning.set(true)
            Log.d(TAG, "UDP client started → $serverAddress:$serverPort")

            // Listen for server assignment response
            startResponseListener()

            // Fire connected immediately (UDP is connectionless)
            onConnectionStateChanged?.invoke(true)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start UDP client", e)
        }
    }

    private fun startResponseListener() {
        Thread({
            val buf = ByteArray(256)
            while (isRunning.get()) {
                try {
                    val pkt = DatagramPacket(buf, buf.size)
                    socket?.receive(pkt)
                    val data = pkt.data.copyOf(pkt.length)

                    // Check for assignment packet (magic 0xAA)
                    if (data.size >= 2 && data[0] == 0xAA.toByte()) {
                        val playerSlot = data[1].toInt()
                        val serverName = if (data.size > 2) String(data, 2, data.size - 2) else "Server"
                        Log.d(TAG, "Received assignment: Player ${playerSlot + 1} from $serverName")
                        onPlayerAssigned?.invoke(playerSlot, serverName)
                    }
                } catch (e: java.net.SocketTimeoutException) {
                    // Normal — no packet within timeout, keep listening
                } catch (e: Exception) {
                    if (isRunning.get()) Log.e(TAG, "Response listener error", e)
                }
            }
        }, "UDP-Response-Listener").start()
    }

    fun stop() {
        isRunning.set(false)
        try { socket?.close() } catch (_: Exception) {}
        socket = null
        onConnectionStateChanged?.invoke(false)
        Log.d(TAG, "UDP client stopped")
    }

    fun sendInput(packet: InputPacket) {
        if (!isRunning.get()) return
        val addr = serverAddress ?: return
        val sock = socket ?: return

        packet.sequence = (sequenceCounter.getAndIncrement() and 0xFFFF).toUShort()

        try {
            val data = packet.serialize()
            sock.send(DatagramPacket(data, data.size, addr, serverPort))
            onPacketsSent?.invoke(1)
        } catch (e: Exception) {
            Log.e(TAG, "Send failed", e)
        }
    }

    fun sendHeartbeat() {
        if (!isRunning.get()) return
        val hb = InputPacket(
            packetType = Protocol.PacketType.HEARTBEAT,
            sequence = (sequenceCounter.getAndIncrement() and 0xFFFF).toUShort()
        )
        sendInput(hb)
    }

    fun isConnected(): Boolean = isRunning.get() && socket != null && serverAddress != null
}
