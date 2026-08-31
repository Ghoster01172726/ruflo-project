package com.ruflo.tgtunnel.core.config

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/**
 * Which apps are routed through the tunnel.
 *
 * ALLOWLIST maps to VpnService.Builder.addAllowedApplication — an empty
 * [ConfigProfile.allowedPackages] under ALLOWLIST would tunnel nothing, so callers
 * must treat that as an invalid profile.
 */
@Serializable
enum class TunnelMode {
    @SerialName("full")
    FULL,

    @SerialName("allowlist")
    ALLOWLIST,
}

@Serializable
data class ConfigProfile(
    val uuid: String,
    val serverAddress: String,
    val serverPort: Int,
    val publicKey: String,
    val shortId: String,
    val sni: String,
    val flow: String = DEFAULT_FLOW,
    val fingerprint: String = DEFAULT_FINGERPRINT,
    val label: String = "",
    val tunnelMode: TunnelMode = TunnelMode.FULL,
    val allowedPackages: Set<String> = emptySet(),
) {
    val displayName: String
        get() = label.ifBlank { "$serverAddress:$serverPort" }

    companion object {
        const val DEFAULT_FLOW: String = "xtls-rprx-vision"
        const val DEFAULT_FINGERPRINT: String = "chrome"
        const val TELEGRAM_PACKAGE: String = "org.telegram.messenger"
    }
}
