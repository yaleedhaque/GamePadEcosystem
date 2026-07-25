package com.gamepad.controller.sensors

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.util.Log
import kotlin.math.abs

/**
 * Reads gyroscope and accelerometer at SENSOR_DELAY_GAME rate.
 * Provides both raw IMU data and processed motion-axis values.
 */
class SensorInputManager(context: Context) : SensorEventListener {

    companion object {
        private const val TAG = "SensorInputManager"
        private const val DEAD_ZONE = 0.02f
        private const val GRAVITY = 9.8f
    }

    private val sensorManager = context.getSystemService(Context.SENSOR_SERVICE) as SensorManager
    private val gyroscope = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE)
    private val accelerometer = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER)

    private val gyroData = FloatArray(3)
    private val accelData = FloatArray(3)

    val gyroX: Float get() = gyroData[0]
    val gyroY: Float get() = gyroData[1]
    val gyroZ: Float get() = gyroData[2]
    val accelX: Float get() = accelData[0]
    val accelY: Float get() = accelData[1]
    val accelZ: Float get() = accelData[2]

    var sensitivity = 1.0f
    var isRunning = false
        private set

    fun start() {
        if (isRunning) return
        gyroscope?.let {
            sensorManager.registerListener(this, it, SensorManager.SENSOR_DELAY_GAME)
            Log.d(TAG, "Gyroscope registered")
        } ?: Log.w(TAG, "Gyroscope unavailable")
        accelerometer?.let {
            sensorManager.registerListener(this, it, SensorManager.SENSOR_DELAY_GAME)
            Log.d(TAG, "Accelerometer registered")
        } ?: Log.w(TAG, "Accelerometer unavailable")
        isRunning = true
    }

    fun stop() {
        sensorManager.unregisterListener(this)
        isRunning = false
    }

    /**
     * Tilt-based axis for left stick: pitch → Y, roll → X.
     * Returns values in -1.0..1.0 range.
     */
    fun getMotionAxis(): Pair<Float, Float> {
        val x = if (abs(accelData[0]) < DEAD_ZONE) 0f else accelData[0]
        val y = if (abs(accelData[1]) < DEAD_ZONE) 0f else accelData[1]
        return Pair(
            (x / GRAVITY * sensitivity).coerceIn(-1f, 1f),
            (y / GRAVITY * sensitivity).coerceIn(-1f, 1f)
        )
    }

    /**
     * Gyro angular velocity for precision aiming.
     */
    fun getGyroDelta(): Triple<Float, Float, Float> {
        fun applyDeadZone(v: Float) = if (abs(v) < DEAD_ZONE) 0f else v * sensitivity
        return Triple(
            applyDeadZone(gyroData[0]),
            applyDeadZone(gyroData[1]),
            applyDeadZone(gyroData[2])
        )
    }

    fun isGyroAvailable(): Boolean = gyroscope != null
    fun isAccelAvailable(): Boolean = accelerometer != null

    override fun onSensorChanged(event: SensorEvent) {
        when (event.sensor.type) {
            Sensor.TYPE_GYROSCOPE -> System.arraycopy(event.values, 0, gyroData, 0, 3)
            Sensor.TYPE_ACCELEROMETER -> System.arraycopy(event.values, 0, accelData, 0, 3)
        }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) {}
}
