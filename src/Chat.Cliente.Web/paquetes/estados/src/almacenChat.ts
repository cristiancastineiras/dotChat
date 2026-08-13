// ============================================================================
// ALMACÉN DEL CHAT
// ============================================================================
// Todo el estado vivo de la conversación: salas, mensajes por sala, quién
// escribe, quién está conectado y en qué situación está la conexión.
//
// Está indexado por identificador y no guardado en listas porque casi todas las
// operaciones son sobre una sala concreta —llega un mensaje, alguien escribe, se
// marca como leída— y en una lista habría que recorrerla entera cada vez. El
// orden se calcula en los selectores, que es donde importa.
// ============================================================================

import { aMilisegundos } from "@paquetes/utiles"
import { create } from "zustand"

import type {
	EstadoConexion,
	Escribiendo,
	Guid,
	Mensaje,
	MensajeVista,
	Presencia,
	Sala,
} from "@paquetes/modelos"

/** Estado y acciones de la conversación. */
interface EstadoChat {
	/** Salas y conversaciones del usuario, por identificador. */
	salas: Record<Guid, Sala>

	/** Mensajes cargados de cada sala, del más antiguo al más reciente. */
	mensajes: Record<Guid, MensajeVista[]>

	/** Quién está escribiendo en cada sala. */
	escribiendo: Record<Guid, Escribiendo[]>

	/** Estado de conexión de cada usuario conocido. */
	presencia: Record<Guid, Presencia>

	/** Sala abierta en este momento. */
	salaActivaId: Guid | null

	/** Situación de la conexión con el hub. */
	estadoConexion: EstadoConexion

	/** Salas cuyo historial ya se ha recorrido entero hacia atrás. */
	historialCompleto: Record<Guid, boolean>

	/** Salas con una página de historial en vuelo. */
	cargandoHistorial: Record<Guid, boolean>

	// --- Salas ---

	/** Reemplaza la lista de salas por la que llega del servidor. */
	fijarSalas: (salas: readonly Sala[]) => void

	/** Inserta o actualiza una sala. */
	aplicarSala: (sala: Sala) => void

	/** Retira una sala y todo lo que colgaba de ella. */
	quitarSala: (salaId: Guid) => void

	/** Abre una sala y pone su contador de pendientes a cero. */
	seleccionarSala: (salaId: Guid | null) => void

	// --- Mensajes ---

	/** Fija la primera página del historial de una sala. */
	fijarHistorial: (salaId: Guid, mensajes: readonly Mensaje[]) => void

	/** Antepone una página más antigua al historial ya cargado. */
	anteponerHistorial: (salaId: Guid, mensajes: readonly Mensaje[]) => void

	/** Marca que una sala ya no tiene más historial hacia atrás. */
	marcarHistorialCompleto: (salaId: Guid, completo: boolean) => void

	/** Marca que una sala está pidiendo historial. */
	marcarCargandoHistorial: (salaId: Guid, cargando: boolean) => void

	/**
	 * Incorpora un mensaje recibido del hub, evitando duplicados y reconciliando
	 * el envío optimista propio si lo hubiera.
	 */
	recibirMensaje: (mensaje: Mensaje, usuarioPropioId: Guid) => void

	/** Añade un mensaje propio que aún no ha salido hacia el servidor. */
	agregarPendiente: (mensaje: MensajeVista) => void

	/** Sustituye un envío pendiente por el mensaje que confirmó el servidor. */
	confirmarEnvio: (salaId: Guid, identificadorEnvio: Guid, mensaje: Mensaje) => void

	/** Marca un envío pendiente como fallido, para poder reintentarlo. */
	marcarEnvioFallido: (salaId: Guid, identificadorEnvio: Guid) => void

	/** Descarta un envío pendiente. */
	descartarPendiente: (salaId: Guid, identificadorEnvio: Guid) => void

	// --- Señales efímeras ---

	/** Anota que alguien está escribiendo en una sala. */
	anotarEscribiendo: (salaId: Guid, nombreUsuario: string) => void

	/** Retira los avisos de escritura que ya han caducado. */
	caducarEscribiendo: (margenMs: number) => void

