// ============================================================================
// USUARIOS, PRESENCIA Y FOTO DE PERFIL
// ============================================================================

import { api, descargarBlob, ejecutar } from "./cliente"

import type { Guid, Perfil, Presencia, Usuario } from "@paquetes/modelos"

/** Lista los usuarios de la plataforma. */
export async function listar(): Promise<Usuario[]> {
	return await ejecutar(() => api.get("usuarios").json<Usuario[]>())
}

/** Devuelve quién está en línea y cuándo se vio por última vez al resto. */
export async function obtenerPresencia(): Promise<Presencia[]> {
	return await ejecutar(() => api.get("usuarios/presencia").json<Presencia[]>())
}

/** Devuelve el perfil del usuario autenticado. */
export async function obtenerPerfil(): Promise<Perfil> {
	return await ejecutar(() => api.get("usuarios/yo").json<Perfil>())
}

/**
 * Sustituye la foto de perfil.
 *
 * @param imagen Imagen ya recortada por el cliente.
 * @param alProgresar Notifica el avance, de 0 a 1.
 * @returns El perfil actualizado, con la marca de versión nueva.
 */
export async function subirAvatar(
	imagen: Blob,
	alProgresar?: (fraccion: number) => void,
): Promise<Perfil> {
	const cuerpo = new FormData()
	cuerpo.append("archivo", imagen, "avatar.jpg")

	return await ejecutar(() =>
		api
			.post("usuarios/yo/avatar", {
				body: cuerpo,
				timeout: false,
				...(alProgresar
					? {
							onUploadProgress: (progreso: { percent: number }) => {
								alProgresar(progreso.percent)
							},
						}
					: {}),
			})
			.json<Perfil>(),
	)
}

/** Retira la foto de perfil y vuelve a las iniciales. */
export async function eliminarAvatar(): Promise<Perfil> {
	return await ejecutar(() => api.delete("usuarios/yo/avatar").json<Perfil>())
}

/**
 * Descarga la foto de perfil de alguien.
 *
 * No se puede enlazar directamente desde una etiqueta de imagen porque el
 * endpoint exige cabecera de autorización, así que se trae como blob y quien
 * llama lo envuelve en una URL de objeto.
 *
 * @param usuarioId Usuario cuya foto se pide.
 */
export async function descargarAvatar(usuarioId: Guid): Promise<Blob> {
	return await descargarBlob(`usuarios/${usuarioId}/avatar`)
}
