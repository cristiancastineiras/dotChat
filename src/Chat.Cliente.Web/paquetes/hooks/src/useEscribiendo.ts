// ============================================================================
// AVISO DE «ESTÁ ESCRIBIENDO»
// ============================================================================

import { useCallback, useRef } from "react"

import { hub } from "./conexionHub"

import type { Guid } from "@paquetes/modelos"

/**
 * Intervalo mínimo entre dos avisos, en milisegundos.
 *
 * Coincide con el que aplica ChatHub. Limitar aquí también no es redundante:
 * ahorra una invocación por pulsación de tecla que el servidor iba a tirar de
 * todas formas, y en una conversación activa eso son decenas de mensajes por
 * minuto y usuario que no llegan a salir del navegador.
 */
const INTERVALO_MS = 2_000

/**
 * Devuelve una función que avisa a la sala de que se está escribiendo, sin
 * saturar la conexión.
 *
 * @param salaId Sala en la que se escribe.
 */
export function useEscribiendo(salaId: Guid | null): () => void {
	const ultimoAviso = useRef(0)

	return useCallback(() => {
		if (!salaId) return

		const ahora = Date.now()
		if (ahora - ultimoAviso.current < INTERVALO_MS) return

		ultimoAviso.current = ahora
		hub.escribiendo(salaId)
	}, [salaId])
}
