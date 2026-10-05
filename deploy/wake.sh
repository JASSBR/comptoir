#!/usr/bin/env bash
# Deploys the waiting page that answers on comptoir.jassbr.me while the facade (app.comptoir.jassbr.me) wakes from zero.
# Static files only, Vercel project "comptoir-wake". Usage: ./deploy/wake.sh   (requires `vercel login`)
set -euo pipefail
cd "$(dirname "$0")/wake"
[ -d .vercel ] || vercel link --yes --project comptoir-wake
vercel deploy --prod --yes
