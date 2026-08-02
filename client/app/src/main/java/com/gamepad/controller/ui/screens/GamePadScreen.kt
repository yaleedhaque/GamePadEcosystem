package com.gamepad.controller.ui.screens

import android.content.Context
import android.util.DisplayMetrics
import android.view.WindowManager
import android.widget.Toast
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.gamepad.controller.layout.*
import com.gamepad.controller.network.InputPacket
import com.gamepad.controller.network.Protocol
import com.gamepad.controller.profiles.ControllerProfile
import com.gamepad.controller.ui.components.Haptics
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import kotlin.math.abs
import kotlin.math.roundToInt

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GamePadScreen(
    profile: ControllerProfile,
    onPacketReady: (InputPacket) -> Unit,
    playerSlot: Int,
    serverName: String = ""
) {
    val context = LocalContext.current
    val density = LocalDensity.current

    // Get screen size in dp for layout positioning
    val wm = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
    val metrics = DisplayMetrics()
    @Suppress("DEPRECATION")
    wm.defaultDisplay.getMetrics(metrics)
    val screenW = metrics.widthPixels / density.density
    val screenH = metrics.heightPixels / density.density

    // State
    var buttons by remember { mutableStateOf(0u) }
    var leftX by remember { mutableFloatStateOf(0f) }
    var leftY by remember { mutableFloatStateOf(0f) }
    var rightX by remember { mutableFloatStateOf(0f) }
    var rightY by remember { mutableFloatStateOf(0f) }
    var leftTrigger by remember { mutableFloatStateOf(0f) }
    var rightTrigger by remember { mutableFloatStateOf(0f) }
    var fps by remember { mutableIntStateOf(0) }

    // Layout editor state
    var isEditMode by remember { mutableStateOf(false) }
    var layout by remember { mutableStateOf(LayoutManager.loadCurrent(context)) }
    var selectedButton by remember { mutableStateOf<String?>(null) }
    var layoutDirty by remember { mutableStateOf(false) }
    var toolbarExpanded by remember { mutableStateOf(false) }

    // Input send loop
    LaunchedEffect(Unit) {
        var frames = 0
        var lastFpsTime = System.currentTimeMillis()
        while (true) {
            withContext(Dispatchers.IO) {
                onPacketReady(InputPacket(
                    packetType = Protocol.PacketType.INPUT,
                    buttons = buttons,
                    leftX = (leftX * 32767).toInt().toShort(),
                    leftY = (leftY * 32767).toInt().toShort(),
                    rightX = (rightX * 32767).toInt().toShort(),
                    rightY = (rightY * 32767).toInt().toShort(),
                    leftTrigger = (leftTrigger * 255).toInt().toByte(),
                    rightTrigger = (rightTrigger * 255).toInt().toByte()
                ))
            }
            frames++
            val now = System.currentTimeMillis()
            if (now - lastFpsTime >= 1000) { fps = frames; frames = 0; lastFpsTime = now }
            delay(8)
        }
    }

    fun toggleButton(flag: UInt, pressed: Boolean) {
        if (isEditMode) return
        buttons = if (pressed) buttons or flag else buttons and flag.inv()
    }

    fun moveButton(id: String, dx: Float, dy: Float) {
        layout = layout.copy(buttons = layout.buttons.map {
            if (it.id == id) it.copy(
                x = (it.x + dx).coerceIn(0.02f, 0.98f),
                y = (it.y + dy).coerceIn(0.02f, 0.98f)
            ) else it
        })
        layoutDirty = true
    }

    fun saveLayout() {
        val ok = LayoutManager.saveCurrent(context, layout)
        layoutDirty = false
        Toast.makeText(context, if (ok) "Layout saved!" else "Save failed", Toast.LENGTH_SHORT).show()
    }

    Box(Modifier.fillMaxSize().background(Color(0xFF0D1117))) {

        // ═══ GAMEPAD CONTROLS ═══
        Box(Modifier.fillMaxSize()) {
            layout.buttons.filter { it.visible && it.isVisibleIn(profile) }.forEach { btn ->
                val posX = (btn.x * screenW).dp
                val posY = (btn.y * screenH).dp

                Box(modifier = Modifier.offset { IntOffset(with(density) { posX.roundToPx() }, with(density) { posY.roundToPx() }) }) {
                    when (btn.type) {
                        ButtonLayout.ElementType.STICK_LEFT -> EditableStick(
                            btn, isEditMode, selectedButton == btn.id,
                            onDrag = { dx, dy -> moveButton(btn.id, dx, dy) },
                            onSelect = { selectedButton = btn.id },
                            onXChanged = { leftX = it }, onYChanged = { leftY = it }
                        )
                        ButtonLayout.ElementType.STICK_RIGHT -> EditableStick(
                            btn, isEditMode, selectedButton == btn.id,
                            onDrag = { dx, dy -> moveButton(btn.id, dx, dy) },
                            onSelect = { selectedButton = btn.id },
                            onXChanged = { rightX = it }, onYChanged = { rightY = it }
                        )
                        ButtonLayout.ElementType.TRIGGER_L -> EditableTrigger(
                            btn, isEditMode, selectedButton == btn.id,
                            onDrag = { dx, dy -> moveButton(btn.id, dx, dy) },
                            onSelect = { selectedButton = btn.id },
                            value = leftTrigger, onValueChanged = { leftTrigger = it }
                        )
                        ButtonLayout.ElementType.TRIGGER_R -> EditableTrigger(
                            btn, isEditMode, selectedButton == btn.id,
                            onDrag = { dx, dy -> moveButton(btn.id, dx, dy) },
                            onSelect = { selectedButton = btn.id },
                            value = rightTrigger, onValueChanged = { rightTrigger = it }
                        )
                        else -> EditableButton(
                            btn, isEditMode, selectedButton == btn.id,
                            onDrag = { dx, dy -> moveButton(btn.id, dx, dy) },
                            onSelect = { selectedButton = btn.id },
                            onButtonChanged = { toggleButton(btn.buttonFlag, it) },
                            isPressed = (buttons and btn.buttonFlag) != 0u
                        )
                    }
                }
            }
        }

        // ═══ HUD ═══
        Row(Modifier.fillMaxWidth().align(Alignment.TopCenter).padding(horizontal = 12.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text("P${playerSlot + 1} · ${profile.displayName}", fontSize = 11.sp, color = Color.White.copy(alpha = 0.4f))
            Text("${fps}Hz", fontSize = 10.sp, color = Color(0xFF4CAF50).copy(alpha = 0.5f))
        }

        // ═══ EDIT MODE TOOLBAR (collapsible) ═══
        AnimatedVisibility(visible = isEditMode,
            enter = slideInVertically(initialOffsetY = { -it }),
            exit = slideOutVertically(targetOffsetY = { -it }),
            modifier = Modifier.align(Alignment.TopCenter)) {
            Surface(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 36.dp),
                shape = RoundedCornerShape(16.dp),
                color = Color(0xFF1A1F2E).copy(alpha = 0.95f), tonalElevation = 8.dp
            ) {
                Column(modifier = Modifier.padding(8.dp)) {
                    // Header — always visible, tap to expand/collapse
                    Row(modifier = Modifier.fillMaxWidth().clickable { toolbarExpanded = !toolbarExpanded },
                        horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Text("LAYOUT EDITOR", fontSize = 12.sp, fontWeight = FontWeight.Bold,
                            color = Color(0xFF00E5FF), letterSpacing = 2.sp)
                        Icon(
                            if (toolbarExpanded) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore,
                            contentDescription = "Toggle",
                            tint = Color(0xFF00E5FF),
                            modifier = Modifier.size(20.dp)
                        )
                    }

                    // Expanded content — only shows when toolbar is expanded
                    AnimatedVisibility(visible = toolbarExpanded) {
                        Column(modifier = Modifier.padding(top = 8.dp)) {
                            val selected = layout.buttons.find { it.id == selectedButton }
                            if (selected != null) {
                                Text("${selected.type.name} — ${selected.label}", fontSize = 11.sp, color = Color.White.copy(alpha = 0.7f))
                                Spacer(Modifier.height(6.dp))
                                Text("Size: ${selected.sizeDp}dp", fontSize = 10.sp, color = Color.White.copy(alpha = 0.5f))
                                Slider(value = selected.sizeDp.toFloat(), onValueChange = { newSize ->
                                    layout = layout.copy(buttons = layout.buttons.map {
                                        if (it.id == selectedButton) it.copy(sizeDp = newSize.roundToInt()) else it
                                    })
                                    layoutDirty = true
                                }, valueRange = 24f..160f, modifier = Modifier.fillMaxWidth(),
                                    colors = SliderDefaults.colors(thumbColor = Color(0xFF00E5FF), activeTrackColor = Color(0xFF00E5FF)))
                                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth().padding(top = 4.dp)) {
                                    Text("Visible", fontSize = 11.sp, color = Color.White.copy(alpha = 0.6f), modifier = Modifier.weight(1f))
                                    Switch(checked = selected.visible, onCheckedChange = { vis ->
                                        layout = layout.copy(buttons = layout.buttons.map {
                                            if (it.id == selectedButton) it.copy(visible = vis) else it
                                        })
                                        layoutDirty = true
                                    }, colors = SwitchDefaults.colors(checkedThumbColor = Color(0xFF00E5FF), checkedTrackColor = Color(0xFF00E5FF).copy(alpha = 0.3f)))
                                }
                            } else {
                                Text("Tap any control to select · Drag to move", fontSize = 11.sp, color = Color.White.copy(alpha = 0.5f))
                            }

                            Spacer(Modifier.height(8.dp))
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = { layout = defaultXboxLayout(); saveLayout() },
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF37474F)), modifier = Modifier.weight(1f)) {
                                    Text("Reset", fontSize = 11.sp)
                                }
                                Button(onClick = { saveLayout(); isEditMode = false; selectedButton = null; toolbarExpanded = false },
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF00E5FF)), modifier = Modifier.weight(1f)) {
                                    Text("Save & Exit", fontSize = 11.sp, color = Color.Black, fontWeight = FontWeight.Bold)
                                }
                                Button(onClick = {
                                    if (layoutDirty) {
                                        layout = LayoutManager.loadCurrent(context)
                                        Toast.makeText(context, "Changes discarded", Toast.LENGTH_SHORT).show()
                                    }
                                    isEditMode = false; selectedButton = null; toolbarExpanded = false
                                },
                                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFE53935)), modifier = Modifier.weight(1f)) {
                                    Text("Cancel", fontSize = 11.sp)
                                }
                            }
                        }
                    }
                }
            }
        }

        // ═══ EDIT TOGGLE FAB ═══
        Box(modifier = Modifier.align(Alignment.BottomEnd).padding(end = 12.dp, bottom = 12.dp)
            .size(48.dp).shadow(8.dp, CircleShape).clip(CircleShape)
            .background(if (isEditMode) Color(0xFF00E5FF) else Color.White.copy(alpha = 0.15f))
            .clickable {
                Haptics.buttonPress(context)
                isEditMode = !isEditMode
                if (!isEditMode) { selectedButton = null; toolbarExpanded = false }
                else { toolbarExpanded = false }
            },
            contentAlignment = Alignment.Center) {
            Icon(if (isEditMode) Icons.Filled.Check else Icons.Filled.Edit,
                contentDescription = null, tint = if (isEditMode) Color.Black else Color.White.copy(alpha = 0.7f),
                modifier = Modifier.size(24.dp))
        }
    }
}

