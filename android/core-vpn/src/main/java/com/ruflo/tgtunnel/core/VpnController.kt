package com.ruflo.tgtunnel.core

import android.content.Intent
import com.ruflo.tgtunnel.core.config.ConfigProfile
import kotlinx.coroutines.flow.StateFlow

sealed interface VpnState {

    data object Idle : VpnState

    /**
     * System VPN consent is being resolved. A non-null [consentIntent] must be launched by the
     * UI (result delivered back through [VpnController.onConsentResult]); null means consent was
     * already granted and the tunnel is about to start on its own.
     */
    data class Preparing(val consentIntent: Intent?) : VpnState

    data object Connecting : VpnState

    /**
     * [connectedSinceElapsedRealtimeMillis] drives uptime counters (monotonic, immune to clock
     * changes); [connectedSinceEpochMillis] is for wall-clock display only.
     */
    data class Connected(
        val connectedSinceElapsedRealtimeMillis: Long,
        val connectedSinceEpochMillis: Long,
    ) : VpnState

    data object Disconnecting : VpnState

    data class Error(val message: String) : VpnState
}

data class VpnError(
    val message: String,
    val cause: Throwable? = null,
    val atEpochMillis: Long = System.currentTimeMillis(),
)

interface VpnController {

    val state: StateFlow<VpnState>

    /** Survives transitions back to [VpnState.Idle] so the UI can still show what went wrong. */
    val lastError: StateFlow<VpnError?>

    fun connect(profile: ConfigProfile)

    fun disconnect()

    /** Result of the intent carried by [VpnState.Preparing]. */
    fun onConsentResult(granted: Boolean)

    fun clearError()
}

val VpnState.isBusy: Boolean
    get() = this is VpnState.Preparing || this is VpnState.Connecting || this is VpnState.Disconnecting

val VpnState.isConnected: Boolean
    get() = this is VpnState.Connected
