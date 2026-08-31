# Server — VLESS + Reality (sing-box)

Single-user sing-box server for the owner's own devices. It terminates a VLESS
inbound protected by Reality and forwards everything out via `direct`. No
multi-user management, no traffic accounting, no web panel.

## Threat model

- The adversary is an ISP-level DPI box that classifies and blocks MTProto by
  signature. It is passive-plus-active: it can fingerprint flows and it can
  actively probe a suspicious IP:port.
- Reality makes the handshake indistinguishable from a real TLS session to a
  genuine third-party site (`__DEST_SNI__`): the ClientHello, certificate chain
  and TLS records all belong to that site, because the server relays the
  handshake to it.
- An active prober that connects without the correct short ID and public key is
  transparently proxied to the real destination site and sees exactly that
  site's normal response. There is nothing anomalous to flag.
- No domain and no certificate are needed — the borrowed identity is the dest
  site's. Nothing about this server is registered anywhere.
- Out of scope: a state actor correlating traffic volume/timing, endpoint
  compromise, or the VPS provider itself. Choose a dest site that is popular,
  TLS 1.3 + HTTP/2 capable, and not itself blocked from the client's network.

## Deploy

Nothing is deployed yet — **deployment is PENDING SSH credentials for the VPS.**
Once they exist:

1. Provision a fresh Ubuntu 22.04+/Debian 12+ VPS, then as root:
   ```
   ./scripts/provision.sh
   ```
   It installs sing-box, generates the Reality keypair, UUID and short ID,
   renders `config/sing-box.server.json.tmpl` into the live config, opens the
   listen port, and enables the systemd unit.
2. Build the client URI on the server:
   ```
   ./scripts/build-client-uri.sh
   ```
   It prints the `vless://` URI plus a terminal QR code.
3. Scan the QR with the Android app (`android/`) or paste the URI into its
   import field.
4. Verify: `systemctl status sing-box` and `journalctl -u sing-box -f`.

## Secrets

The Reality private key, UUID and short ID are generated **on the server** and
written only to `server/.secrets/`, which is gitignored. Nothing in
`config/sing-box.server.json.tmpl` is real — it contains only `__PLACEHOLDER__`
tokens that `provision.sh` substitutes. Never paste a real key into this repo,
a commit message, or an issue.

Note: `listen_port` in the template is an unquoted placeholder, so the `.tmpl`
is deliberately not valid JSON until rendered.