// ══════════════════════════════════════════════
// EDITABLE STICK — uses clickable for select, pointerInput for drag
// ══════════════════════════════════════════════
@Composable
private fun EditableStick(
    btn: ButtonLayout, isEditMode: Boolean, isSelected: Boolean,
    onDrag: (Float, Float) -> Unit, onSelect: () -> Unit,
    onXChanged: (Float) -> Unit, onYChanged: (Float) -> Unit
) {
    val ctx = LocalContext.current
    val density = LocalDensity.current
    val deadZone = 0.08f
    var offsetX by remember { mutableFloatStateOf(0f) }
    var offsetY by remember { mutableFloatStateOf(0f) }
    val size = btn.sizeDp
    val maxR = size / 2f
    val radiusPx = with(density) { maxR.dp.toPx() }

    Box(modifier = Modifier.size(size.dp)
        .then(if (isSelected) Modifier.border(2.dp, Color(0xFF00E5FF), CircleShape) else Modifier)
        .shadow(8.dp, CircleShape).clip(CircleShape).background(Color.White.copy(alpha = 0.08f))
        .pointerInput(isEditMode) {
            if (!isEditMode) {
                detectDragGestures(
                    onDrag = { change, dragAmount ->
                        change.consume()
                        // dragAmount is in pixels; clamp/normalize against the stick radius in px
                        offsetX = (offsetX + dragAmount.x).coerceIn(-radiusPx, radiusPx)
                        offsetY = (offsetY + dragAmount.y).coerceIn(-radiusPx, radiusPx)
                        // XInput thumbstick Y is positive UP; screen-space Y grows DOWN, so negate
                        onXChanged((offsetX / radiusPx).let { if (abs(it) < deadZone) 0f else it })
                        onYChanged((-offsetY / radiusPx).let { if (abs(it) < deadZone) 0f else it })
                    },
                    onDragEnd = { offsetX = 0f; offsetY = 0f; onXChanged(0f); onYChanged(0f) },
                    onDragCancel = { offsetX = 0f; offsetY = 0f; onXChanged(0f); onYChanged(0f) }
                )
            } else {
                detectDragGestures { change, dragAmount ->
                    change.consume()
                    onDrag(dragAmount.x / ctx.resources.displayMetrics.widthPixels,
                        dragAmount.y / ctx.resources.displayMetrics.heightPixels)
                }
            }
        }
        .clickable(enabled = isEditMode) { onSelect() },
        contentAlignment = Alignment.Center
    ) {
        if (!isEditMode) {
            Box(modifier = Modifier.offset { IntOffset(offsetX.roundToInt(), offsetY.roundToInt()) }
                .size((size * 0.35f).dp).clip(CircleShape).background(Color.White.copy(alpha = 0.5f)))
            Text(btn.label, color = Color.White.copy(alpha = 0.25f), fontSize = 14.sp)
        } else {
            Icon(Icons.Filled.OpenWith, null, tint = Color(0xFF00E5FF).copy(alpha = 0.7f), modifier = Modifier.size(20.dp))
        }
    }
}

