// ============================================================================
// ENVÍO DE MENSAJES
// ============================================================================
// El mensaje aparece en pantalla en cuanto se pulsa enviar, sin esperar al
// servidor, y se reconcilia cuando este lo confirma. Es lo que hace que la
// conversación se sienta instantánea incluso con latencia alta.
//
// El identificador de envío lo genera el cliente y viaja con el mensaje: el
// servidor lo usa para descartar duplicados, así que reintentar un envío que
// quizá llegó no publica el mensaje dos veces.
// ============================================================================

import { ErrorApi, mensajes as apiMensajes, mensajeDeError } from "@paquetes/api"
import { useAlmacenChat, useAlmacenSesion } from "@paquetes/estados"
import { useCallback, useState } from "react"

import { estaConectado, hub } from "./conexionHub"

import type { Adjunto, Guid, MensajeVista } from "@paquetes/modelos"

/** Lo que hace falta para publicar un mensaje. */
export interface EnvioMensaje {
	salaId: Guid
	texto: string
	/** Archivo a adjuntar, si lo hay. */
	archivo?: File | null
}

/** Resultado del hook. */
interface UsoEnvio {
	/** Publica un mensaje, con su adjunto si lo lleva. */
	enviar: (envio: EnvioMensaje) => Promise<boolean>
	/** Reintenta un envío que había fallado. */
	reintentar: (mensaje: MensajeVista) => Promise<void>
	/** Descarta un envío fallido. */
	descartar: (mensaje: MensajeVista) => void
	/** Avance de la subida del adjunto, de 0 a 1; `null` si no hay ninguna. */
	progresoSubida: number | null
}

/**
 * Devuelve las acciones de envío de la conversación.
 *
 * @param alFallar Aviso de error para la interfaz.
 */
export function useEnviarMensaje(alFallar?: (mensaje: string) => void): UsoEnvio {
	const sesion = useAlmacenSesion((estado) => estado.sesion)
	const [progresoSubida, setProgresoSubida] = useState<number | null>(null)

	/**
	 * Publica el mensaje, por el hub si hay conexión y por HTTP si no.
	 *
	 * La alternativa por HTTP no es un adorno: si el hub está reconectando, el
	 * mensaje se publica igualmente y los demás lo reciben en cuanto vuelvan.
	 */
	const publicar = useCallback(
		async (salaId: Guid, texto: string, identificadorEnvio: Guid, adjuntoId: Guid | null) => {
			if (estaConectado()) {
				const confirmado = await hub.enviarMensaje(salaId, texto, identificadorEnvio, adjuntoId)

				// El hub devuelve nulo cuando rechaza el envío —límite de frecuencia o
				// error de dominio— y avisa por separado con `ErrorRecibido`.
				if (!confirmado) {
					throw new Error("El servidor ha rechazado el mensaje.")
				}

				return confirmado
			}

			return await apiMensajes.enviar({
				salaId,
				texto,
				identificadorEnvio,
				adjuntoId,
			})
		},
		[],
	)

	const enviar = useCallback(
		async ({ salaId, texto, archivo }: EnvioMensaje): Promise<boolean> => {
			if (!sesion) return false

			const limpio = texto.trim()
			if (!limpio && !archivo) return false

			const identificadorEnvio = crypto.randomUUID()
			const almacen = useAlmacenChat.getState()

			// Previsualización local de la imagen: sin ella, el mensaje se vería como
			// un hueco gris hasta que terminase la subida.
			const vistaPrevia =
				archivo && archivo.type.startsWith("image/") ? URL.createObjectURL(archivo) : null

			const provisional: MensajeVista = {
				id: identificadorEnvio,
				salaId,
				salaNombre: almacen.salas[salaId]?.nombre ?? "",
				usuarioId: sesion.usuarioId,
				nombreUsuario: sesion.nombreUsuario,
				texto: limpio,
				fechaEnvio: new Date().toISOString(),
				adjunto: archivo ? adjuntoProvisional(archivo) : null,
				identificadorEnvio,
				estado: "enviando",
				...(vistaPrevia ? { vistaPreviaLocal: vistaPrevia } : {}),
			}

			almacen.agregarPendiente(provisional)

			try {
				let adjuntoId: Guid | null = null

				if (archivo) {
					setProgresoSubida(0)

					const subido = await apiMensajes.subirAdjunto(salaId, archivo, (fraccion) =>
						setProgresoSubida(fraccion),
					)

					adjuntoId = subido.id
				}

				const confirmado = await publicar(salaId, limpio, identificadorEnvio, adjuntoId)

				useAlmacenChat.getState().confirmarEnvio(salaId, identificadorEnvio, confirmado)

				return true
			} catch (error) {
				useAlmacenChat.getState().marcarEnvioFallido(salaId, identificadorEnvio)

				// El aviso de límite de frecuencia ya lo enseña el hub por su cuenta;
				// repetirlo aquí duplicaría el mensaje en pantalla.
				if (!(error instanceof ErrorApi && error.esDemasiadasPeticiones)) {
					alFallar?.(mensajeDeError(error))
				}

				return false
			} finally {
				setProgresoSubida(null)

				// La previsualización se suelta cuando el mensaje ya está confirmado y
				// la imagen se sirve desde el servidor.
				if (vistaPrevia) {
					setTimeout(() => URL.revokeObjectURL(vistaPrevia), 30_000)
				}
			}
		},
		[sesion, publicar, alFallar],
	)

	const reintentar = useCallback(
		async (mensaje: MensajeVista) => {
			if (!mensaje.identificadorEnvio) return

			const almacen = useAlmacenChat.getState()

			// Se descarta el fallido y se vuelve a enviar. El adjunto no se puede
			// recuperar —los bytes ya no están en memoria—, así que un mensaje con
			// archivo hay que volver a componerlo.
			almacen.descartarPendiente(mensaje.salaId, mensaje.identificadorEnvio)

			await enviar({ salaId: mensaje.salaId, texto: mensaje.texto })
		},
		[enviar],
	)

	const descartar = useCallback((mensaje: MensajeVista) => {
		if (!mensaje.identificadorEnvio) return

		useAlmacenChat.getState().descartarPendiente(mensaje.salaId, mensaje.identificadorEnvio)
	}, [])

	return { enviar, reintentar, descartar, progresoSubida }
}

/**
 * Ficha provisional del adjunto mientras se sube.
 *
 * Los datos reales —nombre saneado, tipo y dimensiones— los decide el servidor
 * al normalizar el archivo; esto solo sirve para dibujar algo entretanto.
 */
function adjuntoProvisional(archivo: File): Adjunto {
	const esImagen = archivo.type.startsWith("image/")

	return {
		id: "",
		nombreArchivo: archivo.name,
		tipoMime: archivo.type,
		tipo: esImagen ? 1 : 0,
		tamanoBytes: archivo.size,
		ancho: null,
		alto: null,
		esImagen,
	}
}
