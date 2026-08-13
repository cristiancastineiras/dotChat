// ============================================================================
// ALMACÉN DE INTERFAZ
// ============================================================================
// Preferencias y estado visual que no vienen del servidor. Se guardan en el
// navegador para que la aplicación se abra como el usuario la dejó.
// ============================================================================

import { CLAVES, guardar, leer } from "@paquetes/utiles"
import { create } from "zustand"

/** Preferencias persistidas. */
interface Preferencias {
	/** Avisar con una notificación del sistema cuando llega un mensaje. */
	notificaciones: boolean
	/** Acompañar los mensajes nuevos con un sonido corto. */
	sonido: boolean
	/** Enviar con Intro; si está desactivado, Intro salta de línea. */
	enviarConIntro: boolean
}

/** Valores de partida la primera vez que se abre la aplicación. */
const POR_DEFECTO: Preferencias = {
	notificaciones: false,
	sonido: true,
	enviarConIntro: true,
}

interface EstadoUi extends Preferencias {
	/** Panel lateral visible; en pantallas estrechas se oculta al abrir una sala. */
	barraLateralVisible: boolean

	/** Panel de miembros de la sala desplegado. */
	panelMiembrosVisible: boolean

	/** Muestra u oculta la barra lateral. */
	alternarBarraLateral: (visible?: boolean) => void

	/** Muestra u oculta el panel de miembros. */
	alternarPanelMiembros: (visible?: boolean) => void

	/** Cambia una preferencia y la persiste. */
	fijarPreferencia: <C extends keyof Preferencias>(clave: C, valor: Preferencias[C]) => void
}

export const useAlmacenUi = create<EstadoUi>()((establecer, obtener) => ({
	...leer<Preferencias>(CLAVES.preferencias, POR_DEFECTO),

	// En un móvil se entra viendo la lista de conversaciones; en un escritorio
	// la barra lateral y la conversación conviven, así que empieza visible en
	// los dos casos y es la disposición la que decide.
	barraLateralVisible: true,
	panelMiembrosVisible: false,

	alternarBarraLateral: (visible) => {
		establecer((estado) => ({
			barraLateralVisible: visible ?? !estado.barraLateralVisible,
		}))
	},

	alternarPanelMiembros: (visible) => {
		establecer((estado) => ({
			panelMiembrosVisible: visible ?? !estado.panelMiembrosVisible,
		}))
	},

	fijarPreferencia: (clave, valor) => {
		establecer({ [clave]: valor } as Pick<Preferencias, typeof clave>)

		const { notificaciones, sonido, enviarConIntro } = obtener()
		guardar(CLAVES.preferencias, { notificaciones, sonido, enviarConIntro })
	},
}))
