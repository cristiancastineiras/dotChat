// ============================================================================
// ALMACÉN DE SESIÓN
// ============================================================================
// Refleja en React la sesión que gestiona el paquete de la API.
//
// La fuente de verdad es la de `@paquetes/api`, porque el cliente HTTP la
// necesita fuera de cualquier componente. Este almacén se limita a suscribirse
// a ella y a exponerla de forma que los componentes se vuelvan a dibujar cuando
// cambia. Duplicar el dato es intencionado; lo que no se duplica es la
// autoridad sobre él.
// ============================================================================

import {
	autenticacion,
	esAdministrador as calcularEsAdministrador,
	obtenerSesion,
	suscribirSesion,
	usuarios,
	vaciarAvatares,
} from "@paquetes/api"
import { create } from "zustand"

import type { Perfil, Sesion, SolicitudLogin, SolicitudRegistro } from "@paquetes/modelos"

/** Estado y acciones de la sesión. */
interface EstadoSesion {
	/** Sesión en curso; `null` si nadie ha entrado. */
	sesion: Sesion | null

	/**
	 * Perfil completo del usuario. Llega en una segunda petición porque la
	 * respuesta de autenticación no incluye ni el correo ni la foto.
	 */
	perfil: Perfil | null

	/** Cierto mientras se comprueba si la sesión guardada sigue valiendo. */
	comprobando: boolean

	/** Indica si hay sesión iniciada. */
	autenticado: () => boolean

	/** Indica si la cuenta tiene rol de administrador. */
	esAdministrador: () => boolean

	/** Inicia sesión y carga el perfil. */
	entrar: (solicitud: SolicitudLogin) => Promise<void>

	/** Crea una cuenta, la deja iniciada y carga el perfil. */
	registrarse: (solicitud: SolicitudRegistro) => Promise<void>

	/** Cierra la sesión y limpia todo rastro local. */
	salir: () => void

	/** Vuelve a pedir el perfil; se usa tras cambiar la foto. */
	recargarPerfil: () => Promise<void>

	/** Sustituye el perfil por el que devuelve una operación. */
	fijarPerfil: (perfil: Perfil) => void

	/**
	 * Comprueba al arrancar si la sesión guardada sigue siendo válida pidiendo
	 * el perfil. Si el token ya no vale, el cliente HTTP intenta renovarlo y, si
	 * tampoco puede, cierra la sesión por su cuenta.
	 */
	restaurar: () => Promise<void>
}

export const useAlmacenSesion = create<EstadoSesion>()((establecer, obtener) => ({
	sesion: obtenerSesion(),
	perfil: null,
	comprobando: true,

	autenticado: () => obtener().sesion !== null,

	esAdministrador: () => calcularEsAdministrador(obtener().sesion),

	entrar: async (solicitud) => {
		await autenticacion.iniciarSesion(solicitud)
		await obtener().recargarPerfil()
	},

	registrarse: async (solicitud) => {
		await autenticacion.registrar(solicitud)
		await obtener().recargarPerfil()
	},

	salir: () => {
		autenticacion.cerrarSesion()

		// Las fotos descargadas son de conversaciones privadas: no deben quedar
		// accesibles para quien entre después en el mismo navegador.
		vaciarAvatares()

		establecer({ perfil: null })
	},

	recargarPerfil: async () => {
		try {
			establecer({ perfil: await usuarios.obtenerPerfil() })
		} catch {
			// Si falla, la interfaz sigue funcionando con lo que trae el token: el
			// nombre de usuario. Solo se pierde el correo y la foto.
		}
	},

	fijarPerfil: (perfil) => {
		establecer({ perfil })
	},

	restaurar: async () => {
		if (!obtenerSesion()) {
			establecer({ comprobando: false })
			return
		}

		await obtener().recargarPerfil()
		establecer({ comprobando: false })
	},
}))

// El almacén sigue a la sesión de la API, no al revés. Así, cuando el cliente
// HTTP cierra la sesión por su cuenta —porque el token de refresco ya no vale—,
// la interfaz se entera sin que nadie tenga que avisarla.
suscribirSesion((sesion) => {
	useAlmacenSesion.setState({ sesion, ...(sesion ? {} : { perfil: null }) })
})
