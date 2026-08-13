// ============================================================================
// UTILIDADES DE TEXTO
// ============================================================================

/** Paleta de fondos para los avatares sin foto. */
const TONOS_AVATAR = [
	"#6b45b0",
	"#2563eb",
	"#0e7490",
	"#15803d",
	"#b45309",
	"#be123c",
	"#7c2d12",
	"#4338ca",
] as const

/**
 * Iniciales con las que se dibuja un avatar sin foto.
 *
 * Se toman una o dos letras: la primera de las dos primeras palabras si el
 * nombre las tiene, y si no las dos primeras letras. Con una sola inicial hay
 * demasiadas colisiones en una lista de conversaciones.
 *
 * @param nombre Nombre de usuario o de sala.
 */
export function iniciales(nombre: string): string {
	const limpio = nombre.trim()
	if (!limpio) return "?"

	const palabras = limpio.split(/[\s._-]+/u).filter(Boolean)

	if (palabras.length >= 2) {
		const primera = palabras[0]?.[0] ?? ""
		const segunda = palabras[1]?.[0] ?? ""
		return (primera + segunda).toUpperCase()
	}

	// `Array.from` y no `slice`: un nombre puede empezar por un carácter fuera
	// del plano básico —un emoji, por ejemplo— y cortarlo por unidades de código
	// partiría el par sustituto y dejaría un símbolo roto.
	return Array.from(limpio).slice(0, 2).join("").toUpperCase()
}

/**
 * Color estable para el avatar de alguien sin foto.
 *
 * Es determinista: la misma persona sale siempre del mismo color, en cualquier
 * navegador y sin que el servidor tenga que guardarlo. Se calcula sobre el
 * identificador y no sobre el nombre, para que cambiar de nombre no cambie el
 * color con el que los demás ya te reconocen.
 *
 * @param clave Identificador del usuario o de la sala.
 */
export function colorDesde(clave: string): string {
	let acumulado = 0

	for (let indice = 0; indice < clave.length; indice++) {
		// Multiplicar por 31 y sumar es el clásico de Java: reparte bien con
		// cadenas cortas y parecidas entre sí, que es justo el caso de los UUID.
		acumulado = (acumulado * 31 + clave.charCodeAt(indice)) | 0
	}

	const indice = Math.abs(acumulado) % TONOS_AVATAR.length
	return TONOS_AVATAR[indice] ?? TONOS_AVATAR[0]
}

/**
 * Recorta un texto a una longitud máxima, cortando por la última palabra
 * entera para no dejar sílabas partidas.
 *
 * @param texto Texto de origen.
 * @param maximo Longitud máxima, incluido el puntos suspensivos.
 */
export function recortar(texto: string, maximo: number): string {
	if (texto.length <= maximo) return texto

	const cortado = texto.slice(0, maximo - 1)
	const ultimoEspacio = cortado.lastIndexOf(" ")

	// Solo se retrocede hasta el espacio si con ello no se pierde media frase.
	const base = ultimoEspacio > maximo * 0.6 ? cortado.slice(0, ultimoEspacio) : cortado

	return `${base.trimEnd()}…`
}

/**
 * Colapsa los saltos de línea en espacios. Se usa en las previsualizaciones,
 * donde un mensaje de varias líneas debe ocupar solo una.
 *
 * @param texto Texto de origen.
 */
export function enUnaLinea(texto: string): string {
	return texto.replace(/\s+/gu, " ").trim()
}

/**
 * Normaliza un texto para buscar: sin mayúsculas, sin acentos y sin espacios
 * sobrantes. Permite que «jose» encuentre a «José».
 *
 * @param texto Texto de origen.
 */
export function normalizar(texto: string): string {
	return (
		texto
			.normalize("NFD")
			// Marcas diacríticas: es lo que separa la «é» descompuesta de la «e».
			.replace(/[̀-ͯ]/gu, "")
			.toLocaleLowerCase("es-ES")
			.trim()
	)
}

/**
 * Indica si un texto contiene a otro, sin distinguir mayúsculas ni acentos.
 *
 * @param texto Texto donde se busca.
 * @param busqueda Texto buscado.
 */
export function contiene(texto: string, busqueda: string): boolean {
	if (!busqueda) return true
	return normalizar(texto).includes(normalizar(busqueda))
}

/**
 * Indica si un mensaje es únicamente emojis (como mucho tres).
 *
 * Cuando lo es, la interfaz lo pinta en grande y sin burbuja: es la convención
 * de cualquier mensajería moderna y hace que una reacción se lea al instante.
 *
 * @param texto Texto del mensaje.
 */
export function esSoloEmojis(texto: string): boolean {
	const limpio = texto.trim()
	if (!limpio) return false

	// `Extended_Pictographic` cubre los emojis; los modificadores, selectores de
	// variación y uniones de ancho cero son las piezas con las que se componen
	// las secuencias (banderas, familias, tonos de piel).
	const soloEmojis = /^(?:\p{Extended_Pictographic}|\p{Emoji_Modifier}|️|‍|\s)+$/u

	if (!soloEmojis.test(limpio)) return false

	// Se cuentan grupos de grafemas para que una secuencia compuesta cuente como
	// un solo emoji y no como las tres o cuatro piezas que la forman.
	const segmentador = new Intl.Segmenter("es", { granularity: "grapheme" })
	const total = [...segmentador.segment(limpio)].filter(
		(parte) => parte.segment.trim().length > 0,
	).length

	return total > 0 && total <= 3
}
