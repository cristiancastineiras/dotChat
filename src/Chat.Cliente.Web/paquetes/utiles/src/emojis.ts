// ============================================================================
// CATÁLOGO DE EMOJIS
// ============================================================================
// Espejo de Chat.Aplicacion.Mensajeria.CatalogoEmojis.
//
// Quien expande de verdad los atajos `:nombre:` es el servidor, dentro del
// envío: así lo que se cifra y se guarda es ya el emoji, y el historial se lee
// igual desde la terminal y desde el navegador. Esta copia existe para dos
// cosas que solo puede hacer el cliente: ofrecer el selector y completar el
// atajo mientras se escribe.
//
// Al ser una copia, puede quedarse corta si el catálogo del servidor crece. No
// pasa nada: un atajo que aquí no aparezca se sigue enviando tal cual y el
// servidor lo expande igualmente. Lo único que se pierde es la sugerencia.
// ============================================================================

/** Una entrada del catálogo. */
export interface EntradaEmoji {
	/** Nombre canónico, en minúsculas y sin acentos. */
	readonly nombre: string
	/** Carácter o secuencia Unicode. */
	readonly simbolo: string
	/** Grupo con el que se presenta en el selector. */
	readonly categoria: string
	/** Nombres alternativos aceptados, normalmente en inglés. */
	readonly alias: readonly string[]
}

/** Orden en que se presentan las categorías en el selector. */
export const CATEGORIAS_EMOJI = [
	"Caras",
	"Manos",
	"Símbolos",
	"Naturaleza",
	"Comida",
	"Objetos",
	"Desarrollo",
] as const

export type CategoriaEmoji = (typeof CATEGORIAS_EMOJI)[number]

/** Construye una entrada del catálogo. */
function entrada(
	nombre: string,
	simbolo: string,
	categoria: CategoriaEmoji,
	...alias: string[]
): EntradaEmoji {
	return { nombre, simbolo, categoria, alias }
}

