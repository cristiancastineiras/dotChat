// ============================================================================
// LISTA DE CONVERSACIONES
// ============================================================================
// Columna izquierda: cabecera con el usuario, buscador, lista ordenada por
// actividad y las acciones para empezar algo nuevo.
// ============================================================================

import { Avatar, ContadorSinLeer, EstadoVacio, IconoSala } from "@paquetes/componentes"
import { useAlmacenSesion, useSalasOrdenadas } from "@paquetes/estados"
import { useRetardo } from "@paquetes/hooks"
import { TipoAdjunto, TipoSala } from "@paquetes/modelos"
import { cn, contiene, enUnaLinea, formatearRelativo, recortar } from "@paquetes/utiles"
import { Button, Dropdown, Input, Tooltip } from "antd"
import {
	Compass,
	FileText,
	Image as IconoImagen,
	LogOut,
	MessageSquarePlus,
	Mic,
	Plus,
	Search,
	Settings,
	UserPlus,
} from "lucide-react"
import { memo, useMemo, useState } from "react"
import { useNavigate } from "react-router-dom"

import { DialogoCrearSala } from "./DialogoCrearSala"
import { DialogoExplorarSalas } from "./DialogoExplorarSalas"
import { DialogoNuevaDirecta } from "./DialogoNuevaDirecta"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	salaActivaId: string | null
	alAbrirSala: (salaId: string) => void
	nombrePropio: string
}

export function ListaConversaciones({ salaActivaId, alAbrirSala, nombrePropio }: Propiedades) {
	const salas = useSalasOrdenadas()
	const perfil = useAlmacenSesion((estado) => estado.perfil)
	const sesion = useAlmacenSesion((estado) => estado.sesion)
	const salir = useAlmacenSesion((estado) => estado.salir)

	const [busqueda, setBusqueda] = useState("")
	const [dialogo, setDialogo] = useState<"crear" | "explorar" | "directa" | null>(null)

	const navegar = useNavigate()

	// El filtro se retrasa: en una lista larga, recalcularlo en cada pulsación se
	// nota al escribir.
	const busquedaAplicada = useRetardo(busqueda, 150)

	const filtradas = useMemo(() => {
		if (!busquedaAplicada.trim()) return salas
		return salas.filter((sala) => contiene(sala.nombre, busquedaAplicada))
	}, [salas, busquedaAplicada])

	return (
		<>
			{/* Cabecera con la cuenta */}
			<header className="h-cabecera shadow-cabecera bg-panel relative z-10 flex shrink-0 items-center gap-2 px-4">
				<Avatar
					usuarioId={sesion?.usuarioId ?? null}
					nombre={nombrePropio}
					avatarActualizado={perfil?.avatarActualizado ?? null}
					tieneAvatar={perfil?.tieneAvatar ?? false}
					tamano="sm"
				/>

				<div className="min-w-0 flex-1">
					<p className="text-tinta truncate text-sm font-medium">{nombrePropio}</p>
				</div>

				<Dropdown
					trigger={["click"]}
					placement="bottomRight"
					menu={{
						items: [
							{
								key: "perfil",
								icon: <Settings className="h-4 w-4" />,
								label: "Mi perfil",
								onClick: () => navegar("/perfil"),
							},
							{ type: "divider" },
							{
								key: "salir",
								icon: <LogOut className="h-4 w-4" />,
								label: "Cerrar sesión",
								danger: true,
								onClick: () => {
									salir()
									navegar("/acceso", { replace: true })
								},
							},
						],
					}}
				>
					<Button type="text" size="small" aria-label="Opciones de cuenta">
						<Settings className="text-tinta-suave h-4 w-4" />
					</Button>
				</Dropdown>
			</header>

			{/* Buscador y acciones */}
			<div className="bg-panel flex shrink-0 items-center gap-2 px-3 py-2.5">
				<Input
					value={busqueda}
					onChange={(evento) => setBusqueda(evento.target.value)}
					placeholder="Buscar conversación"
					prefix={<Search className="text-tinta-tenue h-3.5 w-3.5" />}
					allowClear
					className="!bg-lienzo !rounded-full !border-transparent"
				/>

				<Dropdown
					trigger={["click"]}
					placement="bottomRight"
					menu={{
						items: [
							{
								key: "directa",
								icon: <MessageSquarePlus className="h-4 w-4" />,
								label: "Conversación privada",
								onClick: () => setDialogo("directa"),
							},
							{
								key: "crear",
								icon: <UserPlus className="h-4 w-4" />,
								label: "Crear sala",
								onClick: () => setDialogo("crear"),
							},
							{
								key: "explorar",
								icon: <Compass className="h-4 w-4" />,
								label: "Explorar salas",
								onClick: () => setDialogo("explorar"),
							},
						],
					}}
				>
					<Tooltip title="Nueva conversación">
						<Button type="primary" size="small" aria-label="Nueva conversación">
							<Plus className="h-4 w-4" />
						</Button>
					</Tooltip>
				</Dropdown>
			</div>

			{/* Conversaciones */}
			<nav className="desplazable min-h-0 flex-1 overflow-y-auto p-2" aria-label="Conversaciones">
				{filtradas.length === 0 ? (
					busqueda ? (
						<EstadoVacio
							icono={Search}
							titulo="Sin resultados"
							descripcion={`Ninguna conversación coincide con «${busqueda}».`}
						/>
					) : (
						<EstadoVacio
							icono={MessageSquarePlus}
							titulo="Aún no tienes conversaciones"
							descripcion="Abre una conversación privada o únete a una sala para empezar."
							accion={{
								texto: "Explorar salas",
								alPulsar: () => setDialogo("explorar"),
							}}
						/>
					)
				) : (
					<ul className="space-y-0.5">
						{filtradas.map((sala) => (
							<FilaConversacion
								key={sala.id}
								sala={sala}
								activa={sala.id === salaActivaId}
								alPulsar={alAbrirSala}
							/>
						))}
					</ul>
				)}
			</nav>

			<DialogoCrearSala abierto={dialogo === "crear"} alCerrar={() => setDialogo(null)} />
			<DialogoExplorarSalas abierto={dialogo === "explorar"} alCerrar={() => setDialogo(null)} />
			<DialogoNuevaDirecta abierto={dialogo === "directa"} alCerrar={() => setDialogo(null)} />
		</>
	)
}

