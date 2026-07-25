package com.gamepad.controller.layout

import android.content.Context
import android.content.SharedPreferences
import android.widget.Toast
import com.gamepad.controller.network.Protocol
import org.json.JSONArray
import org.json.JSONObject

/**
 * Each control element in the gamepad layout.
 * Position is in screen-relative fractions (0.0-1.0).
 * Size is in dp.
 */
data class ButtonLayout(
    val id: String,
    val type: ElementType,
    val x: Float = 0.5f,
    val y: Float = 0.5f,
    val sizeDp: Int = 56,
    val label: String = "",
    val color: Long = 0xFF4CAF50,
    val visible: Boolean = true,
    val buttonFlag: UInt = 0u
) {
    enum class ElementType {
        STICK_LEFT,
        STICK_RIGHT,
        FACE_A, FACE_B, FACE_X, FACE_Y,
        DPAD_UP, DPAD_DOWN, DPAD_LEFT, DPAD_RIGHT,
        BUMPER_L, BUMPER_R,
        TRIGGER_L, TRIGGER_R,
        CENTER_BACK, CENTER_GUIDE, CENTER_START
    }

    fun toJson(): JSONObject = JSONObject().apply {
        put("id", id)
        put("type", type.name)
        put("x", x)
        put("y", y)
        put("sizeDp", sizeDp)
        put("label", label)
        put("color", color)
        put("visible", visible)
        put("buttonFlag", buttonFlag.toLong())
    }

    companion object {
        fun fromJson(json: JSONObject): ButtonLayout = ButtonLayout(
            id = json.getString("id"),
            type = ElementType.valueOf(json.getString("type")),
            x = json.getDouble("x").toFloat(),
            y = json.getDouble("y").toFloat(),
            sizeDp = json.getInt("sizeDp"),
            label = json.optString("label", ""),
            color = json.optLong("color", 0xFF4CAF50),
            visible = json.optBoolean("visible", true),
            buttonFlag = json.optLong("buttonFlag", 0).toUInt()
        )
    }
}

/**
 * Complete controller layout — collection of all button positions.
 */
data class ControllerLayout(
    val name: String = "Default",
    val buttons: List<ButtonLayout> = emptyList()
) {
    fun toJson(): String {
        val arr = JSONArray()
        buttons.forEach { arr.put(it.toJson()) }
        return JSONObject().apply {
            put("name", name)
            put("buttons", arr)
        }.toString(2)
    }

    companion object {
        fun fromJson(json: String): ControllerLayout {
            val obj = JSONObject(json)
            val arr = obj.getJSONArray("buttons")
            val buttons = (0 until arr.length()).map { ButtonLayout.fromJson(arr.getJSONObject(it)) }
            return ControllerLayout(name = obj.optString("name", "Default"), buttons = buttons)
        }
    }
}

/**
 * Default Xbox 360 layout — all positions in screen-relative fractions.
 * Layout:
 *   LB(0.05, 0.08)              RB(0.95, 0.08)
 *   LStick(0.18, 0.22)          FaceButtons(0.82, 0.25)
 *        CenterButtons(0.5, 0.55)
 *   DPad(0.18, 0.78)            RStick(0.82, 0.78)
 */
