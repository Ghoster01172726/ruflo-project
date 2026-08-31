#!/usr/bin/env bash
#
# build-client-uri.sh — render the vless://…reality URI for the owner's client
# from the values provision.sh deposited in server/.secrets/, and show a QR.
#
set -euo pipefail

SCRIPT_NAME="$(basename "$0")"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_SECRETS_DIR="$(cd -- "${SCRIPT_DIR}/.." && pwd)/.secrets"
DEFAULT_LABEL="ruflo-tunnel"

if [[ -t 1 ]]; then
  C_OK=$'\033[32m'; C_WARN=$'\033[33m'; C_ERR=$'\033[31m'; C_DIM=$'\033[2m'; C_0=$'\033[0m'
else
  C_OK=''; C_WARN=''; C_ERR=''; C_DIM=''; C_0=''
fi

log()  { printf '%s[*]%s %s\n' "$C_DIM" "$C_0" "$*" >&2; }
ok()   { printf '%s[+]%s %s\n' "$C_OK"  "$C_0" "$*" >&2; }
warn() { printf '%s[!]%s %s\n' "$C_WARN" "$C_0" "$*" >&2; }
die()  { printf '%s[x]%s %s\n' "$C_ERR" "$C_0" "$*" >&2; exit 1; }

usage() {
  cat <<EOF
Usage: ${SCRIPT_NAME} [-d SECRETS_DIR] [-l LABEL] [-a ADDRESS] [-p] [-n] [-h]

Reads the deployed tunnel parameters and prints the client import URI plus a
terminal QR code.

  -d SECRETS_DIR  Where provision.sh stored the values.
                  Default: ${DEFAULT_SECRETS_DIR}
  -l LABEL        Fragment/label on the URI. Default: ${DEFAULT_LABEL}
  -a ADDRESS      Override the server address (IP or hostname).
  -p              Also write a PNG QR to SECRETS_DIR/client-qr.png (mode 600).
  -n              Do not render a QR code, print the URI only.
  -h              Show this help.

Files consumed from SECRETS_DIR:
  uuid  reality.pub  short_id  dest_sni  listen_port  server_ip

The resulting URI embeds the UUID and short_id. Anyone holding it can use the
tunnel. Treat it exactly like a password.
EOF
}

SECRETS_DIR="${RUFLO_SECRETS_DIR:-${DEFAULT_SECRETS_DIR}}"
LABEL="${DEFAULT_LABEL}"
ADDRESS_OVERRIDE=""
WRITE_PNG=0
NO_QR=0

while getopts ":d:l:a:pnh" opt; do
  case "${opt}" in
    d) SECRETS_DIR="${OPTARG}" ;;
    l) LABEL="${OPTARG}" ;;
    a) ADDRESS_OVERRIDE="${OPTARG}" ;;
    p) WRITE_PNG=1 ;;
    n) NO_QR=1 ;;
    h) usage; exit 0 ;;
    :) die "option -${OPTARG} requires an argument (see -h)" ;;
    *) die "unknown option -${OPTARG} (see -h)" ;;
  esac
done
shift $(( OPTIND - 1 ))

[[ -d "${SECRETS_DIR}" ]] \
  || die "${SECRETS_DIR} does not exist — run provision.sh on the server first"

read_secret() {
  local name="$1" path="${SECRETS_DIR}/$1"
  [[ -s "${path}" ]] || die "missing ${path} — provision.sh has not run to completion"
  tr -d '[:space:]' <"${path}"
}

UUID="$(read_secret uuid)"
PUBKEY="$(read_secret reality.pub)"
SHORT_ID="$(read_secret short_id)"
DEST_SNI="$(read_secret dest_sni)"
LISTEN_PORT="$(read_secret listen_port)"

if [[ -n "${ADDRESS_OVERRIDE}" ]]; then
  ADDRESS="${ADDRESS_OVERRIDE}"
else
  ADDRESS="$(read_secret server_ip)"
fi

[[ "${UUID}" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]] \
  || die "uuid is malformed"
[[ "${PUBKEY}" =~ ^[A-Za-z0-9_-]{40,50}$ ]] \
  || die "reality.pub is malformed"
[[ "${SHORT_ID}" =~ ^[0-9a-f]{2,32}$ ]] && (( ${#SHORT_ID} % 2 == 0 )) \
  || die "short_id must be even-length lowercase hex"
[[ "${DEST_SNI}" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]] \
  || die "dest_sni is malformed"
[[ "${LISTEN_PORT}" =~ ^[0-9]+$ ]] && (( LISTEN_PORT >= 1 && LISTEN_PORT <= 65535 )) \
  || die "listen_port is out of range"
[[ -n "${ADDRESS}" ]] || die "server address is empty — pass -a ADDRESS"

# IPv6 literals must be bracketed in a URI authority.
if [[ "${ADDRESS}" == *:* && "${ADDRESS}" != \[*\] ]]; then
  ADDRESS="[${ADDRESS}]"
fi

urlencode() {
  local s="$1" i c out=''
  for (( i = 0; i < ${#s}; i++ )); do
    c="${s:i:1}"
    case "${c}" in
      [a-zA-Z0-9.~_-]) out+="${c}" ;;
      *) printf -v c '%%%02X' "'${c}"; out+="${c}" ;;
    esac
  done
  printf '%s' "${out}"
}

LABEL_ENC="$(urlencode "${LABEL}")"

URI="vless://${UUID}@${ADDRESS}:${LISTEN_PORT}"
URI+="?encryption=none&security=reality&flow=xtls-rprx-vision"
URI+="&sni=$(urlencode "${DEST_SNI}")&fp=chrome"
URI+="&pbk=$(urlencode "${PUBKEY}")&sid=$(urlencode "${SHORT_ID}")&type=tcp"
URI+="#${LABEL_ENC}"

printf '\n%s\n\n' "${URI}"

if (( ! NO_QR )); then
  if command -v qrencode >/dev/null 2>&1; then
    qrencode -t ANSIUTF8 -m 1 -- "${URI}"
    if (( WRITE_PNG )); then
      PNG="${SECRETS_DIR}/client-qr.png"
      ( umask 077; qrencode -t PNG -s 6 -m 2 -o "${PNG}" -- "${URI}" )
      chmod 600 "${PNG}"
      ok "wrote ${PNG}"
      warn "that PNG is a credential — delete it once the phone has scanned it"
    fi
  else
    warn "qrencode not installed; showing the URI only (apt install qrencode)"
  fi
fi

printf '\n'
warn "This URI is a CREDENTIAL. It contains the UUID and short_id that grant"
warn "full access to the tunnel. Do not paste it into chats, issues, pastebins,"
warn "screenshots, or commits. Transfer it to your own device only."
log "import it in the Android app, or scan the QR above"
