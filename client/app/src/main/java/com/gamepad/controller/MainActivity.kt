package com.gamepad.controller

import android.os.Bundle
import android.util.Log
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.*
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.lifecycle.lifecycleScope
import com.gamepad.controller.network.DiscoveryClient
import com.gamepad.controller.network.InputPacket
import com.gamepad.controller.network.Protocol
import com.gamepad.controller.network.UdpInputClient
import com.gamepad.controller.profiles.ControllerProfile
import com.gamepad.controller.sensors.SensorInputManager
import com.gamepad.controller.ui.screens.ConnectionScreen
import com.gamepad.controller.ui.screens.GamePadScreen
import kotlinx.coroutines.*
import java.net.InetAddress

class MainActivity : ComponentActivity() {

    companion object {
        private const val TAG = "MainActivity"
    }

    private val udpClient = UdpInputClient()
    private val discoveryClient = DiscoveryClient()
    private var sensorManager: SensorInputManager? = null

    private var isConnected by mutableStateOf(false)
    private var serverName by mutableStateOf("")
    private var playerSlot by mutableIntStateOf(0)
    private var currentProfile by mutableStateOf(ControllerProfile.XBOX_STANDARD)
    private var isDiscovering by mutableStateOf(false)
    private var showGamepad by mutableStateOf(false)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        // Fullscreen immersive — modern API
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        WindowInsetsControllerCompat(window, window.decorView).let { controller ->
            controller.hide(WindowInsetsCompat.Type.systemBars())
            controller.systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }

        // WiFi low-latency mode (API 34+)
        try {
            val wm = applicationContext.getSystemService(WIFI_SERVICE) as android.net.wifi.WifiManager
            wm.javaClass.getMethod("setWifiMode", Int::class.java)
                .invoke(wm, 4)
        } catch (e: Exception) { Log.w(TAG, "WiFi low-latency unavailable: ${e.message}") }

        sensorManager = SensorInputManager(this)

        // Discovery callback
        discoveryClient.onServerFound = { info ->
            runOnUiThread { connectToServer(info.address, info.port, info.name) }
        }

        // Connection state callback
        udpClient.onConnectionStateChanged = { connected ->
            runOnUiThread {
                isConnected = connected
                showGamepad = connected
            }
        }

        // Server assignment callback — get real player slot from server
        udpClient.onPlayerAssigned = { slot, name ->
            runOnUiThread {
                playerSlot = slot
                serverName = name
                Log.d(TAG, "Assigned Player ${slot + 1} by server: $name")
            }
        }

        setContent {
            MaterialTheme(colorScheme = darkColorScheme()) {
                if (showGamepad) {
                    GamePadScreen(currentProfile, ::sendInputPacket, playerSlot, serverName)
                } else {
                    ConnectionScreen(
                        isConnected = isConnected, serverName = serverName, playerSlot = playerSlot,
                        currentProfile = currentProfile, isDiscovering = isDiscovering,
                        onConnect = ::startDiscovery, onDisconnect = ::disconnect,
                        onProfileChange = { currentProfile = it }, onManualConnect = ::connectManual
                    )
                }
            }
        }

        startDiscovery()
    }

    private fun startDiscovery() {
        isDiscovering = true
        discoveryClient.startDiscovery(lifecycleScope, applicationContext)
    }

    private fun connectToServer(address: InetAddress, port: Int, name: String) {
        isDiscovering = false
        discoveryClient.stopDiscovery()
        serverName = name
        udpClient.start(address, port, applicationContext)
        sensorManager?.start()

        // Heartbeat loop
        lifecycleScope.launch {
            while (isActive) { udpClient.sendHeartbeat(); delay(2000) }
        }
    }

    private fun connectManual(ip: String) {
        try {
            connectToServer(InetAddress.getByName(ip), Protocol.UDP_PORT, "Manual Server")
        } catch (e: Exception) { Log.e(TAG, "Failed to connect to $ip", e) }
    }

    private fun disconnect() {
        udpClient.stop()
        sensorManager?.stop()
        showGamepad = false
        isConnected = false
        isDiscovering = false
    }

    private fun sendInputPacket(packet: InputPacket) {
        sensorManager?.let { s ->
            if (s.isRunning && currentProfile.showMotion) {
                val g = s.getGyroDelta()
                packet.gyroX = g.first; packet.gyroY = g.second; packet.gyroZ = g.third
            }
        }
        udpClient.sendInput(packet)
    }

    override fun onDestroy() {
        super.onDestroy()
        sensorManager?.stop()
        udpClient.stop()
        discoveryClient.stopDiscovery()
    }
}
