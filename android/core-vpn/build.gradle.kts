plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.serialization")
}

android {
    namespace = "com.ruflo.tgtunnel.core"
    compileSdk = 35

    defaultConfig {
        minSdk = 26
        consumerProguardFiles("consumer-rules.pro")
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        buildConfig = false
    }
}

dependencies {
    // gomobile-built sing-box binding; `api` so :app can touch libbox types if needed.
    api(group = "", name = "libbox", ext = "aar")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.annotation:annotation:1.9.1")

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.9.0")
}

// Fail with an actionable message instead of an opaque "cannot resolve :libbox:" error.
val libboxAar = rootProject.layout.projectDirectory.file("libs/libbox.aar").asFile
val checkLibboxAar = tasks.register("checkLibboxAar") {
    outputs.upToDateWhen { libboxAar.isFile }
    doLast {
        if (!libboxAar.isFile) {
            throw GradleException(
                """
                Missing native dependency: ${libboxAar.absolutePath}

                :core-vpn embeds sing-box through its gomobile `libbox` binding. The AAR is a
                build artifact and is intentionally NOT committed to this repository.

                Build it once (needs Go >= 1.22 and gomobile on PATH):

                    ./scripts/build-libbox.sh

                then re-run the Gradle build.
                """.trimIndent()
            )
        }
    }
}

tasks.matching { it.name == "preBuild" }.configureEach { dependsOn(checkLibboxAar) }
