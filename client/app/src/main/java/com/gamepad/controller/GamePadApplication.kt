package com.gamepad.controller

import android.app.Application
import android.os.PowerManager

/**
 * Application-level wake lock to prevent CPU sleep during gameplay.
 */
class GamePadApplication : Application() {

    var wakeLock: PowerManager.WakeLock? = null
        private set

    override fun onCreate() {
        super.onCreate()
        val pm = getSystemService(POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(
            PowerManager.PARTIAL_WAKE_LOCK,
            "GamePadEcosystem::GameplayLock"
        ).apply { acquire(4 * 60 * 60 * 1000L) } // 4 hour max
    }

    override fun onTerminate() {
        wakeLock?.let { if (it.isHeld) it.release() }
        super.onTerminate()
    }
}
