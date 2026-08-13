// ============================================================================
// INDICADORES
// ============================================================================
// Piezas pequeñas y repetidas: contador de pendientes, etiqueta de tipo de sala,
// separador de fecha y el aviso de que alguien escribe.
// ============================================================================

import { TipoSala } from "@paquetes/modelos"
import { cn } from "@paquetes/utiles"
import { Hash, Lock, User } from "lucide-react"

// ---------------------------------------------------------------------------
// Contador de mensajes sin leer
// ---------------------------------------------------------------------------

interface PropiedadesContador {
	cantidad: number
	className?: string
}

/**
 * Burbuja con los mensajes pendientes de una conversación.
 *
 * A partir de cien se muestra «99+»: el número exacto deja de importar y una
 * burbuja de cuatro cifras descuadra la fila.
 */
export function ContadorSinLeer({ cantidad, className }: PropiedadesContador) {
	if (cantidad <= 0) return null

	return (
		<span
			className={cn(
				"inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5",
				"bg-marca-600 text-[11px] font-semibold text-white tabular-nums",
				className,
			)}
			aria-label={`${cantidad} mensajes sin leer`}
		>
			{cantidad > 99 ? "99+" : cantidad}
		</span>
	)
}

// ---------------------------------------------------------------------------
// Tipo de sala
// ---------------------------------------------------------------------------

interface PropiedadesIconoSala {
	tipo: TipoSala
	className?: string
}

/**
 * Icono que identifica la naturaleza de una conversación.
 *
 * La almohadilla para las públicas es la convención de cualquier chat de
 * equipos; el candado y la persona se explican solos.
 */
export function IconoSala({ tipo, className }: PropiedadesIconoSala) {
	const comun = cn("h-3.5 w-3.5", className)

	switch (tipo) {
		case TipoSala.Privada:
			return <Lock className={comun} aria-label="Sala privada" />
		case TipoSala.Directa:
			return <User className={comun} aria-label="Conversación directa" />
		default:
			return <Hash className={comun} aria-label="Sala pública" />
	}
}

// ---------------------------------------------------------------------------
// Separador de fecha
// ---------------------------------------------------------------------------

interface PropiedadesSeparador {
	texto: string
}

/** Marca el cambio de día dentro del historial. */
export function SeparadorFecha({ texto }: PropiedadesSeparador) {
	// Las dos líneas son decoración; lo que informa es la fecha, que se lee como
	// texto normal dentro del historial.
	return (
		<div className="my-4 flex items-center gap-3">
			<span className="bg-borde h-px flex-1" aria-hidden />
			<span className="text-tinta-tenue text-xs font-medium">{texto}</span>
			<span className="bg-borde h-px flex-1" aria-hidden />
		</div>
	)
}

// ---------------------------------------------------------------------------
// Está escribiendo
// ---------------------------------------------------------------------------

interface PropiedadesEscribiendo {
	/** Nombres de quienes escriben en este momento. */
	nombres: readonly string[]
}

/**
 * Aviso de escritura.
 *
 * Ocupa una altura fija aunque no haya nadie escribiendo: si apareciera y
 * desapareciera, empujaría la conversación arriba y abajo constantemente.
 */
export function AvisoEscribiendo({ nombres }: PropiedadesEscribiendo) {
	if (nombres.length === 0) {
		return <div className="h-5" aria-hidden />
	}

	const texto =
		nombres.length === 1
			? `${nombres[0]} está escribiendo`
			: nombres.length === 2
				? `${nombres[0]} y ${nombres[1]} están escribiendo`
				: `${nombres.length} personas están escribiendo`

	return (
		<div className="text-tinta-tenue flex h-5 items-center gap-2 text-xs" aria-live="polite">
			<span className="flex gap-0.5" aria-hidden>
				{[0, 1, 2].map((indice) => (
					<span
						key={indice}
						className="punto-escribiendo bg-tinta-tenue block h-1 w-1 rounded-full"
						style={{ animationDelay: `${indice * 0.15}s` }}
					/>
				))}
			</span>
			{texto}
		</div>
	)
}
