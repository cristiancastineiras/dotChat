#!/usr/bin/env bash
#
# Genera y almacena los secretos de dotChat fuera del código fuente.
#
# Crea una clave de firma JWT (HMAC-SHA256, 256 bits), una clave de cifrado de
# mensajes (AES-256) y la contraseña del administrador inicial, y las guarda en el
# almacén de «user secrets» de .NET asociado al proyecto Chat.Servidor.
#
# Uso:
#   ./scripts/configurar-secretos.sh [clave-administrador]

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROYECTO_SERVIDOR="$RAIZ/src/Chat.Servidor/Chat.Servidor.csproj"

if [[ ! -f "$PROYECTO_SERVIDOR" ]]; then
    echo "No se encuentra el proyecto del servidor en '$PROYECTO_SERVIDOR'." >&2
    exit 1
fi

nueva_clave_base64() {
    # 32 bytes = 256 bits, el tamaño exigido por AES-256 y por la firma HMAC-SHA256.
    openssl rand -base64 32
}

nueva_clave_administrador() {
    # Se garantiza al menos una minúscula, una mayúscula, un dígito y un símbolo.
    local aleatorio
    aleatorio="$(LC_ALL=C tr -dc 'a-zA-Z0-9' </dev/urandom | head -c 12)"
    echo "Ad${aleatorio}#7"
}

echo "Inicializando el almacén de secretos del servidor..."
dotnet user-secrets init --project "$PROYECTO_SERVIDOR" >/dev/null

CLAVE_ADMIN="${1:-}"
GENERADA=0
if [[ -z "$CLAVE_ADMIN" ]]; then
    CLAVE_ADMIN="$(nueva_clave_administrador)"
    GENERADA=1
fi

dotnet user-secrets set 'Jwt:ClaveFirmaBase64' "$(nueva_clave_base64)" --project "$PROYECTO_SERVIDOR" >/dev/null
echo "  + Jwt:ClaveFirmaBase64 establecido."

dotnet user-secrets set 'Cifrado:ClaveBase64' "$(nueva_clave_base64)" --project "$PROYECTO_SERVIDOR" >/dev/null
echo "  + Cifrado:ClaveBase64 establecido."

dotnet user-secrets set 'Administrador:Clave' "$CLAVE_ADMIN" --project "$PROYECTO_SERVIDOR" >/dev/null
echo "  + Administrador:Clave establecido."

echo
if [[ "$GENERADA" -eq 1 ]]; then
    echo "Contraseña del administrador inicial (anótela, no se volverá a mostrar):"
    echo "    usuario : admin"
    echo "    clave   : $CLAVE_ADMIN"
    echo
fi

echo "Secretos configurados. Ya puede arrancar el servidor con:"
echo "    dotnet run --project src/Chat.Servidor"
