// ============================================================================
// HISTORIAL DE MENSAJES
// ============================================================================
// Lista desplazable con los mensajes, agrupados por día y por autor.
//
// No se virtualiza a propósito. Una lista virtual con alturas variables —hay
// mensajes de una línea y otros con una imagen de 400 píxeles— y anclada al
// final es una de las piezas más frágiles que se pueden escribir, y aquí el
// número de nodos ya está acotado: se cargan cincuenta mensajes por página y
// solo se piden más cuando el usuario sube a buscarlos. El día que una
// conversación abierta durante horas acumule miles de nodos, el sitio para
// arreglarlo es este componente y solo este.
// ============================================================================

import { Cargando, SeparadorFecha } from "@paquetes/componentes"
import { useDesplazamientoAlFinal } from "@paquetes/hooks"
import { formatearDia, mismoDia } from "@paquetes/utiles"
import { Button } from "antd"
import { ArrowDown, MessagesSquare } from "lucide-react"
import { useEffect, useMemo, useRef } from "react"

import { BurbujaMensaje } from "./BurbujaMensaje"

import type { Guid, MensajeVista } from "@paquetes/modelos"

/** Minutos dentro de los cuales dos mensajes del mismo autor se agrupan. */
const MINUTOS_AGRUPACION = 5

interface Propiedades {
	salaId: Guid
	mensajes: readonly MensajeVista[]
	cargando: boolean
	historialCompleto: boolean
	alPedirAnteriores: () => Promise<void>
	alReintentar: (mensaje: MensajeVista) => Promise<void>
	alDescartar: (mensaje: MensajeVista) => void
}

/** Un mensaje ya preparado para dibujar, con lo que decide su forma. */
interface Entrada {
	mensaje: MensajeVista
	/** Primer mensaje de un día distinto al anterior. */
	separador: string | null
	/** Continúa un bloque del mismo autor: se dibuja sin cabecera ni avatar. */
	continuacion: boolean
}

export function PanelMensajes({
	salaId,
	mensajes,
	cargando,
	historialCompleto,
	alPedirAnteriores,
	alReintentar,
	alDescartar,
}: Propiedades) {
	const { referencia, hayMensajesAbajo, irAlFinal } = useDesplazamientoAlFinal<HTMLDivElement>(
		mensajes.length,
	)

	// Alto del contenido antes de cargar una página anterior. Sin restaurar la
	// posición después, insertar cincuenta mensajes arriba lanzaría al usuario
	// al principio de la conversación cada vez que sube a leer.
	const altoPrevio = useRef<number | null>(null)

	// El centinela avisa cuando el usuario llega arriba del todo. Un observador
	// es preferible a escuchar el desplazamiento: no se dispara en cada píxel.
	const centinela = useRef<HTMLDivElement | null>(null)

	useEffect(() => {
		const objetivo = centinela.current
		const contenedor = referencia.current

		if (!objetivo || !contenedor || historialCompleto) return

		const observador = new IntersectionObserver(
			(entradas) => {
				if (!entradas[0]?.isIntersecting || cargando) return

				altoPrevio.current = contenedor.scrollHeight
				void alPedirAnteriores()
			},
			{ root: contenedor, rootMargin: "120px" },
		)

		observador.observe(objetivo)
		return () => observador.disconnect()
	}, [referencia, cargando, historialCompleto, alPedirAnteriores, salaId])

	// Restaura la posición tras anteponer una página.
	useEffect(() => {
		const contenedor = referencia.current
		if (!contenedor || altoPrevio.current === null) return

		const diferencia = contenedor.scrollHeight - altoPrevio.current
		altoPrevio.current = null

		if (diferencia > 0) {
			contenedor.scrollTop += diferencia
		}
	}, [mensajes, referencia])

	// Se recorre una sola vez para decidir separadores y agrupación, en lugar de
	// que cada burbuja mire a su vecina.
	const entradas = useMemo<Entrada[]>(() => {
		return mensajes.map((mensaje, indice) => {
			const anterior = indice > 0 ? mensajes[indice - 1] : undefined

			const separador =
				!anterior || !mismoDia(anterior.fechaEnvio, mensaje.fechaEnvio)
					? formatearDia(mensaje.fechaEnvio)
					: null

			const continuacion =
				anterior !== undefined &&
				separador === null &&
				anterior.usuarioId === mensaje.usuarioId &&
				dentroDeVentana(anterior.fechaEnvio, mensaje.fechaEnvio)

			return { mensaje, separador, continuacion }
		})
	}, [mensajes])

	return (
		// Fondo distinto del resto de la ventana: es lo que hace que las burbujas
		// blancas del interlocutor se lean sin necesidad de un borde alrededor.
		<div className="bg-lienzo relative min-h-0">
			<div
				ref={referencia}
				className="desplazable h-full overflow-y-auto px-4 py-3"
				role="log"
				aria-label="Mensajes de la conversación"
				aria-live="polite"
			>
				{/* Cabecera del historial: centinela de paginación o principio de todo */}
				{!historialCompleto && <div ref={centinela} className="h-px" aria-hidden />}

				{cargando && mensajes.length === 0 && <Cargando texto="Cargando conversación…" />}

				{cargando && mensajes.length > 0 && (
					<p className="text-tinta-tenue py-2 text-center text-xs">Cargando mensajes anteriores…</p>
				)}

				{historialCompleto && mensajes.length > 0 && (
					<p className="text-tinta-tenue py-3 text-center text-xs">
						Este es el principio de la conversación.
					</p>
				)}

				{!cargando && mensajes.length === 0 && (
					<div className="flex h-full flex-col items-center justify-center gap-3 text-center">
						<div className="bg-lienzo flex h-12 w-12 items-center justify-center rounded-full">
							<MessagesSquare className="text-tinta-tenue h-6 w-6" aria-hidden />
						</div>
						<div>
							<p className="text-tinta text-sm font-medium">Todavía no hay mensajes</p>
							<p className="text-tinta-tenue mt-1 text-xs">Escribe el primero y rompe el hielo.</p>
						</div>
					</div>
				)}

				{entradas.map(({ mensaje, separador, continuacion }) => (
					<div key={mensaje.identificadorEnvio ?? mensaje.id}>
						{separador && <SeparadorFecha texto={separador} />}

						<BurbujaMensaje
							mensaje={mensaje}
							continuacion={continuacion}
							alReintentar={alReintentar}
							alDescartar={alDescartar}
						/>
					</div>
				))}
			</div>

			{/* Aviso de que hay algo nuevo más abajo */}
			{hayMensajesAbajo && (
				<Button
					shape="circle"
					onClick={() => irAlFinal(true)}
					className="absolute right-6 bottom-4 shadow-md"
					aria-label="Ir al último mensaje"
				>
					<ArrowDown className="h-4 w-4" />
				</Button>
			)}
		</div>
	)
}

/** Indica si dos mensajes están lo bastante cerca en el tiempo para agruparse. */
function dentroDeVentana(anterior: string, actual: string): boolean {
	const diferencia = new Date(actual).getTime() - new Date(anterior).getTime()
	return diferencia < MINUTOS_AGRUPACION * 60_000
}
