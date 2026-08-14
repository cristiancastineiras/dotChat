// ============================================================================
// ERRORES DE LA API
// ============================================================================
// El servidor responde siempre en formato ProblemDetails (RFC 9457), con dos
// añadidos propios: `trazaId`, que es lo que permite encontrar la petición en
// Seq o en Jaeger, y `errores`, con el detalle por campo de las validaciones.
//
// Aquí se convierte esa respuesta en un error tipado con un mensaje ya apto para
// enseñar. La interfaz nunca debería tener que interpretar códigos HTTP.
// ============================================================================

import { HTTPError, TimeoutError } from "ky"

/** Cuerpo de una respuesta de error del servidor. */
interface ProblemDetails {
	readonly status?: number
	readonly title?: string
	readonly detail?: string
	readonly instance?: string
	readonly trazaId?: string
	/** Errores por campo; solo en las respuestas de validación. */
	readonly errores?: Record<string, string[]>
}

/** Error de una llamada a la API, ya interpretado. */
export class ErrorApi extends Error {
	/** Código HTTP; cero si la petición no llegó a completarse. */
	readonly estado: number

	/** Identificador de traza, para cruzarlo con los registros del servidor. */
	readonly trazaId: string | undefined

	/** Errores por campo, en las respuestas de validación. */
	readonly errores: Record<string, string[]> | undefined

	constructor(
		mensaje: string,
		estado: number,
		trazaId?: string,
		errores?: Record<string, string[]>,
	) {
		super(mensaje)
		this.name = "ErrorApi"
		this.estado = estado
		this.trazaId = trazaId
		this.errores = errores
	}

	/** La sesión no vale: hay que volver a autenticarse. */
	get esNoAutenticado(): boolean {
		return this.estado === 401
	}

	/** Autenticado, pero sin permiso para esto. */
	get esProhibido(): boolean {
		return this.estado === 403
	}

	/** El recurso no existe o dejó de existir. */
	get esNoEncontrado(): boolean {
		return this.estado === 404
	}

	/** Se ha superado el límite de peticiones. */
	get esDemasiadasPeticiones(): boolean {
		return this.estado === 429
	}

	/**
	 * El fallo puede ser pasajero, así que reintentar tiene sentido: se cayó la
	 * red, expiró el tiempo de espera o el servidor devolvió un 5xx.
	 */
	get esTransitorio(): boolean {
		return this.estado === 0 || this.estado >= 500 || this.estado === 429
	}
}

/**
 * Convierte cualquier fallo de una llamada en un `ErrorApi`.
 *
 * @param error Lo que sea que haya lanzado la llamada.
 * @returns Un error con mensaje presentable.
 */
export async function comoErrorApi(error: unknown): Promise<ErrorApi> {
	if (error instanceof ErrorApi) return error

	if (error instanceof TimeoutError) {
		return new ErrorApi("El servidor ha tardado demasiado en responder. Comprueba tu conexión.", 0)
	}

	if (error instanceof HTTPError) {
		return await desdeRespuesta(error.response)
	}

	// Sin respuesta: no hay servidor al otro lado, o la red se cayó a mitad.
	if (error instanceof TypeError) {
		return new ErrorApi(
			"No se ha podido contactar con el servidor. Comprueba que está levantado.",
			0,
		)
	}

	const mensaje = error instanceof Error ? error.message : "Error inesperado."
	return new ErrorApi(mensaje, 0)
}

/** Construye el error a partir de la respuesta del servidor. */
async function desdeRespuesta(respuesta: Response): Promise<ErrorApi> {
	let texto = ""

	try {
		texto = await respuesta.text()
	} catch {
		// Sin cuerpo que leer: el estado basta para dar un mensaje razonable.
	}

	return errorApiDesdeEstado(respuesta.status, texto)
}

/**
 * Construye el error a partir de un estado HTTP y el cuerpo ya leído.
 *
 * Es lo mismo que hace {@link desdeRespuesta} a partir de un `Response` de
 * `fetch`, pero para quien no tiene uno: la subida de adjuntos habla con
 * `XMLHttpRequest` y no con `fetch` (ver `subirConProgreso` en `cliente.ts`),
 * así que ya tiene el cuerpo como texto en la mano.
 *
 * @param estado Código de estado HTTP de la respuesta.
 * @param cuerpoTexto Cuerpo de la respuesta, sin analizar todavía.
 */
export function errorApiDesdeEstado(estado: number, cuerpoTexto: string): ErrorApi {
	let problema: ProblemDetails = {}

	try {
		if (cuerpoTexto) problema = JSON.parse(cuerpoTexto) as ProblemDetails
	} catch {
		// Un 502 de nginx o un 429 del limitador no traen cuerpo JSON. No es un
		// problema: el código de estado basta para dar un mensaje razonable.
	}

	return new ErrorApi(
		problema.detail ?? problema.title ?? mensajePorEstado(estado),
		estado,
		problema.trazaId,
		problema.errores,
	)
}

/** Mensaje de reserva cuando el servidor no explica el fallo. */
function mensajePorEstado(estado: number): string {
	switch (estado) {
		case 400:
			return "Los datos enviados no son válidos."
		case 401:
			return "Tu sesión ha caducado. Vuelve a iniciar sesión."
		case 403:
			return "No tienes permiso para hacer esto."
		case 404:
			return "No se ha encontrado lo que buscabas."
		case 409:
			return "La operación choca con el estado actual."
		case 413:
			return "El archivo es demasiado grande."
		case 429:
			return "Estás yendo demasiado rápido. Espera unos segundos."
		case 503:
			return "El servidor no está disponible en este momento."
		default:
			return estado >= 500
				? "El servidor ha fallado al procesar la petición."
				: "No se ha podido completar la operación."
	}
}

/**
 * Extrae un mensaje presentable de cualquier error, venga de donde venga.
 * Es lo que usan los avisos emergentes.
 *
 * @param error Error capturado.
 */
export function mensajeDeError(error: unknown): string {
	if (error instanceof ErrorApi) return error.message
	if (error instanceof Error) return error.message
	return "Se ha producido un error inesperado."
}
