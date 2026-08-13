// ============================================================================
// PERFIL
// ============================================================================
// Datos de la cuenta, foto y preferencias de la interfaz.
// ============================================================================

import { configuracion, mensajeDeError, olvidarAvatar, usuarios } from "@paquetes/api"
import { Avatar, Cargando } from "@paquetes/componentes"
import { useAlmacenSesion, useAlmacenUi } from "@paquetes/estados"
import { useNotificaciones } from "@paquetes/hooks"
import { formatearCompleto } from "@paquetes/utiles"
import { App, Button, Divider, Switch, Tag } from "antd"
import { ArrowLeft, Camera, LogOut, Trash2 } from "lucide-react"
import { useState, type ReactNode } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"

import { DialogoEditarAvatar } from "../caracteristicas/perfil/DialogoEditarAvatar"

export function PaginaPerfil() {
	const sesion = useAlmacenSesion((estado) => estado.sesion)
	const perfil = useAlmacenSesion((estado) => estado.perfil)
	const fijarPerfil = useAlmacenSesion((estado) => estado.fijarPerfil)
	const salir = useAlmacenSesion((estado) => estado.salir)

	const [editorAbierto, setEditorAbierto] = useState(false)
	const [borrando, setBorrando] = useState(false)

	const { modal } = App.useApp()
	const navegar = useNavigate()

	if (!sesion) return null

	return (
		<div className="desplazable bg-lienzo h-full overflow-y-auto">
			{/* Cabecera */}
			<header className="h-cabecera border-borde bg-panel sticky top-0 z-10 flex items-center gap-3 border-b px-4">
				<Button type="text" size="small" onClick={() => navegar("/")} aria-label="Volver al chat">
					<ArrowLeft className="h-4 w-4" />
				</Button>
				<h1 className="text-tinta text-sm font-semibold">Mi perfil</h1>
			</header>

			<div className="mx-auto max-w-2xl px-4 py-6">
				{!perfil ? (
					<Cargando texto="Cargando perfil…" />
				) : (
					<>
						{/* Identidad */}
						<section className="border-borde bg-panel rounded-xl border p-6">
							<div className="flex flex-col items-center gap-4 sm:flex-row sm:items-start">
								<div className="relative">
									<Avatar
										usuarioId={perfil.id}
										nombre={perfil.nombreUsuario}
										avatarActualizado={perfil.avatarActualizado}
										tieneAvatar={perfil.tieneAvatar}
										tamano="xl"
									/>

									<button
										type="button"
										onClick={() => setEditorAbierto(true)}
										aria-label="Cambiar foto de perfil"
										className="border-panel bg-marca-600 hover:bg-marca-700 absolute right-0 bottom-0 flex h-8 w-8 items-center justify-center rounded-full border-2 text-white transition-colors"
									>
										<Camera className="h-4 w-4" aria-hidden />
									</button>
								</div>

								<div className="min-w-0 flex-1 text-center sm:text-left">
									<div className="flex items-center justify-center gap-2 sm:justify-start">
										<h2 className="text-tinta truncate text-lg font-semibold">
											{perfil.nombreUsuario}
										</h2>
										{perfil.esAdministrador && <Tag color="purple">administrador</Tag>}
									</div>

									<p className="text-tinta-suave mt-0.5 truncate text-sm">{perfil.email}</p>

									<div className="mt-4 flex flex-wrap justify-center gap-2 sm:justify-start">
										<Button size="small" onClick={() => setEditorAbierto(true)}>
											Cambiar foto
										</Button>

										{perfil.tieneAvatar && (
											<Button
												size="small"
												danger
												loading={borrando}
												onClick={() => {
													modal.confirm({
														title: "¿Quitar la foto de perfil?",
														content: "Volverás a mostrarte con tus iniciales.",
														okText: "Quitar",
														okButtonProps: { danger: true },
														cancelText: "Cancelar",
														onOk: async () => {
															setBorrando(true)

															try {
																const actualizado = await usuarios.eliminarAvatar()
																olvidarAvatar(perfil.id)
																fijarPerfil(actualizado)
																toast.success("Foto de perfil retirada.")
															} catch (error) {
																toast.error(mensajeDeError(error))
															} finally {
																setBorrando(false)
															}
														},
													})
												}}
											>
												<Trash2 className="h-3.5 w-3.5" />
												Quitar
											</Button>
										)}
									</div>
								</div>
							</div>

							<Divider className="!my-5" />

							<dl className="grid gap-3 text-sm sm:grid-cols-2">
								<Dato titulo="Cuenta creada">{formatearCompleto(perfil.fechaCreacion)}</Dato>
								<Dato titulo="Último acceso">
									{perfil.fechaUltimoAcceso
										? formatearCompleto(perfil.fechaUltimoAcceso)
										: "Es la primera vez"}
								</Dato>
							</dl>
						</section>

						<Preferencias />

						{/* Cierre de sesión */}
						<section className="border-borde bg-panel mt-4 rounded-xl border p-6">
							<div className="flex flex-wrap items-center justify-between gap-3">
								<div>
									<h3 className="text-tinta text-sm font-semibold">Cerrar sesión</h3>
									<p className="text-tinta-tenue mt-0.5 text-xs">
										Se olvidarán los tokens y las fotos descargadas en este navegador.
									</p>
								</div>

								<Button
									danger
									onClick={() => {
										salir()
										navegar("/acceso", { replace: true })
									}}
								>
									<LogOut className="h-4 w-4" />
									Salir
								</Button>
							</div>
						</section>

						<p className="text-tinta-tenue mt-6 text-center text-xs">
							{configuracion.nombreApp} {configuracion.version}
						</p>
					</>
				)}
			</div>

			<DialogoEditarAvatar abierto={editorAbierto} alCerrar={() => setEditorAbierto(false)} />
		</div>
	)
}

