// ============================================================================
// CUERPOS DE PETICIÓN
// ============================================================================
// Lo que la aplicación envía al servidor. Se separan de los tipos de dominio
// porque no son lo mismo: al crear una sala se manda un nombre y poco más,
// mientras que la sala que vuelve trae fechas, contadores y pertenencia.
// ============================================================================

import type { Guid } from "./dominio"

/** Alta de una cuenta nueva. */
export interface SolicitudRegistro {
	nombreUsuario: string
	email: string
	/** Contraseña en claro; viaja por el canal cifrado y el servidor solo guarda su hash. */
	clave: string
}

/** Inicio de sesión. */
export interface SolicitudLogin {
	nombreUsuario: string
	clave: string
}

/** Renovación de la sesión. */
export interface SolicitudRefresco {
	tokenRefresco: string
}

/** Sesión iniciada, tal como la devuelve el servidor. */
export interface RespuestaAutenticacion {
	readonly usuarioId: Guid
	readonly nombreUsuario: string
	readonly tokenAcceso: string
	/** Cuándo caduca el token de acceso. */
	readonly expiraEn: string
	/** Token de refresco de un solo uso: al usarlo, el servidor entrega otro. */
	readonly tokenRefresco: string
	readonly roles: readonly string[]
}

/** Creación de una sala. */
export interface SolicitudCrearSala {
	nombre: string
	descripcion?: string | null
	/** Si es cierto, la sala solo la ven sus miembros y se entra por invitación. */
	privada?: boolean
}

/** Apertura de una conversación directa. Basta con uno de los dos campos. */
export interface SolicitudConversacionDirecta {
	nombreUsuario?: string | null
	/** Tiene prioridad sobre el nombre si se envían los dos. */
	usuarioId?: Guid | null
}

/** Invitación de alguien a una sala. */
export interface SolicitudInvitar {
	nombreUsuario: string
}

/** Publicación de un mensaje por HTTP, como alternativa al hub. */
export interface SolicitudEnviarMensaje {
	salaId: Guid
	/** Puede ir vacío si el mensaje lleva imagen, en cuyo caso hace de pie de foto. */
	texto: string
	/**
	 * Identificador que genera el cliente. Hace la operación idempotente: si el
	 * envío se reintenta tras un corte de red, el servidor reconoce el duplicado
	 * y no publica el mensaje dos veces.
	 */
	identificadorEnvio: Guid
	adjuntoId?: Guid | null
}

/** Parámetros de una página del historial. */
export interface ConsultaHistorial {
	salaId: Guid
	cantidad?: number
	/** Paginación hacia atrás: devuelve lo anterior a esta fecha. */
	anteriorA?: string | null
}
