// ============================================================================
// SESIÓN ACTIVA
// ============================================================================
// Dónde vive el token y quién puede tocarlo.
//
// La sesión se guarda aquí, en el paquete de la API, y no en el almacén de
// Zustand, aunque sea este último quien la enseñe en pantalla. El motivo es la
// dirección de las dependencias: el cliente HTTP necesita el token en cada
// petición, y si lo pidiera al almacén, el paquete de la API dependería del de
// estados, que a su vez depende de la API. Con la sesión aquí, la flecha va en
// un solo sentido y el almacén se limita a suscribirse a los cambios.
//
// Sobre guardarla en localStorage: es vulnerable a XSS, como cualquier token
// accesible desde JavaScript. La alternativa —cookie HttpOnly— exige que el
// servidor emita y valide cookies, con su protección CSRF, y este servidor
// entrega tokens JWT porque su primer cliente era una consola. Se asume el
// riesgo consciente: el token de acceso vive minutos y el de refresco es de un
// solo uso, así que la ventana de aprovechamiento es corta.
// ============================================================================

import { CLAVES, borrar, guardar, leer } from "@paquetes/utiles"

import type { RespuestaAutenticacion, Sesion } from "@paquetes/modelos"

/** Sesión en curso; `null` mientras nadie ha iniciado sesión. */
let sesionActual: Sesion | null = leer<Sesion | null>(CLAVES.sesion, null)

/** Suscriptores a los cambios de sesión. */
const oyentes = new Set<(sesion: Sesion | null) => void>()

/** Devuelve la sesión en curso. */
export function obtenerSesion(): Sesion | null {
	return sesionActual
}

/** Devuelve el token de acceso, o `null` si no hay sesión. */
export function obtenerToken(): string | null {
	return sesionActual?.tokenAcceso ?? null
}

/**
 * Reemplaza la sesión, la persiste y avisa a los suscriptores.
 *
 * @param sesion Sesión nueva, o `null` para cerrarla.
 */
export function establecerSesion(sesion: Sesion | null): void {
	sesionActual = sesion

	if (sesion) {
		guardar(CLAVES.sesion, sesion)
	} else {
		borrar(CLAVES.sesion)
	}

	for (const oyente of oyentes) {
		oyente(sesion)
	}
}

/**
 * Se suscribe a los cambios de sesión.
 *
 * @param oyente Función invocada con la sesión nueva.
 * @returns Función para cancelar la suscripción.
 */
export function suscribirSesion(oyente: (sesion: Sesion | null) => void): () => void {
	oyentes.add(oyente)
	return () => {
		oyentes.delete(oyente)
	}
}

/**
 * Convierte la respuesta de autenticación del servidor en una sesión.
 *
 * La caducidad se pasa a milisegundos desde época: es lo que hace falta para
 * compararla con `Date.now()` en cada petición, y hacerlo una vez aquí evita
 * interpretar la misma cadena miles de veces.
 *
 * @param respuesta Respuesta de registro, inicio de sesión o refresco.
 */
export function comoSesion(respuesta: RespuestaAutenticacion): Sesion {
	return {
		usuarioId: respuesta.usuarioId,
		nombreUsuario: respuesta.nombreUsuario,
		tokenAcceso: respuesta.tokenAcceso,
		tokenRefresco: respuesta.tokenRefresco,
		expiraEn: new Date(respuesta.expiraEn).getTime(),
		roles: respuesta.roles,
	}
}

/** Indica si la sesión incluye el rol de administrador. */
export function esAdministrador(sesion: Sesion | null): boolean {
	return sesion?.roles.includes("Administrador") ?? false
}
