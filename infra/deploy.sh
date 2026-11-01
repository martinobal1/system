#!/usr/bin/env bash
# Nasadenie na serveri (spúšťa ho GitHub Actions po úspešnom builde, dá sa spustiť aj ručne).
# Použitie: bash infra/deploy.sh <commit_pred_pullom>   (FORCE_BUILD=1 vynúti rebuild)
set -euo pipefail
cd "$(dirname "$0")/.."
BEFORE="${1:-}"
AFTER="$(git rev-parse HEAD)"
cd infra

if [ -n "$BEFORE" ] && [ "$BEFORE" = "$AFTER" ] && [ "${FORCE_BUILD:-0}" != "1" ]; then
  echo "Žiadne nové zmeny ($AFTER)."
  exit 0
fi

CHANGED=""
if [ -n "$BEFORE" ] && [ "$BEFORE" != "$AFTER" ]; then
  CHANGED="$(git diff --name-only "$BEFORE" "$AFTER" || true)"
  echo "Zmenené súbory:"; echo "$CHANGED" | sed 's/^/  /'
fi

# Vzhľad (wwwroot) je v kontajneri pripojený priamo z disku → stačí git pull, rebuild netreba.
NEEDS_BUILD=1
if [ "${FORCE_BUILD:-0}" != "1" ] && [ -n "$CHANGED" ] && \
   ! echo "$CHANGED" | grep -qvE '^(backend/src/IsSuas\.Api/wwwroot/|docs/|frontend/|README|CURSOR_|\.github/)'; then
  NEEDS_BUILD=0
fi

if [ "$NEEDS_BUILD" = "1" ]; then
  echo "Záloha databázy pred nasadením…"
  mkdir -p "$HOME/zalohy"
  docker exec issuas-postgres pg_dump -U issuas issuas | gzip > "$HOME/zalohy/issuas_$(date +%F_%H%M%S).sql.gz"
  ls -1t "$HOME"/zalohy/issuas_*.sql.gz 2>/dev/null | tail -n +11 | xargs -r rm --   # ponechá 10 posledných
  echo "Zostavujem a spúšťam (docker compose up -d --build)…"
  docker compose up -d --build
  docker image prune -f >/dev/null || true
else
  echo "Zmenil sa len vzhľad (wwwroot) — rebuild netreba."
fi

echo "Kontrolujem, či aplikácia beží…"
for i in $(seq 1 45); do
  if curl -fsS -o /dev/null "http://127.0.0.1:8081/"; then
    echo "OK — aplikácia beží (commit $AFTER)."
    exit 0
  fi
  sleep 2
done
echo "CHYBA: aplikácia neodpovedá. Posledné logy:"
docker compose logs --tail 80 api || true
exit 1