/** Catálogo completo, en el mismo orden que el del servidor. */
export const EMOJIS: readonly EntradaEmoji[] = [
	// Caras y gestos
	entrada("sonrisa", "😄", "Caras", "smile"),
	entrada("risa", "😂", "Caras", "joy", "lol"),
	entrada("carcajada", "🤣", "Caras", "rofl"),
	entrada("guino", "😉", "Caras", "wink"),
	entrada("sonrojo", "😊", "Caras", "blush"),
	entrada("gafas", "😎", "Caras", "cool", "sunglasses"),
	entrada("pensando", "🤔", "Caras", "thinking"),
	entrada("triste", "😔", "Caras", "sad", "pensive"),
	entrada("llorando", "😢", "Caras", "cry"),
	entrada("enfadado", "😠", "Caras", "angry"),
	entrada("sorpresa", "😮", "Caras", "wow", "open_mouth"),
	entrada("miedo", "😱", "Caras", "scream"),
	entrada("sudor", "😅", "Caras", "sweat_smile"),
	entrada("silencio", "🤐", "Caras", "zipper"),
	entrada("dormido", "😴", "Caras", "sleeping"),
	entrada("beso", "😘", "Caras", "kiss"),
	entrada("lengua", "😛", "Caras", "tongue"),
	entrada("neutral", "😐", "Caras", "poker_face"),
	entrada("ojos", "👀", "Caras", "eyes"),
	entrada("guay", "🥳", "Caras", "party_face"),

	// Manos y personas
	entrada("pulgar", "👍", "Manos", "+1", "thumbsup", "ok"),
	entrada("pulgarabajo", "👎", "Manos", "-1", "thumbsdown"),
	entrada("aplauso", "👏", "Manos", "clap"),
	entrada("saludo", "👋", "Manos", "wave", "hola"),
	entrada("choque", "🤝", "Manos", "handshake"),
	entrada("fuerza", "💪", "Manos", "muscle"),
	entrada("rezar", "🙏", "Manos", "pray", "gracias"),
	entrada("victoria", "✌️", "Manos", "v"),
	entrada("vale", "👌", "Manos", "okhand"),
	entrada("senalar", "👉", "Manos", "point"),

	// Corazones y símbolos
	entrada("corazon", "❤️", "Símbolos", "heart"),
	entrada("corazonroto", "💔", "Símbolos", "broken_heart"),
	entrada("chispa", "✨", "Símbolos", "sparkles"),
	entrada("estrella", "⭐", "Símbolos", "star"),
	entrada("fuego", "🔥", "Símbolos", "fire"),
	entrada("rayo", "⚡", "Símbolos", "zap"),
	entrada("bomba", "💣", "Símbolos", "bomb"),
	entrada("aviso", "⚠️", "Símbolos", "warning"),
	entrada("prohibido", "⛔", "Símbolos", "no_entry"),
	entrada("comprobado", "✅", "Símbolos", "check", "hecho"),
	entrada("cruz", "❌", "Símbolos", "x"),
	entrada("pregunta", "❓", "Símbolos", "question"),
	entrada("exclamacion", "❗", "Símbolos", "exclamation"),
	entrada("cien", "💯", "Símbolos", "100"),
	entrada("campana", "🔔", "Símbolos", "bell"),
	entrada("candado", "🔒", "Símbolos", "lock"),
	entrada("llave", "🔑", "Símbolos", "key"),
	entrada("reloj", "⏰", "Símbolos", "alarm"),
	entrada("diana", "🎯", "Símbolos", "dart", "objetivo"),
	entrada("trofeo", "🏆", "Símbolos", "trophy"),

	// Animales y naturaleza
	entrada("perro", "🐶", "Naturaleza", "dog"),
	entrada("gato", "🐱", "Naturaleza", "cat"),
	entrada("raton", "🐭", "Naturaleza", "mouse"),
	entrada("zorro", "🦊", "Naturaleza", "fox"),
	entrada("oso", "🐻", "Naturaleza", "bear"),
	entrada("panda", "🐼", "Naturaleza"),
	entrada("unicornio", "🦄", "Naturaleza", "unicorn"),
	entrada("bicho", "🐛", "Naturaleza", "bug"),
	entrada("arbol", "🌳", "Naturaleza", "tree"),
	entrada("flor", "🌸", "Naturaleza", "flower"),
	entrada("sol", "☀️", "Naturaleza", "sun"),
	entrada("luna", "🌙", "Naturaleza", "moon"),
	entrada("nube", "☁️", "Naturaleza", "cloud"),
	entrada("lluvia", "🌧️", "Naturaleza", "rain"),
	entrada("nieve", "❄️", "Naturaleza", "snow"),
	entrada("ola", "🌊", "Naturaleza", "wave_sea"),

	// Comida y bebida
	entrada("cafe", "☕", "Comida", "coffee"),
	entrada("cerveza", "🍺", "Comida", "beer"),
	entrada("brindis", "🥂", "Comida", "cheers"),
	entrada("pizza", "🍕", "Comida"),
	entrada("hamburguesa", "🍔", "Comida", "burger"),
	entrada("tarta", "🍰", "Comida", "cake"),
	entrada("manzana", "🍎", "Comida", "apple"),
	entrada("palomitas", "🍿", "Comida", "popcorn"),

	// Actividades y objetos
	entrada("fiesta", "🎉", "Objetos", "tada", "party"),
	entrada("regalo", "🎁", "Objetos", "gift"),
	entrada("musica", "🎵", "Objetos", "music"),
	entrada("libro", "📚", "Objetos", "books"),
	entrada("lapiz", "✏️", "Objetos", "pencil"),
	entrada("carpeta", "📁", "Objetos", "folder"),
	entrada("grafico", "📈", "Objetos", "chart"),
	entrada("camara", "📷", "Objetos", "camera"),
	entrada("imagen", "🖼️", "Objetos", "picture", "foto"),
	entrada("telefono", "📱", "Objetos", "phone"),
	entrada("correo", "📧", "Objetos", "mail"),
	entrada("cohete", "🚀", "Objetos", "rocket"),
	entrada("avion", "✈️", "Objetos", "airplane"),
	entrada("coche", "🚗", "Objetos", "car"),
	entrada("casa", "🏠", "Objetos", "house"),
	entrada("reloj_arena", "⌛", "Objetos", "hourglass"),

	// Trabajo y desarrollo
	entrada("ordenador", "💻", "Desarrollo", "laptop", "pc"),
	entrada("teclado", "⌨️", "Desarrollo", "keyboard"),
	entrada("disquete", "💾", "Desarrollo", "floppy", "guardar"),
	entrada("engranaje", "⚙️", "Desarrollo", "gear", "config"),
	entrada("herramientas", "🛠️", "Desarrollo", "tools"),
	entrada("martillo", "🔨", "Desarrollo", "hammer"),
	entrada("lupa", "🔍", "Desarrollo", "search", "buscar"),
	entrada("bombilla", "💡", "Desarrollo", "idea", "bulb"),
	entrada("robot", "🤖", "Desarrollo"),
	entrada("satelite", "📡", "Desarrollo", "satellite"),
	entrada("basura", "🗑️", "Desarrollo", "trash"),
	entrada("etiqueta", "🏷️", "Desarrollo", "label", "tag"),
	entrada("enlace", "🔗", "Desarrollo", "link"),
	entrada("recargar", "🔄", "Desarrollo", "refresh"),
	entrada("verde", "🟢", "Desarrollo", "green"),
	entrada("rojo", "🔴", "Desarrollo", "red"),
	entrada("amarillo", "🟡", "Desarrollo", "yellow"),
]

