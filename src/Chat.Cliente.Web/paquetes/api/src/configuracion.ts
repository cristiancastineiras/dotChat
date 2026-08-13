// ============================================================================
// CONFIGURACIÓN DEL CLIENTE
// ============================================================================
// Lee las variables de entorno una sola vez y las deja tipadas y con valores de
// reserva. Vite sustituye `import.meta.env.X` por su literal en tiempo de
// compilación, así que esto no cuesta nada en ejecución.
// ============================================================================

/** Lee una variable de entorno con valor de reserva. */
function texto(valor: string | undefined, porDefecto: string): string {
	return valor && valor.length > 0 ? valor : porDefecto
}

/** Lee una variable numérica con valor de reserva. */
function numero(valor: string | undefined, porDefecto: number): number {
	const convertido = Number(valor)
	return Number.isFinite(convertido) && convertido > 0 ? convertido : porDefecto
}

/** Configuración efectiva del cliente. */
export const configuracion = {
	/** Raíz de la API. Relativa: el navegador habla siempre con su propio origen. */
	urlApi: texto(import.meta.env.VITE_API_URL, "/api"),

	/** Ruta del hub de SignalR. */
	urlHub: texto(import.meta.env.VITE_HUB_URL, "/hubs/chat"),

	/** Milisegundos que se espera una respuesta antes de darla por perdida. */
	tiempoEspera: numero(import.meta.env.VITE_API_TIMEOUT, 20_000),

	/** Mensajes que se piden en cada página del historial. */
	mensajesPorPagina: numero(import.meta.env.VITE_MENSAJES_POR_PAGINA, 50),

	/** Milisegundos que dura el aviso de «está escribiendo» sin señal nueva. */
	msEscribiendo: numero(import.meta.env.VITE_ESCRIBIENDO_MS, 4_000),

	/** Tamaño máximo de un adjunto, en MiB. */
	maxAdjuntoMiB: numero(import.meta.env.VITE_ADJUNTO_MAX_MB, 25),

	/** Tamaño máximo de la foto de perfil, en MiB. */
	maxAvatarMiB: numero(import.meta.env.VITE_AVATAR_MAX_MB, 8),

	/** Nombre de la aplicación, para títulos y avisos. */
	nombreApp: texto(import.meta.env.VITE_NOMBRE_APP, "dotChat"),

	/** Versión mostrada en el perfil. */
	version: texto(import.meta.env.VITE_VERSION, "1.0.0"),

	/** Registro detallado en consola. */
	depurar: import.meta.env.VITE_DEBUG === "true",
} as const

/**
 * Margen, en milisegundos, con el que se renueva el token antes de que caduque.
 *
 * Sin margen habría una carrera segura: un token que caduca dentro de 200 ms
 * pasa la comprobación, sale hacia el servidor y llega caducado. Treinta
 * segundos cubren de sobra la latencia y el desfase de reloj que el propio
 * servidor tolera.
 */
export const MARGEN_RENOVACION_MS = 30_000