// ══════════════════════════════════════════════
// EDITABLE BUTTON — uses clickable for select, pointerInput for drag
// ══════════════════════════════════════════════
@Composable
private fun EditableButton(
    btn: ButtonLayout, isEditMode: Boolean, isSelected: Boolean,
    onDrag: (Float, Float) -> Unit, onSelect: () -> Unit,
    onButtonChanged: (Boolean) -> Unit, isPressed: Boolean
) {
    val context = LocalContext.current
    val color = Color(btn.color)
    val size = btn.sizeDp

    val scale by animateFloatAsState(
        targetValue = if (isPressed && !isEditMode) 0.85f else 1f,
        animationSpec = spring(dampingRatio = Spring.DampingRatioMediumBouncy), label = "s")

    Box(modifier = Modifier.size(size.dp)
        .graphicsLayer { scaleX = scale; scaleY = scale }
        .then(if (isSelected) Modifier.border(2.dp, Color(0xFF00E5FF), CircleShape) else Modifier)
        .shadow(if (isPressed) 2.dp else 6.dp, CircleShape).clip(CircleShape)
        .background(if (isEditMode) color.copy(alpha = 0.4f) else if (isPressed) color else color.copy(alpha = 0.3f))
        .pointerInput(isEditMode) {
            if (!isEditMode) {
                // No drag in normal mode
            } else {
                detectDragGestures { change, dragAmount ->
                    change.consume()
                    onDrag(dragAmount.x / context.resources.displayMetrics.widthPixels,
                        dragAmount.y / context.resources.displayMetrics.heightPixels)
                }
            }
        }
        .clickable(enabled = isEditMode) { onSelect() }
        .pointerInput(isEditMode) {
            if (!isEditMode) {
                awaitEachGesture {
                    val down = awaitFirstDown(requireUnconsumed = false)
                    down.consume()
                    Haptics.buttonPress(context)
                    onButtonChanged(true)
                    try {
                        waitForUpOrCancellation()
                    } finally {
                        onButtonChanged(false)
                    }
                }
            }
        },
        contentAlignment = Alignment.Center
    ) {
        if (isEditMode) {
            Icon(Icons.Filled.OpenWith, null, tint = Color(0xFF00E5FF), modifier = Modifier.size((size * 0.35f).dp))
        } else {
            Text(btn.label, color = Color.White.copy(alpha = if (isPressed) 1f else 0.8f),
                fontSize = (size * 0.3f).sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
        }
    }
}

// ══════════════════════════════════════════════
// EDITABLE TRIGGER — uses clickable for select, pointerInput for drag
// ══════════════════════════════════════════════
@Composable
private fun EditableTrigger(
    btn: ButtonLayout, isEditMode: Boolean, isSelected: Boolean,
    onDrag: (Float, Float) -> Unit, onSelect: () -> Unit,
    value: Float, onValueChanged: (Float) -> Unit
) {
    val context = LocalContext.current
    val density = LocalDensity.current
    val size = btn.sizeDp
    val widthDp = size * 2.2f
    val heightDp = size * 0.75f
    val widthPx = with(density) { widthDp.dp.toPx() }

    Box(modifier = Modifier.width(widthDp.dp).height(heightDp.dp)
        .then(if (isSelected) Modifier.border(2.dp, Color(0xFF00E5FF), RoundedCornerShape(10.dp)) else Modifier)
        .clip(RoundedCornerShape(10.dp)).background(Color.White.copy(alpha = 0.10f))
        .pointerInput(isEditMode) {
            if (!isEditMode) {
                // Hold-to-press analog trigger: value tracks the finger's x position
                // across the trigger width, released to 0 on finger-up (never sticks).
                awaitEachGesture {
                    val down = awaitFirstDown(requireUnconsumed = false)
                    down.consume()
                    onValueChanged((down.position.x / widthPx).coerceIn(0f, 1f))
                    try {
                        while (true) {
                            val event = awaitPointerEvent()
                            val change = event.changes.firstOrNull() ?: break
                            if (change.pressed) {
                                onValueChanged((change.position.x / widthPx).coerceIn(0f, 1f))
                            } else {
                                break
                            }
                        }
                    } finally {
                        onValueChanged(0f)
                    }
                }
            } else {
                detectDragGestures { change, dragAmount ->
                    change.consume()
                    onDrag(dragAmount.x / context.resources.displayMetrics.widthPixels,
                        dragAmount.y / context.resources.displayMetrics.heightPixels)
                }
            }
        }
        .clickable(enabled = isEditMode) { onSelect() }
    ) {
        Box(modifier = Modifier.align(Alignment.CenterStart).fillMaxHeight()
            .fillMaxWidth(value.coerceIn(0.01f, 1f))
            .clip(RoundedCornerShape(10.dp)).background(Color(0xFF00E5FF).copy(alpha = 0.45f)))
        if (isEditMode) Icon(Icons.Filled.OpenWith, null, tint = Color(0xFF00E5FF),
            modifier = Modifier.align(Alignment.Center).size(16.dp))
        else Text(btn.label, color = Color.White.copy(alpha = 0.6f), fontSize = 11.sp,
            fontWeight = FontWeight.Bold, modifier = Modifier.align(Alignment.Center))
    }
}
