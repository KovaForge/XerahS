plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

val releaseSigningStoreFile = providers.environmentVariable("XERAHS_ANDROID_UPLOAD_STORE_FILE").orNull
val releaseSigningStorePassword = providers.environmentVariable("XERAHS_ANDROID_UPLOAD_STORE_PASSWORD").orNull
val releaseSigningKeyAlias = providers.environmentVariable("XERAHS_ANDROID_UPLOAD_KEY_ALIAS").orNull
val releaseSigningKeyPassword = providers.environmentVariable("XERAHS_ANDROID_UPLOAD_KEY_PASSWORD").orNull
val hasReleaseSigningConfig =
    !releaseSigningStoreFile.isNullOrBlank() &&
        !releaseSigningStorePassword.isNullOrBlank() &&
        !releaseSigningKeyAlias.isNullOrBlank() &&
        !releaseSigningKeyPassword.isNullOrBlank()

android {
    namespace = "com.getsharex.xerahs.mobile"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.xerahs.xerahs.mobile"
        minSdk = 26
        targetSdk = 35
        versionCode = 22170
        versionName = "0.22.170"
    }

    signingConfigs {
        if (hasReleaseSigningConfig) {
            create("release") {
                storeFile = file(releaseSigningStoreFile!!)
                storePassword = releaseSigningStorePassword
                keyAlias = releaseSigningKeyAlias
                keyPassword = releaseSigningKeyPassword
            }
        }
    }

    buildTypes {
        release {
            if (hasReleaseSigningConfig) {
                signingConfig = signingConfigs.getByName("release")
            }
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
    }

    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.8"
    }
}

dependencies {
    implementation(project(":core:common"))
    implementation(project(":core:data"))
    implementation(project(":core:domain"))
    implementation(project(":feature:upload"))
    implementation(project(":feature:settings"))
    implementation(project(":feature:history"))

    implementation(libs.core.ktx)
    implementation(libs.activity.compose)
    implementation(libs.lifecycle.runtime.compose)
    implementation(libs.lifecycle.viewmodel.compose)
    implementation(libs.navigation.compose)
    implementation(libs.gson)
    implementation(libs.okhttp)

    implementation(platform(libs.compose.bom))
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.graphics)
    implementation(libs.compose.ui.tooling.preview)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons)
    implementation(libs.compose.foundation)

    debugImplementation(libs.compose.ui.tooling)
}
