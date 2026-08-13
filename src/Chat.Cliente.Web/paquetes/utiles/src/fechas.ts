// ============================================================================
// FORMATO DE FECHAS
// ============================================================================
// El servidor envía siempre instantes con desplazamiento; aquí se pintan en la
// zona horaria del navegador. Se usa Intl en lugar de una librería de fechas
// porque todo lo que hace falta es formatear —no hay aritmética de calendarios—
// y los formateadores nativos están traducidos por el propio sistema.
//
// Los objetos Intl.DateTimeFormat se crean una vez y se reutilizan: construir
// uno cuesta bastante y en una conversación larga se formatean cientos de
// fechas por render.
// ============================================================================

/** Idioma con el que se formatean fechas y números en toda la interfaz. */
const IDIOMA = "es-ES"

const soloHora = new Intl.DateTimeFormat(IDIOMA, {
	hour: "2-digit",
	minute: "2-digit",
})

const diaCompleto = new Intl.DateTimeFormat(IDIOMA, {
	weekday: "long",
	day: "numeric",
	month: "long",
})

const diaConAno = new Intl.DateTimeFormat(IDIOMA, {
	day: "numeric",
	month: "long",
	year: "numeric",
})

const diaCorto = new Intl.DateTimeFormat(IDIOMA, {
	day: "2-digit",
	month: "2-digit",
	year: "2-digit",
})

const fechaYHora = new Intl.DateTimeFormat(IDIOMA, {
	day: "numeric",
	month: "long",
	year: "numeric",
	hour: "2-digit",
	minute: "2-digit",
})

/** Milisegundos que dura un día. */
const UN_DIA = 86_400_000

/**
 * Convierte a Date lo que llega del servidor, tolerando nulos.
 *
 * @param valor Instante ISO, o nulo.
 * @returns La fecha, o `null` si no había valor o no era interpretable.
 */
export function aFecha(valor: string | null | undefined): Date | null {
	if (!valor) return null

	const fecha = new Date(valor)
	return Number.isNaN(fecha.getTime()) ? null : fecha
}

/**
 * Devuelve el instante en milisegundos, o cero si no hay fecha. Sirve para
 * ordenar listas sin tener que comprobar nulos en cada comparación.
 *
 * @param valor Instante ISO, o nulo.
 */
export function aMilisegundos(valor: string | null | undefined): number {
	return aFecha(valor)?.getTime() ?? 0
}

/**
 * Hora de un mensaje: «14:32». Es lo que acompaña a cada burbuja, donde el día
 * ya lo da el separador que hay más arriba.
 *
 * @param valor Instante ISO.
 */
export function formatearHora(valor: string | null | undefined): string {
	const fecha = aFecha(valor)
	return fecha ? soloHora.format(fecha) : ""
}

/**
 * Día de un separador del historial: «hoy», «ayer» o la fecha escrita.
 *
 * @param valor Instante ISO.
 */
export function formatearDia(valor: string | null | undefined): string {
	const fecha = aFecha(valor)
	if (!fecha) return ""

	const dias = diasDeDiferencia(fecha)

	if (dias === 0) return "Hoy"
	if (dias === 1) return "Ayer"

	// Dentro del año en curso sobra decir el año; fuera de él, hace falta.
	return fecha.getFullYear() === new Date().getFullYear()
		? capitalizar(diaCompleto.format(fecha))
		: capitalizar(diaConAno.format(fecha))
}

/**
 * Marca de tiempo de la lista de conversaciones: la hora si es de hoy, «Ayer»,
 * el día de la semana dentro de la última semana y la fecha corta más allá.
 *
 * Esta escala es la que permite leer la lista de un vistazo: lo reciente se
 * distingue por la hora y lo viejo no necesita precisión.
 *
 * @param valor Instante ISO.
 */
export function formatearRelativo(valor: string | null | undefined): string {
	const fecha = aFecha(valor)
	if (!fecha) return ""

	const dias = diasDeDiferencia(fecha)

	if (dias === 0) return soloHora.format(fecha)
	if (dias === 1) return "Ayer"

	if (dias < 7) {
		const semana = new Intl.DateTimeFormat(IDIOMA, { weekday: "long" })
		return capitalizar(semana.format(fecha))
	}

	return diaCorto.format(fecha)
}

/**
 * Fecha y hora completas, para tooltips y fichas de perfil, donde sí interesa
 * el dato exacto.
 *
 * @param valor Instante ISO.
 */
export function formatearCompleto(valor: string | null | undefined): string {
	const fecha = aFecha(valor)
	return fecha ? fechaYHora.format(fecha) : ""
}

/**
 * Última conexión de alguien que no está en línea: «hace un momento», «hace
 * 20 min», «hace 3 h» o la fecha si ya es antigua.
 *
 * @param valor Instante ISO de la última vez que se le vio.
 */
export function formatearUltimaVez(valor: string | null | undefined): string {
	const fecha = aFecha(valor)
	if (!fecha) return "sin conexión reciente"

	const segundos = Math.floor((Date.now() - fecha.getTime()) / 1000)

	if (segundos < 60) return "hace un momento"

	const minutos = Math.floor(segundos / 60)
	if (minutos < 60) return `hace ${minutos} min`

	const horas = Math.floor(minutos / 60)
	if (horas < 24) return `hace ${horas} h`

	const dias = diasDeDiferencia(fecha)
	if (dias === 1) return "ayer"
	if (dias < 7) return `hace ${dias} días`

	return diaCorto.format(fecha)
}

/**
 * Indica si dos instantes caen en el mismo día natural. Es lo que decide dónde
 * va cada separador de fecha del historial.
 *
 * @param a Primer instante ISO.
 * @param b Segundo instante ISO.
 */
export function mismoDia(a: string | null | undefined, b: string | null | undefined): boolean {
	const primera = aFecha(a)
	const segunda = aFecha(b)

	if (!primera || !segunda) return false

	return (
		primera.getFullYear() === segunda.getFullYear() &&
		primera.getMonth() === segunda.getMonth() &&
		primera.getDate() === segunda.getDate()
	)
}

/**
 * Días naturales entre una fecha y hoy.
 *
 * Se comparan medianoches y no diferencias de milisegundos: a las 00:30, un
 * mensaje de las 23:50 es de «ayer» aunque solo hayan pasado cuarenta minutos.
 *
 * @param fecha Fecha a comparar.
 */
function diasDeDiferencia(fecha: Date): number {
	const hoy = new Date()

	const medianocheHoy = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate()).getTime()

	const medianocheFecha = new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate()).getTime()

	return Math.round((medianocheHoy - medianocheFecha) / UN_DIA)
}

/**
 * Pone en mayúscula la primera letra. Los formateadores de Intl en español
 * devuelven los días y los meses en minúscula.
 *
 * @param texto Texto a capitalizar.
 */
function capitalizar(texto: string): string {
	return texto.charAt(0).toUpperCase() + texto.slice(1)
}
