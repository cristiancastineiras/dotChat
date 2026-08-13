// ============================================================================
// REDACTOR
// ============================================================================
// Caja de escritura con emojis, adjuntos y autocompletado de atajos.
//
// El texto se envía tal cual se escribe, con sus `:atajos:` sin expandir: quien
// los traduce es el servidor, dentro del envío, para que lo que se cifra y se
// guarda sea ya el emoji y el historial se lea igual desde la terminal y desde
// el navegador. El autocompletado de aquí solo ayuda a escribirlos.
// ============================================================================

import { configuracion } from "@paquetes/api"
import { useEscribiendo } from "@paquetes/hooks"
import {
	cn,
	esImagen,
	formatearTamano,
	sugerirEmojis,
	validarTamano,
	type EntradaEmoji,
} from "@paquetes/utiles"
import { Button, Progress, Tooltip } from "antd"
import { Paperclip, Send, Smile, X } from "lucide-react"
import {
	useCallback,
	useEffect,
	useMemo,
	useRef,
	useState,
	type ChangeEvent,
	type ClipboardEvent,
	type DragEvent,
	type KeyboardEvent,
} from "react"
import { toast } from "sonner"

import { SelectorEmojis } from "./SelectorEmojis"

import type { AdjuntoPendiente, Guid } from "@paquetes/modelos"

/** Alto máximo de la caja antes de que empiece a desplazarse, en píxeles. */
const ALTO_MAXIMO = 160

interface Propiedades {
	salaId: Guid
	nombreSala: string
	/** Avance de la subida en curso, de 0 a 1. */
	progresoSubida: number | null
	alEnviar: (envio: { salaId: Guid; texto: string; archivo?: File | null }) => Promise<boolean>
}

