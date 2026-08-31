#!/usr/bin/env bash
#
# gen-short-id.sh — generate a Reality short_id (even-length lowercase hex).
#
set -euo pipefail

SCRIPT_NAME="$(basename "$0")"
DEFAULT_HEX_CHARS=8

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
Usage: ${SCRIPT_NAME} [-n HEX_CHARS] [-o SECRETS_DIR] [-f] [-h]

Generates a Reality short_id: lowercase hex, even length, 2..32 characters
(1..16 bytes). Prints it to stdout.

  -n HEX_CHARS    Length in hex characters. Must be even, 2..32.
                  Default: ${DEFAULT_HEX_CHARS}.
  -o SECRETS_DIR  Also write it to SECRETS_DIR/short_id (mode 600, dir 700).
  -f              Overwrite an existing SECRETS_DIR/short_id.
                  WITHOUT -f an existing short_id is REUSED, not rotated.
  -h              Show this help.

The short_id is a shared secret between server and client. Rotating it breaks
every already-configured client.
EOF
}

HEX_CHARS="${DEFAULT_HEX_CHARS}"
SECRETS_DIR=""
FORCE=0

while getopts ":n:o:fh" opt; do
  case "${opt}" in
    n) HEX_CHARS="${OPTARG}" ;;
    o) SECRETS_DIR="${OPTARG}" ;;
    f) FORCE=1 ;;
    h) usage; exit 0 ;;
    :) die "option -${OPTARG} requires an argument (see -h)" ;;
    *) die "unknown option -${OPTARG} (see -h)" ;;
  esac
done
shift $(( OPTIND - 1 ))

[[ "${HEX_CHARS}" =~ ^[0-9]+$ ]] || die "-n must be a number, got '${HEX_CHARS}'"
(( HEX_CHARS >= 2 && HEX_CHARS <= 32 )) || die "-n must be within 2..32 (1..16 bytes)"
(( HEX_CHARS % 2 == 0 )) || die "-n must be even — short_id is a byte string in hex"

NUM_BYTES=$(( HEX_CHARS / 2 ))

if [[ -n "${SECRETS_DIR}" && "${FORCE}" -eq 0 && -s "${SECRETS_DIR}/short_id" ]]; then
  SHORT_ID="$(tr -d '[:space:]' <"${SECRETS_DIR}/short_id")"
  if [[ "${SHORT_ID}" =~ ^[0-9a-f]{2,32}$ ]] && (( ${#SHORT_ID} % 2 == 0 )); then
    ok "reusing existing short_id in ${SECRETS_DIR} (use -f to rotate)"
    printf '%s\n' "${SHORT_ID}"
    exit 0
  fi
  die "${SECRETS_DIR}/short_id exists but is not valid hex — inspect it manually"
fi

gen_hex() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex "${NUM_BYTES}" 2>/dev/null && return 0
  fi
  # Fallback for a box without openssl: read raw bytes and hex them.
  if command -v xxd >/dev/null 2>&1; then
    head -c "${NUM_BYTES}" /dev/urandom | xxd -p -c 32 && return 0
  fi
  if command -v od >/dev/null 2>&1; then
    od -An -tx1 -N "${NUM_BYTES}" /dev/urandom | tr -d ' \n' && printf '\n' && return 0
  fi
  return 1
}

SHORT_ID="$(gen_hex | tr -d '[:space:]' | tr 'A-F' 'a-f')" \
  || die "no working entropy source (need openssl, xxd or od plus /dev/urandom)"

[[ "${SHORT_ID}" =~ ^[0-9a-f]+$ && ${#SHORT_ID} -eq ${HEX_CHARS} ]] \
  || die "generated short_id is malformed: '${SHORT_ID}'"

if [[ -n "${SECRETS_DIR}" ]]; then
  if [[ "${FORCE}" -eq 1 && -s "${SECRETS_DIR}/short_id" ]]; then
    warn "rotating short_id — every already-configured client will break"
  fi
  mkdir -p "${SECRETS_DIR}"
  chmod 700 "${SECRETS_DIR}"
  ( umask 077; printf '%s\n' "${SHORT_ID}" >"${SECRETS_DIR}/short_id" )
  chmod 600 "${SECRETS_DIR}/short_id"
  ok "wrote ${SECRETS_DIR}/short_id"
fi

printf '%s\n' "${SHORT_ID}"