fun defaultXboxLayout(): ControllerLayout = ControllerLayout(
    name = "Xbox",
    buttons = listOf(
        // Left stick — upper-left
        ButtonLayout("lstick", ButtonLayout.ElementType.STICK_LEFT, 0.18f, 0.25f, 130, "L", 0x00000000, true),
        // Right stick — lower-right
        ButtonLayout("rstick", ButtonLayout.ElementType.STICK_RIGHT, 0.82f, 0.75f, 130, "R", 0x00000000, true),

        // Face buttons — upper-right quadrant (Xbox diamond)
        ButtonLayout("face_a", ButtonLayout.ElementType.FACE_A, 0.82f, 0.32f, 52, "A", 0xFF4CAF50, true, Protocol.ButtonFlag.A),
        ButtonLayout("face_b", ButtonLayout.ElementType.FACE_B, 0.88f, 0.22f, 52, "B", 0xFFE53935, true, Protocol.ButtonFlag.B),
        ButtonLayout("face_x", ButtonLayout.ElementType.FACE_X, 0.76f, 0.22f, 52, "X", 0xFF2196F3, true, Protocol.ButtonFlag.X),
        ButtonLayout("face_y", ButtonLayout.ElementType.FACE_Y, 0.82f, 0.12f, 52, "Y", 0xFFFFEB3B, true, Protocol.ButtonFlag.Y),

        // D-Pad — lower-left quadrant
        ButtonLayout("dpad_up", ButtonLayout.ElementType.DPAD_UP, 0.18f, 0.68f, 44, "↑", 0x00000000, true, Protocol.ButtonFlag.DPAD_UP),
        ButtonLayout("dpad_down", ButtonLayout.ElementType.DPAD_DOWN, 0.18f, 0.88f, 44, "↓", 0x00000000, true, Protocol.ButtonFlag.DPAD_DOWN),
        ButtonLayout("dpad_left", ButtonLayout.ElementType.DPAD_LEFT, 0.10f, 0.78f, 44, "←", 0x00000000, true, Protocol.ButtonFlag.DPAD_LEFT),
        ButtonLayout("dpad_right", ButtonLayout.ElementType.DPAD_RIGHT, 0.26f, 0.78f, 44, "→", 0x00000000, true, Protocol.ButtonFlag.DPAD_RIGHT),

        // Bumpers — top edge
        ButtonLayout("bumper_l", ButtonLayout.ElementType.BUMPER_L, 0.10f, 0.06f, 64, "LB", 0xFF607D8B, true, Protocol.ButtonFlag.LEFT_BUMPER),
        ButtonLayout("bumper_r", ButtonLayout.ElementType.BUMPER_R, 0.90f, 0.06f, 64, "RB", 0xFF607D8B, true, Protocol.ButtonFlag.RIGHT_BUMPER),

        // Triggers — bottom-right area
        ButtonLayout("trigger_l", ButtonLayout.ElementType.TRIGGER_L, 0.70f, 0.92f, 36, "LT", 0xFF607D8B, true),
        ButtonLayout("trigger_r", ButtonLayout.ElementType.TRIGGER_R, 0.90f, 0.92f, 36, "RT", 0xFF607D8B, true),

        // Center buttons — horizontal row at vertical center
        ButtonLayout("center_back", ButtonLayout.ElementType.CENTER_BACK, 0.40f, 0.55f, 32, "BACK", 0xFF757575, true, Protocol.ButtonFlag.BACK),
        ButtonLayout("center_guide", ButtonLayout.ElementType.CENTER_GUIDE, 0.50f, 0.55f, 36, "GUIDE", 0xFF9E9E9E, true, Protocol.ButtonFlag.GUIDE),
        ButtonLayout("center_start", ButtonLayout.ElementType.CENTER_START, 0.60f, 0.55f, 32, "START", 0xFF757575, true, Protocol.ButtonFlag.START),
    )
)

/**
 * Layout persistence via SharedPreferences.
 * Uses commit() for synchronous writes to prevent data loss.
 */
object LayoutManager {
    private const val PREFS_NAME = "gamepad_layouts"
    private const val KEY_CURRENT = "current_layout"
    private const val KEY_CUSTOM_PREFIX = "custom_"

    private fun prefs(context: Context): SharedPreferences =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    fun saveLayout(context: Context, layout: ControllerLayout): Boolean {
        return prefs(context).edit()
            .putString("$KEY_CUSTOM_PREFIX${layout.name}", layout.toJson())
            .commit()
    }

    fun loadLayout(context: Context, name: String): ControllerLayout? {
        val json = prefs(context).getString("$KEY_CUSTOM_PREFIX$name", null) ?: return null
        return try { ControllerLayout.fromJson(json) } catch (_: Exception) { null }
    }

    fun saveCurrent(context: Context, layout: ControllerLayout): Boolean {
        val saved = saveLayout(context, layout)
        val nameSaved = prefs(context).edit().putString(KEY_CURRENT, layout.name).commit()
        return saved && nameSaved
    }

    fun loadCurrent(context: Context): ControllerLayout {
        val name = prefs(context).getString(KEY_CURRENT, null)
        if (name != null) {
            loadLayout(context, name)?.let { return it }
        }
        return defaultXboxLayout()
    }

    fun listLayouts(context: Context): List<String> {
        return prefs(context).all.keys
            .filter { it.startsWith(KEY_CUSTOM_PREFIX) }
            .map { it.removePrefix(KEY_CUSTOM_PREFIX) }
            .sorted()
    }

    fun deleteLayout(context: Context, name: String) {
        prefs(context).edit().remove("$KEY_CUSTOM_PREFIX$name").commit()
    }
}
