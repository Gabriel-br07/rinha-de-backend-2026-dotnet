#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$baseUrl = if ($env:BASE_URL) { $env:BASE_URL } else { "http://localhost:9999" }

Write-Host "GET $baseUrl/ready"
$null = Invoke-WebRequest -Uri "$baseUrl/ready" -Method GET -UseBasicParsing

$body = @{
    id = "sanity-tx-local-001"
    transaction = @{
        amount        = 120.5
        installments  = 2
        requested_at  = "2026-03-11T18:45:53Z"
    }
    customer = @{
        avg_amount        = 80.0
        tx_count_24h      = 4
        known_merchants   = @("M-SANITY-LOCAL-01")
    }
    merchant = @{
        id         = "M-SANITY-LOCAL-01"
        mcc        = "5411"
        avg_amount = 60.0
    }
    terminal = @{
        is_online    = $false
        card_present = $true
        km_from_home = 12.5
    }
    last_transaction = $null
} | ConvertTo-Json -Compress -Depth 6

Write-Host "POST $baseUrl/fraud-score"
$response = Invoke-WebRequest -Uri "$baseUrl/fraud-score" -Method POST `
    -ContentType "application/json; charset=utf-8" `
    -Body $body `
    -UseBasicParsing

Write-Host $response.Content