	/** Reemplaza el mapa de presencia completo. */
	fijarPresencia: (presencias: readonly Presencia[]) => void

	/** Actualiza la presencia de una persona. */
	aplicarPresencia: (presencia: Presencia) => void

	/** Fija la situación de la conexión con el hub. */
	fijarEstadoConexion: (estado: EstadoConexion) => void

	/** Vacía el almacén entero, al cerrar sesión. */
	limpiar: () => void
}

/** Estado inicial, reutilizado al arrancar y al cerrar sesión. */
const ESTADO_INICIAL = {
	salas: {} as Record<Guid, Sala>,
	mensajes: {} as Record<Guid, MensajeVista[]>,
	escribiendo: {} as Record<Guid, Escribiendo[]>,
	presencia: {} as Record<Guid, Presencia>,
	salaActivaId: null as Guid | null,
	estadoConexion: "desconectado" as EstadoConexion,
	historialCompleto: {} as Record<Guid, boolean>,
	cargandoHistorial: {} as Record<Guid, boolean>,
}

export const useAlmacenChat = create<EstadoChat>()((establecer, obtener) => ({
	...ESTADO_INICIAL,

	// ---------------------------------------------------------------------
	// Salas
	// ---------------------------------------------------------------------

	fijarSalas: (salas) => {
		const indexadas: Record<Guid, Sala> = {}

		for (const sala of salas) {
			indexadas[sala.id] = sala
		}

		establecer({ salas: indexadas })
	},

	aplicarSala: (sala) => {
		establecer((estado) => ({
			salas: { ...estado.salas, [sala.id]: sala },
		}))
	},

	quitarSala: (salaId) => {
		establecer((estado) => {
			// Se desestructura descartando la clave que se va; el guion bajo marca
			// que esas variables existen solo para dejarlas fuera del resto.
			const { [salaId]: _sala, ...salas } = estado.salas
			const { [salaId]: _mensajes, ...mensajes } = estado.mensajes
			const { [salaId]: _escribiendo, ...escribiendo } = estado.escribiendo

			return {
				salas,
				mensajes,
				escribiendo,
				salaActivaId: estado.salaActivaId === salaId ? null : estado.salaActivaId,
			}
		})
	},

	seleccionarSala: (salaId) => {
		establecer((estado) => {
			if (salaId === null) {
				return { salaActivaId: null }
			}

			const sala = estado.salas[salaId]

			// Abrir la sala la deja leída en la interfaz al instante. El servidor se
			// entera por su lado; si esa llamada fallara, el contador volvería en la
			// siguiente recarga de la lista, que es el comportamiento correcto.
			return {
				salaActivaId: salaId,
				salas:
					sala && sala.mensajesSinLeer > 0
						? { ...estado.salas, [salaId]: { ...sala, mensajesSinLeer: 0 } }
						: estado.salas,
			}
		})
	},

	// ---------------------------------------------------------------------
	// Mensajes
	// ---------------------------------------------------------------------

	fijarHistorial: (salaId, mensajes) => {
		establecer((estado) => {
			// Los envíos que siguen en el aire no deben desaparecer porque llegue el
			// historial: el servidor todavía no los conoce y borrarlos daría la
			// sensación de que el mensaje se ha perdido.
			const pendientes = (estado.mensajes[salaId] ?? []).filter(
				(mensaje) => mensaje.estado === "enviando" || mensaje.estado === "fallido",
			)

			return {
				mensajes: { ...estado.mensajes, [salaId]: [...mensajes, ...pendientes] },
			}
		})
	},

	anteponerHistorial: (salaId, mensajes) => {
		establecer((estado) => {
			const actuales = estado.mensajes[salaId] ?? []
			const conocidos = new Set(actuales.map((mensaje) => mensaje.id))

			// La página puede solaparse con lo que ya hay si llegaron mensajes nuevos
			// entre una petición y otra.
			const nuevos = mensajes.filter((mensaje) => !conocidos.has(mensaje.id))

			if (nuevos.length === 0) return {}

			return {
				mensajes: { ...estado.mensajes, [salaId]: [...nuevos, ...actuales] },
			}
		})
	},

	marcarHistorialCompleto: (salaId, completo) => {
		establecer((estado) => ({
			historialCompleto: { ...estado.historialCompleto, [salaId]: completo },
		}))
	},

	marcarCargandoHistorial: (salaId, cargando) => {
		establecer((estado) => ({
			cargandoHistorial: { ...estado.cargandoHistorial, [salaId]: cargando },
		}))
	},

	recibirMensaje: (mensaje, usuarioPropioId) => {
		establecer((estado) => {
			const actuales = estado.mensajes[salaDe(mensaje)] ?? []

			// El servidor difunde a todo el grupo, incluido quien envió. Sin esta
			// comprobación, el autor vería cada mensaje suyo dos veces.
			if (actuales.some((existente) => existente.id === mensaje.id)) {
				return actualizarResumen(estado, mensaje, usuarioPropioId)
			}

			let siguientes: MensajeVista[]

			// La difusión puede adelantarse a la respuesta de la invocación. Cuando
			// pasa, el mensaje propio que se está pintando en optimista es este
			// mismo: se sustituye en su sitio en lugar de añadir un duplicado que
			// desaparecería un instante después.
			const indicePendiente =
				mensaje.usuarioId === usuarioPropioId
					? actuales.findIndex(
							(existente) =>
								existente.estado === "enviando" &&
								existente.texto === mensaje.texto &&
								Boolean(existente.adjunto) === Boolean(mensaje.adjunto),
						)
					: -1

			if (indicePendiente >= 0) {
				siguientes = [...actuales]
				siguientes[indicePendiente] = { ...mensaje, estado: "enviado" }
			} else {
				siguientes = insertarEnOrden(actuales, { ...mensaje })
			}

			return {
				mensajes: { ...estado.mensajes, [mensaje.salaId]: siguientes },
				...actualizarResumen(estado, mensaje, usuarioPropioId),
			}
		})
	},

	agregarPendiente: (mensaje) => {
		establecer((estado) => ({
			mensajes: {
				...estado.mensajes,
				[mensaje.salaId]: [...(estado.mensajes[mensaje.salaId] ?? []), mensaje],
			},
		}))
	},

	confirmarEnvio: (salaId, identificadorEnvio, mensaje) => {
		establecer((estado) => {
			const actuales = estado.mensajes[salaId] ?? []

			// Si la difusión llegó antes, el mensaje real ya está en la lista y lo
			// único que queda por hacer es retirar el provisional.
			const yaEsta = actuales.some(
				(existente) => existente.id === mensaje.id && existente.identificadorEnvio === undefined,
			)

			const siguientes = yaEsta
				? actuales.filter((existente) => existente.identificadorEnvio !== identificadorEnvio)
				: actuales.map((existente) =>
						existente.identificadorEnvio === identificadorEnvio
							? { ...mensaje, estado: "enviado" as const }
							: existente,
					)

			return { mensajes: { ...estado.mensajes, [salaId]: siguientes } }
		})
	},

	marcarEnvioFallido: (salaId, identificadorEnvio) => {
		establecer((estado) => ({
			mensajes: {
				...estado.mensajes,
				[salaId]: (estado.mensajes[salaId] ?? []).map((mensaje) =>
					mensaje.identificadorEnvio === identificadorEnvio
						? { ...mensaje, estado: "fallido" as const }
						: mensaje,
				),
			},
		}))
	},

	descartarPendiente: (salaId, identificadorEnvio) => {
		establecer((estado) => ({
			mensajes: {
				...estado.mensajes,
				[salaId]: (estado.mensajes[salaId] ?? []).filter(
					(mensaje) => mensaje.identificadorEnvio !== identificadorEnvio,
				),
			},
		}))
	},

	// ---------------------------------------------------------------------
	// Señales efímeras
	// ---------------------------------------------------------------------

	anotarEscribiendo: (salaId, nombreUsuario) => {
		establecer((estado) => {
			const actuales = estado.escribiendo[salaId] ?? []
			const ahora = Date.now()

			const sinEsa = actuales.filter((quien) => quien.nombreUsuario !== nombreUsuario)

			return {
				escribiendo: {
					...estado.escribiendo,
					[salaId]: [...sinEsa, { nombreUsuario, desde: ahora }],
				},
			}
		})
	},

	caducarEscribiendo: (margenMs) => {
		const limite = Date.now() - margenMs
		const estado = obtener()

		let huboCambios = false
		const siguientes: Record<Guid, Escribiendo[]> = {}

		for (const [salaId, quienes] of Object.entries(estado.escribiendo)) {
			const vigentes = quienes.filter((quien) => quien.desde > limite)

			if (vigentes.length !== quienes.length) {
				huboCambios = true
			}

			if (vigentes.length > 0) {
				siguientes[salaId] = vigentes
			}
		}

		// Solo se escribe si algo caducó de verdad. Este método lo llama un
		// temporizador cada segundo: escribir siempre volvería a dibujar la
		// conversación entera una vez por segundo sin motivo.
		if (huboCambios) {
			establecer({ escribiendo: siguientes })
		}
	},

	fijarPresencia: (presencias) => {
		const indexadas: Record<Guid, Presencia> = {}

		for (const presencia of presencias) {
			indexadas[presencia.usuarioId] = presencia
		}

		establecer({ presencia: indexadas })
	},

	aplicarPresencia: (presencia) => {
		establecer((estado) => ({
			presencia: { ...estado.presencia, [presencia.usuarioId]: presencia },
		}))
	},

	fijarEstadoConexion: (estado) => {
		establecer({ estadoConexion: estado })
	},

	limpiar: () => {
		establecer({ ...ESTADO_INICIAL })
	},
}))

