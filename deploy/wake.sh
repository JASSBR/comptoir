#!/usr/bin/env bash
# Deploys the waiting page that answers on comptoir.jassbr.me while the facade (app.comptoir.jassbr.me) wakes from zero.
# Static files only, Vercel project "comptoir-wake". Usage: ./deploy/wake.sh   (requires `vercel login`)
set -euo pipefail
cd "$(dirname "$0")/wake"
if [ ! -d .vercel ]; then
  vercel link --yes --project comptoir-wake
  # Linking connects the repository: every push would then deploy the repository root, an empty site, as production.
  vercel git disconnect --yes >/dev/null 2>&1 || true
  rm -f .env.local
fi
vercel deploy --prod --yes