// ---------------------------------------------------------------------------
// Fila
// ---------------------------------------------------------------------------

interface PropiedadesFila {
	sala: Sala
	activa: boolean
	alPulsar: (salaId: string) => void
}

/**
 * Una conversación de la lista.
 *
 * Va memorizada porque la lista entera se vuelve a evaluar con cada mensaje que
 * llega a cualquier sala: sin esto, escribir en una conversación redibujaría las
 * cuarenta filas de la columna.
 */
const FilaConversacion = memo(function FilaConversacion({
	sala,
	activa,
	alPulsar,
}: PropiedadesFila) {
	// La presencia solo aplica a las conversaciones directas; en una sala de
	// varias personas se mira miembro a miembro, no en la lista.
	const enLinea = sala.esDirecta ? (sala.interlocutorEnLinea ?? false) : undefined

	return (
		<li className="relative">
			{activa && (
				<span
					className="bg-marca-600 absolute top-1/2 left-0 h-8 w-[3px] -translate-y-1/2 rounded-full"
					aria-hidden
				/>
			)}

			<button
				type="button"
				onClick={() => alPulsar(sala.id)}
				aria-current={activa ? "page" : undefined}
				className={cn(
					"flex w-full items-center gap-3 rounded-lg px-2.5 py-2.5 text-left transition-colors",
					activa ? "bg-marca-50" : "hover:bg-lienzo",
				)}
			>
				<Avatar
					nombre={sala.nombre}
					tamano="md"
					cuadrado={!sala.esDirecta}
					tieneAvatar={false}
					enLinea={enLinea}
				/>

				<div className="min-w-0 flex-1">
					<div className="flex items-baseline justify-between gap-2">
						<span
							className={cn(
								"flex min-w-0 items-center gap-1.5 truncate text-sm",
								sala.mensajesSinLeer > 0 ? "text-tinta font-semibold" : "text-tinta font-medium",
							)}
						>
							{!sala.esDirecta && (
								<IconoSala tipo={sala.tipo} className="text-tinta-tenue shrink-0" />
							)}
							<span className="truncate">{sala.nombre}</span>
						</span>

						<span className="text-tinta-tenue shrink-0 text-[11px]">
							{formatearRelativo(sala.fechaUltimaActividad)}
						</span>
					</div>

					<div className="mt-0.5 flex items-center justify-between gap-2">
						<span
							className={cn(
								"min-w-0 truncate text-xs",
								sala.mensajesSinLeer > 0 ? "text-tinta-suave" : "text-tinta-tenue",
							)}
						>
							<Previsualizacion sala={sala} />
						</span>

						<ContadorSinLeer cantidad={sala.mensajesSinLeer} />
					</div>
				</div>
			</button>
		</li>
	)
})

/** Línea de previsualización del último mensaje. */
function Previsualizacion({ sala }: { sala: Sala }) {
	const ultimo = sala.ultimoMensaje

	if (!ultimo) {
		return (
			<span className="italic">
				{sala.tipo === TipoSala.Directa
					? "Sin mensajes todavía"
					: `${sala.totalMiembros} ${sala.totalMiembros === 1 ? "miembro" : "miembros"}`}
			</span>
		)
	}

	// En una conversación directa el autor solo se marca cuando es propio: el
	// otro nombre ya es el título de la fila.
	const prefijo = ultimo.esPropio ? "Tú: " : sala.esDirecta ? "" : `${ultimo.nombreAutor}: `

	if (ultimo.nombreAdjunto && !ultimo.texto) {
		const EsImagen = ultimo.tipoAdjunto === TipoAdjunto.Imagen
		const EsAudio = ultimo.tipoAdjunto === TipoAdjunto.Audio
		const Icono = EsImagen ? IconoImagen : EsAudio ? Mic : FileText

		return (
			<span className="flex items-center gap-1">
				{prefijo}
				<Icono className="h-3 w-3 shrink-0" aria-hidden />
				{EsImagen ? "Imagen" : EsAudio ? "Nota de voz" : recortar(ultimo.nombreAdjunto, 24)}
			</span>
		)
	}

	return <>{prefijo + enUnaLinea(ultimo.texto)}</>
}