// ---------------------------------------------------------------------------
// Preferencias
// ---------------------------------------------------------------------------

function Preferencias() {
	const notificaciones = useAlmacenUi((estado) => estado.notificaciones)
	const sonido = useAlmacenUi((estado) => estado.sonido)
	const fijar = useAlmacenUi((estado) => estado.fijarPreferencia)

	const { admitidas, permiso, pedirPermiso } = useNotificaciones(notificaciones)

	return (
		<section className="border-borde bg-panel mt-4 rounded-xl border p-6">
			<h3 className="text-tinta mb-4 text-sm font-semibold">Preferencias</h3>

			<div className="space-y-4">
				<Opcion
					titulo="Avisos del sistema"
					descripcion={
						!admitidas
							? "Tu navegador no admite notificaciones."
							: permiso === "denied"
								? "Has bloqueado las notificaciones para este sitio; cámbialo desde los ajustes del navegador."
								: "Avisar cuando llegue un mensaje y no estés mirando la pestaña."
					}
				>
					<Switch
						checked={notificaciones && permiso === "granted"}
						disabled={!admitidas || permiso === "denied"}
						onChange={async (activo) => {
							// El permiso solo se puede pedir a raíz de un gesto del usuario;
							// este interruptor lo es.
							if (activo && permiso !== "granted") {
								if (!(await pedirPermiso())) {
									toast.error("No se ha concedido el permiso de notificaciones.")
									return
								}
							}

							fijar("notificaciones", activo)
						}}
					/>
				</Opcion>

				<Opcion titulo="Sonido" descripcion="Acompañar los mensajes nuevos con un aviso corto.">
					<Switch checked={sonido} onChange={(activo) => fijar("sonido", activo)} />
				</Opcion>
			</div>
		</section>
	)
}

/** Fila de preferencia con su interruptor. */
function Opcion({
	titulo,
	descripcion,
	children,
}: {
	titulo: string
	descripcion: string
	children: ReactNode
}) {
	return (
		<div className="flex items-start justify-between gap-4">
			<div className="min-w-0">
				<p className="text-tinta text-sm font-medium">{titulo}</p>
				<p className="text-tinta-tenue mt-0.5 text-xs">{descripcion}</p>
			</div>
			<div className="shrink-0 pt-0.5">{children}</div>
		</div>
	)
}

/** Par de etiqueta y valor de la ficha de datos. */
function Dato({ titulo, children }: { titulo: string; children: ReactNode }) {
	return (
		<div>
			<dt className="text-tinta-tenue text-xs">{titulo}</dt>
			<dd className="text-tinta mt-0.5 text-sm">{children}</dd>
		</div>
	)
}
