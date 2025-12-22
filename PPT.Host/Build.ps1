# PPT.Host Build Script
# Usage: Run this script in PowerShell

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PPT.Host Compilation Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Set paths
$projectPath = "e:\workProject\Mate-Engine\PPT.Host"
$solutionFile = "$projectPath\PPT.Host.sln"
$outputDir = "e:\workProject\Mate-Engine\PPTHost"

# Check project file
if (-not (Test-Path $solutionFile)) {
    Write-Host "ERROR: Solution file not found" -ForegroundColor Red
    Write-Host "Path: $solutionFile" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found solution file" -ForegroundColor Green

# Find MSBuild
$msbuildPaths = @(
    "E:\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
)

$msbuild = $null
foreach ($path in $msbuildPaths) {
    if (Test-Path $path) {
        $msbuild = $path
        break
    }
}

if ($null -eq $msbuild) {
    Write-Host "ERROR: MSBuild.exe not found" -ForegroundColor Red
    Write-Host "Please install Visual Studio 2019 or 2022" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Open in Visual Studio manually" -ForegroundColor Cyan
    Write-Host "1. Double-click: $solutionFile" -ForegroundColor Cyan
    Write-Host "2. Select 'Release' configuration" -ForegroundColor Cyan
    Write-Host "3. Build -> Build Solution" -ForegroundColor Cyan
    exit 1
}

Write-Host "Found MSBuild: $msbuild" -ForegroundColor Green
Write-Host ""

# Build project
Write-Host "Building..." -ForegroundColor Yellow
Write-Host ""

& $msbuild $solutionFile /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild /v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build FAILED!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Cyan
    Write-Host "1. Microsoft Office not installed (COM references required)" -ForegroundColor Yellow
    Write-Host "2. Missing .NET Framework 4.7.2" -ForegroundColor Yellow
    Write-Host "3. Open project in Visual Studio to see detailed errors" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Build SUCCESS!" -ForegroundColor Green
Write-Host ""

# Create output directory
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Created output directory: $outputDir" -ForegroundColor Green
}

# Copy files
Write-Host "Copying files to deployment directory..." -ForegroundColor Yellow
$sourceDir = "$projectPath\bin\Release"

Copy-Item "$sourceDir\*" -Destination $outputDir -Force -Recurse
Write-Host "Files copied to: $outputDir" -ForegroundColor Green
Write-Host ""

# Show results
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files:" -ForegroundColor Cyan
Get-ChildItem $outputDir -Filter "*.exe" | ForEach-Object {
    Write-Host "  $($_.Name)" -ForegroundColor Green
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Add PPTService GameObject in Unity scene" -ForegroundColor Yellow
Write-Host "2. Run Unity to test PPT control" -ForegroundColor Yellow
Write-Host ""
Write-Host "Test PPT.Host:" -ForegroundColor Cyan
Write-Host "  cd $outputDir" -ForegroundColor Yellow
Write-Host "  .\PPT.Host.exe" -ForegroundColor Yellow
Write-Host ""
