param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $version = ([xml](Get-Content src/Ditto.Desktop/Ditto.Desktop.csproj -Raw)).Project.PropertyGroup.Version
    $archiveName = "DittoDesktopPet-v$version-win-x64.zip"
    & $Dotnet run --project tests/Ditto.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    & $Dotnet publish src/Ditto.Desktop -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md,LICENSE,ASSETS.md -Destination artifacts/publish
    Copy-Item docs/QUICKSTART.txt -Destination artifacts/publish
    Copy-Item docs -Destination artifacts/publish -Recurse -Force
    $packageRoot = (& $Dotnet msbuild src/Ditto.Desktop/Ditto.Desktop.csproj -getProperty:NuGetPackageRoot -nologo).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate runtime package notices.' }
    $runtimeConfig = Get-Content artifacts/publish/DittoDesktopPet.runtimeconfig.json -Raw | ConvertFrom-Json
    foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
        $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
        $runtimePackage = Join-Path (Join-Path $packageRoot $packageName) $framework.version
        $noticeDir = Join-Path 'artifacts/publish/ThirdParty' $framework.name
        New-Item -ItemType Directory -Force -Path $noticeDir | Out-Null
        $notices = Get-ChildItem -LiteralPath $runtimePackage -File | Where-Object { $_.Name -like 'LICENSE*' -or $_.Name -like 'THIRD-PARTY-NOTICES*' }
        if (-not $notices) { throw "Missing license notices for $packageName" }
        Copy-Item -LiteralPath $notices.FullName -Destination $noticeDir
    }
    $zipPath = Join-Path $repoRoot "artifacts/$archiveName"
    $packageEntries = Get-ChildItem artifacts/publish | Where-Object { $_.Extension -ne '.pdb' }
    Compress-Archive -LiteralPath $packageEntries.FullName -DestinationPath $zipPath -Force
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $archiveName" | Set-Content artifacts/SHA256SUMS.txt -Encoding ascii
    Write-Output $zipPath
} finally { Pop-Location }
