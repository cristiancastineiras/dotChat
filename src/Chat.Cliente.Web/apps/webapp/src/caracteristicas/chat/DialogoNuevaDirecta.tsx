// ============================================================================
// CONVERSACIÓN PRIVADA NUEVA
// ============================================================================

import { Avatar, Cargando, EstadoVacio } from "@paquetes/componentes"
import { useAlmacenSesion } from "@paquetes/estados"
import { useAccionesSala, useRetardo, useUsuarios } from "@paquetes/hooks"
import { contiene, formatearUltimaVez } from "@paquetes/utiles"
import { Input, Modal } from "antd"
import { Search, Users } from "lucide-react"
import { useMemo, useState } from "react"
import { toast } from "sonner"

interface Propiedades {
	abierto: boolean
	alCerrar: () => void
}

export function DialogoNuevaDirecta({ abierto, alCerrar }: Propiedades) {
	const [busqueda, setBusqueda] = useState("")
	const busquedaAplicada = useRetardo(busqueda, 150)

	const usuarioPropioId = useAlmacenSesion((estado) => estado.sesion?.usuarioId)
	const { data: usuarios, isLoading, isError, refetch } = useUsuarios()

	const { abrirDirecta, trabajando } = useAccionesSala({
		error: (mensaje) => toast.error(mensaje),
	})

	const visibles = useMemo(() => {
		if (!usuarios) return []

		const candidatos = usuarios.filter(
			(usuario) => usuario.id !== usuarioPropioId && usuario.activo,
		)

		const filtrados = busquedaAplicada.trim()
			? candidatos.filter((usuario) => contiene(usuario.nombreUsuario, busquedaAplicada))
			: candidatos

		// Primero quien está conectado: es con quien tiene sentido escribir ahora.
		return filtrados.toSorted((a, b) => {
			if (a.enLinea !== b.enLinea) return a.enLinea ? -1 : 1
			return a.nombreUsuario.localeCompare(b.nombreUsuario, "es")
		})
	}, [usuarios, usuarioPropioId, busquedaAplicada])

	async function abrir(nombreUsuario: string) {
		if (await abrirDirecta(nombreUsuario)) {
			alCerrar()
			setBusqueda("")
		}
	}

	return (
		<Modal
			open={abierto}
			onCancel={alCerrar}
			title="Nueva conversación privada"
			footer={null}
			width={460}
			destroyOnHidden
		>
			<Input
				value={busqueda}
				onChange={(evento) => setBusqueda(evento.target.value)}
				placeholder="Buscar persona"
				prefix={<Search className="text-tinta-tenue h-3.5 w-3.5" />}
				allowClear
				autoFocus
				className="!mb-3"
			/>

			<div className="desplazable max-h-96 overflow-y-auto">
				{isLoading && <Cargando texto="Cargando usuarios…" />}

				{isError && (
					<EstadoVacio
						titulo="No se ha podido cargar la lista"
						descripcion="Comprueba tu conexión con el servidor."
						accion={{ texto: "Reintentar", alPulsar: () => void refetch() }}
					/>
				)}

				{!isLoading && !isError && visibles.length === 0 && (
					<EstadoVacio
						icono={Users}
						titulo={busqueda ? "Sin resultados" : "No hay nadie más"}
						descripcion={
							busqueda
								? `Nadie coincide con «${busqueda}».`
								: "Todavía no hay otras cuentas en esta plataforma."
						}
					/>
				)}

				<ul className="space-y-0.5">
					{visibles.map((usuario) => (
						<li key={usuario.id}>
							<button
								type="button"
								disabled={trabajando}
								onClick={() => void abrir(usuario.nombreUsuario)}
								className="hover:bg-lienzo flex w-full items-center gap-3 rounded-lg p-2 text-left disabled:opacity-50"
							>
								<Avatar
									usuarioId={usuario.id}
									nombre={usuario.nombreUsuario}
									avatarActualizado={usuario.avatarActualizado}
									tieneAvatar={usuario.tieneAvatar}
									tamano="md"
									enLinea={usuario.enLinea}
								/>

								<div className="min-w-0 flex-1">
									<p className="text-tinta truncate text-sm font-medium">{usuario.nombreUsuario}</p>
									<p className="text-tinta-tenue truncate text-xs">
										{usuario.enLinea
											? "En línea"
											: `Visto ${formatearUltimaVez(usuario.fechaUltimoAcceso)}`}
									</p>
								</div>
							</button>
						</li>
					))}
				</ul>
			</div>
		</Modal>
	)
}
