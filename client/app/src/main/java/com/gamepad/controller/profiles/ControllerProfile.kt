package com.gamepad.controller.profiles

/**
 * Controller layout profiles for different game genres.
 * Each profile controls which UI elements are visible.
 */
enum class ControllerProfile(
    val displayName: String,
    val showSticks: Boolean,
    val showFaceButtons: Boolean,
    val showTriggers: Boolean,
    val showBumpers: Boolean,
    val showCenterButtons: Boolean,
    val showDpad: Boolean,
    val showMotion: Boolean,
    val description: String
) {
    XBOX_STANDARD(
        displayName = "Xbox",
        showSticks = true,
        showFaceButtons = true,
        showTriggers = true,
        showBumpers = true,
        showCenterButtons = true,
        showDpad = true,
        showMotion = false,
        description = "Full Xbox 360 layout"
    ),
    RETRO_DPAD(
        displayName = "Retro",
        showSticks = false,
        showFaceButtons = true,
        showTriggers = false,
        showBumpers = false,
        showCenterButtons = true,
        showDpad = true,
        showMotion = false,
        description = "D-Pad + face buttons only"
    ),
    FPS_MOTION(
        displayName = "FPS",
        showSticks = true,
        showFaceButtons = true,
        showTriggers = true,
        showBumpers = true,
        showCenterButtons = true,
        showDpad = false,
        showMotion = true,
        description = "Right stick + gyro aiming"
    ),
    RACING(
        displayName = "Race",
        showSticks = true,
        showFaceButtons = true,
        showTriggers = true,
        showBumpers = true,
        showCenterButtons = false,
        showDpad = false,
        showMotion = true,
        description = "Triggers + motion steering"
    ),
    CUSTOM(
        displayName = "Custom",
        showSticks = true,
        showFaceButtons = true,
        showTriggers = true,
        showBumpers = true,
        showCenterButtons = true,
        showDpad = true,
        showMotion = true,
        description = "Everything enabled"
    );

    companion object {
        fun next(current: ControllerProfile): ControllerProfile {
            val values = entries
            return values[(current.ordinal + 1) % values.size]
        }
    }
}
