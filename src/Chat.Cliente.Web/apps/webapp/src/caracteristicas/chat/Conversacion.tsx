// ============================================================================
// CONVERSACIÓN
// ============================================================================
// Cabecera, historial y redactor. Es quien reparte el alto disponible: la
// rejilla deja la cabecera y el redactor con su tamaño natural y da al historial
// todo lo que sobra, que es lo que permite que solo él se desplace.
// ============================================================================

import { AvisoEscribiendo } from "@paquetes/componentes"
import { useEscribiendoEn, useMensajes } from "@paquetes/estados"
import { useEnviarMensaje, useHistorial } from "@paquetes/hooks"
import { useCallback } from "react"
import { toast } from "sonner"

import { CabeceraConversacion } from "./CabeceraConversacion"
import { PanelMensajes } from "./PanelMensajes"
import { Redactor } from "./Redactor"

import type { Sala } from "@paquetes/modelos"

interface Propiedades {
	sala: Sala
	nombrePropio: string
	/** En pantallas estrechas, vuelve a la lista. */
	alVolver?: (() => void) | undefined
}

export function Conversacion({ sala, nombrePropio, alVolver }: Propiedades) {
	const mensajes = useMensajes(sala.id)
	const escribiendo = useEscribiendoEn(sala.id, nombrePropio)

	const alFallar = useCallback((mensaje: string) => {
		toast.error(mensaje)
	}, [])

	const { cargando, completo, cargarAnteriores } = useHistorial(sala.id, alFallar)
	const { enviar, reintentar, descartar, progresoSubida } = useEnviarMensaje(alFallar)

	return (
		<div className="rejilla-conversacion bg-panel">
			<CabeceraConversacion sala={sala} alVolver={alVolver} />

			<PanelMensajes
				salaId={sala.id}
				mensajes={mensajes}
				cargando={cargando}
				historialCompleto={completo}
				alPedirAnteriores={cargarAnteriores}
				alReintentar={reintentar}
				alDescartar={descartar}
			/>

			<div className="border-borde border-t px-4 pt-2 pb-3">
				<AvisoEscribiendo nombres={escribiendo} />

				<Redactor
					salaId={sala.id}
					nombreSala={sala.nombre}
					progresoSubida={progresoSubida}
					alEnviar={enviar}
				/>
			</div>
		</div>
	)
}
