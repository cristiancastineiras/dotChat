
![dotChat](dotChat_.svg "dotChat")

Plataforma de mensajería en tiempo real **totalmente local** 

---

## Índice

1. [Qué trae](#qué-trae)
2. [Requisitos](#requisitos)
3. [Puesta en marcha en cinco minutos](#puesta-en-marcha-en-cinco-minutos)
4. [El cliente: hablar con gente](#el-cliente-hablar-con-gente)
5. [Conversaciones privadas y presencia](#conversaciones-privadas-y-presencia)
6. [La consola de administración](#la-consola-de-administración)
7. [Telemetría con OpenTelemetry](#telemetría-con-opentelemetry)
8. [Configuración](#configuración)
9. [Arquitectura](#arquitectura)
10. [API HTTP y hub de SignalR](#api-http-y-hub-de-signalr)
11. [Seguridad](#seguridad)
12. [Base de datos y migraciones](#base-de-datos-y-migraciones)
13. [Rendimiento: librerías de Cysharp](#rendimiento-librerías-de-cysharp)
14. [Estado de las pruebas](#estado-de-las-pruebas)
15. [Licencia](#licencia)

---

## Qué trae

| Área | Qué hace |
| --- | --- |
| **Salas** | Públicas (cualquiera entra), **privadas** (solo por invitación) y **conversaciones directas 1:1**. |
| **Mensajes sin leer** | Cada sala lleva la cuenta de lo que te falta por leer y se ordena por actividad reciente. |
| **Presencia** | Quién está **en línea** ahora mismo, cuántas conexiones tiene y cuándo se le vio por última vez. |
| **Tiempo real** | Mensajes, altas y bajas de sala, avisos de «está escribiendo…» y cambios de presencia, por SignalR. |
| **Cifrado** | Los mensajes se guardan cifrados con **AES-256-GCM**; en la base de datos no hay texto en claro. |
| **Autenticación** | ASP.NET Core Identity + **JWT** con tokens de refresco. |
| **Caché** | FusionCache para usuarios, salas y configuración, con invalidación al escribir. |
| **Observabilidad** | Trazas, métricas y registros por **OTLP** hacia un receptor local. |
| **Interfaz** | Dos consolas con Spectre.Console: una para usuarios y otra para administración. |

---

## Requisitos

- **.NET SDK 10.0** o superior (`dotnet --version` debe decir `10.x`).
- Windows, Linux o macOS.
- Opcional: un receptor OpenTelemetry local si quieres ver las trazas
  (por ejemplo **OpenTelemetry Desktop Viewer**).

---

## Puesta en marcha en cinco minutos

### 1. Genera los secretos

El servidor **no arranca** sin la clave de firma de los tokens y la clave de cifrado
de los mensajes. Nunca están en el código ni en `appsettings.json`: se guardan en el
almacén de *user secrets* de .NET, fuera del repositorio.

```powershell
# Windows (PowerShell)
.\scripts\configurar-secretos.ps1
```

```bash
# Linux / macOS
./scripts/configurar-secretos.sh
```

El guion genera tres cosas y te enseña **una sola vez** la contraseña del administrador:

- `Jwt:ClaveFirmaBase64` — clave HMAC-SHA256 de 256 bits.
- `Cifrado:ClaveBase64` — clave AES-256 de los mensajes.
- `Administrador:Clave` — contraseña de la cuenta `admin`.

> ⚠️ Anota la contraseña del administrador. No se vuelve a mostrar. Puedes fijarla tú
> mismo: `.\scripts\configurar-secretos.ps1 -ClaveAdministrador 'MiClave.Segura#2026' -Forzar`
> en PowerShell, o `./scripts/configurar-secretos.sh 'MiClave.Segura#2026'` en bash.

### 2. Arranca el servidor

```bash
dotnet run --project src/Chat.Servidor
```

Al arrancar, el servidor aplica las migraciones pendientes, crea los roles, la cuenta
de administrador y una sala `General`. Escucha en `https://localhost:7150`.

### 3. Abre el cliente

En otra terminal:

```bash
dotnet run --project src/Chat.ClienteCli
```

Sin argumentos entra en **modo consola**: el proceso se queda abierto y va aceptando
órdenes, conservando la sesión y la conexión en tiempo real entre unas y otras.

```
 _     _   ___ _         _
| |___| |_/ __| |_  __ _| |_
| / _ \  _| (__| ' \/ _` |  _|
|_\___/\__|\___|_||_\__,_|\__|

Mensajería local cifrada · https://localhost:7150
No hay sesión iniciada. Empiece con login o registro.

chat> registro ana --email ana@ejemplo.local
chat:ana> salas
chat:ana> unirse General
```

### 4. Abre la consola de administración

```bash
dotnet run --project src/Chat.AdminCli
```

---

## El cliente: hablar con gente

### Órdenes

| Orden | Para qué sirve |
| --- | --- |
| `registro <usuario> --email <correo>` | Crea una cuenta y deja la sesión iniciada. |
| `login [usuario]` | Inicia sesión. Si no pones el usuario, te lo pregunta. |
| `salas` | Tu bandeja (con lo pendiente de leer) y el catálogo de salas. |
| `salas --mias` | Solo tus conversaciones. |
| `salas --crear <nombre> [-d <descripción>] [--privada]` | Crea una sala. |
| `unirse <sala>` | Entra en una sala pública y abre la conversación en vivo. |
| `unirse <sala> --sin-chat` | Solo se apunta, sin abrir la conversación. |
| `privado <usuario>` | Abre una conversación **privada 1:1**. |
| `privado <usuario> -m "texto"` | Manda un mensaje suelto y termina. |
| `enviar <sala> "texto"` | Publica un mensaje sin abrir la conversación. |
| `historial <sala> [-n 50]` | Muestra los mensajes recientes en una tabla. |
| `usuarios [--conectados]` | Lista los usuarios y quién está en línea. |
| `salir <sala>` | Abandona una sala. |
| `salir --sesion` | Cierra la sesión local y borra los tokens del disco. |

Dentro de la consola interactiva tienes además `ayuda`, `limpiar` y `terminar`.

### La conversación en vivo

Al entrar en una sala se pinta una cabecera con el nombre, el tipo y el número de
miembros, seguida del historial reciente. A partir de ahí, todo lo que escribas se
envía al pulsar Intro, y los mensajes de los demás aparecen solos.

Dentro de la conversación funcionan estas **órdenes de barra**:

| Orden | Qué hace |
| --- | --- |
| `/salir` | Cierra la conversación y vuelve a la consola. |
| `/miembros` | Quién está en la sala y quién está conectado ahora mismo. |
| `/historial` | Vuelve a cargar los mensajes recientes. |
| `/invitar <usuario>` | Mete a alguien en la sala (única forma de entrar en una privada). |
| `/limpiar` | Borra la pantalla sin perder la conexión. |
| `/ayuda` | Recuerda esta lista. |

Mientras escribes, el resto de la sala ve un **«está escribiendo…»**. El aviso se
manda en cuanto pulsas la primera tecla y el servidor lo limita a uno cada dos
segundos por conexión, así que no genera tráfico apreciable.

---

## Conversaciones privadas y presencia

### Cómo funcionan los mensajes directos

Una conversación 1:1 es, por dentro, una sala de tipo `Directa`. Eso permite
reutilizar tal cual el cifrado, el historial, la difusión en tiempo real y el control
de acceso, sin duplicar nada.

- Se identifican por una **clave canónica** formada con los dos identificadores de
  usuario ordenados, con un índice único. Por eso `privado ana` siempre reabre la
  **misma** conversación, la abra quien la abra.
- **No aparecen** en el catálogo de salas. Solo las ven sus dos participantes.
- Se presentan con el nombre del interlocutor: tú ves «ana» y ana te ve a ti.
- Nadie puede unirse a una conversación ajena ni invitar a un tercero.
- Si el otro ya está conectado cuando le escribes, el servidor suscribe sus clientes
  a la conversación al vuelo y recibe tu mensaje al instante, sin reconectarse.

```
chat:ana> privado luis
```

### Estados de conexión

La presencia se lleva **en memoria** en el servidor, contando conexiones por usuario:

- Cuando se abre la **primera** conexión de alguien, se anuncia a todos que está en línea.
- Cuando se cierra la **última**, se anuncia que se ha desconectado.
- Abrir dos terminales no te pone «en línea» dos veces, ni te desconecta al cerrar una.

Lo ves en `usuarios`, en `/miembros` dentro de una sala y, en vivo, como avisos en la
conversación. La consola de administración lo muestra además con el número de
conexiones y el momento en que se vio a cada uno por última vez.

> La presencia se pierde al reiniciar el servidor: es un dato volátil, no se persiste.
> Para escalar a varias instancias habría que sustituir `IRegistroConexiones` por un
> almacén compartido; el resto del código no se entera.

---

## La consola de administración

```bash
dotnet run --project src/Chat.AdminCli
```

| Orden | Qué hace |
| --- | --- |
| `usuarios listar` | Todas las cuentas, con su estado y su presencia. |
| `usuarios eliminar <usuario> [-y]` | Borra una cuenta y todos sus datos. |
| `salas listar` | Todas las salas, incluidas las privadas y las directas. |
| `salas crear <nombre> [-d <desc>] [--privada]` | Crea una sala. |
| `salas miembros <sala>` | Quién compone una sala y quién está conectado. |
| `salas eliminar <sala> [-y]` | Borra una sala y su historial. |
| `mensajes listar <sala> [-n 100]` | Audita el historial descifrado de una sala. |
| `cache limpiar` | Vacía la caché del servidor. |
| `estadisticas` | Resumen de la plataforma y tabla de presencia. |
| `mostrar-conexiones [--seguir]` | Conexiones abiertas, opcionalmente en vivo. |

Sin argumentos se abre en modo consola y la sesión de administrador se conserva entre
órdenes. Con argumentos ejecuta una sola orden y termina, que es lo que necesitan los
guiones.

Las credenciales se piden **una vez** y se pueden evitar del todo con la variable de
entorno `DOTCHAT_Admin__Clave`.

> **Nota sobre un error que ya no ocurre.** Las versiones anteriores podían abortar con
> `Trying to run one or more interactive functions concurrently`. La causa era que el
> cuadro de contraseña se abría *dentro* de un indicador de progreso, y Spectre.Console
> no admite dos pantallas interactivas a la vez. Ahora la sesión se prepara siempre
> antes de dibujar ningún indicador, y las renovaciones posteriores usan el token de
> refresco, que no necesita teclado.

---

## Telemetría con OpenTelemetry

El servidor exporta **trazas, métricas y registros** por OTLP. Los valores por defecto
apuntan al receptor local que escucha **OpenTelemetry Desktop Viewer**:

- gRPC → `http://localhost:4317` (por defecto)
- HTTP/protobuf → `http://localhost:4318`

Arranca el visor y después el servidor: no hay que configurar nada más.

```jsonc
// src/Chat.Servidor/appsettings.json
"Telemetria": {
  "Activada": true,
  "Protocolo": "grpc",                       // "grpc" (4317) o "http" (4318)
  "PuntoEntrada": "http://localhost:4317",   // vacío = el que toque según el protocolo
  "NombreServicio": "dotchat-servidor",
  "Trazas": true,
  "Metricas": true,
  "Registros": true
}
```

Para cambiar a HTTP sin tocar el fichero:

```bash
DOTCHAT_Telemetria__Protocolo=http DOTCHAT_Telemetria__PuntoEntrada=http://localhost:4318 \
  dotnet run --project src/Chat.Servidor
```

### Qué se exporta

**Trazas**

- Peticiones HTTP entrantes (ASP.NET Core) y salientes (HttpClient).
- Invocaciones del hub: `hub.conectar`, `hub.enviar_mensaje`, `hub.abrir_directa`.
- Un tramo por cada comando y consulta CQRS: `cqrs ComandoEnviarMensaje`,
  `cqrs ConsultaSalasDeUsuario`, etc., con el error marcado si lo hubo.

**Métricas propias** (además de las de ASP.NET Core y las del tiempo de ejecución)

| Métrica | Tipo | Qué mide |
| --- | --- | --- |
| `chat.mensajes.enviados` | contador | Mensajes aceptados y persistidos. |
| `chat.mensajes.rechazados` | contador | Rechazos, etiquetados por `motivo`. |
| `chat.mensajes.longitud` | histograma | Distribución de la longitud de los mensajes. |
| `chat.conexiones.activas` | contador ↕ | Conexiones en tiempo real abiertas. |
| `chat.usuarios.en_linea` | contador ↕ | Usuarios distintos conectados. |

**Registros**: todo lo que la aplicación escribe con `ILogger` sale también como logs
OTLP, correlacionado con la traza en curso.

La capa de aplicación se instrumenta con `ActivitySource`, que forma parte de la
biblioteca base, así que **no depende de ningún paquete de OpenTelemetry**: quien
decide qué hacer con las actividades es el proceso anfitrión. Si no hay receptor
escuchando, el coste de la instrumentación es prácticamente cero.

Para apagarlo del todo: `"Telemetria": { "Activada": false }`.

---

## Configuración

Orden de precedencia (de menos a más):

```
appsettings.json  <  appsettings.{Entorno}.json  <  user secrets  <  variables DOTCHAT_  <  argumentos
```

Las variables de entorno usan el prefijo `DOTCHAT_` y `__` como separador de sección:
`DOTCHAT_Jwt__MinutosVigenciaAcceso=15`.

### Secciones del servidor

| Sección | Contenido |
| --- | --- |
| `Jwt` | Emisor, audiencia, clave de firma, vigencia del acceso y del refresco. |
| `Cifrado` | Clave AES-256, contexto asociado y longitud máxima de mensaje. |
| `Cache` | Duraciones por familia de datos y ventana antirrepetición. |
| `SignalR` | Ruta del hub, latidos, tamaño máximo de mensaje y límite por minuto. |
| `Telemetria` | Exportación OTLP (ver arriba). |
| `Administrador` | Cuenta inicial y sala por defecto. |

### Clientes

`src/Chat.ClienteCli/appsettings.json` y `src/Chat.AdminCli/appsettings.json`
comparten forma:

```jsonc
{
  "Cliente": {
    "UrlServidor": "https://localhost:7150",
    "SegundosTiempoEspera": 30,
    "MensajesHistorialInicial": 30,
    "AceptarCertificadosNoConfiables": false   // solo desarrollo local
  }
}
```

> `AceptarCertificadosNoConfiables` desactiva la validación del certificado TLS.
> Úsalo únicamente contra un certificado autofirmado en tu propia máquina.

---

## Arquitectura

Clean Architecture simplificada, sin sobreingeniería: cada capa depende solo de las de
dentro.

```
src/
├── Chat.Dominio           Entidades, excepciones y contratos de repositorio.
│                          No depende de nada.
├── Chat.Aplicacion        Casos de uso (CQRS ligero), DTOs, validación y
│                          abstracciones de infraestructura.
├── Chat.Infraestructura   EF Core + SQLite, Identity, cifrado, JWT y FusionCache.
├── Chat.Servidor          ASP.NET Core: endpoints mínimos, hub de SignalR,
│                          telemetría y composición del contenedor.
├── Chat.ClienteCli        Consola de usuario (Spectre.Console).
└── Chat.AdminCli          Consola de administración (Spectre.Console).

tests/
└── Chat.Tests             Proyecto de pruebas (xUnit).
```

Decisiones que conviene conocer:

- **CQRS sin biblioteca**: `IComando<T>` / `IConsulta<T>` y un `Despachador` que
  resuelve el manejador del contenedor. Los manejadores se registran **uno a uno**;
  es más verboso que escanear ensamblados, pero el grafo queda documentado y falta
  cualquier manejador se detecta al compilar.
- **Repositorios + unidad de trabajo**: la persistencia queda detrás de interfaces del
  dominio; los manejadores nunca ven `DbContext`.
- **Proyecciones a mano**: la conversión de entidad a DTO está en un único sitio y sin
  reflexión, para que no se filtre nunca un campo sensible.
- **Notificación desacoplada**: la capa de aplicación difunde por
  `INotificadorTiempoReal`; solo el servidor conoce `IHubContext`.

---

## API HTTP y hub de SignalR

Todos los endpoints exigen JWT salvo los marcados como anónimos, y están limitados por
frecuencia.

### Autenticación

| Método | Ruta | Qué hace |
| --- | --- | --- |
| `POST` | `/api/auth/registrar` | Crea una cuenta y devuelve la sesión. |
| `POST` | `/api/auth/login` | Inicia sesión. |
| `POST` | `/api/auth/refrescar` | Renueva la sesión con el token de refresco. |

### Salas y mensajes

| Método | Ruta | Qué hace |
| --- | --- | --- |
| `GET` | `/api/salas` | Catálogo visible: públicas + tus privadas. |
| `GET` | `/api/salas/mias` | Tu bandeja, con los mensajes sin leer. |
| `POST` | `/api/salas` | Crea una sala (`privada: true` para restringida). |
| `POST` | `/api/salas/directas` | Abre o recupera una conversación 1:1. |
| `GET` | `/api/salas/{id}/miembros` | Miembros con su estado de conexión. |
| `POST` | `/api/salas/{id}/unirse` | Entra en una sala pública. |
| `POST` | `/api/salas/{id}/invitar` | Incorpora a alguien a la sala. |
| `POST` | `/api/salas/{id}/leida` | Pone a cero tus mensajes pendientes. |
| `POST` | `/api/salas/{id}/salir` | Abandona la sala. |
| `GET` | `/api/mensajes?salaId=&cantidad=` | Historial descifrado. |
| `POST` | `/api/mensajes` | Publica sin usar el hub. |

### Usuarios, diagnóstico y administración

| Método | Ruta | Qué hace |
| --- | --- | --- |
| `GET` | `/api/usuarios` | Lista de usuarios con presencia. |
| `GET` | `/api/usuarios/presencia` | Quién está en línea y desde cuándo. |
| `GET` | `/api/usuarios/yo` | Identidad asociada al token. |
| `GET` | `/api/estado` | Comprobación de vida *(anónimo)*. |
| `GET` | `/api/configuracion` | Configuración pública para los clientes *(anónimo)*. |
| `GET` | `/api/admin/salas` | Todas las salas, sin filtro de visibilidad. |
| `GET` | `/api/admin/estadisticas` | Resumen de actividad. |
| `GET` | `/api/admin/conexiones` | Conexiones abiertas. |
| `POST` | `/api/admin/cache/limpiar` | Vacía la caché. |
| `DELETE` | `/api/admin/usuarios/{id}` | Elimina una cuenta. |
| `DELETE` | `/api/admin/salas/{id}` | Elimina una sala. |

### Hub `/hubs/chat`

**Del cliente al servidor**

`Conectar` · `Desconectar` · `EnviarMensaje` · `UnirseSala` · `SalirSala` ·
`AbrirConversacionDirecta` · `ListarSalas` · `ListarMiembros` · `ListarPresencia` ·
`MarcarLeida` · `Escribiendo`

**Del servidor al cliente**

`Conectado` · `RecibirMensaje` · `UsuarioUnido` · `UsuarioSalido` · `SalaCreada` ·
`SalaDisponible` · `PresenciaCambiada` · `UsuarioEscribiendo` · `ErrorRecibido`

---

## Seguridad

- **HTTPS obligatorio**, HSTS fuera de desarrollo y cabeceras de endurecimiento
  (`X-Content-Type-Options`, `X-Frame-Options`, CSP, `Referrer-Policy`).
- **JWT** con validación completa de emisor, audiencia, vigencia y firma
  (HMAC-SHA256 exclusivamente). El token por cadena de consulta se acepta **solo** en
  la ruta del hub, porque WebSocket no admite cabeceras.
- **Contraseñas** con el hash de Identity: mínimo 10 caracteres, variedad obligatoria
  y bloqueo temporal a los 5 intentos fallidos.
- **Mensajes cifrados** con AES-256-GCM antes de tocar el disco. Un mensaje ilegible
  (por rotación de clave) no tumba la consulta: se marca y se registra.
- **Validación y saneamiento** de toda entrada: se normaliza Unicode y se eliminan
  controles y caracteres invisibles usados para suplantar identidades o inyectar
  secuencias ANSI en la terminal.
- **Antirrepetición**: cada envío lleva un identificador único y se descartan los
  repetidos dentro de la ventana configurada.
- **Limitación de frecuencia** nativa de ASP.NET Core en la API y por usuario en el hub.
- **Autorización por membresía**: leer el historial, escribir o ver los miembros exige
  pertenecer a la sala. Los administradores pueden auditar cualquiera.
- **Secretos fuera del código**, siempre.
- La identidad se toma **siempre de los claims**, nunca de lo que mande el cliente.

---

## Base de datos y migraciones

SQLite, con las marcas de tiempo guardadas como enteros para que los índices sirvan
para ordenar y comparar.

| Tabla | Contenido |
| --- | --- |
| `Salas` | Nombre, tipo, clave de conversación directa, última actividad. |
| `Mensajes` | Texto cifrado, sala, autor y fecha. |
| `MiembrosSala` | Pertenencias y marca de última lectura. |
| `TokensRefresco` | Tokens emitidos, con su hash. |
| `AspNet*` | Tablas de ASP.NET Core Identity. |

Las migraciones se aplican solas al arrancar. Para crear una nueva:

```bash
dotnet tool restore
dotnet ef migrations add NombreDeLaMigracion \
  --project src/Chat.Infraestructura \
  --startup-project src/Chat.Infraestructura \
  --output-dir Migraciones
```

---

## Rendimiento: librerías de Cysharp

Se usan tres librerías de [Cysharp](https://github.com/Cysharp) en las rutas que se
ejecutan muchas veces, para no generar basura que luego haya que recoger:

| Librería | Dónde | Por qué |
| --- | --- | --- |
| **ZLogger** | Registro del servidor | Escribe en UTF-8 sin construir cadenas intermedias y vuelca de forma asíncrona. Registrar cada mensaje enviado deja de costar asignaciones. |
| **ZLinq** | Registro de conexiones, proyecciones de consultas, tablas de las consolas | LINQ sobre estructuras: la cadena de operadores no asigna nada y solo se paga el array final. |
| **ZString** | Composición de cada línea de la conversación | Constructor de cadenas con memoria agrupada; una sola cadena por mensaje pintado en lugar de una concatenación por tramo. |

No se han metido donde no aportaban: el resto del código sigue usando LINQ y
`string.Create` normales, que en .NET 10 ya son suficientemente buenos.

---

## Estado de las pruebas

El proyecto `tests/Chat.Tests` está creado y configurado (xUnit, NSubstitute,
coverlet, referencias a todas las capas) **pero todavía no contiene ningún caso de
prueba**. La cobertura real es del 0 %.

Las áreas que conviene cubrir primero, por orden de riesgo:

1. `ServicioCifradorMensajes` — ida y vuelta del cifrado, y qué pasa con una clave
   rotada.
2. `GeneradorTokensJwt` — claims emitidos, vigencia y rechazo de claves cortas.
3. `ManejadorAbrirConversacionDirecta` — idempotencia de la clave canónica.
4. `RegistroConexiones` — recuento de conexiones y transiciones de presencia.
5. `ValidadorEntrada` — saneamiento de caracteres invisibles y de control.

Para ejecutarlas cuando las haya:

```bash
dotnet test
```

---

## Licencia

**WTFPL — Do What The Fuck You Want To Public License, versión 2.**

Haz lo que te dé la gana con este código. El texto completo está en
[LICENSE](LICENSE).
