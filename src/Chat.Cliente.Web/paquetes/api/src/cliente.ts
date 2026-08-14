// ============================================================================
// CLIENTE HTTP
// ============================================================================
// Una única instancia de ky con la renovación de sesión resuelta de forma
// transparente: el resto de la aplicación llama a `api.get(...)` y nunca se
// entera de que por debajo hubo que renovar un token.
//
// La renovación se hace en dos sitios a propósito:
//
//   - Antes de salir, si al token le quedan segundos. Es el camino normal y
//     evita gastar una ida y vuelta en recibir un 401 previsible.
//
//   - Al recibir un 401, como red de seguridad. El token puede haber dejado de
//     valer por causas que el cliente no puede prever: el servidor rotó su
//     clave de firma, un administrador desactivó la cuenta, o el reloj del
//     equipo iba adelantado.
//
// En los dos casos la renovación pasa por `renovarUnaVez`, que garantiza que
// veinte peticiones simultáneas provoquen una sola llamada de refresco. Sin
// eso, al volver de la suspensión el navegador dispararía todas las consultas
// a la vez, cada una gastaría el token de refresco —que es de un solo uso— y
// todas menos la primera fallarían, cerrando la sesión sin motivo.
// ============================================================================

import ky, { type KyInstance, type Options } from "ky"

import { MARGEN_RENOVACION_MS, configuracion } from "./configuracion"
import { ErrorApi, comoErrorApi, errorApiDesdeEstado } from "./errores"
import { comoSesion, establecerSesion, obtenerSesion } from "./sesion"

import type { RespuestaAutenticacion } from "@paquetes/modelos"

/** Raíz de la API, siempre con barra final: es lo que ky espera como prefijo. */
const RAIZ = configuracion.urlApi.endsWith("/") ? configuracion.urlApi : `${configuracion.urlApi}/`

/**
 * Cliente desnudo, sin autenticación ni renovación.
 *
 * Lo usan los endpoints anónimos —registro, inicio de sesión y el propio
 * refresco—. Que el refresco no pase por el cliente autenticado no es un
 * detalle: si lo hiciera, un refresco que devuelve 401 dispararía otro
 * refresco, y así hasta agotar la pila.
 */
export const apiPublica: KyInstance = ky.create({
	prefixUrl: RAIZ,
	timeout: configuracion.tiempoEspera,
	retry: 0,
})

/** Renovación en curso, compartida por todas las peticiones que la esperan. */
let renovacionEnCurso: Promise<string | null> | null = null

/** Qué hacer cuando la sesión ya no se puede salvar. */
let alPerderSesion: (() => void) | null = null

/**
 * Registra el aviso de sesión perdida.
 *
 * Lo invoca el arranque de la aplicación para llevar al usuario a la pantalla
 * de acceso. Se hace por devolución de llamada y no navegando desde aquí porque
 * este paquete no conoce el enrutador ni tiene por qué.
 *
 * @param manejador Función a ejecutar cuando la sesión deja de valer.
 */
export function alExpirarSesion(manejador: () => void): void {
	alPerderSesion = manejador
}

/** Cierra la sesión en local y avisa a quien corresponda. */
function abandonarSesion(): void {
	establecerSesion(null)
	alPerderSesion?.()
}

/**
 * Renueva la sesión, garantizando una sola llamada simultánea.
 *
 * @returns El token nuevo, o `null` si no se pudo renovar.
 */
async function renovarUnaVez(): Promise<string | null> {
	// Ya hay una renovación en vuelo: esperar su resultado en lugar de lanzar
	// otra. Este es el punto que impide quemar el token de refresco.
	if (renovacionEnCurso) return renovacionEnCurso

	const sesion = obtenerSesion()
	if (!sesion) return null

	renovacionEnCurso = (async () => {
		try {
			const respuesta = await apiPublica
				.post("auth/refrescar", { json: { tokenRefresco: sesion.tokenRefresco } })
				.json<RespuestaAutenticacion>()

			const renovada = comoSesion(respuesta)
			establecerSesion(renovada)

			return renovada.tokenAcceso
		} catch {
			// El token de refresco ya no vale: se gastó, caducó o el servidor lo
			// revocó. No hay forma de recuperar la sesión sin volver a entrar.
			abandonarSesion()
			return null
		} finally {
			// Se libera siempre, también al fallar: si no, un fallo puntual dejaría
			// la promesa rechazada cacheada y ninguna petición volvería a renovar.
			renovacionEnCurso = null
		}
	})()

	return renovacionEnCurso
}

/** Indica si al token le queda tan poco que conviene renovarlo ya. */
function estaPorCaducar(expiraEn: number): boolean {
	return Date.now() >= expiraEn - MARGEN_RENOVACION_MS
}

/**
 * Devuelve un token de acceso utilizable, renovándolo si hace falta.
 *
 * Es también lo que usa la conexión de SignalR: el hub pide el token en cada
 * negociación y en cada reconexión, y en ese momento puede llevar horas caducado.
 *
 * @returns Token válido, o `null` si no hay sesión que valga.
 */
export async function obtenerTokenValido(): Promise<string | null> {
	const sesion = obtenerSesion()
	if (!sesion) return null

	if (estaPorCaducar(sesion.expiraEn)) {
		return await renovarUnaVez()
	}

	return sesion.tokenAcceso
}

/**
 * Cliente autenticado. Es el que usa todo el resto de la aplicación.
 */
