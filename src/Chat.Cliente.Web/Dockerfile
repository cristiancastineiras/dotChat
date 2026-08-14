# =============================================================================
#  Imagen del cliente web de dotChat
# =============================================================================
#  Construcción en varias etapas, igual que el servidor: la primera trae Node
#  completo para compilar el monorepo (pnpm + turbo) y la última es un nginx
#  ligero que solo sirve los archivos estáticos ya generados. La imagen
#  publicada no lleva ni Node, ni el código fuente, ni node_modules.
#
#  El contexto de construcción es la raíz del repositorio (ver
#  docker-compose.yml), igual que el Dockerfile del servidor, así que todas
#  las rutas de aquí empiezan en "src/Chat.Cliente.Web/".
# =============================================================================

# -----------------------------------------------------------------------------
#  Etapa 1: dependencias
# -----------------------------------------------------------------------------
#  Se copian primero los manifiestos (package.json de cada paquete del
#  workspace) y solo después el código. Así, si cambia el código pero no las
#  dependencias, Docker reutiliza la capa con node_modules ya instalado.
# -----------------------------------------------------------------------------
FROM node:24-alpine AS dependencias
WORKDIR /origen

RUN corepack enable && corepack prepare pnpm@10.28.2 --activate

COPY src/Chat.Cliente.Web/package.json src/Chat.Cliente.Web/pnpm-lock.yaml \
     src/Chat.Cliente.Web/pnpm-workspace.yaml src/Chat.Cliente.Web/turbo.json \
     src/Chat.Cliente.Web/.npmrc ./
COPY src/Chat.Cliente.Web/apps/webapp/package.json apps/webapp/package.json
COPY src/Chat.Cliente.Web/paquetes/api/package.json paquetes/api/package.json
COPY src/Chat.Cliente.Web/paquetes/componentes/package.json paquetes/componentes/package.json
COPY src/Chat.Cliente.Web/paquetes/configuracion-ts/package.json paquetes/configuracion-ts/package.json
COPY src/Chat.Cliente.Web/paquetes/estados/package.json paquetes/estados/package.json
COPY src/Chat.Cliente.Web/paquetes/hooks/package.json paquetes/hooks/package.json
COPY src/Chat.Cliente.Web/paquetes/modelos/package.json paquetes/modelos/package.json
COPY src/Chat.Cliente.Web/paquetes/utiles/package.json paquetes/utiles/package.json

# --frozen-lockfile: falla en vez de "arreglar" un lockfile desincronizado,
# que es justo lo que se quiere en una imagen reproducible.
RUN pnpm install --frozen-lockfile

# -----------------------------------------------------------------------------
#  Etapa 2: compilación
# -----------------------------------------------------------------------------
#  Sin VITE_API_URL ni VITE_HUB_URL: los valores por defecto del cliente
#  ("/api" y "/hubs/chat", ver paquetes/api/src/configuracion.ts) son rutas
#  relativas al propio origen. Es la misma idea que el proxy de Vite en
#  desarrollo — el navegador nunca ve más que un origen — y aquí la cumple el
#  nginx de esta misma imagen, que reenvía esas rutas al balanceador.
# -----------------------------------------------------------------------------
FROM dependencias AS compilacion
WORKDIR /origen

COPY src/Chat.Cliente.Web/ ./

RUN pnpm run build:webapp

# -----------------------------------------------------------------------------
#  Etapa 3: ejecución
# -----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS ejecucion

COPY --from=compilacion /origen/apps/webapp/dist /usr/share/nginx/html
COPY src/Chat.Cliente.Web/nginx.conf /etc/nginx/nginx.conf

HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=10 \
    CMD wget --spider -q http://127.0.0.1:80/ || exit 1
