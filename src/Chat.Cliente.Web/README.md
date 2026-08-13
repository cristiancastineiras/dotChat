# dotChat · Cliente web

Interfaz de navegador para [dotChat](../../README.md). Hace lo mismo que la consola
—salas, conversaciones privadas, imágenes, emojis, presencia y avisos de escritura—
más lo que en una terminal no cabía: foto de perfil.

Monorepo de **pnpm + Turborepo**, con **React 19**, **Vite 8** (Rolldown + Oxc),
**Tailwind CSS v4** y **Ant Design 6**.

---

## Arrancar

Hace falta el servidor levantado. Si no lo está:

```bash
./scripts/arrancar.ps1      # desde la raíz del repositorio
```

Y después, desde esta carpeta:

```bash
pnpm install
pnpm dev
```

La aplicación queda en <http://localhost:4321>.

### Cómo llega al servidor

El navegador **siempre habla con su propio origen**. Nunca apunta directamente al
servidor, y por eso el cliente funciona sin que este publique cabeceras CORS:

- En **desarrollo**, el proxy de Vite reenvía `/api` y `/hubs` a
  `VITE_SERVIDOR_URL` (por defecto `http://localhost:8080`, el nginx que levanta
  `docker-compose`). El reenvío de `/hubs` va con `ws: true`, porque SignalR
  negocia por HTTP y luego sube la conexión a WebSocket.
- En **producción**, el mismo nginx sirve los ficheros estáticos del build y la
  API. No hay proxy que configurar ni dominio horneado en el bundle: las rutas
  son relativas, así que la misma compilación vale para `localhost`, para una
  VPN o para un dominio propio.

Si prefieres apuntar a otro servidor, cambia `VITE_SERVIDOR_URL` en
`apps/webapp/entorno/.env.desarrollo`.

---

## Estructura

```
apps/webapp/                 Aplicación (rutas, páginas, características)
  src/paginas/               Acceso, chat y perfil
  src/caracteristicas/chat/  Lista, conversación, redactor, adjuntos, diálogos
  src/caracteristicas/perfil/ Editor de la foto de perfil
  src/rutas/                 Enrutado y rutas protegidas

paquetes/
  modelos/       Tipos espejo de los DTO del servidor
  utiles/        Fechas, texto, emojis, archivos, almacenamiento
  api/           Cliente HTTP, sesión y caché de fotos
  estados/       Almacenes de Zustand y selectores
  hooks/         Conexión con el hub, historial, envío, interfaz
  componentes/   Piezas reutilizables y el tema de Ant Design
```

La regla de dependencias es una sola flecha, sin ciclos:

```
modelos → utiles → api → estados → hooks → componentes → webapp
```

Por eso la sesión vive en `api` y no en `estados`: el cliente HTTP necesita el
token en cada petición, fuera de React. `estados` se suscribe a ella y la refleja
en la interfaz, pero la autoridad sobre el token es de `api`.

---

## Comandos

| Comando          | Qué hace                                             |
| ---------------- | ---------------------------------------------------- |
| `pnpm dev`       | Servidor de desarrollo con recarga en caliente       |
| `pnpm build`     | Compila los paquetes y la aplicación para producción |
| `pnpm preview`   | Sirve el build de producción en local                |
| `pnpm typecheck` | `tsc --noEmit` en todo el monorepo                   |
| `pnpm lint`      | oxlint                                               |
| `pnpm format`    | oxfmt                                                |
| `pnpm check`     | Lint + comprobación de formato                       |

---

## Decisiones que conviene conocer

**El reinicio de estilos es el de antd, no el de Tailwind.** Ant Design 6 envuelve
sus estilos en `:where()` para que sean fáciles de sobrescribir, lo que los deja
con especificidad cero. El «preflight» de Tailwind declara
`button { background-color: transparent }` con un selector normal y, al ganar por
especificidad, dejaría todos los botones sin fondo. Se toma el reinicio de antd y
Tailwind se importa en dos piezas (`theme` y `utilities`), sin la suya.

**Los emojis los expande el servidor.** El texto se envía con sus `:atajos:` sin
tocar. Lo que se cifra y se guarda es ya el emoji, de modo que el historial se lee
igual desde la terminal y desde el navegador. El catálogo de `paquetes/utiles` es
una copia para ofrecer el selector y el autocompletado; si se queda corto respecto
al del servidor, el atajo se envía igual y se expande allí.

**Las fotos no se pueden enlazar.** El endpoint exige cabecera de autorización, así
que no vale ponerlo en el `src` de una imagen: se descargan con el cliente
autenticado y se envuelven en una URL de objeto. `paquetes/api/avatares.ts` las
cachea por usuario y versión, revoca las que sustituye y las vacía al cerrar
sesión.

**Los mensajes se pintan antes de enviarse.** Cada envío lleva un identificador
generado por el cliente que el servidor usa para descartar duplicados, así que
reintentar un envío que quizá llegó no publica el mensaje dos veces. El servidor
difunde a todo el grupo —incluido quien escribió—, y el almacén reconcilia el
mensaje optimista con el confirmado tanto si llega antes la respuesta como si
llega antes la difusión.

**El historial no está virtualizado.** Con alturas variables y anclaje al final,
una lista virtual es de las piezas más frágiles que se pueden escribir, y aquí el
número de nodos ya está acotado: cincuenta mensajes por página y solo se piden más
al subir. Si algún día hace falta, el sitio para hacerlo es `PanelMensajes.tsx`.

**Sobre `localStorage`.** El token es accesible desde JavaScript y por tanto
vulnerable a XSS. La alternativa —cookie `HttpOnly`— exigiría que el servidor
emitiera y validara cookies con su protección CSRF, y este servidor entrega JWT
porque su primer cliente era una consola. Es un riesgo asumido a conciencia: el
token de acceso vive minutos y el de refresco es de un solo uso.

### Ajustes del linter

- `jsx-a11y/no-autofocus` está desactivado: todos los usos están dentro de
  diálogos y ventanas emergentes, donde llevar el foco al abrir es justamente lo
  que recomiendan las prácticas de accesibilidad, o en la página de acceso, cuyo
  único cometido es ese formulario.
- `react/react-in-jsx-scope` está desactivado porque el proyecto usa la
  transformación automática de JSX y no necesita `React` en el ámbito.

---

## Variables de entorno

En `apps/webapp/entorno/.env.{modo}`. Todas empiezan por `VITE_` y acaban en el
bundle, así que **nunca** pongas secretos.

| Variable                   | Para qué                                              |
| -------------------------- | ----------------------------------------------------- |
| `VITE_API_URL`             | Raíz de la API. Relativa (`/api`)                     |
| `VITE_HUB_URL`             | Ruta del hub. Relativa (`/hubs/chat`)                 |
| `VITE_SERVIDOR_URL`        | Destino del proxy en desarrollo                       |
| `VITE_MENSAJES_POR_PAGINA` | Tamaño de página del historial                        |
| `VITE_ESCRIBIENDO_MS`      | Duración del aviso de escritura                       |
| `VITE_ADJUNTO_MAX_MB`      | Debe coincidir con `Adjuntos:TamanoMaximoBytes`       |
| `VITE_AVATAR_MAX_MB`       | Debe coincidir con `Adjuntos:TamanoMaximoImagenBytes` |
