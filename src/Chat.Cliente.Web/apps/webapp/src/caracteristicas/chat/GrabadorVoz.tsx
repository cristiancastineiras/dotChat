// ============================================================================
// GRABADOR DE VOZ
// ============================================================================
// Sustituye la barra del redactor mientras se graba una nota de voz, igual que
// WhatsApp, Telegram o Signal: cronómetro, «cancelar» y «enviar». Se graba con
// la API `MediaRecorder` del navegador, sin ninguna librería — es soporte
// nativo desde hace años y añadir una dependencia solo para esto no compensa.
//
// El formato lo elige el propio navegador: se prueba una lista de tipos en
// orden de preferencia y se usa el primero que sepa grabar. Chrome y Firefox
// graban WebM/Opus; Safari, MP4/AAC. El servidor no recodifica el audio —lo
// guarda tal cual, como cualquier archivo—, así que lo único que hace falta es
// que el tipo MIME resultante quede bien identificado (ver TiposMime.cs).
// ============================================================================

import { formatearDuracion } from "@paquetes/utiles"
import { Button, Tooltip } from "antd"
import { Send, Trash2 } from "lucide-react"
import { useEffect, useRef, useState } from "react"
import { toast } from "sonner"

/** Tipos de contenedor probados en orden, con la extensión que les corresponde. */
const TIPOS_SOPORTADOS: readonly { mime: string; extension: string }[] = [
	{ mime: "audio/webm;codecs=opus", extension: ".weba" },
	{ mime: "audio/webm", extension: ".weba" },
	{ mime: "audio/ogg;codecs=opus", extension: ".oga" },
	{ mime: "audio/ogg", extension: ".oga" },
	{ mime: "audio/mp4", extension: ".m4a" },
]

/** Duración mínima para que una grabación se considere un mensaje real. */
const DURACION_MINIMA_MS = 400

/** Número de barras de la onda decorativa. */
const NUMERO_BARRAS = 27

interface Propiedades {
	/** Se sube el archivo grabado y se cierra el grabador. */
	alEnviar: (archivo: File, duracionMs: number) => void
	/** Se cierra el grabador sin enviar nada. */
	alCancelar: () => void
}