export const api: KyInstance = ky.create({
	prefixUrl: RAIZ,
	timeout: configuracion.tiempoEspera,

	// Se reintenta solo lo idempotente y solo ante fallos que pueden pasarse
	// solos. Un 401 no está en la lista: lo resuelve el gancho de más abajo, y
	// reintentarlo sin renovar el token daría otro 401.
	retry: {
		limit: 2,
		methods: ["get", "head"],
		statusCodes: [408, 429, 500, 502, 503, 504],
		backoffLimit: 3000,
	},

	hooks: {
		beforeRequest: [
			async (peticion) => {
				const token = await obtenerTokenValido()

				if (token) {
					peticion.headers.set("Authorization", `Bearer ${token}`)
				}
			},
		],

		afterResponse: [
			async (peticion, _opciones, respuesta) => {
				if (respuesta.status !== 401) return respuesta

				// Una subida lleva un cuerpo que ya se ha consumido: no se puede
				// repetir la petición tal cual. Se renueva igualmente para que el
				// siguiente intento del usuario funcione, pero no se reintenta aquí.
				if (peticion.bodyUsed) {
					await renovarUnaVez()
					return respuesta
				}

				const token = await renovarUnaVez()
				if (!token) return respuesta

				const reintento = new Request(peticion, {
					headers: new Headers(peticion.headers),
				})
				reintento.headers.set("Authorization", `Bearer ${token}`)

				// Se reintenta con la instancia desnuda de ky y no con `api`: la
				// petición ya lleva la URL resuelta y el token nuevo, y pasarla otra
				// vez por estos mismos ganchos abriría la puerta a un bucle si el
				// servidor insistiera en responder 401.
				return await ky(reintento, {
					retry: 0,
					timeout: configuracion.tiempoEspera,
				})
			},
		],
	},
})

/**
 * Ejecuta una llamada y normaliza cualquier fallo a `ErrorApi`.
 *
 * Envolver aquí evita que cada módulo repita el mismo try/catch y garantiza que
 * a la interfaz nunca le llegue un `HTTPError` en crudo, cuyo mensaje es el
 * texto del código de estado en inglés.
 *
 * @param operacion Llamada a ejecutar.
 */
export async function ejecutar<T>(operacion: () => Promise<T>): Promise<T> {
	try {
		return await operacion()
	} catch (error) {
		throw await comoErrorApi(error)
	}
}

/**
 * Sube un archivo con progreso, por `XMLHttpRequest` y no por `ky`.
 *
 * `fetch()` solo sabe informar del avance de una subida si el cuerpo se manda
 * como flujo (`ReadableStream` con `duplex: "half"`), que es justo lo que hace
 * `ky` en cuanto se le pasa `onUploadProgress`. El problema es que los
 * navegadores no dejan mandar un flujo así si la conexión no habla HTTP/2, y
 * este servidor habla HTTP/1.1 en claro —tanto en desarrollo como en cualquier
 * despliegue sin TLS delante—: la petición ni llega a salir y `fetch` falla con
 * un «TypeError: Failed to fetch» sin más explicación, así que los adjuntos
 * nunca llegaban a subir. `XMLHttpRequest` informa del avance de subida de
 * forma nativa, sin necesitar ningún flujo, así que es lo único que usa esta
 * función — el único sitio de la aplicación que necesita ese progreso.
 *
 * @param ruta Ruta relativa a la raíz de la API.
 * @param cuerpo Formulario multiparte a enviar.
 * @param alProgresar Notifica el avance, de 0 a 1.
 * @param senal Permite cancelar la subida.
 */
export async function subirConProgreso<T>(
	ruta: string,
	cuerpo: FormData,
	alProgresar?: (fraccion: number) => void,
	senal?: AbortSignal,
): Promise<T> {
	return await ejecutar(async () => {
		const token = await obtenerTokenValido()

		return await new Promise<T>((resolve, reject) => {
			const xhr = new XMLHttpRequest()
			xhr.open("POST", `${RAIZ}${ruta}`)

			if (token) {
				xhr.setRequestHeader("Authorization", `Bearer ${token}`)
			}

			if (alProgresar) {
				xhr.upload.addEventListener("progress", (evento) => {
					if (evento.lengthComputable) alProgresar(evento.loaded / evento.total)
				})
			}

			xhr.addEventListener("load", () => {
				if (xhr.status >= 200 && xhr.status < 300) {
					try {
						resolve(xhr.responseText ? (JSON.parse(xhr.responseText) as T) : (undefined as T))
					} catch (error) {
						reject(error)
					}

					return
				}

				// Igual que con el 401 del gancho de `api`: el cuerpo ya se ha enviado
				// y no se puede repetir tal cual, así que aquí solo se renueva el
				// token para que el siguiente intento del usuario funcione, no se
				// reintenta esta misma subida.
				if (xhr.status === 401) {
					void renovarUnaVez()
				}

				reject(errorApiDesdeEstado(xhr.status, xhr.responseText))
			})

			xhr.addEventListener("error", () => {
				reject(
					new ErrorApi("No se ha podido contactar con el servidor. Comprueba que está levantado.", 0),
				)
			})

			xhr.addEventListener("abort", () => {
				reject(new ErrorApi("Subida cancelada.", 0))
			})

			if (senal) {
				if (senal.aborted) {
					xhr.abort()
				} else {
					senal.addEventListener("abort", () => xhr.abort(), { once: true })
				}
			}

			xhr.send(cuerpo)
		})
	})
}

/**
 * Descarga un contenido binario protegido.
 *
 * Los adjuntos y las fotos de perfil exigen cabecera de autorización, así que no
 * se pueden poner en el `src` de una etiqueta de imagen: hay que traerlos por
 * aquí y envolverlos en una URL de objeto.
 *
 * @param ruta Ruta relativa a la raíz de la API.
 * @param opciones Opciones adicionales de la petición.
 */
export async function descargarBlob(ruta: string, opciones?: Options): Promise<Blob> {
	return await ejecutar(async () => {
		const respuesta = await api.get(ruta, opciones)
		return await respuesta.blob()
	})
}

export { ErrorApi }
