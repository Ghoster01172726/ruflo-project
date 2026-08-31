#!/usr/bin/env bash
#
# check-reality-dest.sh — vet candidate Reality handshake destinations from
# this VPS and rank them. A usable dest must terminate TLS 1.3 with X25519 and
# advertise HTTP/2 over ALPN, present a real (chained, verifiable) certificate,
# and be close enough that borrowing its handshake is not latency-obvious.
#
set -euo pipefail

SCRIPT_NAME="$(basename "$0")"
TIMEOUT_SECS=8

# Defaults: large, TLS1.3+H2, served from CDN PoPs in/near most regions,
# not blocked by the ISPs that block MTProto, and not owned by any VPS host
# (so the dest never resolves back to our own provider's network).
DEFAULT_CANDIDATES=(
  "www.microsoft.com"
  "www.apple.com"
  "www.icloud.com"
  "dl.google.com"
  "www.samsung.com"
  "www.nvidia.com"
  "addons.mozilla.org"
  "www.tesla.com"
)

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
Usage: ${SCRIPT_NAME} [-q] [-t SECONDS] [-h] [host ...]

Probes each candidate host on :443 from THIS machine and prints a ranked table.
With no hosts given, the built-in candidate list is used:
  ${DEFAULT_CANDIDATES[*]}

Per-host checks:
  dns    hostname resolves
  tcp    :443 accepts a connection
  tls13  TLS 1.3 negotiated
  x25519 TLS 1.3 key exchange group is X25519 (Reality requires it)
  h2     ALPN negotiates h2
  cert   verifiable chain of >=2 certs whose leaf actually covers the hostname
  rtt    handshake round-trip in ms (lower == closer to this VPS)

Options:
  -q            Quiet: print only the single best qualifying hostname on stdout.
  -t SECONDS    Per-probe timeout (default ${TIMEOUT_SECS}).
  -h            Show this help.

Exit status: 0 if at least one host qualifies, 1 if none do.
Run this ON THE VPS — results from anywhere else describe the wrong network.
EOF
}

QUIET=0
while getopts ":qt:h" opt; do
  case "${opt}" in
    q) QUIET=1 ;;
    t) TIMEOUT_SECS="${OPTARG}" ;;
    h) usage; exit 0 ;;
    :) die "option -${OPTARG} requires an argument (see -h)" ;;
    *) die "unknown option -${OPTARG} (see -h)" ;;
  esac
done
shift $(( OPTIND - 1 ))

[[ "${TIMEOUT_SECS}" =~ ^[0-9]+$ ]] && (( TIMEOUT_SECS > 0 )) \
  || die "-t must be a positive integer"

