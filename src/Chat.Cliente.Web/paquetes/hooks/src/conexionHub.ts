// ============================================================================
// CONEXIÓN CON EL HUB
// ============================================================================
// Envoltorio sobre la conexión de SignalR. Vive fuera de React a propósito: la
// conexión debe sobrevivir a los remontajes de los componentes, y en desarrollo
// el modo estricto monta cada componente dos veces. Si la conexión colgara de un
// efecto, se abriría y cerraría en cada montaje, y el servidor vería un baile de
// conexiones que además dispara avisos de presencia falsos.
//
// Los nombres de método van en PascalCase porque así se llaman en ChatHub. Los
// DTO viajan en camelCase: el protocolo JSON de SignalR usa las opciones «web»
// de System.Text.Json, igual que los endpoints HTTP.
// ============================================================================

import {
	HubConnection,
	HubConnectionBuilder,
	HubConnectionState,
	LogLevel,
	type IRetryPolicy,
	type RetryContext,
} from "@microsoft/signalr"
import { configuracion, obtenerTokenValido } from "@paquetes/api"

import type {
	EstadoConexion,
	Guid,
	Mensaje,
	MiembroSala,
	Presencia,
	ResultadoOperacion,
	Sala,
} from "@paquetes/modelos"

/** Sucesos que el servidor puede enviar al cliente. */
export interface ManejadoresHub {
	alConectar: (nombreUsuario: string, salas: Sala[]) => void
	alRecibirMensaje: (mensaje: Mensaje) => void
	alEscribir: (salaId: Guid, nombreUsuario: string) => void
	alCambiarPresencia: (presencia: Presencia) => void
	alCrearseSala: (sala: Sala) => void
	alHaberSalaDisponible: (sala: Sala) => void
	alUnirseUsuario: (nombreSala: string, nombreUsuario: string) => void
	alSalirUsuario: (nombreSala: string, nombreUsuario: string) => void
	alRecibirError: (mensaje: string) => void
	alCambiarEstado: (estado: EstadoConexion) => void
}

/**
 * Espera entre reintentos de reconexión.
 *
 * Se sube deprisa hasta medio minuto y ahí se queda, sin rendirse nunca: un
 * portátil que despierta tras horas suspendido debe reconectar solo, y darse por
 * vencido obligaría al usuario a recargar la página para volver a tener chat.
 */
const ESPERAS_MS = [0, 2_000, 5_000, 10_000, 20_000, 30_000] as const

class PoliticaReintento implements IRetryPolicy {
	nextRetryDelayInMilliseconds(contexto: RetryContext): number {
		const indice = Math.min(contexto.previousRetryCount, ESPERAS_MS.length - 1)
		const espera = ESPERAS_MS[indice] ?? 30_000

		// Un poco de aleatoriedad para que veinte clientes que perdieron la
		// conexión a la vez no vuelvan todos en el mismo instante.
		return espera + Math.random() * 1_000
	}
}

/** Conexión en curso; una sola para toda la aplicación. */
let conexion: HubConnection | null = null

/** Arranque en vuelo, para que dos llamadas seguidas no abran dos conexiones. */
let arranque: Promise<void> | null = null

/** Manejadores registrados por la aplicación. */
let manejadores: ManejadoresHub | null = null

/** Construye la conexión y engancha los sucesos del servidor. */
function construir(): HubConnection {
	const nueva = new HubConnectionBuilder()
		.withUrl(configuracion.urlHub, {
			// Se pide en cada negociación y en cada reconexión, así que tiene que
			// renovarse si hace falta: tras una suspensión larga, el token guardado
			// llevará horas caducado.
			accessTokenFactory: async () => (await obtenerTokenValido()) ?? "",
		})
		.withAutomaticReconnect(new PoliticaReintento())
		.configureLogging(configuracion.depurar ? LogLevel.Information : LogLevel.Warning)
		.build()

	// El servidor cierra por inactividad al minuto; el cliente debe darse cuenta
	// antes de que su historial se quede atrás sin avisar.
	nueva.serverTimeoutInMilliseconds = 60_000
	nueva.keepAliveIntervalInMilliseconds = 15_000

	nueva.on("Conectado", (nombreUsuario: string, salas: Sala[]) => {
		manejadores?.alConectar(nombreUsuario, salas)
	})

	nueva.on("RecibirMensaje", (mensaje: Mensaje) => {
		manejadores?.alRecibirMensaje(mensaje)
	})

	nueva.on("UsuarioEscribiendo", (salaId: Guid, nombreUsuario: string) => {
		manejadores?.alEscribir(salaId, nombreUsuario)
	})

	nueva.on("PresenciaCambiada", (presencia: Presencia) => {
		manejadores?.alCambiarPresencia(presencia)
	})

	nueva.on("SalaCreada", (sala: Sala) => {
		manejadores?.alCrearseSala(sala)
	})

	nueva.on("SalaDisponible", (sala: Sala) => {
		manejadores?.alHaberSalaDisponible(sala)
	})

	nueva.on("UsuarioUnido", (nombreSala: string, nombreUsuario: string) => {
		manejadores?.alUnirseUsuario(nombreSala, nombreUsuario)
	})

	nueva.on("UsuarioSalido", (nombreSala: string, nombreUsuario: string) => {
		manejadores?.alSalirUsuario(nombreSala, nombreUsuario)
	})

	nueva.on("ErrorRecibido", (mensaje: string) => {
		manejadores?.alRecibirError(mensaje)
	})

	nueva.onreconnecting(() => {
		manejadores?.alCambiarEstado("reconectando")
	})

	nueva.onreconnected(() => {
		manejadores?.alCambiarEstado("conectado")

		// Al volver, el cliente se ha perdido todo lo ocurrido mientras estuvo
		// fuera. `Conectar` vuelve a suscribir la conexión a sus grupos y devuelve
		// el estado actual de las salas, que es la forma de ponerse al día.
		void nueva.invoke("Conectar").catch(() => {
			// Si esto falla, la reconexión automática lo volverá a intentar.
		})
	})

	nueva.onclose(() => {
		manejadores?.alCambiarEstado("desconectado")
	})

	return nueva
}