/** Sala a la que pertenece un mensaje. */
function salaDe(mensaje: Mensaje): Guid {
	return mensaje.salaId
}

/**
 * Inserta un mensaje conservando el orden cronológico.
 *
 * Lo normal es que sea el más reciente y baste con añadirlo al final; se
 * comprueba primero ese caso para no recorrer la lista en el 99 % de las veces.
 * El recorrido hace falta cuando dos mensajes llegan desordenados, algo que pasa
 * con varias réplicas del servidor detrás del balanceador.
 */
function insertarEnOrden(mensajes: readonly MensajeVista[], nuevo: MensajeVista): MensajeVista[] {
	const ultimo = mensajes.at(-1)

	if (!ultimo || aMilisegundos(ultimo.fechaEnvio) <= aMilisegundos(nuevo.fechaEnvio)) {
		return [...mensajes, nuevo]
	}

	const instante = aMilisegundos(nuevo.fechaEnvio)
	const posicion = mensajes.findIndex((mensaje) => aMilisegundos(mensaje.fechaEnvio) > instante)

	return [...mensajes.slice(0, posicion), nuevo, ...mensajes.slice(posicion)]
}

/**
 * Actualiza la previsualización y el contador de pendientes de la sala a la que
 * llega un mensaje.
 *
 * Es lo que mantiene la lista lateral al día sin tener que volver a pedirla al
 * servidor cada vez que alguien escribe.
 */
function actualizarResumen(
	estado: EstadoChat,
	mensaje: Mensaje,
	usuarioPropioId: Guid,
): Partial<EstadoChat> {
	const sala = estado.salas[mensaje.salaId]
	if (!sala) return {}

	const esPropio = mensaje.usuarioId === usuarioPropioId
	const estaAbierta = estado.salaActivaId === mensaje.salaId

	return {
		salas: {
			...estado.salas,
			[mensaje.salaId]: {
				...sala,
				fechaUltimaActividad: mensaje.fechaEnvio,
				mensajesSinLeer: esPropio || estaAbierta ? sala.mensajesSinLeer : sala.mensajesSinLeer + 1,
				ultimoMensaje: {
					nombreAutor: mensaje.nombreUsuario,
					esPropio,
					texto: mensaje.texto,
					fechaEnvio: mensaje.fechaEnvio,
					nombreAdjunto: mensaje.adjunto?.nombreArchivo ?? null,
					tipoAdjunto: mensaje.adjunto?.tipo ?? null,
				},
			},
		},
	}
}
