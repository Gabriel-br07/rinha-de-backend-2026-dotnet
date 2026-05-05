# Local performance and reliability testing

This guide describes how to run the stack with Docker Compose, sanity-check the API, run **k6** smoke and progressive load tests against **nginx** on port **9999**, export results, and use first-level **.NET diagnostics** on the host.

## Prerequisites

- Docker with Compose
- [k6](https://k6.io/docs/get-started/installation/) installed on your machine (PATH must include `k6`). **Windows:** `winget install --id GrafanaLabs.k6 -e` — then open a **new** terminal so PATH updates.

Optional (for .NET counters on the host):

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [`dotnet-counters`](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters) global tool:  
  `dotnet tool install -g dotnet-counters`

## Start the application

```bash
docker compose up --build
```

Wait until instances are up. Nginx listens on **http://localhost:9999**.

### Follow logs

```bash
docker compose logs -f api1
docker compose logs -f api2
docker compose logs -f nginx
```

## Sanity check (curl)

Ensure the service is ready, then POST a **single** JSON object (synthetic payload, not official challenge fixtures).

**Bash / Git Bash:**

```bash
curl -sf http://localhost:9999/ready
curl -s -X POST "http://localhost:9999/fraud-score" \
  -H "Content-Type: application/json" \
  -d '{"id":"sanity-tx-local-001","transaction":{"amount":120.5,"installments":2,"requested_at":"2026-03-11T18:45:53Z"},"customer":{"avg_amount":80.0,"tx_count_24h":4,"known_merchants":["M-SANITY-LOCAL-01"]},"merchant":{"id":"M-SANITY-LOCAL-01","mcc":"5411","avg_amount":60.0},"terminal":{"is_online":false,"card_present":true,"km_from_home":12.5},"last_transaction":null}'
```

On **Windows PowerShell**, `curl` is an alias for `Invoke-WebRequest` (can prompt about script parsing). Prefer **`curl.exe`**, **`Invoke-WebRequest -UseBasicParsing`**, or **`.\scripts\sanity.ps1`**.

**PowerShell** (`scripts/sanity.ps1` uses the same URL and payload):

```powershell
.\scripts\sanity.ps1
```

Expected: `GET /ready` exits 0; `POST /fraud-score` returns **HTTP 200** with JSON containing **`approved`** (boolean) and **`fraud_score`** (number).

> **Note:** Sending `resources/example-payloads.json` as-is POSTs an **array**; the API expects **one** request object per call. Use a single object or the sanity scripts.

## k6 smoke test

Low virtual users, short duration — validates correctness under negligible load.

Run these from the **repository root** (the folder that contains `test/k6/`), not from inside `test/`.

```bash
k6 run test/k6/smoke.js
```

**Windows (PowerShell),** from the repo root you can also use:

```powershell
k6 run .\test\k6\smoke.js
```

With another base URL (optional):

```bash
$env:BASE_URL="http://localhost:9999"; k6 run test/k6/smoke.js
```

## k6 progressive load test

Staged ramp to find where latency or failures increase.

```bash
k6 run test/k6/load.js
```

## Export k6 results (JSON)

Create the `results` folder if needed (the repo includes `results/.gitkeep`). Summaries are gitignored.

**Bash:** `mkdir -p results`  
**PowerShell:** `New-Item -ItemType Directory -Force results | Out-Null`

```bash
k6 run --summary-export results/smoke-summary.json test/k6/smoke.js
k6 run --summary-export results/load-summary.json test/k6/load.js
```

## Interpreting k6 output

| Metric | Meaning |
|--------|---------|
| **`http_req_failed`** | Fraction of requests k6 treats as failed (non-2xx, network error, etc.). **High** usually means timeouts, connection resets, or 5xx/4xx from upstream. |
| **`http_req_duration`** | Request time (ms). The summary prints percentiles such as **p(95)** and **p(99)**. |
| **p(95) / p(99)** | 95% or 99% of requests completed within this duration. **High p99** with **low failure rate** often points to CPU saturation, GC pauses, or a slow hot path (e.g. search), not necessarily HTTP errors. |
| **checks** | Pass rate of `check()` assertions (status 200, valid JSON, field types). **Low checks** with low `http_req_failed` can mean **200** responses with **unexpected body shape**. |

### What different symptoms suggest

- **`http_req_failed` high** — overload, nginx/upstream timeouts, or instances not ready; correlate with `docker compose logs` and `/ready`.
- **p99 high, failures low** — latency tail; use sampled API logs (`FraudScore sample`) and `dotnet-counters` for CPU/GC.
- **CPU high** (process) — saturated cores under compose limits; may need more CPU for local tuning or optimize hot path later.
- **GC / allocation high** — allocation pressure or large LOH; reduce allocations on the path (later optimization).
- **Exception rate** (if you monitor it) — logic or dependency issues; API logs **Fraud-score** warnings for unexpected exceptions.

### Malformed JSON

Deliberately invalid JSON may yield **HTTP 400** from ASP.NET model binding. That is separate from **HTTP 500** on valid requests; the API avoids 500 for unexpected **handler** errors by returning the fallback JSON.

## Resource limits vs local diagnosis

`docker-compose.yml` caps CPU and memory per service (Rinha-like). Under staged load you may see **high failure rates** or **high p99** even when the code is correct.

For local tuning you can temporarily **raise** CPU/memory limits in your **local** compose override—**do not change nginx’s role** (it should remain a plain load balancer).

## .NET diagnostics (host, while k6 runs)

The default API container sets **`DOTNET_EnableDiagnostics=0`**, which is not ideal for `dotnet-counters` **inside** the container. For first-level counters, run the API **on the host** or use a diagnostic-friendly configuration.

### Find the process ID (Windows)

While `dotnet run` is active for the API project:

```powershell
Get-Process -Name FraudDetection.Api, dotnet | Format-Table Id, ProcessName, Path -AutoSize
```

Pick the `dotnet` process whose command line includes **FraudDetection.Api** (Task Manager → Details also works).

### Find the process ID (Linux / macOS)

```bash
pgrep -af FraudDetection.Api
```

### dotnet-counters monitor

Replace `<PID>` with the process id:

```bash
dotnet-counters monitor --process-id <PID> \
  System.Runtime[cpu-usage,gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,alloc-rate,exception-count,threadpool-queue-length,threadpool-thread-count]
```

Watch:

- **CPU usage** — saturation vs load.
- **GC heap size** and **GC counts** — memory pressure and pauses.
- **Allocation rate** — excessive allocations on the hot path.
- **Exception count** — unexpected errors (should stay flat for steady valid traffic).
- **ThreadPool queue length** — request starvation when the pool backs up.

Press `Ctrl+C` to stop monitoring.

## Scripts

| Script | Purpose |
|--------|---------|
| [`scripts/sanity.sh`](../scripts/sanity.sh) | Bash sanity: `/ready` + `POST /fraud-score` |
| [`scripts/sanity.ps1`](../scripts/sanity.ps1) | PowerShell sanity (same behavior) |

## Files

| Path | Purpose |
|------|---------|
| [`test/k6/smoke.js`](../test/k6/smoke.js) | Low-load smoke |
| [`test/k6/load.js`](../test/k6/load.js) | Staged load + extra stdout summary |
| [`test/k6/lib/payloads.js`](../test/k6/lib/payloads.js) | Synthetic payload factory |

## Recommended next step after the first run

1. If **`http_req_failed`** is high: confirm **`/ready`** is 200, inspect nginx/API logs for timeouts, and consider whether **compose CPU/RAM** caps explain failures.
2. If failures are low but **p99** is high: compare sampled **`FraudScore sample`** logs (vectorize vs search ms) with **dotnet-counters** (CPU, GC, allocations).
3. Only after the bottleneck is clear: optimize the hot path (algorithm changes are out of scope for the test harness itself).
