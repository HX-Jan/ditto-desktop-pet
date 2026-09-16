param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    & $Dotnet run --project tests/Ditto.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    & $Dotnet publish src/Ditto.Desktop -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md,LICENSE,ASSETS.md -Destination artifacts/publish
    Copy-Item docs/QUICKSTART.txt -Destination artifacts/publish
    Copy-Item docs -Destination artifacts/publish -Recurse -Force
    $zipPath = Join-Path $repoRoot 'artifacts/DittoDesktopPet-v0.1.0-win-x64.zip'
    $packageEntries = Get-ChildItem artifacts/publish | Where-Object { $_.Extension -ne '.pdb' }
    Compress-Archive -LiteralPath $packageEntries.FullName -DestinationPath $zipPath -Force
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  DittoDesktopPet-v0.1.0-win-x64.zip" | Set-Content artifacts/SHA256SUMS.txt -Encoding ascii
    Write-Output $zipPath
} finally { Pop-Location }