/**
 * Índice de nombre y alias a símbolo. Se construye una vez al cargar el módulo:
 * la alternativa, recorrer el catálogo en cada pulsación, se nota al escribir.
 */
const INDICE: ReadonlyMap<string, string> = new Map(
	EMOJIS.flatMap((emoji) => [
		[emoji.nombre, emoji.simbolo] as const,
		...emoji.alias.map((alias) => [alias, emoji.simbolo] as const),
	]),
)

/**
 * Busca el símbolo de un atajo.
 *
 * @param nombre Nombre o alias, sin los dos puntos.
 * @returns El emoji, o `undefined` si el atajo no está en el catálogo.
 */
export function buscarEmoji(nombre: string): string | undefined {
	return INDICE.get(nombre.toLowerCase())
}

/**
 * Sustituye los atajos `:nombre:` por sus emojis.
 *
 * Solo se usa para previsualizar lo que se está escribiendo; el texto que se
 * envía va sin tocar, para que sea el servidor quien decida —y para que dos
 * clientes con catálogos distintos no acaben guardando cosas diferentes.
 *
 * @param texto Texto con atajos.
 */
export function expandirAtajos(texto: string): string {
	// Se exige que el nombre no tenga espacios ni dos puntos para no destrozar
	// un texto que use los dos puntos con normalidad («son las 10:30:00»).
	return texto.replace(/:([a-z0-9_+-]{1,32}):/giu, (original, nombre: string) => {
		return buscarEmoji(nombre) ?? original
	})
}

/**
 * Sugerencias para el autocompletado del redactor.
 *
 * @param prefijo Lo escrito tras los dos puntos.
 * @param limite Número máximo de sugerencias.
 * @returns Coincidencias, primero las que empiezan por el prefijo.
 */
export function sugerirEmojis(prefijo: string, limite = 8): EntradaEmoji[] {
	const busqueda = prefijo.toLowerCase()
	if (!busqueda) return []

	const empiezan: EntradaEmoji[] = []
	const contienen: EntradaEmoji[] = []

	for (const emoji of EMOJIS) {
		const nombres = [emoji.nombre, ...emoji.alias]

		if (nombres.some((nombre) => nombre.startsWith(busqueda))) {
			empiezan.push(emoji)
		} else if (nombres.some((nombre) => nombre.includes(busqueda))) {
			contienen.push(emoji)
		}

		// Con los que empiezan por el prefijo ya hay de sobra: se corta antes de
		// recorrer el catálogo entero en cada pulsación.
		if (empiezan.length >= limite) break
	}

	return [...empiezan, ...contienen].slice(0, limite)
}

/** Agrupa el catálogo por categoría, en el orden de presentación. */
export function emojisPorCategoria(): ReadonlyMap<string, EntradaEmoji[]> {
	const grupos = new Map<string, EntradaEmoji[]>()

	for (const categoria of CATEGORIAS_EMOJI) {
		grupos.set(categoria, [])
	}

	for (const emoji of EMOJIS) {
		grupos.get(emoji.categoria)?.push(emoji)
	}

	return grupos
}
