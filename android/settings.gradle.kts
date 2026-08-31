pluginManagement {
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.PREFER_SETTINGS)
    repositories {
        google()
        mavenCentral()
        // Holds the gomobile-built libbox.aar produced by scripts/build-libbox.sh.
        flatDir { dirs("$rootDir/libs") }
    }
}

rootProject.name = "tgtunnel"

include(":app")
include(":core-vpn")
