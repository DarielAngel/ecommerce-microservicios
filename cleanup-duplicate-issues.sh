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
closed=0; skipped=0; mismatch=0

for n in $(seq "$FIRST" "$LAST"); do
  orig=$((n - OFFSET))
  t_dup=$(gh issue view "$n"    --json title --jq .title 2>/dev/null)   || { echo "!! #$n no existe";   continue; }
  t_org=$(gh issue view "$orig" --json title --jq .title 2>/dev/null)   || { echo "!! #$orig no existe"; continue; }

  if [ "$t_dup" != "$t_org" ]; then
    echo "!! #$n NO se toca: su título no coincide con #$orig"
    echo "     #$n   : $t_dup"
    echo "     #$orig: $t_org"
    mismatch=$((mismatch + 1)); continue
  fi

  state=$(gh issue view "$n" --json state --jq .state)
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
echo "Resumen: cerrados=$closed  ya-cerrados=$skipped  sin-coincidencia=$mismatch"
