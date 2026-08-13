// ============================================================================
// CICLO DE VIDA DE LA CONEXIÓN
// ============================================================================
// Engancha el hub a los almacenes. Se usa una sola vez, en la raíz de la zona
// autenticada; el resto de componentes leen el resultado de los almacenes y no
// vuelven a tocar la conexión.
// ============================================================================

import { configuracion } from "@paquetes/api"
import { useAlmacenChat, useAlmacenSesion } from "@paquetes/estados"
import { useEffect, useRef } from "react"

import { conectar, desconectar, type ManejadoresHub } from "./conexionHub"

/** Milisegundos entre repasos de los avisos de escritura caducados. */
const INTERVALO_CADUCIDAD_MS = 1_000

/**
 * Mantiene abierta la conexión con el hub mientras haya sesión y traslada todo
 * lo que llega a los almacenes.
 *
 * @param alAvisar Notificación de sucesos que la interfaz quiere mostrar: los
 *   errores recuperables del servidor y los mensajes que llegan de otros.
 */
export function useConexionChat(alAvisar?: {
	error?: (mensaje: string) => void
	mensajeNuevo?: (salaId: string, nombreUsuario: string, texto: string) => void
}): void {
	const sesion = useAlmacenSesion((estado) => estado.sesion)

	// Las notificaciones cambian de identidad en cada render del componente que
	// las pasa. Guardarlas en una referencia evita que el efecto se vuelva a
	// ejecutar —y la conexión se cierre y se reabra— por ese motivo.
	const avisos = useRef(alAvisar)
	avisos.current = alAvisar

	useEffect(() => {
		if (!sesion) {
			void desconectar()
			useAlmacenChat.getState().limpiar()
			return
		}

		const almacen = useAlmacenChat.getState()

		const manejadores: ManejadoresHub = {
			alConectar: (_nombreUsuario, salas) => {
				// Es la fuente de verdad al conectar y al reconectar: sustituye la
				// lista entera en lugar de fusionarla, para que una sala de la que se
				// salió desde otro dispositivo desaparezca de verdad.
				almacen.fijarSalas(salas)
			},

			alRecibirMensaje: (mensaje) => {
				const estado = useAlmacenChat.getState()
				estado.recibirMensaje(mensaje, sesion.usuarioId)

				const esAjeno = mensaje.usuarioId !== sesion.usuarioId
				const estaMirando =
					estado.salaActivaId === mensaje.salaId && document.visibilityState === "visible"

				if (esAjeno && !estaMirando) {
					avisos.current?.mensajeNuevo?.(mensaje.salaId, mensaje.nombreUsuario, mensaje.texto)
				}
			},

			alEscribir: (salaId, nombreUsuario) => {
				useAlmacenChat.getState().anotarEscribiendo(salaId, nombreUsuario)
			},

			alCambiarPresencia: (presencia) => {
				useAlmacenChat.getState().aplicarPresencia(presencia)
			},

			alCrearseSala: (sala) => {
				// Una sala pública nueva se anuncia a todo el mundo, pero en la lista
				// lateral solo van las propias: quien no es miembro la verá cuando
				// abra el explorador de salas.
				if (sala.esMiembro) {
					useAlmacenChat.getState().aplicarSala(sala)
				}
			},

			alHaberSalaDisponible: (sala) => {
				// Alguien ha abierto una conversación directa o ha invitado a una
				// privada: aparece en la lista sin tener que recargar.
				useAlmacenChat.getState().aplicarSala(sala)
			},

			alUnirseUsuario: () => {
				// El aviso llega a todos los clientes, no solo a los de la sala. No se
				// muestra nada: en una plataforma con gente entrando y saliendo sería
				// ruido constante. El recuento de miembros se actualiza al abrir la sala.
			},

			alSalirUsuario: () => {
				// Mismo motivo que en el caso anterior.
			},

			alRecibirError: (mensaje) => {
				avisos.current?.error?.(mensaje)
			},

			alCambiarEstado: (estado) => {
				useAlmacenChat.getState().fijarEstadoConexion(estado)
			},
		}

		void conectar(manejadores).catch(() => {
			// El fallo ya ha dejado el estado en «desconectado», que es lo que lee la
			// barra de estado para avisar al usuario. La política de reintento se
			// encarga del resto.
		})

		// Los avisos de «está escribiendo» no se retiran solos: el servidor manda
		// que alguien empieza, nunca que ha parado. Este repaso los caduca.
		const temporizador = window.setInterval(() => {
			useAlmacenChat.getState().caducarEscribiendo(configuracion.msEscribiendo)
		}, INTERVALO_CADUCIDAD_MS)

		return () => {
			window.clearInterval(temporizador)
			void desconectar()
		}
	}, [sesion])
}
