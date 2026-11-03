$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.cache/cli'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.cache/nuget'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.cache/nuget-http'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:MSBUILDDISABLENODEREUSE = '1'
    New-Item -ItemType Directory -Path '.cache' -Force | Out-Null
    $sdkExe = Join-Path $PSScriptRoot '.cache/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $sdkExe)) {
        $archive = Join-Path $PSScriptRoot '.cache/dotnet-sdk-10.0.403-win-x64.zip'
        Invoke-WebRequest 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip' -OutFile $archive
        $expected = '7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48'
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expected) { throw 'SDK SHA512 mismatch' }
        Expand-Archive -LiteralPath $archive -DestinationPath '.cache/dotnet'
    }
    if ((& $sdkExe --version) -ne '10.0.403') { throw 'Unexpected SDK version' }
    $macroRoot = Join-Path $PSScriptRoot '.cache/macro-deck'
    if (-not (Test-Path -LiteralPath $macroRoot)) {
        git clone --depth 1 --branch v3.0.0 'https://github.com/Macro-Deck-App/Macro-Deck.git' $macroRoot
        if ($LASTEXITCODE -ne 0) { throw 'Macro Deck download failed' }
    }
    if ((git -C $macroRoot rev-parse HEAD) -ne '614dc0bbb04be21434176208fadff45179a6d5ec') { throw 'Unexpected Macro Deck commit' }
    git -C $macroRoot diff --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Macro Deck source has tracked edits' }
    foreach ($candidate in @('a','b')) {
        $project = "candidate-$candidate/Candidate$($candidate.ToUpper()).csproj"
        & $sdkExe restore $project --locked-mode --configfile NuGet.config --disable-parallel
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $candidate" }
        & $sdkExe build $project -c Release --no-restore --disable-build-servers --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $candidate" }
    }
} finally { Pop-Location }
