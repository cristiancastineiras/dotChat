// ============================================================================
// AUTENTICACIÓN
// ============================================================================

import { apiPublica, ejecutar } from "./cliente"
import { comoSesion, establecerSesion } from "./sesion"

import type {
	RespuestaAutenticacion,
	Sesion,
	SolicitudLogin,
	SolicitudRegistro,
} from "@paquetes/modelos"

/**
 * Da de alta una cuenta y deja la sesión iniciada.
 *
 * @param solicitud Nombre de usuario, correo y contraseña.
 */
export async function registrar(solicitud: SolicitudRegistro): Promise<Sesion> {
	return await ejecutar(async () => {
		const respuesta = await apiPublica
			.post("auth/registrar", { json: solicitud })
			.json<RespuestaAutenticacion>()

		const sesion = comoSesion(respuesta)
		establecerSesion(sesion)

		return sesion
	})
}

/**
 * Inicia sesión.
 *
 * @param solicitud Nombre de usuario y contraseña.
 */
export async function iniciarSesion(solicitud: SolicitudLogin): Promise<Sesion> {
	return await ejecutar(async () => {
		const respuesta = await apiPublica
			.post("auth/login", { json: solicitud })
			.json<RespuestaAutenticacion>()

		const sesion = comoSesion(respuesta)
		establecerSesion(sesion)

		return sesion
	})
}

/**
 * Cierra la sesión en este navegador.
 *
 * No hay llamada al servidor porque no hay nada que cerrar: el token de acceso
 * no es revocable —se valida solo por su firma— y el de refresco caduca por sí
 * mismo. Olvidarlos aquí es exactamente lo que hace falta.
 */
export function cerrarSesion(): void {
	establecerSesion(null)
}
