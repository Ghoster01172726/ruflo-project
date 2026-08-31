#!/usr/bin/env bash
#
# provision.sh — bootstrap a fresh Debian/Ubuntu VPS into the single-user
# VLESS+Reality tunnel described in server/README.md. Idempotent: re-running
# never rotates credentials, so an already-configured client keeps working.
#
# Run as root ON THE VPS.
#
set -euo pipefail

SING_BOX_VERSION="${RUFLO_SING_BOX_VERSION:-1.11.15}"

# Optional pinning for the GitHub tarball fallback. Leave empty and the script
# refuses to install an unverified binary unless --allow-unverified-checksum.
# The apt path below is GPG-verified and is preferred; these are only a backup.
EXPECTED_SHA256_amd64="${RUFLO_SINGBOX_SHA256_AMD64:-}"
EXPECTED_SHA256_arm64="${RUFLO_SINGBOX_SHA256_ARM64:-}"

SCRIPT_NAME="$(basename "$0")"
SERVER_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="${SERVER_DIR}/scripts"
TMPL="${SERVER_DIR}/config/sing-box.server.json.tmpl"
SECRETS_DIR="${RUFLO_SECRETS_DIR:-${SERVER_DIR}/.secrets}"

CONFIG_DIR="/etc/sing-box"
CONFIG_PATH="${CONFIG_DIR}/config.json"
SERVICE_NAME="sing-box"
SYSCTL_PATH="/etc/sysctl.d/99-ruflo-tunnel.conf"

LISTEN_PORT="${RUFLO_LISTEN_PORT:-443}"
DEST_SNI="${RUFLO_DEST_SNI:-}"
SERVER_IP="${RUFLO_SERVER_IP:-}"
SKIP_FIREWALL=0
SKIP_SYSCTL=0
ALLOW_UNVERIFIED=0

if [[ -t 1 ]]; then
  C_OK=$'\033[32m'; C_WARN=$'\033[33m'; C_ERR=$'\033[31m'; C_DIM=$'\033[2m'; C_HL=$'\033[1m'; C_0=$'\033[0m'
else
  C_OK=''; C_WARN=''; C_ERR=''; C_DIM=''; C_HL=''; C_0=''
fi

log()  { printf '%s[*]%s %s\n' "$C_DIM" "$C_0" "$*" >&2; }
ok()   { printf '%s[+]%s %s\n' "$C_OK"  "$C_0" "$*" >&2; }
warn() { printf '%s[!]%s %s\n' "$C_WARN" "$C_0" "$*" >&2; }
die()  { printf '%s[x]%s %s\n' "$C_ERR" "$C_0" "$*" >&2; exit 1; }
step() { printf '\n%s==>%s %s%s%s\n' "$C_OK" "$C_0" "$C_HL" "$*" "$C_0" >&2; }

usage() {
  cat <<EOF
Usage: ${SCRIPT_NAME} [options]

Bootstraps this machine into a single-user VLESS + Reality sing-box server.
Safe to re-run: existing credentials in ${SECRETS_DIR} are reused, never rotated.

Options:
  --dest-sni HOST     Reality handshake destination. Default: auto-selected by
                      scripts/check-reality-dest.sh.
  --port PORT         Listen port. Default: ${LISTEN_PORT}.
  --server-ip ADDR    Public address advertised to the client. Default: detected.
  --version VER       sing-box version to install. Default: ${SING_BOX_VERSION}.
  --secrets-dir DIR   Credential store. Default: ${SECRETS_DIR}.
  --skip-firewall     Do not touch ufw/nftables/iptables.
  --skip-sysctl       Do not enable BBR / touch sysctl.
  --allow-unverified-checksum
                      Permit the GitHub tarball fallback without a pinned
                      SHA256. Only meaningful if the apt repo is unreachable.
  -h, --help          Show this help.

Environment equivalents: RUFLO_DEST_SNI, RUFLO_LISTEN_PORT, RUFLO_SERVER_IP,
RUFLO_SING_BOX_VERSION, RUFLO_SECRETS_DIR.
EOF
}

