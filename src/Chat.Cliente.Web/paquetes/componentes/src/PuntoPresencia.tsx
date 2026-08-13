import { cn } from "@paquetes/utiles"

import { paleta } from "./tema"

interface PropiedadesPunto {
	enLinea: boolean
	className?: string
}

/**
 * Punto de estado de conexión.
 *
 * El color por sí solo no informa a quien no lo distingue, ni a un lector de
 * pantalla. Por eso el punto se marca como decorativo y el estado se anuncia con
 * un texto que solo existe para la accesibilidad: así la información llega por
 * dos vías y no se apoya únicamente en el verde.
 */
export function PuntoPresencia({ enLinea, className }: PropiedadesPunto) {
	const etiqueta = enLinea ? "En línea" : "Desconectado"

	return (
		<>
			<span
				className={cn("block h-2.5 w-2.5 rounded-full", className)}
				style={{ backgroundColor: enLinea ? paleta.conectado : paleta.bordeFuerte }}
				title={etiqueta}
				aria-hidden
			/>
			<span className="sr-only">{etiqueta}</span>
		</>
	)
}
