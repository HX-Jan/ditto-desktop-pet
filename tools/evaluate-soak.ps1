param(
    [string]$InputPath = 'artifacts/standalone-soak/result.json',
    [string]$OutputPath = 'artifacts/soak-evaluation.json'
)
$ErrorActionPreference = 'Stop'
$raw = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json
$baseline = $raw.samples | Where-Object { $_.seconds -ge 600 } | Select-Object -First 1
$last = $raw.samples | Select-Object -Last 1
if (-not $baseline -or $last.seconds -lt 1800) { throw 'Need a full 30-minute sample, including ten minutes of warmup.' }
$behaviorChecks = $raw.checks.PSObject.Properties | Where-Object { $_.Name -notin 'boundedHandleGrowth','boundedPrivateMemoryGrowth' }
$behaviorPass = @($behaviorChecks | Where-Object { $_.Value -ne $true }).Count -eq 0
$memoryGrowth = $last.memory - $baseline.memory
$handleGrowth = $last.handles - $baseline.handles
$pass = $behaviorPass -and $memoryGrowth -lt 64MB -and $handleGrowth -lt 50
$result = [ordered]@{
    passed = $pass
    elapsedSeconds = $raw.elapsedSeconds
    baselineSeconds = $baseline.seconds
    baselinePrivateBytes = $baseline.memory
    finalPrivateBytes = $last.memory
    privateGrowthBytes = $memoryGrowth
    baselineHandles = $baseline.handles
    finalHandles = $last.handles
    handleGrowth = $handleGrowth
    cpuSeconds = $raw.cpuSeconds
    behaviorChecksPassed = $behaviorPass
    criteria = 'After ten-minute warmup: private memory growth < 64 MiB, handle growth < 50, all behavior checks pass, >= 30 minutes alive.'
    rawHarnessPassed = $raw.passed
    note = 'The initial harness used a two-minute resource baseline. This independent evaluation applies the documented ten-minute warmup to unchanged raw samples; the full original result is retained.'
}
$result | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding utf8
$result | ConvertTo-Json
if (-not $pass) { throw 'Soak evaluation failed.' }
