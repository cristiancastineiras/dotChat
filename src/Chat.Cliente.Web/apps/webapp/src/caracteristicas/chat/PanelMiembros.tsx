// ============================================================================
// MIEMBROS DE LA SALA
// ============================================================================

import { Avatar, Cargando } from "@paquetes/componentes"
import { useAlmacenChat } from "@paquetes/estados"
import { useMiembros } from "@paquetes/hooks"
import { formatearCompleto } from "@paquetes/utiles"
import { Tag, Tooltip } from "antd"
import { useMemo } from "react"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	sala: Sala
}

/**
 * Tira horizontal con quién está en la sala.
 *
 * La presencia se cruza con la del almacén y no se usa solo la que trae la
 * consulta: esta última es del momento en que se pidió, y quien se conecta
 * después llegaría por el hub sin que la lista se enterara.
 */
export function PanelMiembros({ sala }: Propiedades) {
	const { data: miembros, isLoading } = useMiembros(sala.id)
	const presencia = useAlmacenChat((estado) => estado.presencia)

	const ordenados = useMemo(() => {
		if (!miembros) return []

		return miembros
			.map((miembro) => ({
				...miembro,
				enLinea: presencia[miembro.usuarioId]?.enLinea ?? miembro.enLinea,
			}))
			.toSorted((a, b) => {
				if (a.enLinea !== b.enLinea) return a.enLinea ? -1 : 1
				if (a.esCreador !== b.esCreador) return a.esCreador ? -1 : 1
				return a.nombreUsuario.localeCompare(b.nombreUsuario, "es")
			})
	}, [miembros, presencia])

	const conectados = ordenados.filter((miembro) => miembro.enLinea).length

	return (
		<section
			className="border-borde bg-panel-suave shrink-0 border-b px-4 py-2.5"
			aria-label="Miembros de la sala"
		>
			{isLoading ? (
				<Cargando className="!py-2" />
			) : (
				<>
					<p className="text-tinta-tenue mb-2 text-[11px] font-semibold tracking-wide uppercase">
						{ordenados.length} {ordenados.length === 1 ? "miembro" : "miembros"}
						{conectados > 0 && ` · ${conectados} en línea`}
					</p>

					<ul className="desplazable flex gap-3 overflow-x-auto pb-1">
						{ordenados.map((miembro) => (
							<li key={miembro.usuarioId} className="shrink-0">
								<Tooltip
									title={
										<span className="text-xs">
											{miembro.nombreUsuario}
											<br />
											En la sala desde {formatearCompleto(miembro.fechaUnion)}
										</span>
									}
								>
									<div className="flex w-16 flex-col items-center gap-1">
										<Avatar
											usuarioId={miembro.usuarioId}
											nombre={miembro.nombreUsuario}
											tamano="md"
											enLinea={miembro.enLinea}
										/>

										<span className="text-tinta-suave w-full truncate text-center text-[11px]">
											{miembro.nombreUsuario}
										</span>

										{miembro.esCreador && (
											<Tag className="!m-0 !px-1 !text-[9px] !leading-4">creador</Tag>
										)}
									</div>
								</Tooltip>
							</li>
						))}
					</ul>
				</>
			)}
		</section>
	)
}
