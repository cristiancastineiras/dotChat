// ============================================================================
// ADJUNTO DE UN MENSAJE
// ============================================================================
// Las imágenes se dibujan; el resto se ofrece como descarga. En los dos casos
// los bytes se piden con el cliente autenticado, porque el endpoint exige
// cabecera de autorización y no se puede enlazar directamente.
// ============================================================================

import { descargarBlob, mensajeDeError } from "@paquetes/api"
import { cn, extensionDe, formatearDuracion, formatearTamano, guardarComoArchivo } from "@paquetes/utiles"
import { Button, Image, Spin } from "antd"
import { Download, FileText, Pause, Play } from "lucide-react"
import { useEffect, useRef, useState } from "react"
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

	if (adjunto.esAudio) {
		return (
			<AudioAdjunto
				adjunto={adjunto}
				vistaPreviaLocal={vistaPreviaLocal}
				pendiente={pendiente}
				esPropio={esPropio}
			/>
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
// Audio
// ---------------------------------------------------------------------------

/** Número de barras con las que se dibuja la onda del audio. */
const NUMERO_BARRAS_ONDA = 40

/**
 * Claves estables de las barras de la onda, una por posición. Cada barra
 * representa siempre el mismo tramo de tiempo del audio —la posición es su
 * identidad—, así que no hace falta más que enumerarlas una vez.
 */
const IDS_BARRAS_ONDA = Array.from({ length: NUMERO_BARRAS_ONDA }, (_valor, indice) => `barra-${indice}`)

function AudioAdjunto({
	adjunto,
	vistaPreviaLocal,
	pendiente,
	esPropio,
}: {
	adjunto: Adjunto
	vistaPreviaLocal?: string | undefined
	pendiente: boolean
	esPropio: boolean
}) {
	const [url, setUrl] = useState<string | null>(vistaPreviaLocal ?? null)
	const [fallo, setFallo] = useState(false)
	const [picos, setPicos] = useState<number[] | null>(null)
	const [reproduciendo, setReproduciendo] = useState(false)
	const [posicionMs, setPosicionMs] = useState(0)
	const [duracionMs, setDuracionMs] = useState(adjunto.duracionMs ?? 0)

	const audioRef = useRef<HTMLAudioElement | null>(null)

	useEffect(() => {
		if (pendiente || !adjunto.id) return

		let vigente = true
		let objetoUrl: string | null = null

		void (async () => {
			try {
				const contenido = await descargarBlob(`adjuntos/${adjunto.id}`)
				if (!vigente) return

				objetoUrl = URL.createObjectURL(contenido)
				setUrl(objetoUrl)

				// La onda se calcula una sola vez, decodificando el audio entero: es
				// mucho más barato que analizarlo en tiempo real mientras suena, y no
				// hace falta que sea exacta —solo que se parezca a algo—, así que se
				// somete a un número fijo de muestras sin más precauciones.
				void calcularPicos(contenido).then((valores) => {
					if (vigente) setPicos(valores)
				})
			} catch {
				if (vigente) setFallo(true)
			}
		})()

		return () => {
			vigente = false
			if (objetoUrl) URL.revokeObjectURL(objetoUrl)
		}
	}, [adjunto.id, pendiente])

	// La previsualización local también se puede analizar de inmediato, sin
	// esperar a que el servidor confirme la subida.
	useEffect(() => {
		if (!vistaPreviaLocal) return

		let vigente = true

		fetch(vistaPreviaLocal)
			.then((respuesta) => respuesta.blob())
			.then(calcularPicos)
			.then((valores) => {
				if (vigente) setPicos(valores)
			})
			.catch(() => {
				// Sin onda no pasa nada: se degrada a la barra de progreso simple.
			})

		return () => {
			vigente = false
		}
	}, [vistaPreviaLocal])

	function alternarReproduccion() {
		const audio = audioRef.current
		if (!audio) return

		if (audio.paused) {
			void audio.play()
		} else {
			audio.pause()
		}
	}

	function buscar(fraccion: number) {
		const audio = audioRef.current
		if (!audio || !Number.isFinite(audio.duration)) return

		audio.currentTime = fraccion * audio.duration
	}

	const fraccion = duracionMs > 0 ? Math.min(1, posicionMs / duracionMs) : 0
	const colorBarra = esPropio ? "bg-white/35" : "bg-marca-200"
	const colorBarraActiva = esPropio ? "bg-white" : "bg-marca-500"

	if (fallo) {
		return (
			<div className={cn("flex items-center gap-2 px-3 py-2.5 text-xs", esPropio ? "text-white/80" : "text-tinta-tenue")}>
				<FileText className="h-4 w-4" aria-hidden />
				No se ha podido cargar el audio.
			</div>
		)
	}

	return (
		<div className="flex items-center gap-2.5 px-2.5 py-2.5">
			{url && (
				// eslint-disable-next-line jsx-a11y/media-has-caption -- es una nota de voz, no lleva pistas de texto.
				<audio
					ref={audioRef}
					src={url}
					preload="metadata"
					onPlay={() => setReproduciendo(true)}
					onPause={() => setReproduciendo(false)}
					onEnded={() => {
						setReproduciendo(false)
						setPosicionMs(0)
					}}
					onLoadedMetadata={(evento) => {
						const real = evento.currentTarget.duration
						if (Number.isFinite(real)) setDuracionMs(real * 1000)
					}}
					onTimeUpdate={(evento) => setPosicionMs(evento.currentTarget.currentTime * 1000)}
					className="hidden"
				/>
			)}

			<Button
				type="text"
				shape="circle"
				disabled={!url || pendiente}
				onClick={alternarReproduccion}
				aria-label={reproduciendo ? "Pausar" : "Reproducir"}
				className={esPropio ? "!text-white hover:!bg-white/15" : ""}
			>
				{!url ? (
					<Spin size="small" />
				) : reproduciendo ? (
					<Pause className="h-4 w-4" />
				) : (
					<Play className="h-4 w-4" />
				)}
			</Button>

			<button
				type="button"
				onClick={(evento) => {
					const rectangulo = evento.currentTarget.getBoundingClientRect()
					buscar((evento.clientX - rectangulo.left) / rectangulo.width)
				}}
				aria-label="Buscar en el audio"
				disabled={!url}
				className="flex h-8 flex-1 items-center gap-[3px] overflow-hidden disabled:cursor-default"
			>
				{(picos ?? Array.from({ length: NUMERO_BARRAS_ONDA }, () => 0.4))
					.map((pico, indice) => (
						<span
							key={IDS_BARRAS_ONDA[indice]}
							className={cn(
								"w-[3px] shrink-0 rounded-full transition-colors",
								indice / NUMERO_BARRAS_ONDA < fraccion ? colorBarraActiva : colorBarra,
							)}
							style={{ height: `${Math.max(15, Math.round(pico * 100))}%` }}
						/>
					))}
			</button>

			<span
				className={cn("w-9 shrink-0 text-right text-[11px] tabular-nums", esPropio ? "text-white/80" : "text-tinta-tenue")}
			>
				{formatearDuracion(reproduciendo || posicionMs > 0 ? posicionMs : duracionMs)}
			</span>
		</div>
	)
}

/**
 * Reduce un audio a un puñado de picos normalizados entre 0 y 1, para dibujar
 * su forma de onda. Se decodifica entero con la API de Audio Web —no hace
 * falta reproducirlo para eso— y se resume por buckets de amplitud máxima.
 *
 * @param blob Contenido del audio.
 */
async function calcularPicos(blob: Blob): Promise<number[]> {
	if (typeof AudioContext === "undefined") {
		return Array.from({ length: NUMERO_BARRAS_ONDA }, () => 0.5)
	}

	const contexto = new AudioContext()

	try {
		const buffer = await contexto.decodeAudioData(await blob.arrayBuffer())
		const canal = buffer.getChannelData(0)
		const tamanoBucket = Math.max(1, Math.floor(canal.length / NUMERO_BARRAS_ONDA))

		const picos = Array.from({ length: NUMERO_BARRAS_ONDA }, (_valor, indice) => {
			const inicio = indice * tamanoBucket
			let maximo = 0

			for (let i = inicio; i < inicio + tamanoBucket && i < canal.length; i++) {
				maximo = Math.max(maximo, Math.abs(canal[i] ?? 0))
			}

			return maximo
		})

		// Se normaliza contra el pico más alto: un audio grabado bajito no debería
		// dibujarse como una línea plana.
		const picoMaximo = Math.max(...picos, 0.01)
		return picos.map((pico) => Math.max(0.08, pico / picoMaximo))
	} finally {
		void contexto.close()
	}
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
