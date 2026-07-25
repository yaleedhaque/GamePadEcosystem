package com.gamepad.controller.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.util.Log
import java.net.DatagramSocket

/**
 * Binds sockets to the WiFi/hotspot network so traffic doesn't leak
 * through mobile data when both are active.
 *
 * When the phone connects to the PC's hotspot, it's a WiFi client.
 * Without explicit binding, Android may route UDP packets through
 * mobile data instead of the hotspot interface.
 */
object NetworkBinder {

    private const val TAG = "NetworkBinder"

    /**
     * Find the WiFi/hotspot Network and bind a socket to it.
     * Returns true if binding succeeded, false otherwise.
     */
    fun bindSocket(context: Context, socket: DatagramSocket): Boolean {
        val network = findWifiNetwork(context)
        if (network == null) {
            Log.w(TAG, "No WiFi network found — socket will use default route (may fail)")
            return false
        }
        return try {
            network.bindSocket(socket)
            Log.d(TAG, "Socket bound to network: $network")
            true
        } catch (e: Exception) {
            Log.e(TAG, "Failed to bind socket to network", e)
            false
        }
    }

    /**
     * Find the WiFi network. When connected to a PC hotspot, this is
     * the WiFi interface connecting to the hotspot AP.
     *
     * Priority: active WiFi > any WiFi > any non-cellular network
     */
    private fun findWifiNetwork(context: Context): Network? {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager

        // Fast path: check if active network is WiFi
        val active = cm.activeNetwork
        if (active != null) {
            val activeCaps = cm.getNetworkCapabilities(active)
            if (activeCaps != null) {
                if (activeCaps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) {
                    Log.d(TAG, "Active network is WiFi: $active")
                    return active
                }
                // Fallback: if active is not cellular, it might be hotspot-related
                if (!activeCaps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) &&
                    !activeCaps.hasTransport(NetworkCapabilities.TRANSPORT_VPN)) {
                    Log.d(TAG, "Active network is non-cellular, non-VPN: $active (transports=${activeCaps.toString().take(100)})")
                    return active
                }
            }
        }

        // Slow path: scan all networks
        @Suppress("DEPRECATION")
        for (network in cm.allNetworks) {
            val caps = cm.getNetworkCapabilities(network) ?: continue
            if (caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) {
                Log.d(TAG, "Found WiFi network: $network")
                return network
            }
        }

        // Last resort: try the active network even if it's cellular
        if (active != null) {
            Log.w(TAG, "No WiFi found, trying active network: $active")
            return active
        }

        Log.e(TAG, "No network found at all")
        return null
    }
}
