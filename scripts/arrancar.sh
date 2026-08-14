#!/usr/bin/env bash
#
# Levanta la plataforma dotChat completa con Docker Compose.
#
# Genera (si hace falta) el fichero .env con los secretos que consume
# docker-compose.yml, construye las imágenes, levanta todos los servicios y
# espera a que el servidor conteste sano en /salud/listo antes de terminar.
#
# El .env nunca se versiona (está en .gitignore): cada máquina que despliega el
# conjunto genera el suyo la primera vez.
#
# Uso:
#   ./arrancar.sh              # conserva el .env si ya existe
#   ./scripts/arrancar.sh --forzar     # regenera los secretos
#   ./scripts/arrancar.sh --sin-build  # no reconstruye las imágenes

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUTA_ENV="$RAIZ/.env"

FORZAR=0
SIN_BUILD=0
for arg in "$@"; do
    case "$arg" in
        --forzar) FORZAR=1 ;;
        --sin-build) SIN_BUILD=1 ;;
        *) echo "Opción desconocida: $arg" >&2; exit 1 ;;
    esac
done

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

# -----------------------------------------------------------------------------
# 1. Comprobaciones previas
# -----------------------------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
    echo "No se encuentra 'docker' en el PATH. Instale Docker y vuelva a intentarlo." >&2
    exit 1
fi

if ! docker info >/dev/null 2>&1; then
    echo "Docker no responde. Arranque el demonio y vuelva a ejecutar este guion." >&2
    exit 1
fi

# -----------------------------------------------------------------------------
# 2. Secretos (.env)
# -----------------------------------------------------------------------------
CLAVE_ADMIN_MOSTRAR=""

if [[ -f "$RUTA_ENV" && "$FORZAR" -eq 0 ]]; then
    echo "Ya existe un .env; se conserva. Use --forzar para regenerar los secretos."
else
    echo "Generando secretos en .env..."

    CLAVE_JWT="$(nueva_clave_base64)"
    CLAVE_CIFRADO="$(nueva_clave_base64)"
    CLAVE_ADMIN="$(nueva_clave_administrador)"
    CLAVE_ADMIN_MOSTRAR="$CLAVE_ADMIN"

    cat > "$RUTA_ENV" <<EOF
# Generado por arrancar.sh — no se versiona (ver .gitignore).
DOTCHAT_CLAVE_JWT=$CLAVE_JWT
DOTCHAT_CLAVE_CIFRADO=$CLAVE_CIFRADO
DOTCHAT_CLAVE_ADMIN=$CLAVE_ADMIN
EOF

    echo "  + .env creado."
fi

# -----------------------------------------------------------------------------
# 3. Construcción y arranque
# -----------------------------------------------------------------------------
echo
echo "Levantando el conjunto con Docker Compose..."

cd "$RAIZ"
if [[ "$SIN_BUILD" -eq 1 ]]; then
    docker compose up -d
else
    docker compose up -d --build
fi

# -----------------------------------------------------------------------------
# 4. Espera a que el servidor esté listo
# -----------------------------------------------------------------------------
echo
echo "Esperando a que el servidor esté listo (base de datos, Valkey, MinIO)..."

LISTO=0
LIMITE=$(( $(date +%s) + 180 ))

while [[ "$(date +%s)" -lt "$LIMITE" ]]; do
    if curl -fsS -o /dev/null "http://localhost:8080/salud/listo" 2>/dev/null; then
        LISTO=1
        break
    fi
    sleep 3
done

echo
if [[ "$LISTO" -eq 1 ]]; then
    echo "✔ dotChat está arriba y sano."
else
    echo "⚠ El servidor no ha respondido sano en 3 minutos."
    echo "  Revise el estado con: docker compose ps"
    echo "  Y los registros con:  docker compose logs servidor"
fi

echo
echo "Puntos de entrada:"
echo "  - Cliente web            : http://localhost:3000"
echo "  - API / hub (por nginx) : http://localhost:8080"
echo "  - Consola MinIO          : http://localhost:9001  (minioadmin / minioadmin123)"
echo "  - Jaeger (trazas)        : http://localhost:16686"
echo "  - Seq (registros)        : http://localhost:5341"
echo "  - Portainer              : https://localhost:9443"

if [[ -n "$CLAVE_ADMIN_MOSTRAR" ]]; then
    echo
    echo "Cuenta de administrador inicial (anótela, no se vuelve a mostrar):"
    echo "    usuario : admin"
    echo "    clave   : $CLAVE_ADMIN_MOSTRAR"
fi

echo
echo "Cliente de usuario:"
echo "    dotnet run --project src/Chat.ClienteCli"
echo "    (ajuste Cliente:UrlServidor a http://localhost:8080 en su appsettings.json o con"
echo "     DOTCHAT_Cliente__UrlServidor=http://localhost:8080)"
echo
