// ============================================================================
// AVATAR
// ============================================================================
// Foto de perfil, o iniciales sobre un color estable cuando no la hay.
//
// No se usa el Avatar de antd: aquí hace falta resolver la foto desde la caché
// de blobs —no se puede enlazar directamente al endpoint, que exige cabecera de
// autorización— y componer encima el punto de presencia. Envolver el de antd
// para eso acaba siendo más código que escribirlo.
// ============================================================================

import { useAvatar } from "@paquetes/hooks"
import { cn, colorDesde, iniciales } from "@paquetes/utiles"

import { PuntoPresencia } from "./PuntoPresencia"

import type { Guid } from "@paquetes/modelos"

/** Tamaños disponibles. */
export type TamanoAvatar = "xs" | "sm" | "md" | "lg" | "xl"

/** Clases de cada tamaño: caja y tipografía de las iniciales. */
const TAMANOS: Record<TamanoAvatar, string> = {
	xs: "h-6 w-6 text-[10px]",
	sm: "h-8 w-8 text-xs",
	md: "h-10 w-10 text-sm",
	lg: "h-12 w-12 text-base",
	xl: "h-24 w-24 text-2xl",
}

interface PropiedadesAvatar {
	/** Usuario del que se muestra la foto. Sin él solo se pintan las iniciales. */
	usuarioId?: Guid | null
	/** Nombre del que salen las iniciales y el color. */
	nombre: string
	/** Marca de versión de la foto. */
	avatarActualizado?: string | null
	/** Si se sabe que no hay foto, se evita la petición. */
	tieneAvatar?: boolean
	tamano?: TamanoAvatar
	/** Muestra el punto de presencia en la esquina. */
	enLinea?: boolean | undefined
	/** Cuadrado con esquinas redondeadas, para las salas. */
	cuadrado?: boolean
	className?: string
}

/**
 * Avatar de una persona o de una sala.
 *
 * El color de fondo se deriva del identificador y no del nombre: así, cambiar de
 * nombre no cambia el color con el que los demás ya reconocen a alguien.
 */
export function Avatar({
	usuarioId,
	nombre,
	avatarActualizado,
	tieneAvatar = true,
	tamano = "md",
	enLinea,
	cuadrado = false,
	className,
}: PropiedadesAvatar) {
	const foto = useAvatar(usuarioId, avatarActualizado, tieneAvatar && Boolean(usuarioId))

	const color = colorDesde(usuarioId ?? nombre)
	const letras = iniciales(nombre)

	return (
		<div className={cn("relative shrink-0", className)}>
			<div
				className={cn(
					"flex items-center justify-center overflow-hidden font-semibold text-white select-none",
					TAMANOS[tamano],
					cuadrado ? "rounded-lg" : "rounded-full",
				)}
				style={foto ? undefined : { backgroundColor: color }}
				// El nombre completo queda accesible aunque solo se vean dos letras.
				title={nombre}
			>
				{foto ? (
					<img src={foto} alt={nombre} className="h-full w-full object-cover" draggable={false} />
				) : (
					<span aria-hidden>{letras}</span>
				)}
			</div>

			{enLinea !== undefined && (
				<PuntoPresencia enLinea={enLinea} className="absolute right-0 bottom-0 ring-2 ring-white" />
			)}
		</div>
	)
}
