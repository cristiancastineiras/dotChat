// ============================================================================
// SELECTOR DE EMOJIS
// ============================================================================
// El mismo catálogo que la consola, agrupado por categorías y con buscador.
//
// No se usa ninguna librería de emojis: las habituales arrastran varios cientos
// de kilobytes con el catálogo Unicode completo y sus imágenes. Aquí basta con
// el centenar de entradas que el servidor sabe expandir, que es lo que mantiene
// el cliente web y la consola diciendo lo mismo.
// ============================================================================

import { CATEGORIAS_EMOJI, cn, emojisPorCategoria, sugerirEmojis } from "@paquetes/utiles"
import { Input, Popover } from "antd"
import { Search } from "lucide-react"
import { useMemo, useState, type ReactNode } from "react"

interface Propiedades {
	abierto: boolean
	alCambiarApertura: (abierto: boolean) => void
	alElegir: (simbolo: string) => void
	children: ReactNode
}

export function SelectorEmojis({ abierto, alCambiarApertura, alElegir, children }: Propiedades) {
	const [busqueda, setBusqueda] = useState("")

	const grupos = useMemo(() => emojisPorCategoria(), [])

	// Con búsqueda se enseña una sola lista de resultados; sin ella, todas las
	// categorías en su orden.
	const resultados = useMemo(
		() => (busqueda.trim() ? sugerirEmojis(busqueda.trim(), 40) : null),
		[busqueda],
	)

	return (
		<Popover
			open={abierto}
			onOpenChange={(siguiente) => {
				alCambiarApertura(siguiente)
				if (!siguiente) setBusqueda("")
			}}
			trigger="click"
			placement="topRight"
			arrow={false}
			content={
				<div className="w-72">
					<Input
						value={busqueda}
						onChange={(evento) => setBusqueda(evento.target.value)}
						placeholder="Buscar emoji"
						prefix={<Search className="text-tinta-tenue h-3.5 w-3.5" />}
						size="small"
						allowClear
						autoFocus
					/>

					<div className="desplazable mt-2 max-h-64 overflow-y-auto">
						{resultados ? (
							resultados.length === 0 ? (
								<p className="text-tinta-tenue py-6 text-center text-xs">
									Ningún emoji coincide con «{busqueda}».
								</p>
							) : (
								<Rejilla
									emojis={resultados.map((emoji) => [emoji.simbolo, emoji.nombre])}
									alElegir={alElegir}
								/>
							)
						) : (
							CATEGORIAS_EMOJI.map((categoria) => {
								const entradas = grupos.get(categoria) ?? []
								if (entradas.length === 0) return null

								return (
									<section key={categoria} className="mb-2">
										<h3 className="text-tinta-tenue mb-1 px-1 text-[11px] font-semibold">
											{categoria}
										</h3>
										<Rejilla
											emojis={entradas.map((emoji) => [emoji.simbolo, emoji.nombre])}
											alElegir={alElegir}
										/>
									</section>
								)
							})
						)}
					</div>
				</div>
			}
		>
			{children}
		</Popover>
	)
}

/** Cuadrícula de emojis pulsables. */
function Rejilla({
	emojis,
	alElegir,
}: {
	emojis: [simbolo: string, nombre: string][]
	alElegir: (simbolo: string) => void
}) {
	return (
		<div className="grid grid-cols-8 gap-0.5">
			{emojis.map(([simbolo, nombre]) => (
				<button
					key={nombre}
					type="button"
					onClick={() => alElegir(simbolo)}
					title={`:${nombre}:`}
					aria-label={nombre}
					className={cn(
						"flex h-8 w-8 items-center justify-center rounded-md text-lg",
						"hover:bg-marca-50 transition-colors",
					)}
				>
					{simbolo}
				</button>
			))}
		</div>
	)
}
