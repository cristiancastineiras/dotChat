// ============================================================================
// ESTADOS DE LA INTERFAZ
// ============================================================================
// Qué se enseña cuando no hay nada que enseñar, cuando algo se está cargando y
// cuando algo ha fallado. Son las tres pantallas que más se descuidan y las que
// más se ven cuando algo va mal.
// ============================================================================

import { cn } from "@paquetes/utiles"
import { Button, Skeleton, Spin } from "antd"

import type { LucideIcon } from "lucide-react"

// ---------------------------------------------------------------------------
// Estado vacío
// ---------------------------------------------------------------------------

interface PropiedadesVacio {
	icono?: LucideIcon
	titulo: string
	descripcion?: string
	/** Acción sugerida, cuando hay una obvia. */
	accion?: { texto: string; alPulsar: () => void }
	className?: string
}

/** Hueco con explicación para una lista sin contenido. */
export function EstadoVacio({
	icono: Icono,
	titulo,
	descripcion,
	accion,
	className,
}: PropiedadesVacio) {
	return (
		<div
			className={cn(
				"flex flex-col items-center justify-center gap-3 px-6 py-12 text-center",
				className,
			)}
		>
			{Icono && (
				<div className="bg-lienzo flex h-12 w-12 items-center justify-center rounded-full">
					<Icono className="text-tinta-tenue h-6 w-6" aria-hidden />
				</div>
			)}

			<div className="space-y-1">
				<p className="text-tinta text-sm font-medium">{titulo}</p>
				{descripcion && <p className="text-tinta-tenue max-w-xs text-xs">{descripcion}</p>}
			</div>

			{accion && (
				<Button size="small" onClick={accion.alPulsar}>
					{accion.texto}
				</Button>
			)}
		</div>
	)
}

// ---------------------------------------------------------------------------
// Cargando
// ---------------------------------------------------------------------------

interface PropiedadesCargando {
	texto?: string
	className?: string
}

/** Indicador centrado, para esperas cortas. */
export function Cargando({ texto, className }: PropiedadesCargando) {
	return (
		<output className={cn("flex flex-col items-center justify-center gap-3 py-12", className)}>
			<Spin />
			{texto && <p className="text-tinta-tenue text-xs">{texto}</p>}
		</output>
	)
}

/**
 * Esqueleto de la lista de conversaciones.
 *
 * Se prefiere a un indicador giratorio porque reserva el espacio real de las
 * filas: cuando llegan los datos, nada salta de sitio.
 */
export function EsqueletoConversaciones({ filas = 6 }: { filas?: number }) {
	return (
		<div className="space-y-1 p-2" aria-hidden>
			{Array.from({ length: filas }, (_, indice) => (
				<div key={indice} className="flex items-center gap-3 rounded-lg p-2">
					<Skeleton.Avatar active size={40} shape="circle" />
					<div className="min-w-0 flex-1">
						<Skeleton active title={false} paragraph={{ rows: 2, width: ["60%", "85%"] }} />
					</div>
				</div>
			))}
		</div>
	)
}

// ---------------------------------------------------------------------------
// Error
// ---------------------------------------------------------------------------

interface PropiedadesError {
	titulo?: string
	mensaje: string
	/** Identificador de traza, para cruzarlo con los registros del servidor. */
	trazaId?: string | undefined
	alReintentar?: () => void
	className?: string
}

/** Aviso de fallo con reintento. */
export function EstadoError({
	titulo = "Algo ha fallado",
	mensaje,
	trazaId,
	alReintentar,
	className,
}: PropiedadesError) {
	return (
		<div
			className={cn(
				"flex flex-col items-center justify-center gap-3 px-6 py-12 text-center",
				className,
			)}
			role="alert"
		>
			<div className="space-y-1">
				<p className="text-tinta text-sm font-medium">{titulo}</p>
				<p className="text-tinta-suave max-w-sm text-xs">{mensaje}</p>

				{trazaId && <p className="text-tinta-tenue font-mono text-[11px]">Traza: {trazaId}</p>}
			</div>

			{alReintentar && (
				<Button size="small" onClick={alReintentar}>
					Reintentar
				</Button>
			)}
		</div>
	)
}
