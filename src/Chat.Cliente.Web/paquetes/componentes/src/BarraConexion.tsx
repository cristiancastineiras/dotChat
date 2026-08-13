// ============================================================================
// BARRA DE ESTADO DE LA CONEXIÓN
// ============================================================================

import { cn } from "@paquetes/utiles"
import { CloudOff, RefreshCw } from "lucide-react"

import type { EstadoConexion } from "@paquetes/modelos"

interface PropiedadesBarra {
	estado: EstadoConexion
	className?: string
}

/**
 * Avisa cuando la conversación ha dejado de estar al día.
 *
 * Solo aparece si hay algo que decir: con la conexión establecida no ocupa
 * espacio. Es importante que se vea, porque con el hub caído los mensajes que
 * se escriben salen igualmente por HTTP pero los ajenos no llegan hasta que se
 * reconecta, y sin aviso eso se interpreta como que nadie contesta.
 */
export function BarraConexion({ estado, className }: PropiedadesBarra) {
	if (estado === "conectado") return null

	const reconectando = estado === "reconectando" || estado === "conectando"

	// `output` ya tiene el papel de «estado» de forma nativa, sin necesidad de
	// declararlo con un atributo.
	return (
		<output
			className={cn(
				"flex items-center justify-center gap-2 px-4 py-1.5 text-xs font-medium",
				reconectando ? "bg-amber-50 text-amber-800" : "bg-red-50 text-red-800",
				className,
			)}
			aria-live="polite"
		>
			{reconectando ? (
				<>
					<RefreshCw className="h-3.5 w-3.5 animate-spin" aria-hidden />
					{estado === "conectando"
						? "Conectando con el servidor…"
						: "Se ha perdido la conexión. Reintentando…"}
				</>
			) : (
				<>
					<CloudOff className="h-3.5 w-3.5" aria-hidden />
					Sin conexión en tiempo real. Los mensajes nuevos no llegarán solos.
				</>
			)}
		</output>
	)
}
