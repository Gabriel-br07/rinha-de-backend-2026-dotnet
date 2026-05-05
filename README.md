# Rinha de Backend 2026 - Fraud Detection with .NET 9

## Overview

This repository implements a **production-oriented and benchmark-oriented** .NET 9 backend for **Rinha de Backend 2026** (fraud detection using vector search).

The main goals are:
- **Low memory usage** under the challenge limits (1 CPU / 350 MB total across containers)
- **Low latency** for `POST /fraud-score`
- **Deterministic behavior** for fraud scoring
- **No database** on the request path

## API endpoints

The API exposes exactly:
- `GET /ready`
- `POST /fraud-score`

### `GET /ready`
- Returns **200** only after the binary vector index (`data/references.bin` + `data/labels.bin`) is loaded in memory.
- Returns **503** while the index is loading or if it failed.

### `POST /fraud-score`
Input format (DTO shape) matches the challenge contract (see `resources/example-payloads.json`).

Response format:

```json
{ "approved": true, "fraud_score": 0.0 }
```

Fraud scoring rule:

```txt
fraud_score = number_of_fraud_neighbors / 5
approved = fraud_score < 0.6
```

**Error handling**: unexpected runtime errors return the valid fallback response below instead of HTTP 500:

```json
{ "approved": true, "fraud_score": 0.0 }
```

## Architecture

High-level runtime flow:

```mermaid
flowchart LR
  Client[Client] --> Nginx[Nginx:9999]
  Nginx --> Api1[api1:8080]
  Nginx --> Api2[api2:8080]

  subgraph ApiInstance[API instance]
    Ready[GET_/ready] --> IndexStore[IndexStore]
    Fraud[POST_/fraud-score] --> Vectorizer[Vectorizer]
    Vectorizer --> Quantizer[Quantizer(byte)]
    Quantizer --> Knn[Exact_kNN_scan_k=5]
    Knn --> Index[Index_in_memory]
  end
```

- Nginx only load-balances; **no fraud logic** exists in the load balancer.
- Each API instance holds the index in memory as **compact byte arrays**.

## Why .NET 9

- Good performance for Minimal APIs and `System.Text.Json`
- Strong control over allocations with `Span<T>` and stackalloc
- Simple containerization story for a single binary API + preprocessed dataset

## Main technical decisions

- **ASP.NET Core Minimal API**: chosen to reduce framework overhead (`src/FraudDetection.Api/Program.cs`).
- **Nginx load balancer**: simple, deterministic, round-robin distribution (`docker/nginx.conf`).
- **Preprocessed dataset**: `resources/references.json.gz` is converted to compact binaries at build-time.
- **Quantized byte vectors**: reduces memory vs `float[]` and enables fast integer distance calculations.
- **Exact k-NN baseline**: squared Euclidean distance in byte-space, `k=5`, no `Math.Sqrt`, no full sort.
- **No DB in request path**: index is in-memory; no per-request disk access.
- **Avoid HTTP 500**: fallback JSON response for unexpected errors.
- **Designed for the Rinha limits**: 2 API instances behind LB, total 1 CPU / 350MB.

## Vectorization rules

The request is mapped to a **14-dimensional** normalized vector in this exact order:

```txt
0  amount
1  installments
2  amount_vs_avg
3  hour_of_day
4  day_of_week
5  minutes_since_last_tx
6  km_from_last_tx
7  km_from_home
8  tx_count_24h
9  is_online
10 card_present
11 unknown_merchant
12 mcc_risk
13 merchant_avg_amount
```

Rules are implemented in `src/FraudDetection.Api/Vectorization/TransactionVectorizer.cs` using:
- constants from `resources/normalization.json`
- MCC risk map from `resources/mcc_risk.json`
- `clamp` to `[0, 1]` for normal dimensions
- **preserves `-1`** for dims 5 and 6 when `last_transaction` is null (not replaced by `0`)

## Dataset preprocessing

The dataset (~3M vectors) is shipped as `resources/references.json.gz`.

A preprocessing tool converts it to:
- `data/references.bin`
- `data/labels.bin`

Tool: `src/FraudDetection.Preprocess`

