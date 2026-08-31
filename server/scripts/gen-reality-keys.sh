#!/usr/bin/env bash
#
# gen-reality-keys.sh — generate a Reality X25519 keypair via sing-box.
#
set -euo pipefail

SCRIPT_NAME="$(basename "$0")"

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
Usage: ${SCRIPT_NAME} [-o SECRETS_DIR] [-f] [-h]

Generates a Reality X25519 keypair using \`sing-box generate reality-keypair\`.

  -o SECRETS_DIR  Write the keypair into SECRETS_DIR as 'reality.key' (private)
                  and 'reality.pub' (public), mode 600, directory mode 700.
                  Without -o the keypair is printed to stdout only.
  -f              Overwrite an existing keypair in SECRETS_DIR.
                  WITHOUT -f an existing keypair is REUSED, not rotated.
  -h              Show this help.

Stdout format (always, parseable):
  PrivateKey: <base64url>
  PublicKey: <base64url>

The private key is a credential. Never commit it, paste it into an issue, or
send it over a channel you do not control.
EOF
}

SECRETS_DIR=""
FORCE=0

while getopts ":o:fh" opt; do
  case "${opt}" in
    o) SECRETS_DIR="${OPTARG}" ;;
    f) FORCE=1 ;;
    h) usage; exit 0 ;;
    :) die "option -${OPTARG} requires an argument (see -h)" ;;
    *) die "unknown option -${OPTARG} (see -h)" ;;
  esac
done
shift $(( OPTIND - 1 ))

command -v sing-box >/dev/null 2>&1 \
  || die "sing-box not found in PATH — run provision.sh first"

PRIV=""
PUB=""

if [[ -n "${SECRETS_DIR}" && "${FORCE}" -eq 0 \
      && -s "${SECRETS_DIR}/reality.key" && -s "${SECRETS_DIR}/reality.pub" ]]; then
  PRIV="$(cat "${SECRETS_DIR}/reality.key")"
  PUB="$(cat "${SECRETS_DIR}/reality.pub")"
  ok "reusing existing keypair in ${SECRETS_DIR} (use -f to rotate)"
else
  log "generating Reality keypair"
  RAW="$(sing-box generate reality-keypair 2>/dev/null)" \
    || die "sing-box generate reality-keypair failed"

  # sing-box has printed both "PrivateKey:" and "private_key:" across versions.
  PRIV="$(printf '%s\n' "${RAW}" | grep -iE '^[[:space:]]*private[_ ]?key' \
          | head -n1 | sed -E 's/.*[:=][[:space:]]*//' | tr -d '[:space:]')"
  PUB="$(printf '%s\n' "${RAW}" | grep -iE '^[[:space:]]*public[_ ]?key' \
         | head -n1 | sed -E 's/.*[:=][[:space:]]*//' | tr -d '[:space:]')"

  [[ -n "${PRIV}" && -n "${PUB}" ]] \
    || die "could not parse sing-box keypair output"
  [[ "${PRIV}" =~ ^[A-Za-z0-9_-]{40,50}$ ]] \
    || die "private key does not look like base64url x25519 material"
  [[ "${PUB}" =~ ^[A-Za-z0-9_-]{40,50}$ ]] \
    || die "public key does not look like base64url x25519 material"

  if [[ -n "${SECRETS_DIR}" ]]; then
    if [[ "${FORCE}" -eq 1 && -s "${SECRETS_DIR}/reality.key" ]]; then
      warn "rotating keypair — every already-configured client will break"
    fi
    mkdir -p "${SECRETS_DIR}"
    chmod 700 "${SECRETS_DIR}"
    ( umask 077
      printf '%s\n' "${PRIV}" >"${SECRETS_DIR}/reality.key"
      printf '%s\n' "${PUB}"  >"${SECRETS_DIR}/reality.pub" )
    chmod 600 "${SECRETS_DIR}/reality.key" "${SECRETS_DIR}/reality.pub"
    ok "wrote ${SECRETS_DIR}/reality.key and ${SECRETS_DIR}/reality.pub"
  fi
fi

printf 'PrivateKey: %s\n' "${PRIV}"
printf 'PublicKey: %s\n' "${PUB}"
