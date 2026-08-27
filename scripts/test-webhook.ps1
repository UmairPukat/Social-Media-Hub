# Test App Connections webhook with a fake inbound user DM.
# Usage:
#   .\scripts\test-webhook.ps1 -PageId "YOUR_PAGE_ID"
#   .\scripts\test-webhook.ps1 -PageId "YOUR_PAGE_ID" -AppSecret "your_meta_app_secret"

param(
    [Parameter(Mandatory = $true)]
    [string]$PageId,

    [string]$AppSecret = "1f459bca03e2820ee019dd80d3e4dced",

    [string]$WebhookUrl = "https://socialbackend-production-a9ea.up.railway.app/api/app-connections/webhooks",

    [string]$CustomerId = "9876543210123456",

    [string]$MessageId = "curl_test_msg_001",

    [string]$MessageText = "Hello from curl test"
)

$bodyObj = [ordered]@{
    object = "page"
    entry  = @(
        [ordered]@{
            id        = $PageId
            time      = [int][double]::Parse((Get-Date -UFormat %s))
            messaging = @(
                [ordered]@{
                    sender    = @{ id = $CustomerId }
                    recipient = @{ id = $PageId }
                    timestamp = [int][double]::Parse((Get-Date -UFormat %s))
                    message   = [ordered]@{
                        mid  = $MessageId
                        text = $MessageText
                    }
                }
            )
        }
    )
}

$body = ($bodyObj | ConvertTo-Json -Depth 10 -Compress)

$hmac = [System.Security.Cryptography.HMACSHA256]::new()
$hmac.Key = [Text.Encoding]::UTF8.GetBytes($AppSecret)
$hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))
$signature = "sha256=" + (-join ($hash | ForEach-Object { $_.ToString("x2") }))

Write-Host "URL:       $WebhookUrl"
Write-Host "Signature: $signature"
Write-Host "Body:      $body"
Write-Host ""

$response = Invoke-WebRequest -Method POST -Uri $WebhookUrl `
    -ContentType "application/json" `
    -Headers @{ "X-Hub-Signature-256" = $signature } `
    -Body $body `
    -UseBasicParsing

Write-Host "Status: $($response.StatusCode)"
Write-Host "Response: $($response.Content)"
