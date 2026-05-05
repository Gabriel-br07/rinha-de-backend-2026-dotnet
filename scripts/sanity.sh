#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:9999}"

echo "GET $BASE_URL/ready"
curl -sf "$BASE_URL/ready" >/dev/null

BODY='{"id":"sanity-tx-local-001","transaction":{"amount":120.5,"installments":2,"requested_at":"2026-03-11T18:45:53Z"},"customer":{"avg_amount":80.0,"tx_count_24h":4,"known_merchants":["M-SANITY-LOCAL-01"]},"merchant":{"id":"M-SANITY-LOCAL-01","mcc":"5411","avg_amount":60.0},"terminal":{"is_online":false,"card_present":true,"km_from_home":12.5},"last_transaction":null}'

echo "POST $BASE_URL/fraud-score"
curl -s -X POST "$BASE_URL/fraud-score" \
  -H "Content-Type: application/json" \
  -d "$BODY"
echo
