#!/usr/bin/env bash
# ============================================================================
# Cierra los issues DUPLICADOS creados por haber corrido sync-github-issues-roadmap.sh
# dos veces.
#
#   1ª corrida (se conserva):  #24 a #57
#   2ª corrida (duplicados):   #58 a #91   (cada uno es el #n-34 de la 1ª)
#
# SEGURIDAD: antes de cerrar cualquier issue compara su título con el de su original.
# Si NO coinciden exactamente, no lo toca y te avisa. Los que ya están cerrados se saltan.
# Cierra como "not planned" (reversible: se pueden reabrir desde GitHub).
#
# Requisitos: GitHub CLI autenticado (gh auth login), correr dentro del repo.
# ============================================================================
set -u

OFFSET=34
FIRST=58
LAST=91
closed=0; skipped=0; mismatch=0; errors=0

# Lee un campo de un issue. Reintenta 3 veces (fallos transitorios de red/API) y, si sigue
# fallando, MUESTRA el error real de gh en vez de ocultarlo.
get () {
  local out try
  for try in 1 2 3; do
    if out=$(gh issue view "$1" --json "$2" --jq ".$2" 2>&1); then printf '%s' "$out"; return 0; fi
    sleep $((try * 2))
  done
  echo "   error de gh al leer #$1: $out" >&2
  return 1
}

for n in $(seq "$FIRST" "$LAST"); do
  orig=$((n - OFFSET))
  if ! t_dup=$(get "$n" title);    then echo "!! #$n no se pudo leer, se omite";    errors=$((errors + 1)); continue; fi
  if ! t_org=$(get "$orig" title); then echo "!! #$orig no se pudo leer, se omite #$n"; errors=$((errors + 1)); continue; fi

  if [ "$t_dup" != "$t_org" ]; then
    echo "!! #$n NO se toca: su título no coincide con #$orig"
    echo "     #$n   : $t_dup"
    echo "     #$orig: $t_org"
    mismatch=$((mismatch + 1)); continue
  fi

  if ! state=$(get "$n" state); then echo "!! #$n no se pudo leer su estado, se omite"; errors=$((errors + 1)); continue; fi
  if [ "$state" != "OPEN" ]; then
    echo "-- #$n ya está cerrado (duplicado de #$orig), se omite"
    skipped=$((skipped + 1)); continue
  fi

  gh issue close "$n" --reason "not planned" \
    --comment "Duplicado de #$orig: el script del roadmap se ejecutó dos veces. Se conserva #$orig." >/dev/null
  echo "OK #$n cerrado como duplicado de #$orig  ($t_dup)"
  closed=$((closed + 1))
done

echo ""
echo "Resumen: cerrados=$closed  ya-cerrados=$skipped  sin-coincidencia=$mismatch  errores=$errors"
if [ "$errors" -gt 0 ]; then
  echo "Hubo errores: vuelve a correr el script (es seguro repetirlo, salta lo ya cerrado)."
  exit 1
fi
