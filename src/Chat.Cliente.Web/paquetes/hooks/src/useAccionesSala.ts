// ============================================================================
// ACCIONES SOBRE SALAS
// ============================================================================
// Crear, unirse, invitar, abrir una conversación directa y salir. Todas siguen
// el mismo patrón: se hace la llamada, se refleja el resultado en el almacén y
// se invalidan las consultas que hayan podido quedarse desfasadas.
// ============================================================================

import { salas as apiSalas, mensajeDeError } from "@paquetes/api"
import { useAlmacenChat } from "@paquetes/estados"
import { useCallback, useState } from "react"

import { estaConectado, hub } from "./conexionHub"
import { useInvalidar } from "./useConsultas"

import type { Guid, Sala, SolicitudCrearSala } from "@paquetes/modelos"

/** Resultado del hook. */
interface UsoAccionesSala {
	/** Cierto mientras hay una acción en curso. */
	trabajando: boolean
	/** Crea una sala y la abre. */
	crear: (solicitud: SolicitudCrearSala) => Promise<Sala | null>
	/** Se une a una sala pública y la abre. */
	unirse: (salaId: Guid) => Promise<Sala | null>
	/** Abre o recupera la conversación directa con alguien. */
	abrirDirecta: (nombreUsuario: string) => Promise<Sala | null>
	/** Invita a alguien a una sala. */
	invitar: (salaId: Guid, nombreUsuario: string) => Promise<boolean>
	/** Abandona una sala. */
	salir: (salaId: Guid) => Promise<boolean>
	/** Marca una sala como leída en el servidor. */
	marcarLeida: (salaId: Guid) => Promise<void>
}

/**
 * Devuelve las acciones sobre salas.
 *
 * @param avisos Notificaciones de éxito y error para la interfaz.
 */
export function useAccionesSala(avisos?: {
	exito?: (mensaje: string) => void
	error?: (mensaje: string) => void
}): UsoAccionesSala {
	const [trabajando, setTrabajando] = useState(false)
	const invalidar = useInvalidar()

	/** Envuelve una acción con el indicador de trabajo y el aviso de error. */
	const ejecutar = useCallback(
		async <T>(accion: () => Promise<T>, alFallar: T): Promise<T> => {
			setTrabajando(true)

			try {
				return await accion()
			} catch (error) {
				avisos?.error?.(mensajeDeError(error))
				return alFallar
			} finally {
				setTrabajando(false)
			}
		},
		[avisos],
	)

	const crear = useCallback(
		(solicitud: SolicitudCrearSala) =>
			ejecutar(async () => {
				const sala = await apiSalas.crear(solicitud)
				const almacen = useAlmacenChat.getState()

				almacen.aplicarSala(sala)
				almacen.seleccionarSala(sala.id)
				invalidar.catalogo()

				avisos?.exito?.(`Sala «${sala.nombre}» creada.`)
				return sala
			}, null),
		[ejecutar, invalidar, avisos],
	)

	const unirse = useCallback(
		(salaId: Guid) =>
			ejecutar(async () => {
				// Por el hub cuando se puede: además de unir, suscribe la conexión al
				// grupo de la sala en el mismo viaje, así que los mensajes empiezan a
				// llegar de inmediato. Por HTTP habría que esperar a reconectar.
				const sala = estaConectado() ? await hub.unirseSala(salaId) : await apiSalas.unirse(salaId)

				if (!sala) return null

				const almacen = useAlmacenChat.getState()
				almacen.aplicarSala(sala)
				almacen.seleccionarSala(sala.id)
				invalidar.catalogo()

				return sala
			}, null),
		[ejecutar, invalidar],
	)

	const abrirDirecta = useCallback(
		(nombreUsuario: string) =>
			ejecutar(async () => {
				const sala = estaConectado()
					? await hub.abrirDirecta(nombreUsuario)
					: await apiSalas.abrirDirecta({ nombreUsuario })

				if (!sala) return null

				const almacen = useAlmacenChat.getState()
				almacen.aplicarSala(sala)
				almacen.seleccionarSala(sala.id)

				return sala
			}, null),
		[ejecutar],
	)

	const invitar = useCallback(
		(salaId: Guid, nombreUsuario: string) =>
			ejecutar(async () => {
				const resultado = await apiSalas.invitar(salaId, { nombreUsuario })

				if (resultado.exito) {
					avisos?.exito?.(resultado.mensaje)
					invalidar.miembros(salaId)
				} else {
					avisos?.error?.(resultado.mensaje)
				}

				return resultado.exito
			}, false),
		[ejecutar, invalidar, avisos],
	)

	const salir = useCallback(
		(salaId: Guid) =>
			ejecutar(async () => {
				const resultado = estaConectado()
					? await hub.salirSala(salaId)
					: await apiSalas.salir(salaId)

				if (resultado.exito) {
					useAlmacenChat.getState().quitarSala(salaId)
					invalidar.catalogo()
					avisos?.exito?.(resultado.mensaje)
				} else {
					avisos?.error?.(resultado.mensaje)
				}

				return resultado.exito
			}, false),
		[ejecutar, invalidar, avisos],
	)

	const marcarLeida = useCallback(async (salaId: Guid) => {
		try {
			if (estaConectado()) {
				await hub.marcarLeida(salaId)
			} else {
				await apiSalas.marcarLeida(salaId)
			}
		} catch {
			// Silencioso a propósito: el contador ya se ha puesto a cero en la
			// interfaz al abrir la sala. Si esto falla, volverá a aparecer en la
			// próxima recarga, que es preferible a molestar con un aviso.
		}
	}, [])

	return { trabajando, crear, unirse, abrirDirecta, invitar, salir, marcarLeida }
}
