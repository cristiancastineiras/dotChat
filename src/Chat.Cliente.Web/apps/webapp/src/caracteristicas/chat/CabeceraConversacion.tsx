// ============================================================================
// CABECERA DE LA CONVERSACIÓN
// ============================================================================

import { Avatar, IconoSala } from "@paquetes/componentes"
import { useAlmacenChat, useAlmacenUi } from "@paquetes/estados"
import { useAccionesSala } from "@paquetes/hooks"
import { TipoSala } from "@paquetes/modelos"
import { formatearUltimaVez } from "@paquetes/utiles"
import { App, Button, Dropdown, Tooltip } from "antd"
import { ArrowLeft, LogOut, MoreVertical, UserPlus, Users } from "lucide-react"
import { useState } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"

import { DialogoInvitar } from "./DialogoInvitar"
import { PanelMiembros } from "./PanelMiembros"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	sala: Sala
	alVolver?: (() => void) | undefined
}

export function CabeceraConversacion({ sala, alVolver }: Propiedades) {
	const panelVisible = useAlmacenUi((estado) => estado.panelMiembrosVisible)
	const alternarPanel = useAlmacenUi((estado) => estado.alternarPanelMiembros)

	// La presencia del interlocutor llega por el hub y actualiza el almacén; la
	// que trae la sala es solo la del momento en que se cargó la lista.
	const presencia = useAlmacenChat((estado) => {
		if (!sala.esDirecta) return null

		return (
			Object.values(estado.presencia).find((quien) => quien.nombreUsuario === sala.nombre) ?? null
		)
	})

	const [invitarAbierto, setInvitarAbierto] = useState(false)
	const { salir } = useAccionesSala({
		exito: (mensaje) => toast.success(mensaje),
		error: (mensaje) => toast.error(mensaje),
	})

	const { modal } = App.useApp()
	const navegar = useNavigate()

	const enLinea = presencia?.enLinea ?? sala.interlocutorEnLinea ?? false

	/** Línea de estado bajo el nombre. */
	const subtitulo = sala.esDirecta
		? enLinea
			? "En línea"
			: `Visto ${formatearUltimaVez(presencia?.ultimaVez ?? null)}`
		: `${sala.totalMiembros} ${sala.totalMiembros === 1 ? "miembro" : "miembros"}${
				sala.descripcion ? ` · ${sala.descripcion}` : ""
			}`

	function confirmarSalida() {
		modal.confirm({
			title: `¿Salir de «${sala.nombre}»?`,
			content: sala.esDirecta
				? "Dejarás de ver esta conversación en tu lista."
				: "Dejarás de recibir sus mensajes. Podrás volver a entrar si la sala es pública.",
			okText: "Salir",
			okButtonProps: { danger: true },
			cancelText: "Cancelar",
			onOk: async () => {
				if (await salir(sala.id)) {
					navegar("/")
				}
			},
		})
	}

	return (
		<>
			<header className="h-cabecera border-borde flex shrink-0 items-center gap-3 border-b px-3">
				{alVolver && (
					<Button type="text" size="small" onClick={alVolver} aria-label="Volver">
						<ArrowLeft className="h-4 w-4" />
					</Button>
				)}

				<Avatar
					nombre={sala.nombre}
					tamano="sm"
					cuadrado={!sala.esDirecta}
					tieneAvatar={false}
					{...(sala.esDirecta ? { enLinea } : {})}
				/>

				<div className="min-w-0 flex-1">
					<h2 className="text-tinta flex items-center gap-1.5 truncate text-sm font-semibold">
						{!sala.esDirecta && (
							<IconoSala tipo={sala.tipo} className="text-tinta-tenue shrink-0" />
						)}
						{sala.nombre}
					</h2>
					<p className="text-tinta-tenue truncate text-xs">{subtitulo}</p>
				</div>

				{!sala.esDirecta && (
					<Tooltip title="Miembros">
						<Button
							type={panelVisible ? "default" : "text"}
							size="small"
							onClick={() => alternarPanel()}
							aria-label="Ver miembros"
							aria-pressed={panelVisible}
						>
							<Users className="h-4 w-4" />
						</Button>
					</Tooltip>
				)}

				<Dropdown
					trigger={["click"]}
					placement="bottomRight"
					menu={{
						items: [
							...(sala.tipo !== TipoSala.Directa
								? [
										{
											key: "invitar",
											icon: <UserPlus className="h-4 w-4" />,
											label: "Invitar a alguien",
											onClick: () => setInvitarAbierto(true),
										},
									]
								: []),
							{
								key: "salir",
								icon: <LogOut className="h-4 w-4" />,
								label: sala.esDirecta ? "Cerrar conversación" : "Salir de la sala",
								danger: true,
								onClick: confirmarSalida,
							},
						],
					}}
				>
					<Button type="text" size="small" aria-label="Más opciones">
						<MoreVertical className="h-4 w-4" />
					</Button>
				</Dropdown>
			</header>

			{panelVisible && !sala.esDirecta && <PanelMiembros sala={sala} />}

			<DialogoInvitar
				sala={sala}
				abierto={invitarAbierto}
				alCerrar={() => setInvitarAbierto(false)}
			/>
		</>
	)
}
