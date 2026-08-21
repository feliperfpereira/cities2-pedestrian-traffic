$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Import-UserEnvironmentVariable([string]$name) {
    if (-not (Get-Item "Env:$name" -ErrorAction SilentlyContinue)) {
        $value = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::User)
        if ($value) {
            Set-Item "Env:$name" $value
        }
    }
}

Import-UserEnvironmentVariable "CSII_TOOLPATH"
Import-UserEnvironmentVariable "CSII_USERDATAPATH"

if (-not $env:CSII_TOOLPATH) {
    throw "CSII_TOOLPATH nao encontrado. No Cities: Skylines II, instale o Modding Toolchain em Options > Modding e abra este script novamente."
}

if (-not $env:CSII_USERDATAPATH) {
    throw "CSII_USERDATAPATH nao encontrado. O Modding Toolchain oficial precisa estar instalado."
}

Write-Host "[1/2] Compilando e instalando o mod C#..." -ForegroundColor Cyan
Push-Location $root
try {
    dotnet build .\Cities2PedestrianTraffic.csproj -c Release
}
finally {
    Pop-Location
}

Write-Host "[2/2] Compilando e instalando a interface..." -ForegroundColor Cyan
Push-Location (Join-Path $root "ui")
try {
    if (-not (Test-Path "node_modules")) {
        npm install --no-audit --no-fund
    }
    npm run build
}
finally {
    Pop-Location
}

$destination = Join-Path $env:CSII_USERDATAPATH "Mods\Cities2PedestrianTraffic"
Write-Host "" 
Write-Host "Pronto. Arquivos instalados em:" -ForegroundColor Green
Write-Host $destination -ForegroundColor Green
Write-Host "Abra/reinicie Cities: Skylines II e habilite Cities2PedestrianTraffic no playset." -ForegroundColor Yellow
