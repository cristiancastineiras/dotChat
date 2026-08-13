// ============================================================================
// UTILIDADES DE ARCHIVOS
// ============================================================================

/** Unidades binarias, que son las que usa el servidor para sus límites. */
const UNIDADES = ["B", "KiB", "MiB", "GiB"] as const

/**
 * Formatea un tamaño en bytes de forma legible.
 *
 * @param bytes Tamaño en bytes.
 * @returns Cadena como «1,4 MiB». Devuelve «—» si el tamaño no se conoce.
 */
export function formatearTamano(bytes: number): string {
	if (!Number.isFinite(bytes) || bytes < 0) return "—"
	if (bytes === 0) return "0 B"

	const escala = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), UNIDADES.length - 1)

	const valor = bytes / 1024 ** escala

	// Sin decimales en los bytes sueltos, uno a partir de ahí: «1536 B» no
	// necesita precisión y «1,5 KiB» sí.
	const decimales = escala === 0 ? 0 : valor < 10 ? 1 : 0

	return `${valor.toLocaleString("es-ES", {
		minimumFractionDigits: decimales,
		maximumFractionDigits: decimales,
	})} ${UNIDADES[escala]}`
}

/**
 * Extensión de un nombre de archivo, en mayúsculas y sin punto.
 *
 * @param nombreArchivo Nombre del fichero.
 * @returns La extensión, o cadena vacía si no tiene.
 */
export function extensionDe(nombreArchivo: string): string {
	const punto = nombreArchivo.lastIndexOf(".")

	// Un punto en la primera posición es un fichero oculto, no una extensión.
	if (punto <= 0 || punto === nombreArchivo.length - 1) return ""

	return nombreArchivo.slice(punto + 1).toUpperCase()
}

/**
 * Indica si un archivo del navegador es una imagen que se puede previsualizar.
 *
 * Es una comprobación de conveniencia para decidir si se enseña una miniatura
 * antes de subir. Quien decide de verdad si algo es una imagen es el servidor,
 * que descodifica el contenido en lugar de creerse el tipo declarado.
 *
 * @param archivo Archivo elegido por el usuario.
 */
export function esImagen(archivo: File): boolean {
	return archivo.type.startsWith("image/")
}

/**
 * Comprueba que un archivo no supere el límite del servidor.
 *
 * Se valida aquí además de en el servidor para no hacerle subir veinte megas a
 * alguien y rechazárselos al final; el límite que cuenta sigue siendo el suyo.
 *
 * @param archivo Archivo elegido.
 * @param limiteMiB Límite en mebibytes.
 * @returns Mensaje de error, o `null` si el archivo vale.
 */
export function validarTamano(archivo: File, limiteMiB: number): string | null {
	const limite = limiteMiB * 1024 * 1024

	if (archivo.size > limite) {
		return `«${archivo.name}» ocupa ${formatearTamano(archivo.size)} y el máximo son ${limiteMiB} MiB.`
	}

	if (archivo.size === 0) {
		return `«${archivo.name}» está vacío.`
	}

	return null
}

/**
 * Descarga un contenido ya recuperado, con el nombre indicado.
 *
 * Los adjuntos exigen cabecera de autorización, así que no se puede enlazar a
 * ellos directamente: hay que pedirlos con el cliente HTTP y entregar al
 * navegador el resultado, que es lo que hace esta función.
 *
 * @param contenido Contenido descargado.
 * @param nombreArchivo Nombre con el que guardarlo.
 */
export function guardarComoArchivo(contenido: Blob, nombreArchivo: string): void {
	const url = URL.createObjectURL(contenido)
	const enlace = document.createElement("a")

	enlace.href = url
	enlace.download = nombreArchivo
	enlace.rel = "noopener"

	document.body.append(enlace)
	enlace.click()
	enlace.remove()

	// La URL de objeto retiene el blob en memoria hasta que se revoca. Se espera
	// un instante porque revocarla en el mismo tick cancela la descarga en
	// algunos navegadores.
	setTimeout(() => URL.revokeObjectURL(url), 1000)
}
