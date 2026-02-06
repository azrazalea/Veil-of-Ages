# Veil of Ages - dotnet-trace profiler script
# Collects a .nettrace file, then opens it in PerfView for flame graph analysis.
#
# Usage: powershell -ExecutionPolicy Bypass -File profile.ps1
#
# PerfView is auto-downloaded to profiles/PerfView.exe on first run.

$GodotPath = "C:\Users\azraz\Godot\Godot_v4.6-stable_mono_win64.exe"
$ProjectPath = $PSScriptRoot
$OutputDir = Join-Path $ProjectPath "profiles"
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$NettraceFile = Join-Path $OutputDir "trace_$Timestamp.nettrace"
$PerfViewPath = Join-Path $OutputDir "PerfView.exe"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Ensure dotnet-trace is installed
if (-not (Get-Command dotnet-trace -ErrorAction SilentlyContinue)) {
    Write-Host "Installing dotnet-trace..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-trace
}

# Download PerfView if not present (single .exe, no install needed)
if (-not (Test-Path $PerfViewPath)) {
    Write-Host "Downloading PerfView..." -ForegroundColor Yellow
    $perfviewUrl = "https://github.com/microsoft/perfview/releases/latest/download/PerfView.exe"
    Invoke-WebRequest -Uri $perfviewUrl -OutFile $PerfViewPath
    Write-Host "PerfView downloaded to: $PerfViewPath" -ForegroundColor Green
}

# Build in Debug configuration to generate PDB symbol files
Write-Host "Building in Debug configuration (for PDB symbols)..." -ForegroundColor Cyan
dotnet build "$ProjectPath" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Enable .NET diagnostics for the Godot process
$env:DOTNET_EnableDiagnostics = "1"
$env:DOTNET_EnableDiagnostics_IPC = "1"

# Launch Godot with the project
Write-Host "Launching Godot..." -ForegroundColor Cyan
$godotProcess = Start-Process -FilePath $GodotPath `
    -ArgumentList "--path `"$ProjectPath`"" `
    -PassThru `
    -UseNewEnvironment:$false

Write-Host "Waiting for .NET runtime to initialize..." -ForegroundColor Cyan
Start-Sleep -Seconds 8

Write-Host ""
Write-Host "Checking for .NET processes..." -ForegroundColor Yellow
dotnet-trace ps
Write-Host ""

$godotPid = $godotProcess.Id
Write-Host "Godot PID: $godotPid" -ForegroundColor Green
Write-Host ""

# Collect as .nettrace
Write-Host "Starting dotnet-trace in a separate window..." -ForegroundColor Cyan
Write-Host "Press Enter or Ctrl+C in THAT window to stop tracing." -ForegroundColor Yellow
Write-Host ""

$traceCmd = "dotnet-trace collect --process-id $godotPid --profile dotnet-sampled-thread-time --output `"$NettraceFile`" & pause"
$traceProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $traceCmd -PassThru
$traceProcess.WaitForExit()

Write-Host ""
if (Test-Path $NettraceFile) {
    $size = (Get-Item $NettraceFile).Length / 1MB
    Write-Host ("Trace collected: $NettraceFile ({0:N1} MB)" -f $size) -ForegroundColor Green
    Write-Host ""
    Write-Host "Opening in PerfView..." -ForegroundColor Cyan
    Write-Host "  In PerfView: double-click 'Thread Time Stacks' then use 'Flame Graph' tab" -ForegroundColor Yellow
    Start-Process -FilePath $PerfViewPath -ArgumentList "`"$NettraceFile`""
} else {
    Write-Host "No trace file found." -ForegroundColor Red
}