/**
 * Abre la conexión si no lo estaba ya.
 *
 * @param nuevosManejadores Sucesos a los que responder.
 */
export async function conectar(nuevosManejadores: ManejadoresHub): Promise<void> {
	manejadores = nuevosManejadores

	if (conexion?.state === HubConnectionState.Connected) return
	if (arranque) return arranque

	conexion ??= construir()
	const actual = conexion

	manejadores.alCambiarEstado("conectando")

	arranque = (async () => {
		try {
			await actual.start()
			manejadores?.alCambiarEstado("conectado")

			// El servidor invoca `Conectado` por su cuenta al establecerse la
			// conexión, así que aquí no hace falta pedir nada más.
		} catch (error) {
			manejadores?.alCambiarEstado("desconectado")
			throw error
		} finally {
			arranque = null
		}
	})()

	return arranque
}

/** Cierra la conexión y olvida los manejadores. */
export async function desconectar(): Promise<void> {
	const actual = conexion

	conexion = null
	arranque = null
	manejadores = null

	if (actual) {
		// `stop` no lanza si ya estaba parada, pero sí puede rechazar si el
		// transporte murió antes: no hay nada que hacer con ese error.
		await actual.stop().catch(() => undefined)
	}
}

/** Indica si el hub está disponible para invocar. */
export function estaConectado(): boolean {
	return conexion?.state === HubConnectionState.Connected
}

/**
 * Invoca un método del hub.
 *
 * @param metodo Nombre del método en ChatHub.
 * @param argumentos Argumentos posicionales.
 * @throws Si no hay conexión abierta.
 */
async function invocar<T>(metodo: string, ...argumentos: unknown[]): Promise<T> {
	if (!conexion || conexion.state !== HubConnectionState.Connected) {
		throw new Error("No hay conexión con el servidor.")
	}

	return await conexion.invoke<T>(metodo, ...argumentos)
}

/** Métodos del hub, con la firma que declara ChatHub. */
export const hub = {
	/** Publica un mensaje y lo difunde a la sala. Devuelve `null` si se rechazó. */
	enviarMensaje: (
		salaId: Guid,
		texto: string,
		identificadorEnvio: Guid,
		adjuntoId: Guid | null = null,
	) => invocar<Mensaje | null>("EnviarMensaje", salaId, texto, identificadorEnvio, adjuntoId),

	/** Une al usuario a una sala pública. */
	unirseSala: (salaId: Guid) => invocar<Sala | null>("UnirseSala", salaId),

	/** Abre o recupera la conversación directa con alguien. */
	abrirDirecta: (nombreUsuario: string) =>
		invocar<Sala | null>("AbrirConversacionDirecta", nombreUsuario),

	/** Saca al usuario de una sala. */
	salirSala: (salaId: Guid) => invocar<ResultadoOperacion>("SalirSala", salaId),

	/** Devuelve las salas del usuario con sus pendientes. */
	listarSalas: () => invocar<Sala[]>("ListarSalas"),

	/** Devuelve los miembros de una sala con su presencia. */
	listarMiembros: (salaId: Guid) => invocar<MiembroSala[]>("ListarMiembros", salaId),

	/** Devuelve el estado de conexión de los usuarios conocidos. */
	listarPresencia: () => invocar<Presencia[]>("ListarPresencia"),

	/** Deja la conversación al día. */
	marcarLeida: (salaId: Guid) => invocar<ResultadoOperacion>("MarcarLeida", salaId),

	/**
	 * Avisa de que se está escribiendo.
	 *
	 * Se envía sin esperar respuesta y se traga cualquier error: es una señal
	 * prescindible, y el servidor descarta las que llegan demasiado seguidas.
	 */
	escribiendo: (salaId: Guid) => {
		if (!estaConectado()) return
		void conexion?.send("Escribiendo", salaId).catch(() => undefined)
	},
} as const
