// ============================================================================
// FOTOS DE PERFIL
// ============================================================================

import { avatarEnCache, obtenerAvatar } from "@paquetes/api"
import { useEffect, useState } from "react"

import type { Guid } from "@paquetes/modelos"

/**
 * Devuelve la URL de la foto de alguien, o `null` mientras no la haya.
 *
 * La caché se consulta de forma síncrona para el estado inicial: si la foto ya
 * se había descargado, se pinta en el primer render en lugar de enseñar las
 * iniciales y sustituirlas un instante después, que es un parpadeo muy visible
 * en una lista larga.
 *
 * @param usuarioId Usuario del que se quiere la foto.
 * @param avatarActualizado Marca de versión; cambia cuando cambia la foto.
 * @param tieneAvatar Si se sabe que no hay foto, no se pide nada.
 */
export function useAvatar(
	usuarioId: Guid | null | undefined,
	avatarActualizado: string | null | undefined,
	tieneAvatar = true,
): string | null {
	const [url, setUrl] = useState<string | null>(() =>
		usuarioId && tieneAvatar ? avatarEnCache(usuarioId, avatarActualizado ?? null) : null,
	)

	useEffect(() => {
		if (!usuarioId || !tieneAvatar) {
			setUrl(null)
			return
		}

		const version = avatarActualizado ?? null
		const cacheada = avatarEnCache(usuarioId, version)

		if (cacheada) {
			setUrl(cacheada)
			return
		}

		let vigente = true

		void obtenerAvatar(usuarioId, version).then((descargada) => {
			// El componente puede haberse desmontado, o haber pasado a mostrar a otra
			// persona, mientras la descarga estaba en vuelo.
			if (vigente) setUrl(descargada)
		})

		return () => {
			vigente = false
		}
	}, [usuarioId, avatarActualizado, tieneAvatar])

	return url
}
