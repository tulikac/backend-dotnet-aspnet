param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^https?://")]
    [string]$BaseUrl
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd("/")

function Assert-Response {
    param(
        [string]$Path,
        [int]$ExpectedStatus,
        [string]$ExpectedContent
    )

    try {
        $response = Invoke-WebRequest -Uri "$base$Path" -SkipHttpErrorCheck
    }
    catch {
        throw "Request to $Path failed: $($_.Exception.Message)"
    }

    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "$Path returned $($response.StatusCode); expected $ExpectedStatus."
    }

    if ($ExpectedContent -and $response.Content -notmatch [regex]::Escape($ExpectedContent)) {
        throw "$Path did not contain expected content: $ExpectedContent"
    }

    Write-Host "PASS $Path ($ExpectedStatus)"
}

Assert-Response -Path "/" -ExpectedStatus 200 -ExpectedContent "ASP.NET Core backend is running"
Assert-Response -Path "/products/widget-1" -ExpectedStatus 200 -ExpectedContent "Widget 1"
Assert-Response -Path "/health" -ExpectedStatus 200 -ExpectedContent '"status":"ok"'
Assert-Response -Path "/api/status" -ExpectedStatus 200 -ExpectedContent '"status":"available"'
Assert-Response -Path "/api/version" -ExpectedStatus 200 -ExpectedContent '"app":"backend-dotnet-aspnet"'
Assert-Response -Path "/missing-page" -ExpectedStatus 404 -ExpectedContent "Page not found"
Assert-Response -Path "/api/missing" -ExpectedStatus 404 -ExpectedContent '"error":"not_found"'

Write-Host "All endpoint checks passed."
