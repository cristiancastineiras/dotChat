// ============================================================================
// EXPLORAR SALAS
// ============================================================================

import { Avatar, Cargando, EstadoVacio, IconoSala } from "@paquetes/componentes"
import { useAccionesSala, useCatalogoSalas, useRetardo } from "@paquetes/hooks"
import { TipoSala } from "@paquetes/modelos"
import { contiene, formatearRelativo } from "@paquetes/utiles"
import { Button, Input, Modal, Tag } from "antd"
import { Compass, Search } from "lucide-react"
import { useMemo, useState } from "react"
import { toast } from "sonner"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	abierto: boolean
	alCerrar: () => void
}

export function DialogoExplorarSalas({ abierto, alCerrar }: Propiedades) {
	const [busqueda, setBusqueda] = useState("")
	const busquedaAplicada = useRetardo(busqueda, 150)

	// Solo se pide el catálogo con el diálogo abierto: es una lista que puede
	// crecer y no hace falta tenerla cargada mientras se conversa.
	const { data: salas, isLoading, isError, refetch } = useCatalogoSalas(abierto)

	const { unirse, trabajando } = useAccionesSala({
		error: (mensaje) => toast.error(mensaje),
	})

	const visibles = useMemo(() => {
		if (!salas) return []

		// Las conversaciones directas no son un sitio al que unirse, y las salas de
		// las que ya se es miembro están en la lista lateral.
		const candidatas = salas.filter((sala) => sala.tipo !== TipoSala.Directa && !sala.esMiembro)

		if (!busquedaAplicada.trim()) return candidatas

		return candidatas.filter(
			(sala) =>
				contiene(sala.nombre, busquedaAplicada) ||
				contiene(sala.descripcion ?? "", busquedaAplicada),
		)
	}, [salas, busquedaAplicada])

	async function entrar(sala: Sala) {
		if (await unirse(sala.id)) {
			alCerrar()
		}
	}

	return (
		<Modal
			open={abierto}
			onCancel={alCerrar}
			title="Explorar salas"
			footer={null}
			width={520}
			destroyOnHidden
		>
			<Input
				value={busqueda}
				onChange={(evento) => setBusqueda(evento.target.value)}
				placeholder="Buscar por nombre o descripción"
				prefix={<Search className="text-tinta-tenue h-3.5 w-3.5" />}
				allowClear
				autoFocus
				className="!mb-3"
			/>

			<div className="desplazable max-h-96 overflow-y-auto">
				{isLoading && <Cargando texto="Cargando salas…" />}

				{isError && (
					<EstadoVacio
						titulo="No se ha podido cargar el catálogo"
						descripcion="Comprueba tu conexión con el servidor."
						accion={{ texto: "Reintentar", alPulsar: () => void refetch() }}
					/>
				)}

				{!isLoading && !isError && visibles.length === 0 && (
					<EstadoVacio
						icono={Compass}
						titulo={busqueda ? "Sin resultados" : "No hay salas a las que unirse"}
						descripcion={
							busqueda
								? `Ninguna sala coincide con «${busqueda}».`
								: "Ya estás en todas las salas disponibles. Crea una nueva si te hace falta."
						}
					/>
				)}

				<ul className="space-y-1">
					{visibles.map((sala) => (
						<li key={sala.id} className="hover:bg-lienzo flex items-center gap-3 rounded-lg p-2">
							<Avatar nombre={sala.nombre} tamano="md" cuadrado tieneAvatar={false} />

							<div className="min-w-0 flex-1">
								<p className="text-tinta flex items-center gap-1.5 truncate text-sm font-medium">
									<IconoSala tipo={sala.tipo} className="text-tinta-tenue shrink-0" />
									{sala.nombre}
									{sala.tipo === TipoSala.Privada && (
										<Tag color="default" className="!ml-1 !text-[10px]">
											privada
										</Tag>
									)}
								</p>

								<p className="text-tinta-tenue truncate text-xs">
									{sala.descripcion ||
										`${sala.totalMiembros} ${sala.totalMiembros === 1 ? "miembro" : "miembros"}`}
									{sala.fechaUltimaActividad &&
										` · activa ${formatearRelativo(sala.fechaUltimaActividad).toLowerCase()}`}
								</p>
							</div>

							<Button
								size="small"
								type="primary"
								loading={trabajando}
								onClick={() => void entrar(sala)}
								// Una sala privada solo admite entrada por invitación: el
								// servidor lo rechazaría, así que no se ofrece.
								disabled={sala.tipo === TipoSala.Privada}
								title={sala.tipo === TipoSala.Privada ? "Solo se entra por invitación" : undefined}
							>
								Unirse
							</Button>
						</li>
					))}
				</ul>
			</div>
		</Modal>
	)
}
