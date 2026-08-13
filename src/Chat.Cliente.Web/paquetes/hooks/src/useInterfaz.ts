// ============================================================================
// HOOKS DE INTERFAZ
// ============================================================================
// Utilidades pequeñas de comportamiento: consultas de medios, retardo de
// escritura, desplazamiento automático y notificaciones del navegador.
// ============================================================================

import { useCallback, useEffect, useRef, useState } from "react"

/**
 * Sigue una consulta de medios.
 *
 * @param consulta Consulta CSS, por ejemplo `(min-width: 768px)`.
 */
export function useConsultaMedios(consulta: string): boolean {
	const [coincide, setCoincide] = useState(() => globalThis.matchMedia?.(consulta).matches ?? false)

	useEffect(() => {
		const lista = globalThis.matchMedia(consulta)
		const alCambiar = (evento: MediaQueryListEvent) => setCoincide(evento.matches)

		setCoincide(lista.matches)
		lista.addEventListener("change", alCambiar)

		return () => lista.removeEventListener("change", alCambiar)
	}, [consulta])

	return coincide
}

/** Indica si la ventana tiene ancho de escritorio. */
export function useEsEscritorio(): boolean {
	return useConsultaMedios("(min-width: 768px)")
}

/**
 * Retrasa un valor hasta que deja de cambiar.
 *
 * @param valor Valor que cambia con frecuencia.
 * @param retardoMs Milisegundos de calma que hay que esperar.
 */
export function useRetardo<T>(valor: T, retardoMs = 250): T {
	const [retrasado, setRetrasado] = useState(valor)

	useEffect(() => {
		const temporizador = window.setTimeout(() => setRetrasado(valor), retardoMs)
		return () => window.clearTimeout(temporizador)
	}, [valor, retardoMs])

	return retrasado
}

/** Resultado del desplazamiento automático. */
interface UsoDesplazamiento<E extends HTMLElement> {
	/** Referencia que hay que colocar en el contenedor desplazable. */
	referencia: React.RefObject<E | null>
	/** Cierto cuando el usuario ha subido y no está viendo el final. */
	hayMensajesAbajo: boolean
	/** Lleva la vista al último mensaje. */
	irAlFinal: (suave?: boolean) => void
}

/**
 * Mantiene la conversación pegada al último mensaje, salvo que el usuario haya
 * subido a leer algo.
 *
 * Es el comportamiento que se espera de cualquier chat: si estás leyendo el
 * historial, un mensaje nuevo no debe arrancarte de donde estás; si estás al
 * final, debe seguirte solo.
 *
 * @param dependencia Valor que cambia cuando llegan mensajes nuevos.
 */
export function useDesplazamientoAlFinal<E extends HTMLElement>(
	dependencia: unknown,
): UsoDesplazamiento<E> {
	const referencia = useRef<E | null>(null)
	const [hayMensajesAbajo, setHayMensajesAbajo] = useState(false)

	// Si el usuario está al final se guarda en una referencia y no en el estado:
	// se consulta en cada evento de desplazamiento y no debe provocar renders.
	const pegadoAlFinal = useRef(true)

	const irAlFinal = useCallback((suave = false) => {
		const elemento = referencia.current
		if (!elemento) return

		elemento.scrollTo({
			top: elemento.scrollHeight,
			behavior: suave ? "smooth" : "auto",
		})

		pegadoAlFinal.current = true
		setHayMensajesAbajo(false)
	}, [])

	useEffect(() => {
		const elemento = referencia.current
		if (!elemento) return

		const alDesplazar = () => {
			// Un margen de holgura: el ajuste de píxeles del navegador hace que
			// «estar abajo» rara vez sea una diferencia de cero exacta.
			const distanciaAlFinal = elemento.scrollHeight - elemento.scrollTop - elemento.clientHeight

			pegadoAlFinal.current = distanciaAlFinal < 80
			setHayMensajesAbajo(!pegadoAlFinal.current)
		}

		elemento.addEventListener("scroll", alDesplazar, { passive: true })
		return () => elemento.removeEventListener("scroll", alDesplazar)
	}, [])

	useEffect(() => {
		if (pegadoAlFinal.current) {
			irAlFinal()
		} else {
			// Hay algo nuevo más abajo: se avisa con el botón flotante en lugar de
			// arrastrar la vista.
			setHayMensajesAbajo(true)
		}
	}, [dependencia, irAlFinal])

	return { referencia, hayMensajesAbajo, irAlFinal }
}

/** Resultado de las notificaciones del navegador. */
interface UsoNotificaciones {
	/** Cierto si el navegador las admite. */
	admitidas: boolean
	/** Permiso concedido por el usuario. */
	permiso: NotificationPermission
	/** Pide permiso; devuelve si quedó concedido. */
	pedirPermiso: () => Promise<boolean>
	/** Muestra una notificación, si hay permiso. */
	notificar: (titulo: string, cuerpo: string) => void
}

/**
 * Notificaciones del sistema para los mensajes que llegan con la pestaña en
 * segundo plano.
 */
export function useNotificaciones(activadas: boolean): UsoNotificaciones {
	const admitidas = typeof Notification !== "undefined"

	const [permiso, setPermiso] = useState<NotificationPermission>(() =>
		admitidas ? Notification.permission : "denied",
	)

	const pedirPermiso = useCallback(async () => {
		if (!admitidas) return false

		const resultado = await Notification.requestPermission()
		setPermiso(resultado)

		return resultado === "granted"
	}, [admitidas])

	const notificar = useCallback(
		(titulo: string, cuerpo: string) => {
			if (!admitidas || !activadas || permiso !== "granted") return

			// Con la pestaña delante no aporta nada: el mensaje ya se está viendo.
			if (document.visibilityState === "visible") return

			try {
				const aviso = new Notification(titulo, {
					body: cuerpo,
					icon: "/dotChat_.svg",
					// Sustituye a la anterior en lugar de apilarlas: veinte mensajes de
					// la misma persona no deben dejar veinte avisos en el escritorio.
					tag: "dotchat-mensaje",
				})

				aviso.addEventListener("click", () => {
					window.focus()
					aviso.close()
				})
			} catch {
				// Algunos navegadores exigen que la notificación salga de un service
				// worker. No es motivo para romper la recepción del mensaje.
			}
		},
		[admitidas, activadas, permiso],
	)

	return { admitidas, permiso, pedirPermiso, notificar }
}

/**
 * Ejecuta una acción cuando se pulsa una combinación de teclas.
 *
 * @param tecla Tecla, tal como la da `KeyboardEvent.key`.
 * @param accion Qué hacer.
 * @param opciones Modificadores exigidos.
 */
export function useAtajoTeclado(
	tecla: string,
	accion: () => void,
	opciones: { control?: boolean; mayusculas?: boolean } = {},
): void {
	const accionRef = useRef(accion)
	accionRef.current = accion

	const { control = false, mayusculas = false } = opciones

	useEffect(() => {
		const alPulsar = (evento: KeyboardEvent) => {
			if (evento.key.toLowerCase() !== tecla.toLowerCase()) return
			if (control && !(evento.ctrlKey || evento.metaKey)) return
			if (mayusculas && !evento.shiftKey) return

			evento.preventDefault()
			accionRef.current()
		}

		window.addEventListener("keydown", alPulsar)
		return () => window.removeEventListener("keydown", alPulsar)
	}, [tecla, control, mayusculas])
}