During `docker build`, preprocessing runs in a dedicated stage (see `Dockerfile`) so the runtime path only loads the binary files.

## Compact binary format

- `data/references.bin`
  - `byte[referenceCount * 14]`
  - vectors are stored back-to-back (contiguous memory)
- `data/labels.bin`
  - `byte[referenceCount]`
  - `legit -> 0`, `fraud -> 1`

Quantization encoding:

```txt
-1      -> 0
0.0     -> 1
1.0     -> 255
normal  -> 1 + round(value * 254)
```

This encoding is used for:
- preprocessing (writing dataset vectors)
- request vectors (before distance calculations)

## Search strategy

Baseline search is an **exact brute-force scan** over all references:
- `k = 5`
- distance: **squared Euclidean**
- compute in **byte-space** using integer math
- keep top-5 neighbors without sorting the full dataset

Implementation: `src/FraudDetection.Api/Search/ExactKnnSearcher.cs`.

## Error handling strategy

- Startup/index errors are stored in memory and `/ready` remains `503`.
- `/fraud-score` is wrapped in a try/catch and returns a valid fallback JSON response on unexpected errors.
- **Sampled** timing logs on `/fraud-score` and **minimal** exception logs — see [docs/testing.md](docs/testing.md).

## Load testing (k6)

Full steps, result export, interpreting metrics, and `dotnet-counters` guidance: **[docs/testing.md](docs/testing.md)**.

```bash
docker compose up --build
curl http://localhost:9999/ready
k6 run test/k6/smoke.js
k6 run test/k6/load.js
k6 run --summary-export results/load-summary.json test/k6/load.js
```

Follow logs:

```bash
docker compose logs -f api1
docker compose logs -f api2
docker compose logs -f nginx
```

## Docker architecture

- `nginx` listens on host port **9999** and proxies to:
  - `api1:8080`
  - `api2:8080`

Files:
- `docker-compose.yml`
- `docker/nginx.conf`
- `Dockerfile`

## Resource limits

The compose file declares limits summing to ≤ **1 CPU** and **350 MB**:
- `nginx`: 0.05 CPU / 20MB
- `api1`: 0.475 CPU / 155MB
- `api2`: 0.475 CPU / 155MB

## How to run locally

Build + run:

```bash
docker compose up --build
```

Then:
- `GET http://localhost:9999/ready`
- `POST http://localhost:9999/fraud-score`

## How to preprocess the dataset

Preprocess manually (requires a local .NET 9 SDK):

```bash
dotnet run -c Release --project src/FraudDetection.Preprocess -- resources/references.json.gz data
```

Output:
- `data/references.bin`
- `data/labels.bin`

## How to test the API

See **[docs/testing.md](docs/testing.md)** for sanity checks (`curl` / `scripts/sanity.ps1`), smoke, and load tests.

The file `resources/example-payloads.json` contains **multiple** example objects in an array; the API expects **one JSON object per request**. Example single-object POST:

```bash
curl -s -X POST "http://localhost:9999/fraud-score" -H "Content-Type: application/json" \
  -d '{"id":"sanity-tx-local-001","transaction":{"amount":120.5,"installments":2,"requested_at":"2026-03-11T18:45:53Z"},"customer":{"avg_amount":80.0,"tx_count_24h":4,"known_merchants":["M-SANITY-LOCAL-01"]},"merchant":{"id":"M-SANITY-LOCAL-01","mcc":"5411","avg_amount":60.0},"terminal":{"is_online":false,"card_present":true,"km_from_home":12.5},"last_transaction":null}'
```

## Future optimizations

The first version uses an **exact k-NN baseline** optimized for compact memory representation. Further benchmark results are needed to decide whether bucket-based pruning or ANN is necessary.

Possible future improvements:
- bucket/pruning by coarse quantization
- IVF / HNSW / VP-Tree style ANN
- SIMD distance loops
- better cache locality (block scanning)
- prefetching / threading strategies tuned to the 1 CPU constraint

## Trade-offs

- **Exact brute force** is simpler and accurate, but may increase p99 latency on the full dataset.
- **Byte quantization** reduces memory and improves speed, but can slightly affect neighbor precision.
- **No vector database** reduces memory and infrastructure complexity, but requires careful in-process optimization.