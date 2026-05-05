/**
 * Synthetic fraud-score payloads for local load testing.
 * Values are generated from (vu, iter) — not challenge lookup tables.
 */

const MCC_COMMON = ['5411', '5912', '5812'];
const MCC_RISKY = ['7995', '7801', '7802'];
const MERCHANT_KNOWN = 'M-BENCH-KNOWN-01';
const MERCHANT_UNKNOWN = 'M-BENCH-UNKNOWN-99';

/**
 * @param {number} vu k6 virtual user id (1-based in default fn)
 * @param {number} iter iteration counter from __ITER (0-based)
 */
export function makePayload(vu, iter) {
  const s = vu * 1000000 + iter;
  const pick = (arr, i) => arr[Math.abs(i) % arr.length];

  const mccPool = s % 3 === 0 ? MCC_RISKY : MCC_COMMON;
  const mcc = pick(mccPool, s);

  const highAmount = s % 7 === 0;
  const lowAmount = s % 11 === 0;
  let amount = 120.5 + (s % 200);
  if (highAmount) amount = 9800 + (s % 500);
  if (lowAmount) amount = 3.5 + (s % 5) * 0.01;

  const installments = 1 + (s % 12);
  const useLast = s % 4 !== 0;

  const knownFirst = s % 6 !== 0;
  const merchantId = knownFirst ? MERCHANT_KNOWN : MERCHANT_UNKNOWN;
  const knownMerchants = knownFirst
    ? [MERCHANT_KNOWN, 'M-BENCH-OTHER-02']
    : ['M-BENCH-OTHER-02', 'M-BENCH-OTHER-03'];

  const online = s % 5 === 0;
  const cardPresent = s % 3 !== 0;
  const farFromHome = s % 8 === 0 ? 420.5 + (s % 50) : 8.2 + (s % 30) * 0.1;

  const tsBase = new Date('2026-03-11T12:00:00Z').getTime();
  const requestedAt = new Date(tsBase + (s % 86400000)).toISOString();

  let last_transaction = null;
  if (useLast) {
    const prevTs = new Date(tsBase + (s % 80000000)).toISOString();
    last_transaction = {
      timestamp: prevTs,
      km_from_current: 5 + (s % 120) * 0.25,
    };
  }

  return JSON.stringify({
    id: `bench-tx-${vu}-${iter}-${s}`,
    transaction: {
      amount,
      installments,
      requested_at: requestedAt,
    },
    customer: {
      avg_amount: Math.max(1, amount * 0.5 + (s % 100)),
      tx_count_24h: s % 48,
      known_merchants: knownMerchants,
    },
    merchant: {
      id: merchantId,
      mcc,
      avg_amount: Math.max(1, amount * 0.4 + (s % 80)),
    },
    terminal: {
      is_online: online,
      card_present: cardPresent,
      km_from_home: farFromHome,
    },
    last_transaction,
  });
}
