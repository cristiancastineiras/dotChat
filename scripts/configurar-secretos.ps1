<#
.SYNOPSIS
    Genera y almacena los secretos de dotChat fuera del código fuente.

.DESCRIPTION
    Crea una clave de firma JWT (HMAC-SHA256, 256 bits), una clave de cifrado de
    mensajes (AES-256) y la contraseña del administrador inicial, y las guarda en
    el almacén de «user secrets» de .NET, asociado al proyecto Chat.Servidor.

    El almacén vive fuera del repositorio (%APPDATA%\Microsoft\UserSecrets), por lo
    que los secretos nunca se versionan ni aparecen en appsettings.json.

.PARAMETER ClaveAdministrador
    Contraseña del administrador inicial. Si se omite, se genera una aleatoria y se
    muestra una única vez por pantalla.

.PARAMETER Forzar
    Sobrescribe los secretos ya existentes. Sin este parámetro, se conservan.

.EXAMPLE
    .\scripts\configurar-secretos.ps1

.EXAMPLE
    .\scripts\configurar-secretos.ps1 -ClaveAdministrador 'MiClave.Segura#2026' -Forzar
#>
[CmdletBinding()]
param(
    [string] $ClaveAdministrador,
    [switch] $Forzar
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$proyectoServidor = Join-Path $raiz 'src\Chat.Servidor\Chat.Servidor.csproj'

if (-not (Test-Path $proyectoServidor)) {
    throw "No se encuentra el proyecto del servidor en '$proyectoServidor'."
}

function New-ClaveBase64 {
    param([int] $Bytes = 32)

    $material = New-Object byte[] $Bytes
    $generador = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generador.GetBytes($material) } finally { $generador.Dispose() }

    return [Convert]::ToBase64String($material)
}

function New-ClaveAdministrador {
    # Se compone con al menos un carácter de cada familia para cumplir la política
    # de contraseñas de ASP.NET Core Identity sin depender del azar.
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

Write-Host 'Inicializando el almacén de secretos del servidor...' -ForegroundColor Cyan
dotnet user-secrets init --project $proyectoServidor | Out-Null

$existentes = @{}
$listado = dotnet user-secrets list --project $proyectoServidor 2>$null
if ($LASTEXITCODE -eq 0 -and $listado) {
    foreach ($linea in $listado) {
        if ($linea -match '^(?<clave>[^=]+)\s=\s(?<valor>.*)$') {
            $existentes[$Matches.clave.Trim()] = $Matches.valor
        }
    }
}

function Set-Secreto {
    param(
        [Parameter(Mandatory)] [string] $Clave,
        [Parameter(Mandatory)] [string] $Valor
    )

    if ($existentes.ContainsKey($Clave) -and -not $Forzar) {
        Write-Host "  - $Clave ya estaba definido (use -Forzar para regenerarlo)." -ForegroundColor DarkYellow
        return $false
    }

    dotnet user-secrets set $Clave $Valor --project $proyectoServidor | Out-Null
    Write-Host "  + $Clave establecido." -ForegroundColor Green
    return $true
}

Set-Secreto -Clave 'Jwt:ClaveFirmaBase64' -Valor (New-ClaveBase64 -Bytes 32) | Out-Null
Set-Secreto -Clave 'Cifrado:ClaveBase64'  -Valor (New-ClaveBase64 -Bytes 32) | Out-Null

if (-not $ClaveAdministrador) {
    $ClaveAdministrador = New-ClaveAdministrador
    $generada = $true
}

$establecida = Set-Secreto -Clave 'Administrador:Clave' -Valor $ClaveAdministrador

Write-Host ''
if ($establecida -and $generada) {
    Write-Host 'Contraseña del administrador inicial (anótela, no se volverá a mostrar):' -ForegroundColor Yellow
    Write-Host "    usuario : admin"
    Write-Host "    clave   : $ClaveAdministrador"
}

Write-Host ''
Write-Host 'Secretos configurados. Ya puede arrancar el servidor con:' -ForegroundColor Cyan
Write-Host '    dotnet run --project src/Chat.Servidor'

Write-Host ''

Read-Host 'Pulse ENTER para salir'
