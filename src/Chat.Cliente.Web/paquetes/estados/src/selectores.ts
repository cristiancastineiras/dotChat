// ============================================================================
// SELECTORES
// ============================================================================
// Lecturas derivadas del almacén del chat, envueltas en hooks.
//
// Los que devuelven listas u objetos nuevos pasan por `useShallow`: sin él, cada
// render construiría un array distinto, Zustand lo vería como un cambio y el
// componente se volvería a dibujar sin parar. Con la comparación superficial,
// solo se redibuja si cambia alguno de los elementos de verdad.
// ============================================================================

import { aMilisegundos } from "@paquetes/utiles"
import { useShallow } from "zustand/shallow"

import { useAlmacenChat } from "./almacenChat"

import type { Guid, MensajeVista, Sala } from "@paquetes/modelos"

/** Lista vacía compartida, para no crear una nueva en cada lectura sin datos. */
const SIN_MENSAJES: readonly MensajeVista[] = []

/** Lista vacía compartida para los avisos de escritura. */
const SIN_ESCRIBIENDO: readonly string[] = []

/**
 * Conversaciones ordenadas para la lista lateral: primero las que tienen
 * actividad más reciente, y las que aún no tienen ningún mensaje al final por
 * su fecha de creación.
 */
export function useSalasOrdenadas(): Sala[] {
	return useAlmacenChat(
		useShallow((estado) =>
			Object.values(estado.salas).toSorted((a, b) => {
				const actividadA = aMilisegundos(a.fechaUltimaActividad ?? a.fechaCreacion)
				const actividadB = aMilisegundos(b.fechaUltimaActividad ?? b.fechaCreacion)
				return actividadB - actividadA
			}),
		),
	)
}

/** Sala abierta en este momento. */
export function useSalaActiva(): Sala | null {
	return useAlmacenChat((estado) =>
		estado.salaActivaId ? (estado.salas[estado.salaActivaId] ?? null) : null,
	)
}

/**
 * Mensajes de una sala.
 *
 * @param salaId Sala consultada; `null` mientras no hay ninguna abierta.
 */
export function useMensajes(salaId: Guid | null): readonly MensajeVista[] {
	return useAlmacenChat(
		useShallow((estado) => (salaId ? (estado.mensajes[salaId] ?? SIN_MENSAJES) : SIN_MENSAJES)),
	)
}

/**
 * Nombres de quienes están escribiendo en una sala, excluyendo al propio
 * usuario: el servidor difunde el aviso solo a los demás, pero al reconectar
 * podría llegar una señal propia y verse uno mismo escribiendo.
 *
 * @param salaId Sala consultada.
 * @param nombrePropio Nombre del usuario actual.
 */
export function useEscribiendoEn(salaId: Guid | null, nombrePropio: string): readonly string[] {
	return useAlmacenChat(
		useShallow((estado) => {
			if (!salaId) return SIN_ESCRIBIENDO

			const quienes = estado.escribiendo[salaId]
			if (!quienes || quienes.length === 0) return SIN_ESCRIBIENDO

			return quienes.map((quien) => quien.nombreUsuario).filter((nombre) => nombre !== nombrePropio)
		}),
	)
}

/** Total de mensajes sin leer en todas las conversaciones. */
export function useTotalSinLeer(): number {
	return useAlmacenChat((estado) =>
		Object.values(estado.salas).reduce((total, sala) => total + sala.mensajesSinLeer, 0),
	)
}

/**
 * Indica si alguien está conectado.
 *
 * @param usuarioId Usuario consultado.
 */
export function useEstaEnLinea(usuarioId: Guid | null): boolean {
	return useAlmacenChat((estado) =>
		usuarioId ? (estado.presencia[usuarioId]?.enLinea ?? false) : false,
	)
}

/** Situación de la conexión con el hub. */
export function useEstadoConexion() {
	return useAlmacenChat((estado) => estado.estadoConexion)
}