export function Redactor({ salaId, nombreSala, progresoSubida, alEnviar }: Propiedades) {
	const [texto, setTexto] = useState("")
	const [adjunto, setAdjunto] = useState<AdjuntoPendiente | null>(null)
	const [selectorAbierto, setSelectorAbierto] = useState(false)
	const [arrastrando, setArrastrando] = useState(false)
	const [enviando, setEnviando] = useState(false)

	const areaRef = useRef<HTMLTextAreaElement | null>(null)
	const archivoRef = useRef<HTMLInputElement | null>(null)

	const avisarEscribiendo = useEscribiendo(salaId)

	// Al cambiar de conversación se descarta el borrador: mezclarlo con otra sala
	// es la clase de error que acaba enviando un mensaje a quien no debía.
	useEffect(() => {
		setTexto("")
		setAdjunto((anterior) => {
			if (anterior?.vistaPrevia) URL.revokeObjectURL(anterior.vistaPrevia)
			return null
		})
	}, [salaId])

	// La caja crece con el contenido hasta un tope. Se recalcula en cada cambio
	// porque no hay forma de hacerlo solo en CSS con un textarea.
	useEffect(() => {
		const area = areaRef.current
		if (!area) return

		area.style.height = "auto"
		area.style.height = `${Math.min(area.scrollHeight, ALTO_MAXIMO)}px`
	}, [texto])

	// ---------------------------------------------------------------------
	// Autocompletado de atajos
	// ---------------------------------------------------------------------

	/** Atajo `:nombre` que se está escribiendo justo antes del cursor. */
	const atajoEnCurso = useMemo(() => {
		const area = areaRef.current
		const posicion = area?.selectionStart ?? texto.length

		const antes = texto.slice(0, posicion)
		const coincidencia = /:([a-z0-9_+-]{1,32})$/iu.exec(antes)

		if (!coincidencia?.[1]) return null

		return { prefijo: coincidencia[1], inicio: posicion - coincidencia[0].length, fin: posicion }
	}, [texto])

	const sugerencias = useMemo(
		() => (atajoEnCurso ? sugerirEmojis(atajoEnCurso.prefijo) : []),
		[atajoEnCurso],
	)

	const [indiceSugerencia, setIndiceSugerencia] = useState(0)

	useEffect(() => {
		setIndiceSugerencia(0)
	}, [atajoEnCurso?.prefijo])

	/** Sustituye el atajo en curso por el emoji elegido. */
	const aplicarSugerencia = useCallback(
		(emoji: EntradaEmoji) => {
			if (!atajoEnCurso) return

			const nuevo =
				texto.slice(0, atajoEnCurso.inicio) + emoji.simbolo + texto.slice(atajoEnCurso.fin)

			setTexto(nuevo)

			// El cursor debe quedar detrás del emoji, no al final del texto.
			requestAnimationFrame(() => {
				const area = areaRef.current
				if (!area) return

				const posicion = atajoEnCurso.inicio + emoji.simbolo.length
				area.focus()
				area.setSelectionRange(posicion, posicion)
			})
		},
		[atajoEnCurso, texto],
	)

	// ---------------------------------------------------------------------
	// Adjuntos
	// ---------------------------------------------------------------------

	const aceptarArchivo = useCallback((archivo: File) => {
		const error = validarTamano(archivo, configuracion.maxAdjuntoMiB)

		if (error) {
			toast.error(error)
			return
		}

		setAdjunto((anterior) => {
			if (anterior?.vistaPrevia) URL.revokeObjectURL(anterior.vistaPrevia)

			return {
				archivo,
				vistaPrevia: esImagen(archivo) ? URL.createObjectURL(archivo) : null,
				esImagen: esImagen(archivo),
			}
		})
	}, [])

	const quitarAdjunto = useCallback(() => {
		setAdjunto((anterior) => {
			if (anterior?.vistaPrevia) URL.revokeObjectURL(anterior.vistaPrevia)
			return null
		})
	}, [])

	/** Pegar una imagen del portapapeles la adjunta, igual que arrastrarla. */
	function alPegar(evento: ClipboardEvent<HTMLTextAreaElement>) {
		const archivo = [...evento.clipboardData.files][0]

		if (archivo) {
			evento.preventDefault()
			aceptarArchivo(archivo)
		}
	}

	function alSoltar(evento: DragEvent<HTMLDivElement>) {
		evento.preventDefault()
		setArrastrando(false)

		const archivo = evento.dataTransfer.files[0]
		if (archivo) aceptarArchivo(archivo)
	}

	function alElegirArchivo(evento: ChangeEvent<HTMLInputElement>) {
		const archivo = evento.target.files?.[0]
		if (archivo) aceptarArchivo(archivo)

		// Se limpia para que elegir el mismo archivo dos veces seguidas vuelva a
		// disparar el evento.
		evento.target.value = ""
	}

	// ---------------------------------------------------------------------
	// Envío
	// ---------------------------------------------------------------------

	const enviar = useCallback(async () => {
		const limpio = texto.trim()
		if ((!limpio && !adjunto) || enviando) return

		setEnviando(true)

		// La caja se vacía antes de que el servidor confirme: el mensaje ya se está
		// pintando en la conversación y esperar aquí haría que pareciera colgada.
		const textoEnviado = limpio
		const archivoEnviado = adjunto?.archivo ?? null

		setTexto("")
		quitarAdjunto()

		try {
			const correcto = await alEnviar({
				salaId,
				texto: textoEnviado,
				archivo: archivoEnviado,
			})

			// Si falló, el mensaje se queda en la conversación marcado como fallido y
			// con su botón de reintento: no se devuelve el texto a la caja, que
			// borraría lo que el usuario haya escrito mientras tanto.
			if (!correcto) {
				areaRef.current?.focus()
			}
		} finally {
			setEnviando(false)
			areaRef.current?.focus()
		}
	}, [texto, adjunto, enviando, alEnviar, salaId, quitarAdjunto])

	function alPulsarTecla(evento: KeyboardEvent<HTMLTextAreaElement>) {
		// El autocompletado se queda con las teclas de navegación mientras esté
		// desplegado; si no, mover el cursor cerraría la lista.
		if (sugerencias.length > 0) {
			if (evento.key === "ArrowDown") {
				evento.preventDefault()
				setIndiceSugerencia((indice) => (indice + 1) % sugerencias.length)
				return
			}

			if (evento.key === "ArrowUp") {
				evento.preventDefault()
				setIndiceSugerencia((indice) => (indice - 1 + sugerencias.length) % sugerencias.length)
				return
			}

			if (evento.key === "Tab" || (evento.key === "Enter" && !evento.shiftKey)) {
				const elegido = sugerencias[indiceSugerencia]

				if (elegido) {
					evento.preventDefault()
					aplicarSugerencia(elegido)
					return
				}
			}

			if (evento.key === "Escape") {
				evento.preventDefault()
				setTexto((actual) => `${actual} `)
				return
			}
		}

		// Intro envía; Mayús+Intro salta de línea.
		if (evento.key === "Enter" && !evento.shiftKey) {
			evento.preventDefault()
			void enviar()
		}
	}

	const puedeEnviar = (texto.trim().length > 0 || adjunto !== null) && !enviando

	return (
		<div
			onDragOver={(evento) => {
				evento.preventDefault()
				setArrastrando(true)
			}}
			onDragLeave={() => setArrastrando(false)}
			onDrop={alSoltar}
			className={cn(
				"relative rounded-xl border transition-colors",
				arrastrando ? "border-marca-500 bg-marca-50" : "border-borde bg-panel",
			)}
		>
			{/* Zona de arrastre */}
			{arrastrando && (
				<div className="bg-marca-50/90 text-marca-700 pointer-events-none absolute inset-0 z-10 flex items-center justify-center rounded-xl text-sm font-medium">
					Suelta el archivo para adjuntarlo
				</div>
			)}

			{/* Autocompletado */}
			{sugerencias.length > 0 && (
				<ul className="border-borde bg-panel absolute bottom-full left-0 z-20 mb-2 w-64 overflow-hidden rounded-lg border py-1 shadow-lg">
					{sugerencias.map((emoji, indice) => (
						<li key={emoji.nombre}>
							<button
								type="button"
								onMouseDown={(evento) => {
									// `mousedown` y no `click`: al hacer clic, el textarea perdería
									// el foco antes de que llegara el evento y se perdería la
									// posición del cursor.
									evento.preventDefault()
									aplicarSugerencia(emoji)
								}}
								onMouseEnter={() => setIndiceSugerencia(indice)}
								className={cn(
									"flex w-full items-center gap-2.5 px-3 py-1.5 text-left text-sm",
									indice === indiceSugerencia ? "bg-marca-50" : "hover:bg-lienzo",
								)}
							>
								<span className="text-base">{emoji.simbolo}</span>
								<span className="text-tinta-suave">:{emoji.nombre}:</span>
							</button>
						</li>
					))}
				</ul>
			)}

			{/* Adjunto elegido */}
			{adjunto && (
				<div className="border-borde flex items-center gap-3 border-b px-3 py-2">
					{adjunto.vistaPrevia ? (
						<img
							src={adjunto.vistaPrevia}
							alt=""
							className="h-11 w-11 shrink-0 rounded-md object-cover"
						/>
					) : (
						<div className="bg-lienzo flex h-11 w-11 shrink-0 items-center justify-center rounded-md">
							<Paperclip className="text-tinta-suave h-4 w-4" aria-hidden />
						</div>
					)}

					<div className="min-w-0 flex-1">
						<p className="text-tinta truncate text-xs font-medium">{adjunto.archivo.name}</p>
						<p className="text-tinta-tenue text-[11px]">{formatearTamano(adjunto.archivo.size)}</p>

						{progresoSubida !== null && (
							<Progress
								percent={Math.round(progresoSubida * 100)}
								size="small"
								showInfo={false}
								className="!mb-0"
							/>
						)}
					</div>

					<Button type="text" size="small" onClick={quitarAdjunto} aria-label="Quitar adjunto">
						<X className="h-4 w-4" />
					</Button>
				</div>
			)}

			{/* Caja de escritura */}
			<div className="flex items-end gap-1 p-1.5">
				<Tooltip title="Adjuntar archivo">
					<Button
						type="text"
						onClick={() => archivoRef.current?.click()}
						aria-label="Adjuntar archivo"
					>
						<Paperclip className="text-tinta-suave h-4.5 w-4.5" />
					</Button>
				</Tooltip>

				<input
					ref={archivoRef}
					type="file"
					className="hidden"
					onChange={alElegirArchivo}
					aria-hidden
					tabIndex={-1}
				/>

				<textarea
					ref={areaRef}
					value={texto}
					onChange={(evento) => {
						setTexto(evento.target.value)
						avisarEscribiendo()
					}}
					onKeyDown={alPulsarTecla}
					onPaste={alPegar}
					rows={1}
					placeholder={`Escribe en ${nombreSala}…`}
					aria-label={`Mensaje para ${nombreSala}`}
					className="desplazable text-tinta placeholder:text-tinta-tenue max-h-40 min-h-9 flex-1 resize-none bg-transparent px-1 py-2 text-sm outline-none"
				/>

				<SelectorEmojis
					abierto={selectorAbierto}
					alCambiarApertura={setSelectorAbierto}
					alElegir={(emoji) => {
						setTexto((actual) => actual + emoji)
						areaRef.current?.focus()
					}}
				>
					<Button type="text" aria-label="Emojis">
						<Smile className="text-tinta-suave h-4.5 w-4.5" />
					</Button>
				</SelectorEmojis>

				<Button
					type="primary"
					disabled={!puedeEnviar}
					loading={enviando}
					onClick={() => void enviar()}
					aria-label="Enviar mensaje"
				>
					{!enviando && <Send className="h-4 w-4" />}
				</Button>
			</div>
		</div>
	)
}
