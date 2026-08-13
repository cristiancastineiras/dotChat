// ============================================================================
// RECORTE DE LA FOTO DE PERFIL
// ============================================================================
// Recorta en el navegador antes de subir. No es un capricho: sube muchos menos
// bytes, y sobre todo evita que el servidor tenga que descodificar una foto de
// cuarenta megapíxeles recién sacada del móvil para acabar guardando un cuadrado
// pequeño.
//
// El servidor vuelve a procesar lo que llegue —lo reescala, le quita los
// metadatos y lo recodifica—, así que esto es una optimización, nunca una
// medida de seguridad.
// ============================================================================

/** Lado del cuadrado que se sube, en píxeles. */
const LADO = 512

/** Calidad JPEG del recorte. */
const CALIDAD = 0.9

/** Área recortada, en píxeles de la imagen original. */
export interface AreaRecorte {
	x: number
	y: number
	width: number
	height: number
}

/**
 * Recorta una imagen a un cuadrado y la devuelve lista para subir.
 *
 * @param origen URL de la imagen elegida.
 * @param area Recuadro seleccionado, en píxeles de la imagen original.
 * @returns Un JPEG cuadrado.
 */
export async function recortarCuadrado(origen: string, area: AreaRecorte): Promise<Blob> {
	const imagen = await cargarImagen(origen)

	const lienzo = document.createElement("canvas")
	lienzo.width = LADO
	lienzo.height = LADO

	const contexto = lienzo.getContext("2d")
	if (!contexto) {
		throw new Error("El navegador no permite procesar la imagen.")
	}

	// Fondo blanco: si la imagen original es un PNG con transparencia, el JPEG de
	// salida la convertiría en negro.
	contexto.fillStyle = "#ffffff"
	contexto.fillRect(0, 0, LADO, LADO)

	contexto.imageSmoothingEnabled = true
	contexto.imageSmoothingQuality = "high"

	contexto.drawImage(imagen, area.x, area.y, area.width, area.height, 0, 0, LADO, LADO)

	return await new Promise<Blob>((resolver, rechazar) => {
		lienzo.toBlob(
			(resultado) => {
				if (resultado) {
					resolver(resultado)
				} else {
					rechazar(new Error("No se ha podido generar la imagen recortada."))
				}
			},
			"image/jpeg",
			CALIDAD,
		)
	})
}

/** Carga una imagen y espera a que esté disponible para dibujarla. */
function cargarImagen(origen: string): Promise<HTMLImageElement> {
	return new Promise((resolver, rechazar) => {
		const imagen = new Image()

		imagen.addEventListener("load", () => resolver(imagen))
		imagen.addEventListener("error", () => rechazar(new Error("No se ha podido leer la imagen.")))

		imagen.src = origen
	})
}
