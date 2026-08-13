// ============================================================================
// INVITAR A UNA SALA
// ============================================================================

import { Avatar, Cargando } from "@paquetes/componentes"
import { useAccionesSala, useMiembros, useRetardo, useUsuarios } from "@paquetes/hooks"
import { contiene } from "@paquetes/utiles"
import { Button, Input, Modal } from "antd"
import { Search } from "lucide-react"
import { useMemo, useState } from "react"
import { toast } from "sonner"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	sala: Sala
	abierto: boolean
	alCerrar: () => void
}

export function DialogoInvitar({ sala, abierto, alCerrar }: Propiedades) {
	const [busqueda, setBusqueda] = useState("")
	const busquedaAplicada = useRetardo(busqueda, 150)

	const { data: usuarios, isLoading } = useUsuarios()
	const { data: miembros } = useMiembros(sala.id, abierto)

	const { invitar, trabajando } = useAccionesSala({
		exito: (mensaje) => toast.success(mensaje),
		error: (mensaje) => toast.error(mensaje),
	})

	const candidatos = useMemo(() => {
		if (!usuarios) return []

		// Quien ya está dentro no se puede invitar; el servidor lo rechazaría con un
		// conflicto y es mejor no ofrecerlo.
		const dentro = new Set(miembros?.map((miembro) => miembro.usuarioId) ?? [])

		const fuera = usuarios.filter((usuario) => usuario.activo && !dentro.has(usuario.id))

		return busquedaAplicada.trim()
			? fuera.filter((usuario) => contiene(usuario.nombreUsuario, busquedaAplicada))
			: fuera
	}, [usuarios, miembros, busquedaAplicada])

	return (
		<Modal
			open={abierto}
			onCancel={alCerrar}
			title={`Invitar a «${sala.nombre}»`}
			footer={null}
			width={440}
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

			<div className="desplazable max-h-80 overflow-y-auto">
				{isLoading && <Cargando />}

				{!isLoading && candidatos.length === 0 && (
					<p className="text-tinta-tenue py-8 text-center text-xs">
						{busqueda ? `Nadie coincide con «${busqueda}».` : "Ya están todos dentro."}
					</p>
				)}

				<ul className="space-y-0.5">
					{candidatos.map((usuario) => (
						<li key={usuario.id} className="hover:bg-lienzo flex items-center gap-3 rounded-lg p-2">
							<Avatar
								usuarioId={usuario.id}
								nombre={usuario.nombreUsuario}
								avatarActualizado={usuario.avatarActualizado}
								tieneAvatar={usuario.tieneAvatar}
								tamano="sm"
								enLinea={usuario.enLinea}
							/>

							<span className="text-tinta min-w-0 flex-1 truncate text-sm">
								{usuario.nombreUsuario}
							</span>

							<Button
								size="small"
								loading={trabajando}
								onClick={() => void invitar(sala.id, usuario.nombreUsuario)}
							>
								Invitar
							</Button>
						</li>
					))}
				</ul>
			</div>
		</Modal>
	)
}
