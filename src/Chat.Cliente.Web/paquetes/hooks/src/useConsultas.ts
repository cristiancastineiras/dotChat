// ============================================================================
// CONSULTAS AL SERVIDOR
// ============================================================================
// Datos de solo lectura que no llegan por el hub: el directorio de usuarios, el
// catálogo de salas y los miembros de una conversación. Aquí sí encaja React
// Query: son listas que se piden, se cachean y se invalidan, sin nadie
// empujando cambios desde fuera.
//
// Lo que sí llega por el hub —salas propias, mensajes, presencia— vive en el
// almacén de Zustand. Mezclar las dos cosas para el mismo dato obligaría a
// mantener sincronizadas dos copias, que es de donde salen las incoherencias
// más difíciles de reproducir.
// ============================================================================

import { salas as apiSalas, usuarios as apiUsuarios } from "@paquetes/api"
import { useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query"
import { useCallback } from "react"

import type { Guid, MiembroSala, Sala, Usuario } from "@paquetes/modelos"

/** Claves de caché, en un solo sitio para que invalidar no dependa de recordarlas. */
export const clavesConsulta = {
	usuarios: ["usuarios"] as const,
	catalogoSalas: ["salas", "catalogo"] as const,
	miembros: (salaId: Guid) => ["salas", salaId, "miembros"] as const,
} as const

/**
 * Directorio de usuarios de la plataforma.
 *
 * Se usa para buscar con quién abrir una conversación y para resolver la foto
 * del autor de cada mensaje, que no viaja con el mensaje.
 */
export function useUsuarios(): UseQueryResult<Usuario[]> {
	return useQuery({
		queryKey: clavesConsulta.usuarios,
		queryFn: () => apiUsuarios.listar(),
		// El servidor ya cachea esta lista. Un minuto en el cliente evita pedirla
		// otra vez cada vez que se abre el diálogo de conversación nueva.
		staleTime: 60_000,
	})
}

/** Catálogo de salas visibles, para el explorador. */
export function useCatalogoSalas(activa = true): UseQueryResult<Sala[]> {
	return useQuery({
		queryKey: clavesConsulta.catalogoSalas,
		queryFn: () => apiSalas.listarCatalogo(),
		enabled: activa,
		staleTime: 30_000,
	})
}

/**
 * Miembros de una sala con su estado de conexión.
 *
 * @param salaId Sala consultada; `null` desactiva la consulta.
 * @param activa Permite no pedirlo mientras el panel está cerrado.
 */
export function useMiembros(salaId: Guid | null, activa = true): UseQueryResult<MiembroSala[]> {
	return useQuery({
		queryKey: clavesConsulta.miembros(salaId ?? "sin-sala"),
		queryFn: () => apiSalas.listarMiembros(salaId as Guid),
		enabled: activa && salaId !== null,
		staleTime: 30_000,
	})
}

/**
 * Devuelve un buscador de usuarios por identificador, para resolver la foto del
 * autor de un mensaje sin recorrer la lista en cada fila.
 */
export function useDirectorio(): (usuarioId: Guid) => Usuario | undefined {
	const { data } = useUsuarios()

	return useCallback((usuarioId: Guid) => data?.find((usuario) => usuario.id === usuarioId), [data])
}

/** Invalidaciones de caché que dispara la interfaz tras cambiar algo. */
export function useInvalidar(): {
	usuarios: () => void
	catalogo: () => void
	miembros: (salaId: Guid) => void
} {
	const cliente = useQueryClient()

	return {
		usuarios: () => {
			void cliente.invalidateQueries({ queryKey: clavesConsulta.usuarios })
		},
		catalogo: () => {
			void cliente.invalidateQueries({ queryKey: clavesConsulta.catalogoSalas })
		},
		miembros: (salaId) => {
			void cliente.invalidateQueries({ queryKey: clavesConsulta.miembros(salaId) })
		},
	}
}