while (( $# )); do
  case "$1" in
    --dest-sni)     DEST_SNI="${2:?--dest-sni needs a value}"; shift 2 ;;
    --port)         LISTEN_PORT="${2:?--port needs a value}"; shift 2 ;;
    --server-ip)    SERVER_IP="${2:?--server-ip needs a value}"; shift 2 ;;
    --version)      SING_BOX_VERSION="${2:?--version needs a value}"; shift 2 ;;
    --secrets-dir)  SECRETS_DIR="${2:?--secrets-dir needs a value}"; shift 2 ;;
    --skip-firewall) SKIP_FIREWALL=1; shift ;;
    --skip-sysctl)  SKIP_SYSCTL=1; shift ;;
    --allow-unverified-checksum) ALLOW_UNVERIFIED=1; shift ;;
    -h|--help)      usage; exit 0 ;;
    *)              usage >&2; die "unknown argument: $1" ;;
  esac
done

# ---------------------------------------------------------------- preflight --
step "Preflight"

[[ "${EUID}" -eq 0 ]] || die "must run as root (try: sudo ${SCRIPT_NAME})"
command -v apt-get >/dev/null 2>&1 || die "no apt-get — this script targets Debian/Ubuntu only"
[[ -r /etc/os-release ]] || die "/etc/os-release missing — cannot identify the distro"

# shellcheck disable=SC1091
. /etc/os-release
DISTRO_ID="${ID:-unknown}"
DISTRO_LIKE="${ID_LIKE:-}"
case "${DISTRO_ID}:${DISTRO_LIKE}" in
  debian:*|ubuntu:*|*:*debian*) : ;;
  *) die "unsupported distro '${DISTRO_ID}' — need Debian 12+ or Ubuntu 22.04+" ;;
esac

DPKG_ARCH="$(dpkg --print-architecture)"
case "${DPKG_ARCH}" in
  amd64|arm64) : ;;
  *) die "unsupported architecture '${DPKG_ARCH}' (need amd64 or arm64)" ;;
esac

[[ "${LISTEN_PORT}" =~ ^[0-9]+$ ]] && (( LISTEN_PORT >= 1 && LISTEN_PORT <= 65535 )) \
  || die "--port must be 1..65535, got '${LISTEN_PORT}'"
[[ -r "${TMPL}" ]] || die "template not found: ${TMPL}"

ok "${PRETTY_NAME:-${DISTRO_ID}} / ${DPKG_ARCH} / root"

# ------------------------------------------------------------------- deps ----
step "Dependencies"

export DEBIAN_FRONTEND=noninteractive
APT_UPDATED=0
apt_update_once() { (( APT_UPDATED )) || { apt-get update -qq; APT_UPDATED=1; }; }

MISSING=()
for pkg in curl ca-certificates openssl qrencode jq iproute2; do
  dpkg-query -W -f='${Status}' "${pkg}" 2>/dev/null | grep -q '^install ok installed$' \
    || MISSING+=( "${pkg}" )
