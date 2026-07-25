package com.gamepad.controller.ui.screens

import androidx.compose.animation.*
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.gamepad.controller.profiles.ControllerProfile

@Composable
fun ConnectionScreen(
    isConnected: Boolean,
    serverName: String,
    playerSlot: Int,
    currentProfile: ControllerProfile,
    isDiscovering: Boolean,
    onConnect: () -> Unit,
    onDisconnect: () -> Unit,
    onProfileChange: (ControllerProfile) -> Unit,
    onManualConnect: (String) -> Unit
) {
    var showManualInput by remember { mutableStateOf(false) }
    var manualIp by remember { mutableStateOf("") }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF0D1117))
            .padding(24.dp)
    ) {
        Column(
            modifier = Modifier.fillMaxSize(),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text("GamePad Controller", fontSize = 28.sp, fontWeight = FontWeight.Bold, color = Color.White)
            Spacer(Modifier.height(4.dp))
            Text("Offline Multiplayer Virtual Gamepad", fontSize = 14.sp, color = Color.White.copy(alpha = 0.45f))
            Spacer(Modifier.height(32.dp))

            // Status Card
            Card(
                modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)),
                colors = CardDefaults.cardColors(containerColor = Color.White.copy(alpha = 0.05f))
            ) {
                Column(Modifier.padding(20.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    if (isConnected) {
                        Icon(Icons.Filled.CheckCircle, null, tint = Color(0xFF4CAF50), modifier = Modifier.size(48.dp))
                        Spacer(Modifier.height(12.dp))
                        Text("Connected", fontSize = 20.sp, fontWeight = FontWeight.Bold, color = Color(0xFF4CAF50))
                        Text(serverName, fontSize = 14.sp, color = Color.White.copy(alpha = 0.7f))
                        Text("Player ${playerSlot + 1}", fontSize = 16.sp, fontWeight = FontWeight.Medium, color = Color.White)
                        Spacer(Modifier.height(16.dp))
                        Button(onDisconnect, Modifier.fillMaxWidth(),
                            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFE53935))) {
                            Icon(Icons.Filled.LinkOff, null); Spacer(Modifier.width(8.dp)); Text("Disconnect")
                        }
                    } else {
                        if (isDiscovering) {
                            CircularProgressIndicator(Modifier.size(24.dp), color = Color(0xFFFFC107), strokeWidth = 2.dp)
                            Spacer(Modifier.height(8.dp))
                            Text("Searching for server...", fontSize = 14.sp, color = Color.White.copy(alpha = 0.7f),
                                textAlign = TextAlign.Center)
                        } else {
                            Icon(Icons.Filled.WifiFind, null, tint = Color(0xFFFFC107), modifier = Modifier.size(48.dp))
                            Spacer(Modifier.height(12.dp))
                            Text("No server found", fontSize = 16.sp, fontWeight = FontWeight.Medium, color = Color(0xFFFFC107))
                            Text("Make sure PC hotspot is ON and phone is connected to it.\nDo NOT use phone hotspot — it blocks device communication.", fontSize = 12.sp, color = Color.White.copy(alpha = 0.5f),
                                textAlign = TextAlign.Center)
                        }
                        Spacer(Modifier.height(16.dp))
                        Button(onConnect, Modifier.fillMaxWidth(),
                            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF2196F3))) {
                            Icon(Icons.Filled.Refresh, null); Spacer(Modifier.width(8.dp)); Text("Scan Again")
                        }
                        Spacer(Modifier.height(8.dp))
                        TextButton(onClick = { showManualInput = !showManualInput }) { Text("Manual IP Entry") }
                        AnimatedVisibility(visible = showManualInput) {
                            Column(Modifier.fillMaxWidth()) {
                                OutlinedTextField(
                                    manualIp,
                                    { manualIp = it },
                                    Modifier.fillMaxWidth(),
                                    singleLine = true,
                                    placeholder = { Text("e.g. 192.168.58.165") },
                                    label = { Text("Server IP Address") },
                                    colors = OutlinedTextFieldDefaults.colors(
                                        focusedBorderColor = Color(0xFF2196F3),
                                        unfocusedBorderColor = Color.White.copy(alpha = 0.3f),
                                        focusedTextColor = Color.White,
                                        unfocusedTextColor = Color.White,
                                        focusedLabelColor = Color(0xFF2196F3),
                                        unfocusedLabelColor = Color.White.copy(alpha = 0.5f)
                                    )
                                )
                                Spacer(Modifier.height(8.dp))
                                Button(
                                    onClick = { if (manualIp.isNotBlank()) onManualConnect(manualIp) },
                                    Modifier.fillMaxWidth(),
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF2196F3))
                                ) { Text("Connect") }
                            }
                        }
                    }
                }
            }

            Spacer(Modifier.height(24.dp))

            // Profile Selector
            Text("Controller Profile", fontSize = 16.sp, fontWeight = FontWeight.Medium, color = Color.White.copy(alpha = 0.7f))
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.fillMaxWidth()) {
                ControllerProfile.entries.forEach { profile ->
                    val selected = profile == currentProfile
                    Box(
                        modifier = Modifier.weight(1f).clip(RoundedCornerShape(10.dp))
                            .background(if (selected) Color(0xFF2196F3) else Color.White.copy(alpha = 0.08f))
                            .clickable { onProfileChange(profile) }
                            .padding(vertical = 10.dp, horizontal = 4.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(profile.displayName, fontSize = 11.sp, color = Color.White, textAlign = TextAlign.Center,
                            fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal)
                    }
                }
            }

            Spacer(Modifier.height(20.dp))

            // Quick Start
            Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = Color.White.copy(alpha = 0.03f))) {
                Column(Modifier.padding(14.dp)) {
                    Text("Quick Start", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = Color.White.copy(alpha = 0.8f))
                    Spacer(Modifier.height(6.dp))
                    listOf(
                        "1" to "On PC: Settings > Network > Mobile Hotspot > ON",
                        "2" to "On this phone: connect to PC's WiFi hotspot",
                        "3" to "Run GamePadServer.exe on your PC",
                        "4" to "Tap Scan — auto-discovery handles the rest",
                        "5" to "Connect more phones for multiplayer"
                    ).forEach { (n, t) ->
                        Row(Modifier.padding(vertical = 1.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text(n, fontSize = 12.sp, color = Color(0xFF2196F3), fontWeight = FontWeight.Bold, modifier = Modifier.width(18.dp))
                            Text(t, fontSize = 12.sp, color = Color.White.copy(alpha = 0.55f))
                        }
                    }
                }
            }
        }
    }
}
