// ============================================================================
// PANTALLA PRINCIPAL
// ============================================================================
// Reparte la ventana en dos columnas —conversaciones y conversación abierta— y
// es quien monta la conexión con el hub. Todo lo demás cuelga de aquí.
// ============================================================================

import { BarraConexion } from "@paquetes/componentes"
import {
	useAlmacenChat,
	useAlmacenSesion,
	useAlmacenUi,
	useEstadoConexion,
	useSalaActiva,
} from "@paquetes/estados"
import {
	useAccionesSala,
	useConexionChat,
	useEsEscritorio,
	useNotificaciones,
} from "@paquetes/hooks"
import { cn } from "@paquetes/utiles"
import { useCallback, useEffect } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { toast } from "sonner"

import { Conversacion } from "../caracteristicas/chat/Conversacion"
import { ListaConversaciones } from "../caracteristicas/chat/ListaConversaciones"
import { SinConversacion } from "../caracteristicas/chat/SinConversacion"

export function PaginaChat() {
	const { salaId } = useParams<{ salaId: string }>()
	const navegar = useNavigate()

	const esEscritorio = useEsEscritorio()
	const estadoConexion = useEstadoConexion()
	const salaActiva = useSalaActiva()

	const nombrePropio = useAlmacenSesion((estado) => estado.sesion?.nombreUsuario ?? "")
	const notificacionesActivas = useAlmacenUi((estado) => estado.notificaciones)
	const barraLateralVisible = useAlmacenUi((estado) => estado.barraLateralVisible)

	const { notificar } = useNotificaciones(notificacionesActivas)
	const { marcarLeida } = useAccionesSala()

	// Los avisos que llegan del hub. Se memorizan para que la conexión no se
	// reinicie en cada render de esta pantalla.
	const alRecibirError = useCallback((mensaje: string) => {
		toast.error(mensaje)
	}, [])

	const alLlegarMensaje = useCallback(
		(_salaIdMensaje: string, nombreUsuario: string, texto: string) => {
			notificar(nombreUsuario, texto || "Te ha enviado un archivo")
		},
		[notificar],
	)

	useConexionChat({ error: alRecibirError, mensajeNuevo: alLlegarMensaje })

	// La sala abierta la manda la URL: así un enlace a una conversación funciona,
	// y el botón de retroceso del navegador hace lo que se espera.
	useEffect(() => {
		const almacen = useAlmacenChat.getState()

		if (salaId && almacen.salas[salaId]) {
			almacen.seleccionarSala(salaId)
			void marcarLeida(salaId)
		} else if (!salaId) {
			almacen.seleccionarSala(null)
		}
	}, [salaId, marcarLeida])

	// Una sala que llega por el hub después de que la URL ya la pedía —al abrir
	// un enlace directo antes de que cargue la lista— hay que seleccionarla en
	// cuanto aparece.
	const salaConocida = useAlmacenChat((estado) => (salaId ? Boolean(estado.salas[salaId]) : false))

	useEffect(() => {
		if (salaId && salaConocida) {
			useAlmacenChat.getState().seleccionarSala(salaId)
		}
	}, [salaId, salaConocida])

	const abrirSala = useCallback(
		(id: string) => {
			navegar(`/sala/${id}`)

			// En una pantalla estrecha las dos columnas no caben: al abrir una
			// conversación, la lista se retira.
			if (!esEscritorio) {
				useAlmacenUi.getState().alternarBarraLateral(false)
			}
		},
		[navegar, esEscritorio],
	)

	const volverALista = useCallback(() => {
		navegar("/")
		useAlmacenUi.getState().alternarBarraLateral(true)
	}, [navegar])

	// En escritorio la lista siempre está; el conmutador solo manda en móvil.
	const mostrarLista = esEscritorio || barraLateralVisible
	const mostrarConversacion = esEscritorio || !barraLateralVisible

	return (
		<div className="bg-lienzo flex h-full flex-col">
			<BarraConexion estado={estadoConexion} />

			<div className="flex min-h-0 flex-1">
				{mostrarLista && (
					<aside
						className={cn(
							"border-borde bg-panel flex min-h-0 flex-col border-r",
							esEscritorio ? "w-80 shrink-0" : "w-full",
						)}
					>
						<ListaConversaciones
							salaActivaId={salaActiva?.id ?? null}
							alAbrirSala={abrirSala}
							nombrePropio={nombrePropio}
						/>
					</aside>
				)}

				{mostrarConversacion && (
					<main className="flex min-h-0 flex-1 flex-col">
						{salaActiva ? (
							<Conversacion
								sala={salaActiva}
								nombrePropio={nombrePropio}
								alVolver={esEscritorio ? undefined : volverALista}
							/>
						) : (
							<SinConversacion />
						)}
					</main>
				)}
			</div>
		</div>
	)
}
