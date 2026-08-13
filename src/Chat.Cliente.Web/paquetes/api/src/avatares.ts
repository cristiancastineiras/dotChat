// ============================================================================
// CACHÉ DE FOTOS DE PERFIL
// ============================================================================
// Las fotos no se pueden poner en el `src` de una imagen: el endpoint exige
// cabecera de autorización. Hay que traerlas por fetch y envolverlas en una URL
// de objeto, y eso obliga a gestionar a mano lo que el navegador haría solo.
//
// Sin caché, una conversación de cien mensajes de cinco personas dispararía cien
// descargas de cinco imágenes. Aquí se guarda una URL de objeto por usuario y se
// reutiliza en todas partes.
//
// La clave incluye la marca de versión que envía el servidor (`avatarActualizado`):
// cuando alguien cambia su foto, la clave cambia, la entrada vieja se descarta y
// se pide la nueva. Sin ese componente, quien ya tuviera la foto en caché seguiría
// viendo la anterior hasta recargar la página.
// ============================================================================

import { descargarAvatar } from "./usuarios"

import type { Guid } from "@paquetes/modelos"

/** Entrada de la caché. */
interface EntradaAvatar {
	/** URL de objeto lista para usar en una imagen. */
	readonly url: string
	/** Versión con la que se descargó, para detectar que quedó obsoleta. */
	readonly version: string
}

/** Fotos ya descargadas, por identificador de usuario. */
const cache = new Map<Guid, EntradaAvatar>()

/** Descargas en curso, para no pedir dos veces la misma foto a la vez. */
const enVuelo = new Map<Guid, Promise<string | null>>()

/** Usuarios que no tienen foto o cuya descarga falló; se dejan de pedir. */
const sinFoto = new Set<string>()

/** Construye la clave de versión de un usuario. */
function versionDe(avatarActualizado: string | null): string {
	return avatarActualizado ?? "sin-fecha"
}

/**
 * Devuelve la URL de objeto con la foto de alguien, descargándola si hace falta.
 *
 * @param usuarioId Usuario del que se quiere la foto.
 * @param avatarActualizado Marca de versión que acompaña al usuario.
 * @returns URL utilizable en una imagen, o `null` si no tiene foto.
 */
export async function obtenerAvatar(
	usuarioId: Guid,
	avatarActualizado: string | null,
): Promise<string | null> {
	const version = versionDe(avatarActualizado)

	const guardada = cache.get(usuarioId)
	if (guardada && guardada.version === version) {
		return guardada.url
	}

	// Ya se intentó con esta misma versión y no había foto: no insistir en cada
	// render, que es exactamente lo que provocaría una tormenta de 404.
	if (sinFoto.has(`${usuarioId}:${version}`)) {
		return null
	}

	const pendiente = enVuelo.get(usuarioId)
	if (pendiente) return pendiente

	const descarga = (async (): Promise<string | null> => {
		try {
			const contenido = await descargarAvatar(usuarioId)
			const url = URL.createObjectURL(contenido)

			// La versión anterior de esta misma persona ya no sirve; su URL retiene
			// el blob en memoria hasta que se revoca explícitamente.
			const anterior = cache.get(usuarioId)
			if (anterior) {
				URL.revokeObjectURL(anterior.url)
			}

			cache.set(usuarioId, { url, version })
			return url
		} catch {
			// No tiene foto, o no se pudo traer. En los dos casos se cae a las
			// iniciales, que es una degradación perfectamente aceptable.
			sinFoto.add(`${usuarioId}:${version}`)
			return null
		} finally {
			enVuelo.delete(usuarioId)
		}
	})()

	enVuelo.set(usuarioId, descarga)
	return descarga
}

/**
 * Devuelve la foto ya cacheada, sin lanzar ninguna descarga.
 *
 * Permite que un componente pinte la imagen en el primer render cuando ya se
 * había descargado, en lugar de enseñar las iniciales y sustituirlas después.
 *
 * @param usuarioId Usuario consultado.
 * @param avatarActualizado Marca de versión que acompaña al usuario.
 */
export function avatarEnCache(usuarioId: Guid, avatarActualizado: string | null): string | null {
	const guardada = cache.get(usuarioId)
	return guardada && guardada.version === versionDe(avatarActualizado) ? guardada.url : null
}

/**
 * Olvida la foto de alguien y libera su memoria.
 *
 * Se usa al cambiar la foto propia: la subida devuelve una marca de versión
 * nueva, pero conviene soltar la anterior sin esperar a que caiga sola.
 *
 * @param usuarioId Usuario cuya foto se descarta.
 */
export function olvidarAvatar(usuarioId: Guid): void {
	const guardada = cache.get(usuarioId)

	if (guardada) {
		URL.revokeObjectURL(guardada.url)
		cache.delete(usuarioId)
	}

	for (const clave of sinFoto) {
		if (clave.startsWith(`${usuarioId}:`)) {
			sinFoto.delete(clave)
		}
	}
}

/**
 * Vacía la caché entera y revoca todas las URL.
 *
 * Se invoca al cerrar sesión: las fotos son de una conversación privada y no
 * tienen por qué seguir accesibles para quien entre después en el mismo equipo.
 */
export function vaciarAvatares(): void {
	for (const entrada of cache.values()) {
		URL.revokeObjectURL(entrada.url)
	}

	cache.clear()
	enVuelo.clear()
	sinFoto.clear()
}
