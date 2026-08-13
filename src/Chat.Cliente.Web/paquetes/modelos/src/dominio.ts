// ============================================================================
// TIPOS DEL DOMINIO
// ============================================================================
// Espejo de los DTO que publica Chat.Aplicacion. Se escriben a mano en lugar de
// generarse: el contrato es pequeño y estable, y a cambio se pueden documentar
// los matices que el generador perdería (qué campos solo llegan en una consulta
// concreta, qué significa un nulo, desde qué punto de vista se calcula cada
// nombre).
//
// Las fechas llegan como cadena ISO 8601 con desplazamiento, que es como
// System.Text.Json serializa un DateTimeOffset. Se conservan así y solo se
// convierten a Date en el momento de darles formato: guardar objetos Date en el
// estado obligaría a normalizarlos en cada comparación.
// ============================================================================

/** Instante en formato ISO 8601 con desplazamiento, tal como lo envía el servidor. */
export type FechaIso = string

/** Identificador universal, en la representación con guiones que usa .NET. */
export type Guid = string

// ---------------------------------------------------------------------------
// Enumeraciones
// ---------------------------------------------------------------------------
// Viajan como número: System.Text.Json serializa los enum por su valor salvo que
// se configure lo contrario, y el servidor no lo hace. Se declaran como objeto
// constante en lugar de `enum` de TypeScript porque `isolatedModules` obliga a
// que los enum no sean `const`, y un enum normal genera código en tiempo de
// ejecución que aquí no aporta nada.

/** Naturaleza de una sala. Coincide con Chat.Dominio.Entidades.TipoSala. */
export const TipoSala = {
	/** Visible en el catálogo; cualquiera puede unirse. */
	Publica: 0,
	/** Solo visible para sus miembros; se entra por invitación. */
	Privada: 1,
	/** Conversación entre dos personas. */
	Directa: 2,
} as const

export type TipoSala = (typeof TipoSala)[keyof typeof TipoSala]

/** Naturaleza de un adjunto. Coincide con Chat.Dominio.Entidades.TipoAdjunto. */
export const TipoAdjunto = {
	/** Archivo genérico: solo se ofrece descargarlo. */
	Archivo: 0,
	/** Imagen que el cliente puede dibujar. */
	Imagen: 1,
} as const

export type TipoAdjunto = (typeof TipoAdjunto)[keyof typeof TipoAdjunto]

// ---------------------------------------------------------------------------
// Usuarios y presencia
// ---------------------------------------------------------------------------

/** Proyección pública de un usuario. Nunca trae credenciales. */
export interface Usuario {
	readonly id: Guid
	readonly nombreUsuario: string
	readonly email: string
	readonly fechaCreacion: FechaIso
	readonly fechaUltimoAcceso: FechaIso | null
	readonly activo: boolean
	/** Estado volátil: el servidor lo resuelve en cada respuesta, fuera de su caché. */
	readonly enLinea: boolean
	/** Indica si hay foto de perfil que descargar. */
	readonly tieneAvatar: boolean
	/**
	 * Marca de versión de la foto. Mientras no cambie, la copia ya descargada
	 * sigue siendo válida; cuando cambia, hay que volver a pedirla.
	 */
	readonly avatarActualizado: FechaIso | null
}

/** Identidad del usuario autenticado tal como la ve él mismo. */
export interface Perfil {
	readonly id: Guid
	readonly nombreUsuario: string
	readonly email: string
	readonly fechaCreacion: FechaIso
	readonly fechaUltimoAcceso: FechaIso | null
	readonly esAdministrador: boolean
	readonly tieneAvatar: boolean
	readonly avatarActualizado: FechaIso | null
}

/** Estado de conexión de un usuario. */
export interface Presencia {
	readonly usuarioId: Guid
	readonly nombreUsuario: string
	readonly enLinea: boolean
	/**
	 * Cuándo se le vio por última vez: la hora de conexión si sigue conectado, o
	 * la de su última desconexión observada.
	 */
	readonly ultimaVez: FechaIso | null
	/** Conexiones simultáneas abiertas; más de una significa varios dispositivos. */
	readonly conexiones: number
}

// ---------------------------------------------------------------------------
// Salas
// ---------------------------------------------------------------------------

/**
 * Resumen del último mensaje de una conversación, con lo justo para pintar la
 * línea de previsualización de la lista.
 */
export interface ResumenMensaje {
	readonly nombreAutor: string
	readonly esPropio: boolean
	/** Texto ya recortado por el servidor; vacío si el mensaje era solo un archivo. */
	readonly texto: string
	readonly fechaEnvio: FechaIso
	readonly nombreAdjunto: string | null
	readonly tipoAdjunto: TipoAdjunto | null
}

/** Proyección pública de una sala o conversación. */
export interface Sala {
	readonly id: Guid
	/**
	 * Nombre a mostrar. En una conversación directa es el del interlocutor,
	 * calculado por el servidor desde el punto de vista de quien consulta: dos
	 * personas ven nombres distintos para la misma sala.
	 */
	readonly nombre: string
	readonly descripcion: string | null
	readonly tipo: TipoSala
	readonly fechaCreacion: FechaIso
	/** Fecha del último mensaje; nula si la sala todavía no tiene ninguno. */
	readonly fechaUltimaActividad: FechaIso | null
	readonly totalMiembros: number
	readonly esMiembro: boolean
	readonly mensajesSinLeer: number
	readonly ultimoMensaje: ResumenMensaje | null
	/**
	 * En una conversación directa, si la otra persona está conectada. Nulo en el
	 * resto de salas, donde la presencia se mira miembro a miembro.
	 */
	readonly interlocutorEnLinea: boolean | null
	/** Calculado por el servidor a partir del tipo. */
	readonly esDirecta: boolean
}

/** Miembro de una sala junto con su estado de conexión. */
export interface MiembroSala {
	readonly usuarioId: Guid
	readonly nombreUsuario: string
	readonly fechaUnion: FechaIso
	readonly enLinea: boolean
	readonly esCreador: boolean
}

// ---------------------------------------------------------------------------
// Mensajes y adjuntos
// ---------------------------------------------------------------------------

/**
 * Archivo adjunto a un mensaje. Solo viajan los metadatos: los bytes se piden
 * aparte y únicamente si hay que dibujarlos o el usuario decide descargarlos.
 */
export interface Adjunto {
	readonly id: Guid
	readonly nombreArchivo: string
	readonly tipoMime: string
	readonly tipo: TipoAdjunto
	/** Tamaño del contenido en claro, en bytes. */
	readonly tamanoBytes: number
	/** Anchura en píxeles; solo en las imágenes. */
	readonly ancho: number | null
	/** Altura en píxeles; solo en las imágenes. */
	readonly alto: number | null
	/** Calculado por el servidor a partir del tipo. */
	readonly esImagen: boolean
}

/** Mensaje ya descifrado, listo para mostrarse. */
export interface Mensaje {
	readonly id: Guid
	readonly salaId: Guid
	readonly salaNombre: string
	readonly usuarioId: Guid
	readonly nombreUsuario: string
	/** Contenido en claro; vacío si el mensaje es solo una imagen. */
	readonly texto: string
	readonly fechaEnvio: FechaIso
	readonly adjunto: Adjunto | null
}

// ---------------------------------------------------------------------------
// Resultados genéricos
// ---------------------------------------------------------------------------

/** Resultado de una operación que no devuelve datos. */
export interface ResultadoOperacion {
	readonly exito: boolean
	readonly mensaje: string
}
