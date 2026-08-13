// ============================================================================
// TIPOS PROPIOS DEL CLIENTE
// ============================================================================
// Conceptos que no existen en el servidor y solo tienen sentido en la interfaz:
// una sesión guardada entre recargas, un mensaje que aún no ha llegado a
// publicarse, el estado de la conexión en tiempo real.
// ============================================================================

import type { Guid, Mensaje } from "./dominio"

/** Sesión persistida entre recargas de la página. */
export interface Sesion {
	readonly usuarioId: Guid
	readonly nombreUsuario: string
	readonly tokenAcceso: string
	readonly tokenRefresco: string
	/** Milisegundos desde época en que caduca el token de acceso. */
	readonly expiraEn: number
	readonly roles: readonly string[]
}

/**
 * Situación de la conexión con el hub. La interfaz la usa para decidir si puede
 * enviar, si debe avisar de que está reconectando y si el historial que muestra
 * puede haberse quedado atrás.
 */
export type EstadoConexion = "desconectado" | "conectando" | "conectado" | "reconectando"

/**
 * Situación de un mensaje que ha escrito el usuario. Un mensaje se pinta en
 * cuanto se envía, sin esperar al servidor; su estado dice si ya está
 * confirmado, si sigue en camino o si hubo que rendirse.
 */
export type EstadoEnvio = "enviando" | "enviado" | "fallido"

/**
 * Mensaje mostrado en la conversación. Es el del servidor más lo que la interfaz
 * necesita saber sobre su envío.
 *
 * Un mensaje propio nace con `estado: "enviando"` y un identificador provisional;
 * cuando el servidor lo confirma, se sustituye por el definitivo conservando el
 * `identificadorEnvio`, que es lo que permite reconocerlo tanto en la respuesta
 * directa como en la difusión que llega por el hub.
 */
export interface MensajeVista extends Mensaje {
	/** Presente solo en los mensajes enviados desde esta pestaña. */
	readonly identificadorEnvio?: Guid
	/** Ausente en los mensajes que llegan del historial o de otros usuarios. */
	readonly estado?: EstadoEnvio
	/**
	 * Adjunto todavía en el navegador, mientras el mensaje no está confirmado.
	 * Permite enseñar la imagen al instante en lugar de un hueco gris.
	 */
	readonly vistaPreviaLocal?: string
}

/** Alguien que está escribiendo en una sala, con el momento del último aviso. */
export interface Escribiendo {
	readonly nombreUsuario: string
	/** Milisegundos desde época; pasado el margen configurado, el aviso se retira. */
	readonly desde: number
}

/** Archivo elegido en el redactor y aún no enviado. */
export interface AdjuntoPendiente {
	readonly archivo: File
	/** URL de objeto para la previsualización; hay que revocarla al descartarlo. */
	readonly vistaPrevia: string | null
	readonly esImagen: boolean
}
