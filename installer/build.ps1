# vatGram installer build pipeline
#
# 1. Publishes the tray app self-contained (win-x64) so end users don't need
#    to install .NET runtime separately.
# 2. Builds the plugin (net472).
# 3. Compiles the Inno Setup script.
# 4. Wraps the setup .exe in a .zip so browsers / Defender don't quarantine it
#    on download (unsigned .exe direct downloads get auto-deleted; .zip is fine).
#
# Output: installer\setup\vatgram-setup.exe + a zip alongside.
#
# Requires:
#   - .NET 10 SDK
#   - Inno Setup 6 (https://jrsoftware.org/isdl.php)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "[1/4] Publishing tray app (self-contained, win-x64)..." -ForegroundColor Cyan
dotnet publish "$repoRoot\src\Vatgram.Tray\Vatgram.Tray.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Tray publish failed" }

Write-Host "[2/4] Building plugin (net472)..." -ForegroundColor Cyan
dotnet build "$repoRoot\src\Vatgram.Plugin\Vatgram.Plugin.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed" }

Write-Host "[3/4] Locating Inno Setup..." -ForegroundColor Cyan
$iscc = $null
$candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
)
foreach ($c in $candidates) {
    if (Test-Path $c) { $iscc = $c; break }
}
if (-not $iscc) {
    Write-Host ""
    Write-Host "FAIL: Inno Setup 6 not found." -ForegroundColor Red
    Write-Host "      Install from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}
Write-Host "      Found: $iscc" -ForegroundColor DarkGray

Write-Host "      Compiling installer..." -ForegroundColor Cyan
& $iscc "$PSScriptRoot\vatgram.iss"
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed" }

$exe = Join-Path $PSScriptRoot 'setup\vatgram-setup.exe'
if (-not (Test-Path $exe)) {
    Write-Host "FAIL: Setup binary not found at expected path." -ForegroundColor Red
    exit 1
}

Write-Host "[4/4] Packaging zip..." -ForegroundColor Cyan
# Pick zip filename based on git branch so internal builds don't get confused
# with public ones in the user's Downloads folder.
$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null)
$zipName = if ($branch -eq 'public' -or [string]::IsNullOrWhiteSpace($branch)) { 'vatGram-Setup.zip' } else { 'vatGram-Setup-internal.zip' }
$zip = Join-Path $PSScriptRoot "setup\$zipName"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $exe -DestinationPath $zip -CompressionLevel Optimal
Write-Host "      Wrapped in: $zipName" -ForegroundColor DarkGray

$exeSize = [math]::Round((Get-Item $exe).Length / 1MB, 1)
$zipSize = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "OK   exe: $exe ($exeSize MB)" -ForegroundColor Green
Write-Host "OK   zip: $zip ($zipSize MB)" -ForegroundColor Green