done
if (( ${#MISSING[@]} )); then
  log "installing: ${MISSING[*]}"
  apt_update_once
  apt-get install -y -qq "${MISSING[@]}" >/dev/null
fi
ok "dependencies present"

# --------------------------------------------------------------- sing-box ----
step "sing-box ${SING_BOX_VERSION}"

install_from_apt_repo() {
  local keyring=/etc/apt/keyrings/sagernet.asc
  install -d -m 0755 /etc/apt/keyrings
  if [[ ! -s "${keyring}" ]]; then
    curl -fsSL --retry 3 https://sing-box.app/gpg.key -o "${keyring}.tmp" || return 1
    chmod 0644 "${keyring}.tmp"
    mv "${keyring}.tmp" "${keyring}"
  fi
  printf 'deb [arch=%s signed-by=%s] https://deb.sagernet.org * *\n' \
    "${DPKG_ARCH}" "${keyring}" >/etc/apt/sources.list.d/sagernet.list
  apt-get update -qq -o Dir::Etc::sourcelist=sources.list.d/sagernet.list \
    -o Dir::Etc::sourceparts=- -o APT::Get::List-Cleanup=0 || return 1
  APT_UPDATED=0
  apt-get install -y -qq "sing-box=${SING_BOX_VERSION}" >/dev/null 2>&1 && return 0
  warn "version ${SING_BOX_VERSION} not in the repo; installing the repo's current stable"
  apt-get install -y -qq sing-box >/dev/null || return 1
}

install_from_github() {
  local arch="${DPKG_ARCH}" tarball url tmp expected actual
  tarball="sing-box-${SING_BOX_VERSION}-linux-${arch}.tar.gz"
  url="https://github.com/SagerNet/sing-box/releases/download/v${SING_BOX_VERSION}/${tarball}"
  tmp="$(mktemp -d)"
  trap 'rm -rf "${tmp}"' RETURN

  log "downloading ${url}"
  curl -fsSL --retry 3 "${url}" -o "${tmp}/${tarball}" || return 1

  actual="$(sha256sum "${tmp}/${tarball}" | awk '{print $1}')"
  eval "expected=\${EXPECTED_SHA256_${arch}}"
  if [[ -n "${expected}" ]]; then
    [[ "${expected}" == "${actual}" ]] \
      || die "SHA256 mismatch for ${tarball}: expected ${expected}, got ${actual}"
    ok "checksum verified"
  elif (( ALLOW_UNVERIFIED )); then
    warn "no pinned checksum; proceeding on --allow-unverified-checksum"
    warn "sha256(${tarball}) = ${actual}"
  else
    printf '%s\n' "sha256(${tarball}) = ${actual}" >&2
    die "no pinned SHA256 for ${arch}. Verify the hash above against the official
     release page, then set RUFLO_SINGBOX_SHA256_${arch^^}=<hash> and re-run
     (or pass --allow-unverified-checksum to accept it as-is)."
  fi

  tar -xzf "${tmp}/${tarball}" -C "${tmp}"
  install -m 0755 "${tmp}/sing-box-${SING_BOX_VERSION}-linux-${arch}/sing-box" /usr/bin/sing-box
}

INSTALLED_VERSION=""
command -v sing-box >/dev/null 2>&1 \
  && INSTALLED_VERSION="$(sing-box version 2>/dev/null | awk '/version/{print $3; exit}')"

if [[ "${INSTALLED_VERSION}" == "${SING_BOX_VERSION}" ]]; then
  ok "sing-box ${INSTALLED_VERSION} already installed"
else
  [[ -n "${INSTALLED_VERSION}" ]] && log "found sing-box ${INSTALLED_VERSION}, want ${SING_BOX_VERSION}"
  install_from_apt_repo || {
    warn "apt repo install failed — falling back to the pinned GitHub release"
    install_from_github || die "could not install sing-box"
  }
  command -v sing-box >/dev/null 2>&1 || die "sing-box still not on PATH after install"
  ok "installed $(sing-box version 2>/dev/null | head -n1)"
fi

# -------------------------------------------------------------- credentials --
step "Credentials"

mkdir -p "${SECRETS_DIR}"
chmod 700 "${SECRETS_DIR}"

write_secret() {
  ( umask 077; printf '%s\n' "$2" >"${SECRETS_DIR}/$1" )
  chmod 600 "${SECRETS_DIR}/$1"
}

if [[ -s "${SECRETS_DIR}/uuid" ]]; then
  UUID="$(tr -d '[:space:]' <"${SECRETS_DIR}/uuid")"
  ok "reusing existing uuid"
else
  UUID="$(sing-box generate uuid)"
  write_secret uuid "${UUID}"
  ok "generated uuid"
fi
[[ "${UUID}" =~ ^[0-9a-fA-F-]{36}$ ]] || die "uuid in ${SECRETS_DIR} is malformed"

KEYS="$(bash "${SCRIPTS_DIR}/gen-reality-keys.sh" -o "${SECRETS_DIR}")"
REALITY_PRIVATE_KEY="$(awk '/^PrivateKey:/{print $2}' <<<"${KEYS}")"
REALITY_PUBLIC_KEY="$(awk '/^PublicKey:/{print $2}' <<<"${KEYS}")"
[[ -n "${REALITY_PRIVATE_KEY}" && -n "${REALITY_PUBLIC_KEY}" ]] || die "reality keypair unavailable"

SHORT_ID="$(bash "${SCRIPTS_DIR}/gen-short-id.sh" -o "${SECRETS_DIR}")"
[[ "${SHORT_ID}" =~ ^[0-9a-f]{2,32}$ ]] || die "short_id is malformed"

# ------------------------------------------------------------------- dest ----
step "Reality destination"

if [[ -z "${DEST_SNI}" && -s "${SECRETS_DIR}/dest_sni" ]]; then
  DEST_SNI="$(tr -d '[:space:]' <"${SECRETS_DIR}/dest_sni")"
  ok "reusing dest ${DEST_SNI} (override with --dest-sni)"
elif [[ -z "${DEST_SNI}" ]]; then
  log "no dest given — probing candidates"
  DEST_SNI="$(bash "${SCRIPTS_DIR}/check-reality-dest.sh" -q || true)"
  [[ -n "${DEST_SNI}" ]] \
    || die "no candidate dest qualified — run scripts/check-reality-dest.sh and pass --dest-sni"
  ok "selected dest ${DEST_SNI}"
else
  log "verifying ${DEST_SNI}"
  bash "${SCRIPTS_DIR}/check-reality-dest.sh" -q "${DEST_SNI}" >/dev/null \
    || die "${DEST_SNI} failed the Reality dest checks — pick another"
fi
write_secret dest_sni "${DEST_SNI}"
write_secret listen_port "${LISTEN_PORT}"

if [[ -z "${SERVER_IP}" ]]; then
  SERVER_IP="$(curl -4sf --max-time 8 https://api.ipify.org 2>/dev/null || true)"
  [[ -n "${SERVER_IP}" ]] \
    || SERVER_IP="$(ip -4 route get 1.1.1.1 2>/dev/null | awk '{for(i=1;i<=NF;i++) if($i=="src") print $(i+1); exit}')"
fi
[[ -n "${SERVER_IP}" ]] || die "could not determine the public address — pass --server-ip"
write_secret server_ip "${SERVER_IP}"
ok "public address ${SERVER_IP}:${LISTEN_PORT}"

# ----------------------------------------------------------------- config ----
step "Config"

install -d -m 0700 "${CONFIG_DIR}"
EXTRA_JSON="$(find "${CONFIG_DIR}" -maxdepth 1 -name '*.json' ! -name 'config.json' -printf '%f ' 2>/dev/null || true)"
[[ -z "${EXTRA_JSON}" ]] \
  || warn "other JSON in ${CONFIG_DIR} (${EXTRA_JSON}) will be merged by the unit — remove it if unintended"

RENDERED="$(<"${TMPL}")"
RENDERED="${RENDERED//__LISTEN_PORT__/${LISTEN_PORT}}"
RENDERED="${RENDERED//__UUID__/${UUID}}"
RENDERED="${RENDERED//__REALITY_PRIVATE_KEY__/${REALITY_PRIVATE_KEY}}"
RENDERED="${RENDERED//__SHORT_ID__/${SHORT_ID}}"
RENDERED="${RENDERED//__DEST_SNI__/${DEST_SNI}}"

grep -q '__[A-Z_]\+__' <<<"${RENDERED}" \
  && die "template still has unsubstituted placeholders — ${TMPL} changed shape"

STAGED="${CONFIG_DIR}/.config.json.staged"
( umask 077; printf '%s\n' "${RENDERED}" >"${STAGED}" )
chmod 600 "${STAGED}"

jq -e . "${STAGED}" >/dev/null || { rm -f "${STAGED}"; die "rendered config is not valid JSON"; }
sing-box check -c "${STAGED}" || { rm -f "${STAGED}"; die "sing-box rejected the rendered config"; }
ok "config validated"

if [[ -f "${CONFIG_PATH}" ]] && cmp -s "${STAGED}" "${CONFIG_PATH}"; then
  rm -f "${STAGED}"
  CONFIG_CHANGED=0
  ok "config unchanged"
else
  [[ -f "${CONFIG_PATH}" ]] && cp -a "${CONFIG_PATH}" "${CONFIG_PATH}.bak"
  mv "${STAGED}" "${CONFIG_PATH}"
  chmod 600 "${CONFIG_PATH}"
  CONFIG_CHANGED=1
  ok "wrote ${CONFIG_PATH}"
fi

# ---------------------------------------------------------------- service ----
step "Service"

UNIT_PATH="/etc/systemd/system/${SERVICE_NAME}.service"
if [[ ! -f "${UNIT_PATH}" && ! -f "/usr/lib/systemd/system/${SERVICE_NAME}.service" \
      && ! -f "/lib/systemd/system/${SERVICE_NAME}.service" ]]; then
  log "writing ${UNIT_PATH}"
  cat >"${UNIT_PATH}" <<'UNIT'
[Unit]
Description=sing-box service
Documentation=https://sing-box.sagernet.org
After=network.target nss-lookup.target network-online.target

[Service]
Type=simple
CapabilityBoundingSet=CAP_NET_ADMIN CAP_NET_BIND_SERVICE CAP_NET_RAW
AmbientCapabilities=CAP_NET_ADMIN CAP_NET_BIND_SERVICE CAP_NET_RAW
ExecStart=/usr/bin/sing-box -D /var/lib/sing-box -C /etc/sing-box run
ExecReload=/bin/kill -HUP $MAINPID
Restart=on-failure
RestartSec=10s
LimitNOFILE=infinity

[Install]
WantedBy=multi-user.target
UNIT
  chmod 644 "${UNIT_PATH}"
fi

install -d -m 0700 /var/lib/sing-box
systemctl daemon-reload
systemctl enable "${SERVICE_NAME}" >/dev/null 2>&1 || true

if (( CONFIG_CHANGED )) || ! systemctl is-active --quiet "${SERVICE_NAME}"; then
  systemctl restart "${SERVICE_NAME}"
else
  ok "service already running with the current config"
fi

for _ in $(seq 1 15); do
  systemctl is-active --quiet "${SERVICE_NAME}" && break
  sleep 1
done
systemctl is-active --quiet "${SERVICE_NAME}" \
  || { journalctl -u "${SERVICE_NAME}" -n 40 --no-pager >&2 || true
       die "${SERVICE_NAME} failed to start (log above)"; }
ok "${SERVICE_NAME} is active"

# ---------------------------------------------------------------- firewall ---
SSH_PORTS=()
mapfile -t SSH_PORTS < <(awk '/^[[:space:]]*Port[[:space:]]+[0-9]+/{print $2}' /etc/ssh/sshd_config 2>/dev/null || true)
(( ${#SSH_PORTS[@]} )) || SSH_PORTS=( 22 )

if (( SKIP_FIREWALL )); then
  warn "skipping firewall (--skip-firewall) — ensure ${LISTEN_PORT}/tcp is reachable"
else
  step "Firewall"
  if command -v ufw >/dev/null 2>&1; then
    for p in "${SSH_PORTS[@]}"; do ufw allow "${p}/tcp" >/dev/null; done
    ufw allow "${LISTEN_PORT}/tcp" >/dev/null
    if ! ufw status 2>/dev/null | grep -qi '^Status: active'; then
      warn "enabling ufw (SSH ports ${SSH_PORTS[*]} already allowed)"
      ufw --force enable >/dev/null
    fi
    ok "ufw allows ${LISTEN_PORT}/tcp and SSH ${SSH_PORTS[*]}"
  elif command -v nft >/dev/null 2>&1 && nft list table inet filter >/dev/null 2>&1; then
    # Only ADD accepts. Never change the default policy — that is how you get
    # locked out of a box you can only reach over SSH.
    for p in "${LISTEN_PORT}" "${SSH_PORTS[@]}"; do
      nft list chain inet filter input 2>/dev/null | grep -q "dport ${p} accept" \
        || nft add rule inet filter input tcp dport "${p}" accept
    done
    ok "nftables accepts ${LISTEN_PORT}/tcp and SSH ${SSH_PORTS[*]}"
  elif command -v iptables >/dev/null 2>&1; then
    for p in "${LISTEN_PORT}" "${SSH_PORTS[@]}"; do
      iptables -C INPUT -p tcp --dport "${p}" -j ACCEPT 2>/dev/null \
        || iptables -I INPUT -p tcp --dport "${p}" -j ACCEPT
    done
    command -v netfilter-persistent >/dev/null 2>&1 && netfilter-persistent save >/dev/null 2>&1 \
      || warn "iptables rules are not persisted across reboot (install iptables-persistent)"
    ok "iptables accepts ${LISTEN_PORT}/tcp and SSH ${SSH_PORTS[*]}"
  else
    warn "no firewall tool found — assuming the provider's edge filter is open"
  fi
  warn "also open ${LISTEN_PORT}/tcp in your VPS provider's cloud firewall if it has one"
fi

# ------------------------------------------------------------------ sysctl ---
if (( SKIP_SYSCTL )); then
  warn "skipping sysctl tuning (--skip-sysctl)"
else
  step "Kernel tuning"
  if grep -q '\bbbr\b' /proc/sys/net/ipv4/tcp_available_congestion_control 2>/dev/null \
     || modprobe tcp_bbr 2>/dev/null; then
    cat >"${SYSCTL_PATH}" <<'SYSCTL'
# ruflo tunnel — managed by server/provision.sh
net.core.default_qdisc = fq
net.ipv4.tcp_congestion_control = bbr
net.core.rmem_max = 16777216
net.core.wmem_max = 16777216
net.ipv4.tcp_rmem = 4096 87380 16777216
net.ipv4.tcp_wmem = 4096 65536 16777216
net.ipv4.tcp_fastopen = 3
net.ipv4.tcp_mtu_probing = 1
net.ipv4.tcp_slow_start_after_idle = 0
net.core.somaxconn = 8192
net.ipv4.ip_local_port_range = 10240 65535
fs.file-max = 1048576
SYSCTL
    chmod 644 "${SYSCTL_PATH}"
    sysctl --system >/dev/null 2>&1 || warn "sysctl --system reported errors"
    ok "BBR + tuning applied ($(sysctl -n net.ipv4.tcp_congestion_control 2>/dev/null || echo '?'))"
  else
    warn "kernel has no BBR support — leaving congestion control alone"
  fi
fi

# ------------------------------------------------------------------ verify ---
step "Listening check"

LISTENING=0
if command -v ss >/dev/null 2>&1; then
  ss -lntH 2>/dev/null | awk '{print $4}' | grep -qE "[:.]${LISTEN_PORT}\$" && LISTENING=1
fi
(( LISTENING )) \
  || die "nothing is listening on ${LISTEN_PORT} — check: journalctl -u ${SERVICE_NAME} -n 50"
ok "listening on :${LISTEN_PORT}"

# -------------------------------------------------------------------- done ---
step "Done"

cat >&2 <<EOF
  service     ${SERVICE_NAME} (systemctl status ${SERVICE_NAME})
  config      ${CONFIG_PATH}
  secrets     ${SECRETS_DIR}
  endpoint    ${SERVER_IP}:${LISTEN_PORT}
  dest sni    ${DEST_SNI}
  sing-box    $(sing-box version 2>/dev/null | awk '/version/{print $3; exit}')

Next:
  1. ${SCRIPTS_DIR}/build-client-uri.sh      # prints the client URI + QR
  2. scan the QR with the Android app, or paste the URI into its import field
  3. journalctl -u ${SERVICE_NAME} -f        # watch the first connection

Re-running this script is safe — it reuses the credentials in ${SECRETS_DIR}
and will not rotate them.
EOF

warn "${SECRETS_DIR} holds the Reality private key, UUID and short_id. It is"
warn "gitignored. Never commit it, and never paste its contents anywhere."
