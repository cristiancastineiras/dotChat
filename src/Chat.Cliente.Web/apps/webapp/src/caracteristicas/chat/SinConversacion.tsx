import { configuracion } from "@paquetes/api"
import { MessagesSquare } from "lucide-react"

/**
 * Panel derecho cuando no hay ninguna conversación abierta.
 *
 * Solo se ve en escritorio: en una pantalla estrecha las dos columnas no
 * conviven, y sin conversación abierta lo que se enseña es la lista.
 */
export function SinConversacion() {
	return (
		<div className="bg-lienzo flex h-full flex-col items-center justify-center gap-4 px-6 text-center">
			<div className="border-borde bg-panel flex h-16 w-16 items-center justify-center rounded-2xl border">
				<MessagesSquare className="text-tinta-tenue h-7 w-7" aria-hidden />
			</div>

			<div>
				<h2 className="text-tinta text-base font-semibold">{configuracion.nombreApp}</h2>
				<p className="text-tinta-tenue mt-1 max-w-xs text-sm">
					Elige una conversación de la izquierda, o empieza una nueva con el botón de más.
				</p>
			</div>

			<p className="text-tinta-tenue mt-2 max-w-xs text-xs">
				Todo lo que escribas se cifra antes de guardarse. Ni el servidor puede leerlo.
			</p>
		</div>
	)
}
