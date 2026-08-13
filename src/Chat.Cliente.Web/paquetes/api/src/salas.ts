// ============================================================================
// SALAS Y CONVERSACIONES
// ============================================================================

import { api, ejecutar } from "./cliente"

import type {
	Guid,
	MiembroSala,
	ResultadoOperacion,
	Sala,
	SolicitudConversacionDirecta,
	SolicitudCrearSala,
	SolicitudInvitar,
} from "@paquetes/modelos"

/**
 * Catálogo de salas visibles: las públicas y las privadas propias.
 * Es lo que se enseña en el explorador de salas.
 */
export async function listarCatalogo(): Promise<Sala[]> {
	return await ejecutar(() => api.get("salas").json<Sala[]>())
}

/**
 * Salas y conversaciones a las que pertenece el usuario, con sus mensajes
 * pendientes. Es la lista lateral.
 */
export async function listarPropias(): Promise<Sala[]> {
	return await ejecutar(() => api.get("salas/mias").json<Sala[]>())
}

/**
 * Crea una sala.
 *
 * @param solicitud Nombre, descripción y visibilidad.
 */
export async function crear(solicitud: SolicitudCrearSala): Promise<Sala> {
	return await ejecutar(() => api.post("salas", { json: solicitud }).json<Sala>())
}

/**
 * Abre o recupera la conversación directa con otra persona.
 *
 * Es idempotente por diseño: si ya existe la conversación, el servidor devuelve
 * la que hay en lugar de crear una segunda.
 *
 * @param solicitud Nombre o identificador del interlocutor.
 */
export async function abrirDirecta(solicitud: SolicitudConversacionDirecta): Promise<Sala> {
	return await ejecutar(() => api.post("salas/directas", { json: solicitud }).json<Sala>())
}

/**
 * Miembros de una sala con su estado de conexión.
 *
 * @param salaId Sala consultada.
 */
export async function listarMiembros(salaId: Guid): Promise<MiembroSala[]> {
	return await ejecutar(() => api.get(`salas/${salaId}/miembros`).json<MiembroSala[]>())
}

/**
 * Une al usuario a una sala pública.
 *
 * @param salaId Sala a la que unirse.
 */
export async function unirse(salaId: Guid): Promise<Sala> {
	return await ejecutar(() => api.post(`salas/${salaId}/unirse`).json<Sala>())
}

/**
 * Incorpora a otra persona a la sala. Es la única vía de entrada a una privada.
 *
 * @param salaId Sala destino.
 * @param solicitud Nombre del invitado.
 */
export async function invitar(
	salaId: Guid,
	solicitud: SolicitudInvitar,
): Promise<ResultadoOperacion> {
	return await ejecutar(() =>
		api.post(`salas/${salaId}/invitar`, { json: solicitud }).json<ResultadoOperacion>(),
	)
}

/**
 * Pone a cero los mensajes pendientes del usuario en la sala.
 *
 * @param salaId Sala leída.
 */
export async function marcarLeida(salaId: Guid): Promise<ResultadoOperacion> {
	return await ejecutar(() => api.post(`salas/${salaId}/leida`).json<ResultadoOperacion>())
}

/**
 * Saca al usuario de una sala.
 *
 * @param salaId Sala que se abandona.
 */
export async function salir(salaId: Guid): Promise<ResultadoOperacion> {
	return await ejecutar(() => api.post(`salas/${salaId}/salir`).json<ResultadoOperacion>())
}
