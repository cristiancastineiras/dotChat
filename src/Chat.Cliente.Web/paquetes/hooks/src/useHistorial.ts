// ============================================================================
// HISTORIAL DE UNA SALA
// ============================================================================
// Carga la primera página al abrir la conversación y las anteriores según se
// sube. No usa React Query a propósito: el historial no es un dato de solo
// lectura que se pueda invalidar y volver a pedir, sino una lista que además
// crece por el hub. Tenerlo en el almacén, con una sola representación, evita
// reconciliar dos fuentes que se contradirían constantemente.
// ============================================================================

import { ErrorApi, configuracion, mensajes as apiMensajes } from "@paquetes/api"
import { useAlmacenChat } from "@paquetes/estados"
import { useCallback, useEffect, useRef } from "react"

import type { Guid } from "@paquetes/modelos"

/** Resultado del hook. */
interface UsoHistorial {
	/** Cierto mientras hay una página en vuelo. */
	cargando: boolean
	/** Cierto cuando ya no queda nada más hacia atrás. */
	completo: boolean
	/** Pide la página anterior a la más antigua ya cargada. */
	cargarAnteriores: () => Promise<void>
}

/**
 * Gestiona el historial de la sala abierta.
 *
 * @param salaId Sala abierta; `null` cuando no hay ninguna.
 * @param alFallar Aviso de error para la interfaz.
 */
export function useHistorial(
	salaId: Guid | null,
	alFallar?: (mensaje: string) => void,
): UsoHistorial {
	const cargando = useAlmacenChat((estado) =>
		salaId ? (estado.cargandoHistorial[salaId] ?? false) : false,
	)

	const completo = useAlmacenChat((estado) =>
		salaId ? (estado.historialCompleto[salaId] ?? false) : false,
	)

	// Salas cuya primera página ya se pidió. Sin esto, cada vez que el usuario
	// vuelve a una conversación se recargaría el historial entero, tirando los
	// mensajes que ya tenía y perdiendo la posición del desplazamiento.
	const cargadas = useRef(new Set<Guid>())

	const alFallarRef = useRef(alFallar)
	alFallarRef.current = alFallar

	useEffect(() => {
		if (!salaId || cargadas.current.has(salaId)) return

		let cancelado = false
		const almacen = useAlmacenChat.getState()

		cargadas.current.add(salaId)
		almacen.marcarCargandoHistorial(salaId, true)

		void (async () => {
			try {
				const pagina = await apiMensajes.obtenerHistorial(salaId)
				if (cancelado) return

				useAlmacenChat.getState().fijarHistorial(salaId, pagina)

				// Una página incompleta significa que no hay nada más detrás.
				if (pagina.length < configuracion.mensajesPorPagina) {
					useAlmacenChat.getState().marcarHistorialCompleto(salaId, true)
				}
			} catch (error) {
				if (cancelado) return

				// Se olvida para que un reintento —volver a entrar en la sala— pueda
				// pedirlo de nuevo en lugar de quedarse con la conversación vacía.
				cargadas.current.delete(salaId)

				alFallarRef.current?.(
					error instanceof ErrorApi ? error.message : "No se ha podido cargar la conversación.",
				)
			} finally {
				if (!cancelado) {
					useAlmacenChat.getState().marcarCargandoHistorial(salaId, false)
				}
			}
		})()

		return () => {
			cancelado = true
		}
	}, [salaId])

	const cargarAnteriores = useCallback(async () => {
		if (!salaId) return

		const estado = useAlmacenChat.getState()

		if (estado.cargandoHistorial[salaId] || estado.historialCompleto[salaId]) {
			return
		}

		// Se pagina desde el mensaje más antiguo que ya se tiene, saltándose los
		// envíos propios aún sin confirmar: esos no existen para el servidor y su
		// fecha provisional descolocaría la paginación.
		const masAntiguo = (estado.mensajes[salaId] ?? []).find(
			(mensaje) => mensaje.estado === undefined || mensaje.estado === "enviado",
		)

		if (!masAntiguo) return

		estado.marcarCargandoHistorial(salaId, true)

		try {
			const pagina = await apiMensajes.obtenerHistorial(salaId, masAntiguo.fechaEnvio)

			useAlmacenChat.getState().anteponerHistorial(salaId, pagina)

			if (pagina.length < configuracion.mensajesPorPagina) {
				useAlmacenChat.getState().marcarHistorialCompleto(salaId, true)
			}
		} catch (error) {
			alFallarRef.current?.(
				error instanceof ErrorApi
					? error.message
					: "No se han podido cargar los mensajes anteriores.",
			)
		} finally {
			useAlmacenChat.getState().marcarCargandoHistorial(salaId, false)
		}
	}, [salaId])

	return { cargando, completo, cargarAnteriores }
}
