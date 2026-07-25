package com.gamepad.controller.ui.components

import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.ArrowDropUp
import androidx.compose.material.icons.automirrored.filled.ArrowLeft
import androidx.compose.material.icons.automirrored.filled.ArrowRight
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlin.math.abs
import kotlin.math.roundToInt

// ──────────────────────────────────────────────────────
//  HAPTIC FEEDBACK
// ──────────────────────────────────────────────────────

object Haptics {
    private fun getVibrator(context: android.content.Context): Vibrator {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            val vm = context.getSystemService(android.content.Context.VIBRATOR_MANAGER_SERVICE) as VibratorManager
            vm.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            context.getSystemService(android.content.Context.VIBRATOR_SERVICE) as Vibrator
        }
    }

    fun lightTap(context: android.content.Context) {
        getVibrator(context).vibrate(VibrationEffect.createOneShot(10, 40))
    }

    fun buttonPress(context: android.content.Context) {
        getVibrator(context).vibrate(VibrationEffect.createOneShot(20, 80))
    }
}

// ──────────────────────────────────────────────────────
//  TOUCH BUTTON
// ──────────────────────────────────────────────────────

@Composable
fun TouchButton(
    icon: ImageVector,
    label: String,
    isPressed: Boolean,
    onButtonChanged: (Boolean) -> Unit,
    size: Int = 56,
    color: Color = MaterialTheme.colorScheme.primary
) {
    val context = LocalContext.current
    val scale by animateFloatAsState(
        targetValue = if (isPressed) 0.85f else 1f,
        animationSpec = spring(dampingRatio = Spring.DampingRatioMediumBouncy),
        label = "scale"
    )

    Box(
        modifier = Modifier
            .size(size.dp)
            .graphicsLayer {
                scaleX = scale
                scaleY = scale
            }
            .shadow(if (isPressed) 2.dp else 6.dp, CircleShape)
            .clip(CircleShape)
            .background(if (isPressed) color else color.copy(alpha = 0.3f))
            .pointerInput(Unit) {
                detectTapGestures(
                    onPress = {
                        Haptics.buttonPress(context)
                        onButtonChanged(true)
                        tryAwaitRelease()
                        onButtonChanged(false)
                    }
                )
            },
        contentAlignment = Alignment.Center
    ) {
        Icon(
            imageVector = icon,
            contentDescription = label,
            tint = if (isPressed) Color.White else Color.White.copy(alpha = 0.8f),
            modifier = Modifier.size((size * 0.4).dp)
        )
    }
}

// ──────────────────────────────────────────────────────
//  ANALOG STICK
// ──────────────────────────────────────────────────────

@Composable
fun AnalogStick(
    onXChanged: (Float) -> Unit,
    onYChanged: (Float) -> Unit,
    label: String = "L",
    size: Int = 120,
    modifier: Modifier = Modifier
) {
    val deadZone = 0.08f
    var offsetX by remember { mutableFloatStateOf(0f) }
    var offsetY by remember { mutableFloatStateOf(0f) }

    Box(
        modifier = modifier
            .size(size.dp)
            .shadow(8.dp, CircleShape)
            .clip(CircleShape)
            .background(Color.White.copy(alpha = 0.08f))
            .pointerInput(Unit) {
                detectDragGestures(
                    onDrag = { change, dragAmount ->
                        change.consume()
                        val maxRadius = size / 2f
                        offsetX = (offsetX + dragAmount.x).coerceIn(-maxRadius, maxRadius)
                        offsetY = (offsetY + dragAmount.y).coerceIn(-maxRadius, maxRadius)
                        val nx = (offsetX / maxRadius).let { if (abs(it) < deadZone) 0f else it }
                        val ny = (offsetY / maxRadius).let { if (abs(it) < deadZone) 0f else it }
                        onXChanged(nx)
                        onYChanged(ny)
                    },
                    onDragEnd = { offsetX = 0f; offsetY = 0f; onXChanged(0f); onYChanged(0f) },
                    onDragCancel = { offsetX = 0f; offsetY = 0f; onXChanged(0f); onYChanged(0f) }
                )
            },
        contentAlignment = Alignment.Center
    ) {
        Box(
            modifier = Modifier
                .offset { IntOffset(offsetX.roundToInt(), offsetY.roundToInt()) }
                .size(44.dp)
                .clip(CircleShape)
                .background(Color.White.copy(alpha = 0.5f))
        )
        Text(label, color = Color.White.copy(alpha = 0.25f), fontSize = 14.sp)
    }
}

// ──────────────────────────────────────────────────────
//  D-PAD — proper cross layout with separated touch zones
// ──────────────────────────────────────────────────────

@Composable
fun DPad(
    onUp: (Boolean) -> Unit,
    onDown: (Boolean) -> Unit,
    onLeft: (Boolean) -> Unit,
    onRight: (Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    val ctx = LocalContext.current
    // Each arm: 50dp wide, 44dp tall (or 44dp wide, 50dp tall for vertical)
    // Total: ~140dp × 140dp
    val armW = 50
    val armH = 44
    val totalSize = 140

    Box(modifier = modifier.size(totalSize.dp), contentAlignment = Alignment.Center) {
        // Center disc
        Box(modifier = Modifier.size(48.dp).clip(CircleShape).background(Color.White.copy(alpha = 0.12f)))

        // Up arm
        Box(modifier = Modifier.align(Alignment.TopCenter)) {
            TouchButton(Icons.Filled.ArrowDropUp, "", false, {
                Haptics.lightTap(ctx); onUp(it)
            }, armH)
        }
        // Down arm
        Box(modifier = Modifier.align(Alignment.BottomCenter)) {
            TouchButton(Icons.Filled.ArrowDropDown, "", false, {
                Haptics.lightTap(ctx); onDown(it)
            }, armH)
        }
        // Left arm
        Box(modifier = Modifier.align(Alignment.CenterStart)) {
            TouchButton(Icons.AutoMirrored.Filled.ArrowLeft, "", false, {
                Haptics.lightTap(ctx); onLeft(it)
            }, armW)
        }
        // Right arm
        Box(modifier = Modifier.align(Alignment.CenterEnd)) {
            TouchButton(Icons.AutoMirrored.Filled.ArrowRight, "", false, {
                Haptics.lightTap(ctx); onRight(it)
            }, armW)
        }
    }
}

// ──────────────────────────────────────────────────────
//  TRIGGER SLIDER
// ──────────────────────────────────────────────────────

@Composable
fun TriggerSlider(
    value: Float,
    onValueChanged: (Float) -> Unit,
    label: String,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .width(32.dp)
            .height(100.dp)
            .clip(RoundedCornerShape(8.dp))
            .background(Color.White.copy(alpha = 0.08f))
            .pointerInput(Unit) {
                detectDragGestures { change, dragAmount ->
                    change.consume()
                    onValueChanged((value - dragAmount.y / size.height).coerceIn(0f, 1f))
                }
            }
    ) {
        Box(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .fillMaxHeight(value.coerceIn(0.01f, 1f))
                .clip(RoundedCornerShape(8.dp))
                .background(Color.White.copy(alpha = 0.4f))
        )
        Text(label, color = Color.White.copy(alpha = 0.35f), fontSize = 10.sp,
            modifier = Modifier.align(Alignment.TopCenter).padding(top = 4.dp))
    }
}