CANDIDATES=( "$@" )
(( ${#CANDIDATES[@]} )) || CANDIDATES=( "${DEFAULT_CANDIDATES[@]}" )

command -v openssl >/dev/null 2>&1 || die "openssl is required"
command -v timeout >/dev/null 2>&1 || die "coreutils 'timeout' is required"

resolve_host() {
  local h="$1"
  if command -v getent >/dev/null 2>&1; then
    getent ahostsv4 "${h}" 2>/dev/null | awk 'NR==1{print $1}' && return 0
  fi
  if command -v dig >/dev/null 2>&1; then
    dig +short +time=3 "${h}" A 2>/dev/null | grep -Em1 '^[0-9.]+$' && return 0
  fi
  return 1
}

now_ms() {
  local ns
  ns="$(date +%s%N 2>/dev/null || true)"
  if [[ "${ns}" =~ ^[0-9]{10,}$ ]]; then printf '%s' $(( ns / 1000000 )); else printf '0'; fi
}

RESULTS=()

probe_host() {
  local host="$1"
  local ip="-" dns=0 tcp=0 tls13=0 x25519=0 h2=0 cert=0 rtt=9999 score=0
  local out='' group='-' certs=0 verify='' subj=''

  if ip="$(resolve_host "${host}")" && [[ -n "${ip}" ]]; then dns=1; else ip="-"; fi

  if (( dns )); then
    local t0 t1
    t0="$(now_ms)"
    out="$( timeout "${TIMEOUT_SECS}" openssl s_client \
              -connect "${host}:443" -servername "${host}" \
              -tls1_3 -alpn h2 -showcerts -verify_hostname "${host}" \
              </dev/null 2>&1 || true )"
    t1="$(now_ms)"
    (( t1 > t0 )) && rtt=$(( t1 - t0 ))

    grep -q 'CONNECTED(' <<<"${out}" && tcp=1
    grep -qE 'Protocol[[:space:]]*:[[:space:]]*TLSv1\.3' <<<"${out}" && tls13=1
    grep -qE 'ALPN protocol[[:space:]]*:[[:space:]]*h2[[:space:]]*$' <<<"${out}" && h2=1

    group="$(grep -oE 'Negotiated TLS1\.3 group:[[:space:]]*[A-Za-z0-9_+-]+' <<<"${out}" \
             | head -n1 | sed -E 's/.*:[[:space:]]*//')"
    if [[ "${group}" == X25519* ]]; then
      x25519=1
    elif [[ -z "${group}" && "${tls13}" -eq 1 ]]; then
      # Older openssl does not report the group; force it and see if it holds.
      if timeout "${TIMEOUT_SECS}" openssl s_client -connect "${host}:443" \
           -servername "${host}" -tls1_3 -groups X25519 </dev/null 2>&1 \
           | grep -qE 'Protocol[[:space:]]*:[[:space:]]*TLSv1\.3'; then
        x25519=1
        group="X25519(forced)"
      fi
    fi
    [[ -n "${group}" ]] || group="-"

    certs="$(grep -c -- '-----BEGIN CERTIFICATE-----' <<<"${out}" || true)"
    verify="$(grep -oE 'Verify return code: [0-9]+' <<<"${out}" | head -n1 | grep -oE '[0-9]+$' || true)"
    subj="$(grep -m1 -E '^(subject=|[[:space:]]*0 s:)' <<<"${out}" || true)"
    # -verify_hostname above makes "Verify return code: 0" mean the leaf really
    # covers this hostname, so a wildcard-of-nothing or self-signed leaf fails
    # here. Requiring >=2 certs additionally rules out an unchained leaf: no
    # site a browser actually visits presents one.
    if [[ "${certs:-0}" -ge 2 && "${verify:-1}" == "0" && -n "${subj}" ]]; then
      cert=1
    fi
  fi

  score=$(( dns + tcp + 2*tls13 + x25519 + 2*h2 + cert ))
  local qualifies=0
  (( dns && tcp && tls13 && x25519 && h2 && cert )) && qualifies=1

  RESULTS+=( "${qualifies}|${score}|${rtt}|${host}|${ip}|${dns}|${tcp}|${tls13}|${x25519}|${h2}|${cert}|${group}" )
}

mark() { (( $1 )) && printf 'yes' || printf 'NO'; }

(( QUIET )) || log "probing ${#CANDIDATES[@]} candidate dest host(s) on :443"
for h in "${CANDIDATES[@]}"; do
  (( QUIET )) || log "  ${h}"
  probe_host "${h}"
done

# Rank: qualifying first, then higher score, then lower handshake RTT.
mapfile -t SORTED < <(printf '%s\n' "${RESULTS[@]}" \
  | sort -t'|' -k1,1nr -k2,2nr -k3,3n)

BEST=""
for row in "${SORTED[@]}"; do
  IFS='|' read -r q _score _rtt host _rest <<<"${row}"
  if (( q )); then BEST="${host}"; break; fi
done

if (( QUIET )); then
  [[ -n "${BEST}" ]] || { warn "no candidate dest qualified"; exit 1; }
  printf '%s\n' "${BEST}"
  exit 0
fi

printf '\n%-22s %-16s %-4s %-4s %-6s %-7s %-4s %-5s %-7s %s\n' \
  HOST IP DNS TCP TLS13 X25519 H2 CERT RTT_MS GROUP
printf '%.0s-' {1..104}; printf '\n'

for row in "${SORTED[@]}"; do
  IFS='|' read -r q _score rtt host ip dns tcp tls13 x25519 h2 cert group <<<"${row}"
  local_rtt="${rtt}"
  [[ "${local_rtt}" == "9999" ]] && local_rtt="-"
  colour="${C_ERR}"; (( q )) && colour="${C_OK}"
  printf '%s%-22s%s %-16s %-4s %-4s %-6s %-7s %-4s %-5s %-7s %s\n' \
    "${colour}" "${host}" "${C_0}" "${ip}" \
    "$(mark "${dns}")" "$(mark "${tcp}")" "$(mark "${tls13}")" \
    "$(mark "${x25519}")" "$(mark "${h2}")" "$(mark "${cert}")" \
    "${local_rtt}" "${group}"
done
printf '\n'

if [[ -z "${BEST}" ]]; then
  warn "no candidate satisfied every requirement"
  warn "pass your own hosts as arguments, or investigate whether this VPS's"
  warn "egress is filtered — a dest that fails here will fail for clients too"
  exit 1
fi

ok "best dest: ${BEST}"
log "use it with: RUFLO_DEST_SNI=${BEST} ./provision.sh"
log "sanity-check by hand that this site is reachable and unblocked from the"
log "CLIENT's network too — the client must be able to plausibly visit it."
