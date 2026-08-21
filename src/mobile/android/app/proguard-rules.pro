# Add project specific ProGuard rules here.
# By default, the flags in this file are appended to flags specified
# in /sdk/tools/proguard/proguard-android.txt

# Gson reads and writes these models by field name for persisted settings,
# upload queues, and .sxcu/.xsdc import/export compatibility.
-keepattributes Signature,*Annotation*
-keep class com.google.gson.reflect.TypeToken { *; }
-keep class * extends com.google.gson.reflect.TypeToken
-keep class com.getsharex.xerahs.mobile.core.domain.** { *; }
-keep class com.getsharex.xerahs.mobile.core.data.CustomUploaderImportPreview { *; }
-keep class com.getsharex.xerahs.mobile.core.data.CustomUploaderImportResult { *; }

# The AWS Android SDK uses reflection and service metadata internally.
-keep class com.amazonaws.** { *; }
-dontwarn com.amazonaws.**
