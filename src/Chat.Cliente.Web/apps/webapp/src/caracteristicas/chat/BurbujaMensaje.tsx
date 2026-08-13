// ============================================================================
// BURBUJA DE MENSAJE
// ============================================================================

import { Avatar } from "@paquetes/componentes"
import { useAlmacenSesion } from "@paquetes/estados"
import { useDirectorio } from "@paquetes/hooks"
import { cn, esSoloEmojis, formatearHora, formatearCompleto } from "@paquetes/utiles"
import { Button, Tooltip } from "antd"
import { AlertCircle, Check, Clock } from "lucide-react"
import { memo } from "react"

import { AdjuntoMensaje } from "./AdjuntoMensaje"

import type { MensajeVista } from "@paquetes/modelos"

interface Propiedades {
	mensaje: MensajeVista
	/** Continúa un bloque del mismo autor: sin avatar ni cabecera. */
	continuacion: boolean
	alReintentar: (mensaje: MensajeVista) => Promise<void>
	alDescartar: (mensaje: MensajeVista) => void
}

/**
 * Un mensaje del historial.
 *
 * Memorizado: en una conversación con doscientos mensajes cargados, cada mensaje
 * nuevo volvería a evaluar los doscientos. Con la comparación por propiedades,
 * solo se redibuja el que cambia de verdad.
 */
export const BurbujaMensaje = memo(function BurbujaMensaje({
	mensaje,
	continuacion,
	alReintentar,
	alDescartar,
}: Propiedades) {
	const usuarioPropioId = useAlmacenSesion((estado) => estado.sesion?.usuarioId)
	const buscarUsuario = useDirectorio()

	const esPropio = mensaje.usuarioId === usuarioPropioId
	const autor = buscarUsuario(mensaje.usuarioId)

	// Un mensaje que solo son emojis se pinta grande y sin burbuja: es la
	// convención de cualquier mensajería y hace que una reacción se lea de golpe.
	const soloEmojis = !mensaje.adjunto && esSoloEmojis(mensaje.texto)

	return (
		<div
			className={cn(
				"entrada-mensaje group flex gap-2.5",
				continuacion ? "mt-0.5" : "mt-3",
				esPropio && "flex-row-reverse",
			)}
		>
			{/* Avatar, solo en el primer mensaje del bloque */}
			<div className="w-8 shrink-0">
				{!continuacion && (
					<Avatar
						usuarioId={mensaje.usuarioId}
						nombre={mensaje.nombreUsuario}
						avatarActualizado={autor?.avatarActualizado ?? null}
						tieneAvatar={autor?.tieneAvatar ?? false}
						tamano="sm"
					/>
				)}
			</div>

			<div className={cn("flex max-w-[min(78%,42rem)] min-w-0 flex-col", esPropio && "items-end")}>
				{/* Autor y hora */}
				{!continuacion && (
					<div
						className={cn("mb-1 flex items-baseline gap-2 px-1", esPropio && "flex-row-reverse")}
					>
						<span className="text-tinta text-xs font-semibold">
							{esPropio ? "Tú" : mensaje.nombreUsuario}
						</span>
						<Tooltip title={formatearCompleto(mensaje.fechaEnvio)}>
							<span className="text-tinta-tenue text-[11px]">
								{formatearHora(mensaje.fechaEnvio)}
							</span>
						</Tooltip>
					</div>
				)}

				<div className={cn("flex items-end gap-1.5", esPropio && "flex-row-reverse")}>
					{soloEmojis ? (
						<p className="px-1 py-0.5 text-4xl leading-tight">{mensaje.texto}</p>
					) : (
						<div
							className={cn(
								"overflow-hidden rounded-2xl",
								esPropio
									? "bg-marca-600 rounded-br-md text-white"
									: "border-borde bg-panel-suave text-tinta rounded-bl-md border",
								mensaje.estado === "fallido" && "opacity-60",
							)}
						>
							{mensaje.adjunto && (
								<AdjuntoMensaje
									adjunto={mensaje.adjunto}
									vistaPreviaLocal={mensaje.vistaPreviaLocal}
									pendiente={mensaje.estado === "enviando"}
									esPropio={esPropio}
								/>
							)}

							{mensaje.texto && (
								<p
									className={cn(
										"px-3 py-2 text-sm break-words whitespace-pre-wrap",
										mensaje.adjunto && "pt-1.5",
									)}
								>
									{mensaje.texto}
								</p>
							)}
						</div>
					)}

					{/* Estado del envío, solo en los propios */}
					{esPropio && mensaje.estado && (
						<EstadoEnvio estado={mensaje.estado} hora={formatearHora(mensaje.fechaEnvio)} />
					)}

					{/* En una continuación la hora aparece al pasar el ratón, para no
					    repetirla en cada línea de un bloque. */}
					{continuacion && !mensaje.estado && (
						<span className="text-tinta-tenue mb-1 text-[10px] opacity-0 transition-opacity group-hover:opacity-100">
							{formatearHora(mensaje.fechaEnvio)}
						</span>
					)}
				</div>

				{/* Recuperación de un envío fallido */}
				{mensaje.estado === "fallido" && (
					<div className="mt-1 flex items-center gap-2 px-1">
						<span className="text-[11px] text-red-700">No se ha podido enviar.</span>
						<Button
							type="link"
							size="small"
							className="!h-auto !p-0 !text-[11px]"
							onClick={() => void alReintentar(mensaje)}
						>
							Reintentar
						</Button>
						<Button
							type="link"
							size="small"
							className="!text-tinta-tenue !h-auto !p-0 !text-[11px]"
							onClick={() => alDescartar(mensaje)}
						>
							Descartar
						</Button>
					</div>
				)}
			</div>
		</div>
	)
})

/** Icono con la situación de un mensaje propio. */
function EstadoEnvio({ estado, hora }: { estado: string; hora: string }) {
	if (estado === "enviando") {
		return (
			<Tooltip title="Enviando…">
				<Clock className="text-tinta-tenue mb-1 h-3 w-3 shrink-0" aria-label="Enviando" />
			</Tooltip>
		)
	}

	if (estado === "fallido") {
		return (
			<Tooltip title="No se ha enviado">
				<AlertCircle className="mb-1 h-3 w-3 shrink-0 text-red-600" aria-label="Fallido" />
			</Tooltip>
		)
	}

	return (
		<Tooltip title={`Enviado a las ${hora}`}>
			<Check className="text-tinta-tenue mb-1 h-3 w-3 shrink-0" aria-label="Enviado" />
		</Tooltip>
	)
}
