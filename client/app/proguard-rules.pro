# ProGuard Rules — GamePad Controller
-keepattributes Signature, *Annotation*
-keep class java.nio.** { *; }
-keep class java.net.** { *; }
-dontwarn androidx.compose.**
