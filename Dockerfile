# =============================================================================
#  Imagen del servidor de dotChat
# =============================================================================
#  Construcción en varias etapas: la primera trae el SDK completo para compilar
#  y la última se queda solo con el tiempo de ejecución. La imagen publicada no
#  lleva ni compilador, ni código fuente, ni paquetes de NuGet.
# =============================================================================

# -----------------------------------------------------------------------------
#  Etapa 1: restauración
# -----------------------------------------------------------------------------
#  Se copian primero los ficheros de proyecto y solo después el código. Así, si
#  cambia el código pero no las dependencias, Docker reutiliza la capa con los
#  paquetes ya restaurados y la construcción tarda segundos en lugar de minutos.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restauracion
WORKDIR /origen

COPY Directory.Build.props Directory.Packages.props NuGet.config ./
COPY src/Chat.Dominio/Chat.Dominio.csproj              src/Chat.Dominio/
COPY src/Chat.Aplicacion/Chat.Aplicacion.csproj        src/Chat.Aplicacion/
COPY src/Chat.Infraestructura/Chat.Infraestructura.csproj src/Chat.Infraestructura/
COPY src/Chat.Servidor/Chat.Servidor.csproj            src/Chat.Servidor/

RUN dotnet restore src/Chat.Servidor/Chat.Servidor.csproj

# -----------------------------------------------------------------------------
#  Etapa 2: publicación
# -----------------------------------------------------------------------------
FROM restauracion AS publicacion
WORKDIR /origen

COPY src/ src/

RUN dotnet publish src/Chat.Servidor/Chat.Servidor.csproj \
        --configuration Release \
        --no-restore \
        --output /aplicacion

# -----------------------------------------------------------------------------
#  Etapa 3: ejecución
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /aplicacion

# El proceso no corre como root: si alguien logra ejecutar algo dentro del
# contenedor, se encuentra con una cuenta sin permisos sobre el sistema.
RUN groupadd --system --gid 2000 dotchat \
    && useradd --system --uid 2000 --gid dotchat --no-create-home dotchat

COPY --from=publicacion --chown=dotchat:dotchat /aplicacion ./

# Dentro del contenedor se sirve en claro por el 8080: quien termina TLS es el
# balanceador que hay delante, que es donde vive el certificado.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

EXPOSE 8080

USER dotchat

# La sonda de vida solo comprueba que el proceso contesta. Que le falte una
# dependencia lo decide «/salud/listo», y de eso se encarga el balanceador: si
# reiniciáramos el contenedor por una base de datos caída, entraríamos en un
# bucle de reinicios que no arregla nada.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD ["/aplicacion/Chat.Servidor", "--comprobar-salud"]

ENTRYPOINT ["/aplicacion/Chat.Servidor"]