export function GrabadorVoz({ alEnviar, alCancelar }: Propiedades) {
	const [segundos, setSegundos] = useState(0)
	const [error, setError] = useState<string | null>(null)

	const grabadorRef = useRef<MediaRecorder | null>(null)
	const trozosRef = useRef<Blob[]>([])
	const streamRef = useRef<MediaStream | null>(null)
	const inicioRef = useRef(0)
	const barrasRef = useRef(
		Array.from({ length: NUMERO_BARRAS }, (_valor, indice) => ({
			id: `barra-${indice}`,
			altura: 0.25 + Math.random() * 0.75,
		})),
	)

	// Arranca la grabación al montarse y suelta el micrófono al desmontarse, pase
	// lo que pase: es la única forma de garantizar que no queda abierto si el
	// componente se retira por cualquier otro camino que no sea sus propios
	// botones (cambiar de sala en mitad de una grabación, por ejemplo).
	useEffect(() => {
		let cancelado = false

		void (async () => {
			try {
				const flujo = await navigator.mediaDevices.getUserMedia({ audio: true })

				if (cancelado) {
					for (const pista of flujo.getTracks()) pista.stop()
					return
				}

				streamRef.current = flujo

				const tipo = TIPOS_SOPORTADOS.find(
					(candidato) => "MediaRecorder" in window && MediaRecorder.isTypeSupported(candidato.mime),
				)

				const grabador = new MediaRecorder(flujo, tipo ? { mimeType: tipo.mime } : undefined)
				grabadorRef.current = grabador
				trozosRef.current = []

				grabador.ondataavailable = (evento) => {
					if (evento.data.size > 0) trozosRef.current.push(evento.data)
				}

				grabador.start()
				inicioRef.current = Date.now()
			} catch {
				if (!cancelado) {
					setError("No se ha podido acceder al micrófono. Revisa los permisos del navegador.")
				}
			}
		})()

		return () => {
			cancelado = true
			detener()
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps -- se arranca una sola vez, al montar.
	}, [])

	useEffect(() => {
		if (error) return

		const identificador = setInterval(() => setSegundos((actual) => actual + 1), 1000)
		return () => clearInterval(identificador)
	}, [error])

	/** Para la grabación y suelta el micrófono, sin tocar los trozos ya capturados. */
	function detener() {
		const grabador = grabadorRef.current

		if (grabador && grabador.state !== "inactive") {
			grabador.stop()
		}

		for (const pista of streamRef.current?.getTracks() ?? []) {
			pista.stop()
		}
	}

	function manejarCancelar() {
		detener()
		alCancelar()
	}

	function manejarEnviar() {
		const grabador = grabadorRef.current

		if (!grabador || error) {
			alCancelar()
			return
		}

		const duracionMs = Date.now() - inicioRef.current
		const tipoMime = grabador.mimeType || "audio/webm"

		grabador.addEventListener(
			"stop",
			() => {
				if (duracionMs < DURACION_MINIMA_MS || trozosRef.current.length === 0) {
					// Una grabación tan corta suele ser un toque accidental al botón, no
					// una nota de voz que alguien quisiera mandar de verdad.
					alCancelar()
					return
				}

				const extension = extensionParaMime(tipoMime)
				const blob = new Blob(trozosRef.current, { type: tipoMime })
				const archivo = new File([blob], `nota-de-voz${extension}`, { type: tipoMime })

				alEnviar(archivo, duracionMs)
			},
			{ once: true },
		)

		detener()
	}

	if (error) {
		return (
			<div className="flex items-center justify-between gap-3 p-1.5 px-3">
				<span className="text-xs text-red-700">{error}</span>
				<Button size="small" onClick={manejarCancelar}>
					Cerrar
				</Button>
			</div>
		)
	}

	return (
		<div className="flex items-center gap-3 p-1.5 px-2">
			<Tooltip title="Cancelar">
				<Button
					type="text"
					shape="circle"
					onClick={manejarCancelar}
					aria-label="Cancelar la grabación"
					className="shrink-0"
				>
					<Trash2 className="h-4.5 w-4.5 text-tinta-suave" />
				</Button>
			</Tooltip>

			<span className="h-2 w-2 shrink-0 animate-pulse rounded-full bg-red-500" aria-hidden />

			<span className="text-tinta w-10 shrink-0 text-sm tabular-nums" aria-hidden>
				{formatearDuracion(segundos * 1000)}
			</span>

			<div className="flex h-8 flex-1 items-center gap-[3px] overflow-hidden" aria-hidden>
				{barrasRef.current.map((barra, indice) => (
					<span
						key={barra.id}
						className="punto-escribiendo bg-marca-300 w-[3px] shrink-0 rounded-full"
						style={{ height: `${Math.round(barra.altura * 100)}%`, animationDelay: `${indice * 60}ms` }}
					/>
				))}
			</div>

			<output className="sr-only">
				Grabando nota de voz, {formatearDuracion(segundos * 1000)} transcurridos.
			</output>

			<Tooltip title="Enviar nota de voz">
				<Button
					type="primary"
					shape="circle"
					onClick={manejarEnviar}
					aria-label="Enviar nota de voz"
					className="shrink-0"
				>
					<Send className="h-4 w-4" />
				</Button>
			</Tooltip>
		</div>
	)
}

/** Aviso reutilizable cuando el navegador no ofrece grabación de audio. */
export function avisarSinMicrofono(): void {
	toast.error("Este navegador no permite grabar audio.")
}

/**
 * Extensión que corresponde a un tipo MIME de audio grabado.
 *
 * No se compara por igualdad exacta contra {@link TIPOS_SOPORTADOS}: `MediaRecorder`
 * puede reportar el tipo con parámetros distintos a los que se pidieron (por
 * ejemplo, sin el `codecs=opus` con el que se solicitó), así que basta con
 * reconocer el contenedor. La extensión sólo ayuda a que el nombre del archivo
 * tenga sentido —lo que de verdad decide cómo se trata en el servidor es el
 * contenido, no el nombre (ver ProcesadorAudioSniffer.cs)—, así que un mensaje
 * grabado en un formato no reconocido aquí igualmente se sube y se reproduce bien.
 *
 * @param tipoMime Tipo MIME real que reporta `MediaRecorder`.
 */
function extensionParaMime(tipoMime: string): string {
	const tipo = tipoMime.toLowerCase()

	if (tipo.includes("webm")) return ".weba"
	if (tipo.includes("ogg")) return ".oga"
	if (tipo.includes("mp4") || tipo.includes("aac") || tipo.includes("m4a")) return ".m4a"

	return ".weba"
}
