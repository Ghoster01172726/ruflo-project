# Android — tgtunnel

A personal VPN client that runs sing-box in-process through its gomobile
`libbox` binding and routes traffic over the VLESS+Reality server in `server/`.

Package: `com.ruflo.tgtunnel` · minSdk 26 · targetSdk/compileSdk 35 · JVM 17.

## Modules

| Module     | What it holds |
|------------|---------------|
| `core-vpn` | Pure Kotlin Android library. `VpnController` / `VpnState` contract, `ConfigProfile`, the `VpnService` implementation, sing-box client-config generation, `vless://` URI parsing. **No Compose.** |
| `app`      | Compose UI (Material 3): connect screen, profile import (paste URI or scan QR), settings (full-tunnel vs per-app allowlist). Depends on `core-vpn`. |
| `libs/`    | Drop location for `libbox.aar`. Gitignored — build artifact, never committed. |

`core-vpn` exposes `libbox` with `api(...)`, so `app` sees it transitively.

## Prerequisite: build libbox.aar

**The project will not build until `android/libs/libbox.aar` exists.** It is a
Go artifact, so it is not in the repo and no Maven coordinate provides it.

Requires Go >= 1.22, the Android NDK, and `gomobile` on `PATH`. From the repo
root:

```
./scripts/build-libbox.sh
```

It clones sing-box at a pinned tag, builds the `libbox` package with
`gomobile bind` for `arm64-v8a`/`armeabi-v7a`, and copies the result to
`android/libs/libbox.aar`. Missing the AAR fails the build early with an
explicit message from the `:core-vpn:checkLibboxAar` task rather than an
unresolved-dependency stack trace.

Then, as usual:

```
./gradlew :app:assembleDebug
```

## Importing a profile

`server/scripts/build-client-uri.sh` prints a URI of the form:

```
vless://<uuid>@<host>:<port>?type=tcp&security=reality&pbk=<public-key>&sid=<short-id>&sni=<dest-sni>&fp=chrome&flow=xtls-rprx-vision#<label>
```

In the app, either scan the QR code that script renders or paste the URI into
the import field. It is parsed into a `ConfigProfile`; every query parameter
above maps 1:1 to a field on that data class.

## Verification

Unit tests and an emulator only prove the tunnel establishes and carries
traffic. They cannot prove DPI evasion. **Real verification requires a physical
device on the actually-blocked ISP network:** confirm Telegram fails with the
tunnel off, succeeds with it on, and that the connection survives several
minutes of sustained use without being reset.
