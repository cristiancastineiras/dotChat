// ============================================================================
// ADJUNTO DE UN MENSAJE
// ============================================================================
// Las imágenes se dibujan; el resto se ofrece como descarga. En los dos casos
// los bytes se piden con el cliente autenticado, porque el endpoint exige
// cabecera de autorización y no se puede enlazar directamente.
// ============================================================================

import { descargarBlob, mensajeDeError } from "@paquetes/api"
import { cn, extensionDe, formatearTamano, guardarComoArchivo } from "@paquetes/utiles"
import { Button, Image, Spin } from "antd"
import { Download, FileText } from "lucide-react"
import { useEffect, useState } from "react"
import { toast } from "sonner"

import type { Adjunto } from "@paquetes/modelos"

interface Propiedades {
	adjunto: Adjunto
	/** Previsualización local mientras el mensaje aún no está confirmado. */
	vistaPreviaLocal?: string | undefined
	/** Cierto mientras la subida está en curso. */
	pendiente: boolean
	esPropio: boolean
}

export function AdjuntoMensaje({ adjunto, vistaPreviaLocal, pendiente, esPropio }: Propiedades) {
	if (adjunto.esImagen) {
		return (
			<ImagenAdjunta adjunto={adjunto} vistaPreviaLocal={vistaPreviaLocal} pendiente={pendiente} />
		)
	}

	return <ArchivoAdjunto adjunto={adjunto} pendiente={pendiente} esPropio={esPropio} />
}

// ---------------------------------------------------------------------------
// Imagen
// ---------------------------------------------------------------------------

function ImagenAdjunta({
	adjunto,
	vistaPreviaLocal,
	pendiente,
}: {
	adjunto: Adjunto
	vistaPreviaLocal?: string | undefined
	pendiente: boolean
}) {
	const [url, setUrl] = useState<string | null>(vistaPreviaLocal ?? null)
	const [fallo, setFallo] = useState(false)

	useEffect(() => {
		// Mientras se sube se enseña la copia local; no hay nada que descargar
		// todavía porque el adjunto aún no tiene identificador.
		if (pendiente || !adjunto.id) return

		let vigente = true
		let objetoUrl: string | null = null

		void (async () => {
			try {
				const contenido = await descargarBlob(`adjuntos/${adjunto.id}`)
				if (!vigente) return

				objetoUrl = URL.createObjectURL(contenido)
				setUrl(objetoUrl)
			} catch {
				if (vigente) setFallo(true)
			}
		})()

		return () => {
			vigente = false

			// La URL de objeto retiene el blob hasta que se revoca: sin esto, una
			// conversación con muchas fotos acabaría con decenas de megas retenidos.
			if (objetoUrl) URL.revokeObjectURL(objetoUrl)
		}
	}, [adjunto.id, pendiente])

	// Se reserva el hueco con la proporción real que envía el servidor: así la
	// conversación no da un salto cuando la imagen termina de cargar.
	const proporcion = adjunto.ancho && adjunto.alto ? adjunto.ancho / adjunto.alto : undefined

	if (fallo) {
		return (
			<div className="text-tinta-tenue flex items-center gap-2 px-3 py-2 text-xs">
				<FileText className="h-4 w-4" aria-hidden />
				No se ha podido cargar la imagen.
			</div>
		)
	}

	return (
		<div
			className="bg-lienzo relative max-w-sm overflow-hidden"
			style={proporcion ? { aspectRatio: String(proporcion) } : undefined}
		>
			{url ? (
				<Image
					src={url}
					alt={adjunto.nombreArchivo}
					className="!block h-full w-full !object-cover"
					preview={
						pendiente
							? false
							: {
									mask: <span className="text-xs font-medium">Ver a tamaño completo</span>,
								}
					}
				/>
			) : (
				<div className="flex min-h-40 items-center justify-center">
					<Spin size="small" />
				</div>
			)}

			{pendiente && (
				<div className="absolute inset-0 flex items-center justify-center bg-white/40">
					<Spin size="small" />
				</div>
			)}
		</div>
	)
}

// ---------------------------------------------------------------------------
// Archivo
// ---------------------------------------------------------------------------

function ArchivoAdjunto({
	adjunto,
	pendiente,
	esPropio,
}: {
	adjunto: Adjunto
	pendiente: boolean
	esPropio: boolean
}) {
	const [descargando, setDescargando] = useState(false)

	async function descargar() {
		if (!adjunto.id) return

		setDescargando(true)

		try {
			const contenido = await descargarBlob(`adjuntos/${adjunto.id}`)
			guardarComoArchivo(contenido, adjunto.nombreArchivo)
		} catch (error) {
			toast.error(mensajeDeError(error))
		} finally {
			setDescargando(false)
		}
	}

	const extension = extensionDe(adjunto.nombreArchivo)

	return (
		<div className="flex items-center gap-3 px-3 py-2.5">
			<div
				className={cn(
					"flex h-9 w-9 shrink-0 items-center justify-center rounded-lg",
					esPropio ? "bg-white/15" : "bg-lienzo",
				)}
			>
				{extension ? (
					<span
						className={cn("text-[9px] font-bold", esPropio ? "text-white" : "text-tinta-suave")}
					>
						{extension.slice(0, 4)}
					</span>
				) : (
					<FileText className="h-4 w-4" aria-hidden />
				)}
			</div>

			<div className="min-w-0 flex-1">
				<p className="truncate text-sm font-medium">{adjunto.nombreArchivo}</p>
				<p className={cn("text-xs", esPropio ? "text-white/70" : "text-tinta-tenue")}>
					{formatearTamano(adjunto.tamanoBytes)}
				</p>
			</div>

			<Button
				type="text"
				size="small"
				loading={descargando}
				disabled={pendiente || !adjunto.id}
				onClick={() => void descargar()}
				aria-label={`Descargar ${adjunto.nombreArchivo}`}
				className={esPropio ? "!text-white hover:!bg-white/15" : ""}
			>
				{!descargando && <Download className="h-4 w-4" />}
			</Button>
		</div>
	)
}
