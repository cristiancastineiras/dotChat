// ============================================================================
// MENSAJES Y ADJUNTOS
// ============================================================================

import { api, descargarBlob, ejecutar, subirConProgreso } from "./cliente"
import { configuracion } from "./configuracion"

import type { Adjunto, Guid, Mensaje, SolicitudEnviarMensaje } from "@paquetes/modelos"

/**
 * Página del historial de una sala, de la más reciente hacia atrás.
 *
 * @param salaId Sala consultada.
 * @param anteriorA Fecha del mensaje más antiguo ya cargado; sin ella se pide
 *   la página más reciente.
 * @param cantidad Mensajes por página.
 * @returns Mensajes en el orden que los devuelve el servidor.
 */
export async function obtenerHistorial(
	salaId: Guid,
	anteriorA?: string,
	cantidad = configuracion.mensajesPorPagina,
): Promise<Mensaje[]> {
	const parametros = new URLSearchParams({
		salaId,
		cantidad: String(cantidad),
	})

	if (anteriorA) {
		parametros.set("anteriorA", anteriorA)
	}

	return await ejecutar(() => api.get(`mensajes?${parametros.toString()}`).json<Mensaje[]>())
}

/**
 * Publica un mensaje por HTTP.
 *
 * La vía normal es el hub, que difunde a la sala en el mismo viaje. Esta existe
 * como reserva para cuando la conexión en tiempo real está caída: el mensaje se
 * publica igual y los demás lo reciben al reconectar.
 *
 * @param solicitud Sala, texto, identificador de envío y adjunto opcional.
 */
export async function enviar(solicitud: SolicitudEnviarMensaje): Promise<Mensaje> {
	return await ejecutar(() => api.post("mensajes", { json: solicitud }).json<Mensaje>())
}

/**
 * Sube un archivo a una sala y devuelve el identificador con el que publicarlo.
 *
 * Va por HTTP y no por el hub a propósito: SignalR limita el tamaño de mensaje
 * a decenas de kilobytes, y meter megabytes por ese canal bloquearía la
 * conversación de todos los miembros mientras dura la transferencia.
 *
 * @param salaId Sala para la que se sube.
 * @param archivo Archivo elegido por el usuario.
 * @param alProgresar Notifica el avance, de 0 a 1.
 * @param senal Permite cancelar la subida.
 * @param duracionMs Duración en milisegundos, si es una nota de voz grabada en el
 *   cliente. El servidor la ignora si el contenido no resulta ser audio.
 */
export async function subirAdjunto(
	salaId: Guid,
	archivo: File,
	alProgresar?: (fraccion: number) => void,
	senal?: AbortSignal,
	duracionMs?: number,
): Promise<Adjunto> {
	const cuerpo = new FormData()
	cuerpo.append("archivo", archivo, archivo.name)

	if (duracionMs !== undefined) {
		cuerpo.append("duracionMs", String(Math.round(duracionMs)))
	}

	// Por `XMLHttpRequest` y no por `api.post`: ver el porqué en
	// `subirConProgreso`, en `cliente.ts`.
	return await subirConProgreso<Adjunto>(`adjuntos?salaId=${salaId}`, cuerpo, alProgresar, senal)
}

/**
 * Descarga el contenido de un adjunto.
 *
 * @param adjuntoId Adjunto pedido.
 */
export async function descargarAdjunto(adjuntoId: Guid): Promise<Blob> {
	return await descargarBlob(`adjuntos/${adjuntoId}`)
}
