package com.gamepad.controller.network

import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.NetworkInterface
import java.util.concurrent.atomic.AtomicBoolean

class DiscoveryClient {

    companion object {
        private const val TAG = "DiscoveryClient"
        private val COMMON_SERVER_IPS = listOf(
            "192.168.137.1",  // Windows Mobile Hotspot default gateway
            "192.168.43.1",  // Android hotspot gateway
            "192.168.58.1",
            "192.168.1.1",
            "192.168.0.1",
            "10.0.0.1",
        )
    }

    private var socket: DatagramSocket? = null
    private val isDiscovering = AtomicBoolean(false)
    private var discoveryJob: Job? = null
    private var appContext: Context? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    data class ServerInfo(
        val address: java.net.InetAddress,
        val port: Int,
        val name: String
    )

    var onServerFound: ((ServerInfo) -> Unit)? = null

    fun startDiscovery(scope: CoroutineScope, context: Context? = null) {
        if (isDiscovering.get()) return
        isDiscovering.set(true)
        appContext = context

        acquireMulticastLock(context)

        discoveryJob = scope.launch(Dispatchers.IO) {
            try {
                socket = DatagramSocket().apply {
                    broadcast = true
                    soTimeout = 1500
                }

                // Try binding to WiFi — log result
                val bound = if (context != null) {
                    NetworkBinder.bindSocket(context, socket!!)
                } else false
                Log.d(TAG, "Socket WiFi binding: $bound")

                val deviceName = android.os.Build.MODEL
                val payload = Protocol.DISCOVERY_MAGIC + deviceName.toByteArray()

                Log.d(TAG, "=== Discovery Phase 1: Direct IP probes ===")

                // Phase 1: Send to common server IPs directly
                for (ip in COMMON_SERVER_IPS) {
                    if (!isDiscovering.get() || !isActive) break
                    try {
                        val addr = InetAddress.getByName(ip)
                        socket?.send(DatagramPacket(payload, payload.size, addr, Protocol.DISCOVERY_PORT))
                        Log.d(TAG, "Sent probe to $ip")
                    } catch (e: Exception) {
                        Log.w(TAG, "Failed to probe $ip: ${e.message}")
                    }
                }

                // Listen for responses
                var found = listenForServer(2000)
                if (found) return@launch

                Log.d(TAG, "=== Discovery Phase 2: Broadcast scan ===")

                // Phase 2: Broadcast to all interfaces
                var retries = 0
                while (isDiscovering.get() && isActive && retries < 10) {
                    broadcastToAllInterfaces(payload)
                    found = listenForServer(1500)
                    if (found) return@launch
                    retries++
                }

                Log.w(TAG, "Discovery complete — no server found after ${retries + 1} rounds")
                Log.w(TAG, "Try: Manual IP entry, or verify PC hotspot is ON")
            } catch (e: CancellationException) {
                Log.d(TAG, "Discovery cancelled")
            } catch (e: Exception) {
                Log.e(TAG, "Discovery error", e)
            }
        }
    }

    /**
     * Listen for a server response. Returns true if found.
     */
    private suspend fun listenForServer(timeoutMs: Long): Boolean {
        val startTime = System.currentTimeMillis()
        while (System.currentTimeMillis() - startTime < timeoutMs) {
            try {
                val buf = ByteArray(512)
                val pkt = DatagramPacket(buf, buf.size)
                socket?.receive(pkt)

                val response = String(pkt.data, 0, pkt.length)
                Log.d(TAG, "Received: '${response}' from ${pkt.address.hostAddress}")

                if (response.startsWith("GPAD_SERVER_V1")) {
                    val serverName = if (response.contains("|")) response.substringAfter("|") else "Server"
                    val serverAddr = pkt.address
                    Log.d(TAG, "SERVER FOUND: $serverName @ ${serverAddr.hostAddress}")
                    onServerFound?.invoke(ServerInfo(serverAddr, Protocol.UDP_PORT, serverName))
                    delay(300)
                    return true
                }
            } catch (_: java.net.SocketTimeoutException) {
                // Normal
            }
        }
        return false
    }

    private fun acquireMulticastLock(context: Context?) {
        try {
            val wifiManager = context?.applicationContext?.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            multicastLock = wifiManager?.createMulticastLock("GamePadDiscovery")?.apply {
                setReferenceCounted(false)
                acquire()
                Log.d(TAG, "Multicast lock acquired")
            }
        } catch (e: Exception) {
            Log.w(TAG, "Could not acquire multicast lock: ${e.message}")
        }
    }

    private fun broadcastToAllInterfaces(payload: ByteArray) {
        try {
            val interfaces = NetworkInterface.getNetworkInterfaces()
            while (interfaces.hasMoreElements()) {
                val ni = interfaces.nextElement()
                if (!ni.isUp || ni.isLoopback) continue
                for (ia in ni.interfaceAddresses) {
                    val addr = ia.address ?: continue
                    if (addr !is java.net.Inet4Address) continue

                    // Send to interface broadcast address
                    ia.broadcast?.let { bcast ->
                        try {
                            socket?.send(DatagramPacket(payload, payload.size, bcast, Protocol.DISCOVERY_PORT))
                            Log.d(TAG, "Broadcast via ${ia.broadcast} on ${ni.displayName}")
                        } catch (_: Exception) {}
                    }

                    // Compute and send to subnet broadcast
                    try {
                        val prefixLen = ia.networkPrefixLength.toInt()
                        val broadcastAddr = computeBroadcast(addr, prefixLen)
                        socket?.send(DatagramPacket(payload, payload.size, broadcastAddr, Protocol.DISCOVERY_PORT))
                        Log.d(TAG, "Broadcast to computed: $broadcastAddr")
                    } catch (_: Exception) {}
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "Broadcast error: ${e.message}")
        }
    }

    private fun computeBroadcast(ip: InetAddress, prefixLen: Int): InetAddress {
        val ipBytes = ip.address
        val broadcastBytes = ByteArray(4)
        for (i in 0 until 4) {
            val bitOffset = i * 8
            for (bit in 0 until 8) {
                val globalBit = bitOffset + bit
                if (globalBit < prefixLen) {
                    broadcastBytes[i] = (broadcastBytes[i].toInt() or (ipBytes[i].toInt() and (1 shl (7 - bit)))).toByte()
                } else {
                    broadcastBytes[i] = (broadcastBytes[i].toInt() or (1 shl (7 - bit))).toByte()
                }
            }
        }
        return InetAddress.getByAddress(broadcastBytes)
    }

    fun stopDiscovery() {
        isDiscovering.set(false)
        discoveryJob?.cancel()
        try { socket?.close() } catch (_: Exception) {}
        socket = null
        releaseMulticastLock()
    }

    private fun releaseMulticastLock() {
        try {
            multicastLock?.release()
            multicastLock = null
        } catch (_: Exception) {}
    }
}
