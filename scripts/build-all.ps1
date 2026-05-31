Write-Host "=== COMMENCING THE SCOURGE: MASTER BUILD ===" -ForegroundColor Cyan
Write-Host "Building Godot C# assemblies..."
Push-Location "..\apps\client-godot"
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Godot build failed." -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}
Pop-Location

Write-Host "Building Web client..."
Push-Location "..\apps\web"
if (Test-Path "package.json") {
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Web build failed." -ForegroundColor Red
        Pop-Location
        exit $LASTEXITCODE
    }
} else {
    Write-Host "No package.json found in apps\web, skipping." -ForegroundColor Yellow
}
Pop-Location

Write-Host "Build complete. Exit Code 0." -ForegroundColor Green
exit 0
