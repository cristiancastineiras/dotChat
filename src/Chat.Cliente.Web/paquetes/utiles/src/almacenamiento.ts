// ============================================================================
// ALMACENAMIENTO LOCAL
// ============================================================================
// Envoltorio sobre localStorage que no revienta cuando no hay localStorage.
//
// Hace falta más de lo que parece: en navegación privada de algunos navegadores
// escribir lanza una excepción de cuota, dentro de un iframe sin permisos el
// acceso lanza una excepción de seguridad, y un valor guardado por una versión
// anterior de la aplicación puede no ser el JSON que se espera. Ninguna de esas
// tres cosas debería tumbar la pantalla de inicio de sesión.
// ============================================================================

/** Prefijo de todas las claves, para no pisar las de otra app del mismo origen. */
const PREFIJO = "dotchat:"

/**
 * Lee un valor y lo interpreta como JSON.
 *
 * @param clave Clave, sin prefijo.
 * @param porDefecto Valor devuelto si no hay nada guardado o está corrupto.
 */
export function leer<T>(clave: string, porDefecto: T): T {
	try {
		const crudo = globalThis.localStorage?.getItem(PREFIJO + clave)
		if (crudo === null || crudo === undefined) return porDefecto

		return JSON.parse(crudo) as T
	} catch {
		// Almacenamiento inaccesible o contenido que no es JSON válido. En los dos
		// casos la respuesta correcta es la misma: seguir con el valor por defecto.
		return porDefecto
	}
}

/**
 * Guarda un valor serializándolo a JSON.
 *
 * @param clave Clave, sin prefijo.
 * @param valor Valor a guardar.
 * @returns `true` si se pudo guardar.
 */
export function guardar(clave: string, valor: unknown): boolean {
	try {
		globalThis.localStorage?.setItem(PREFIJO + clave, JSON.stringify(valor))
		return true
	} catch {
		return false
	}
}

/**
 * Borra un valor.
 *
 * @param clave Clave, sin prefijo.
 */
export function borrar(clave: string): void {
	try {
		globalThis.localStorage?.removeItem(PREFIJO + clave)
	} catch {
		// Si no se puede borrar tampoco se pudo escribir: no hay nada que limpiar.
	}
}

/** Claves usadas por la aplicación, en un solo sitio para evitar erratas. */
export const CLAVES = {
	/** Sesión iniciada: identificador, nombre y tokens. */
	sesion: "sesion",
	/** Última conversación abierta, para volver a ella al recargar. */
	ultimaSala: "ultima-sala",
	/** Preferencias de la interfaz. */
	preferencias: "preferencias",
} as const
