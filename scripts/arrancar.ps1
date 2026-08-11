<#
.SYNOPSIS
    Levanta la plataforma dotChat completa con Docker Compose.

.DESCRIPTION
    Genera (si hace falta) el fichero .env con los secretos que consume
    docker-compose.yml, construye las imágenes, levanta todos los servicios y
    espera a que el servidor conteste sano en /salud/listo antes de terminar.

    El .env nunca se versiona (está en .gitignore): cada máquina que despliega el
    conjunto genera el suyo la primera vez.

.PARAMETER Forzar
    Regenera los secretos del .env aunque ya exista uno.

.PARAMETER SinBuild
    No reconstruye las imágenes; solo levanta lo que ya esté construido.

.EXAMPLE
    .\scripts\arrancar.ps1

.EXAMPLE
    .\scripts\arrancar.ps1 -Forzar
#>
[CmdletBinding()]
param(
    [switch] $Forzar,
    [switch] $SinBuild
)


# No se usa 'Stop': docker/docker compose escriben su progreso normal (pull,
# build) por stderr, y con ErrorActionPreference en 'Stop' PowerShell convierte
# esa salida en una excepción aunque el comando termine bien. Los fallos reales
# se detectan a mano comprobando $LASTEXITCODE tras cada invocación externa.
$ErrorActionPreference = 'Continue'
$raiz = Split-Path -Parent $PSScriptRoot
$rutaEnv = Join-Path $raiz '.env'

function New-ClaveBase64 {
    param([int] $Bytes = 32)

    $material = New-Object byte[] $Bytes
    $generador = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generador.GetBytes($material) } finally { $generador.Dispose() }

    return [Convert]::ToBase64String($material)
}

function New-ClaveAdministrador {
    # Al menos un carácter de cada familia, para cumplir la política de
    # contraseñas de ASP.NET Core Identity sin depender del azar.
    $minusculas = 'abcdefghijkmnopqrstuvwxyz'
    $mayusculas = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $digitos    = '23456789'
    $simbolos   = '!#$%&*+-?@'
    $todos      = $minusculas + $mayusculas + $digitos + $simbolos

    $caracteres = @(
        $minusculas[(Get-Random -Maximum $minusculas.Length)]
        $mayusculas[(Get-Random -Maximum $mayusculas.Length)]
        $digitos[(Get-Random -Maximum $digitos.Length)]
        $simbolos[(Get-Random -Maximum $simbolos.Length)]
    )

    for ($i = 0; $i -lt 12; $i++) {
        $caracteres += $todos[(Get-Random -Maximum $todos.Length)]
    }

    return -join ($caracteres | Sort-Object { Get-Random })
}

# -----------------------------------------------------------------------------
# 1. Comprobaciones previas
# -----------------------------------------------------------------------------
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "No se encuentra 'docker' en el PATH. Instale Docker Desktop y vuelva a intentarlo."
}

try {
    docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'motor no disponible' }
}
catch {
    throw "Docker no responde. Abra Docker Desktop, espere a que arranque y vuelva a ejecutar este guion."
}

# -----------------------------------------------------------------------------
# 2. Secretos (.env)
# -----------------------------------------------------------------------------
$claveAdminMostrar = $null

if ((Test-Path $rutaEnv) -and -not $Forzar) {
    Write-Host "Ya existe un .env; se conserva. Use -Forzar para regenerar los secretos." -ForegroundColor DarkYellow
}
else {
    Write-Host 'Generando secretos en .env...' -ForegroundColor Cyan

    $claveJwt     = New-ClaveBase64 -Bytes 32
    $claveCifrado = New-ClaveBase64 -Bytes 32
    $claveAdmin   = New-ClaveAdministrador
    $claveAdminMostrar = $claveAdmin

    $contenidoEnv = @"
# Generado por arrancar.ps1 — no se versiona (ver .gitignore).
DOTCHAT_CLAVE_JWT=$claveJwt
DOTCHAT_CLAVE_CIFRADO=$claveCifrado
DOTCHAT_CLAVE_ADMIN=$claveAdmin
"@

    # Set-Content -Encoding utf8NoBOM solo existe en PowerShell 7+; para que el
    # guion funcione igual en Windows PowerShell 5.1 se escribe sin BOM a mano,
    # que es lo que docker compose espera al leer el .env.
    $codificacionSinBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($rutaEnv, $contenidoEnv + "`n", $codificacionSinBom)

    Write-Host '  + .env creado.' -ForegroundColor Green
}

# -----------------------------------------------------------------------------
# 3. Construcción y arranque
# -----------------------------------------------------------------------------
Write-Host ''
Write-Host 'Levantando el conjunto con Docker Compose...' -ForegroundColor Cyan

Push-Location $raiz
try {
    if ($SinBuild) {
        docker compose up -d
    }
    else {
        docker compose up -d --build
    }

    if ($LASTEXITCODE -ne 0) {
        throw 'docker compose up ha fallado; revise el registro de arriba.'
    }
}
finally {
    Pop-Location
}

# -----------------------------------------------------------------------------
# 4. Espera a que el servidor esté listo
# -----------------------------------------------------------------------------
Write-Host ''
Write-Host 'Esperando a que el servidor esté listo (base de datos, Valkey, MinIO)...' -ForegroundColor Cyan

$listo = $false
$limite = (Get-Date).AddMinutes(3)

while ((Get-Date) -lt $limite) {
    try {
        $respuesta = Invoke-WebRequest -Uri 'http://localhost:8080/salud/listo' -UseBasicParsing -TimeoutSec 5
        if ($respuesta.StatusCode -eq 200) {
            $listo = $true
            break
        }
    }
    catch {
        # Todavía no contesta; se reintenta.
    }

    Start-Sleep -Seconds 3
}

Write-Host ''
if ($listo) {
    Write-Host '✔ dotChat está arriba y sano.' -ForegroundColor Green
}
else {
    Write-Host '⚠ El servidor no ha respondido sano en 3 minutos.' -ForegroundColor Yellow
    Write-Host '  Revise el estado con: docker compose ps' -ForegroundColor Yellow
    Write-Host '  Y los registros con:  docker compose logs servidor' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Puntos de entrada:' -ForegroundColor Cyan
Write-Host '  - API / hub (por nginx) : http://localhost:8080'
Write-Host '  - Consola MinIO          : http://localhost:9001  (minioadmin / minioadmin123)'
Write-Host '  - Jaeger (trazas)        : http://localhost:16686'
Write-Host '  - Seq (registros)        : http://localhost:5341'
Write-Host '  - Portainer              : https://localhost:9443'

if ($claveAdminMostrar) {
    Write-Host ''
    Write-Host 'Cuenta de administrador inicial (anótela, no se vuelve a mostrar):' -ForegroundColor Yellow
    Write-Host '    usuario : admin'
    Write-Host "    clave   : $claveAdminMostrar"
}

Write-Host ''
Write-Host 'Cliente de usuario:' -ForegroundColor Cyan
Write-Host '    dotnet run --project src/Chat.ClienteCli'
Write-Host '    (ajuste Cliente:UrlServidor a http://localhost:8080 en su appsettings.json o con'
Write-Host '     DOTCHAT_Cliente__UrlServidor=http://localhost:8080)'
Write-Host ''
