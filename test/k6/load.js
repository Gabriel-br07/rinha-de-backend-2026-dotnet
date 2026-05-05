import http from 'k6/http';
import { check, sleep } from 'k6';
import { makePayload } from './lib/payloads.js';

export const options = {
  stages: [
    { duration: '15s', target: 5 },
    { duration: '30s', target: 20 },
    { duration: '30s', target: 50 },
    { duration: '15s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(99)<2000'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:9999';

export function setup() {
  const deadline = Date.now() + 120000;
  while (Date.now() < deadline) {
    const res = http.get(`${BASE_URL}/ready`);
    if (res.status === 200) {
      return { baseUrl: BASE_URL };
    }
    sleep(1);
  }
  throw new Error('GET /ready did not return HTTP 200 within timeout');
}

export function handleSummary(data) {
  const failedRate = data.metrics.http_req_failed?.values?.rate ?? 0;
  const checksRate = data.metrics.checks?.values?.rate ?? 0;
  const duration = data.metrics.http_req_duration?.values;
  const reqs = data.metrics.http_reqs?.values?.count ?? 0;

  const lines = [
    '--- k6 load summary (custom) ---',
    `http_reqs (total requests): ${reqs}`,
    `http_req_failed rate: ${(failedRate * 100).toFixed(4)}%`,
    `checks pass rate: ${(checksRate * 100).toFixed(2)}%`,
    `http_req_duration p(50): ${duration?.['p(50)']?.toFixed(3) ?? 'n/a'} ms`,
    `http_req_duration p(95): ${duration?.['p(95)']?.toFixed(3) ?? 'n/a'} ms`,
    `http_req_duration p(99): ${duration?.['p(99)']?.toFixed(3) ?? 'n/a'} ms`,
    '----------------------------------',
  ];

  return {
    stdout: lines.join('\n') + '\n',
  };
}

export default function (data) {
  const base = data.baseUrl || BASE_URL;
  const body = makePayload(__VU, __ITER);
  const res = http.post(`${base}/fraud-score`, body, {
    headers: { 'Content-Type': 'application/json' },
  });

  let parsed = null;
  try {
    parsed = JSON.parse(res.body);
  } catch (_) {
    parsed = null;
  }

  check(res, {
    'status 200': (r) => r.status === 200,
    'json parse': () => parsed !== null,
    'approved boolean': () => typeof parsed?.approved === 'boolean',
    'fraud_score number': () => typeof parsed?.fraud_score === 'number',
  });

  sleep(0.02);
}
