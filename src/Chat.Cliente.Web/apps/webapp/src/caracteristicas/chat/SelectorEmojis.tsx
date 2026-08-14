// ============================================================================
// SELECTOR DE EMOJIS
// ============================================================================
// Picker completo (catálogo Unicode entero, buscador y tonos de piel) con
// `emoji-picker-react`, la librería de este tipo con más adopción y
// mantenimiento activo del ecosistema React.
//
// Se fuerza `emojiStyle={EmojiStyle.NATIVE}`: por defecto la librería puede
// pintar los emojis como imágenes traídas de un CDN externo (Apple/Google/
// Twitter/Facebook); con «nativo» usa los glifos Unicode que ya renderiza el
// sistema operativo —los mismos que empleaba el catálogo hecho a mano que
// sustituye— y la aplicación no depende de red externa para dibujar un
// emoji, coherente con ser una app autoalojada.
//
// El catálogo de atajos `:nombre:` (`@paquetes/utiles/emojis`) sigue aparte y
// no se toca: este componente sólo sustituye la cuadrícula de selección
// manual, no el autocompletado ni la expansión de atajos del redactor.
// ============================================================================

import { Popover } from "antd"
import EmojiPicker, { EmojiStyle, Theme, type EmojiClickData } from "emoji-picker-react"
import type { ReactNode } from "react"

interface Propiedades {
	abierto: boolean
	alCambiarApertura: (abierto: boolean) => void
	alElegir: (simbolo: string) => void
	children: ReactNode
}

export function SelectorEmojis({ abierto, alCambiarApertura, alElegir, children }: Propiedades) {
	function alHacerClic(datos: EmojiClickData) {
		alElegir(datos.emoji)
	}

	return (
		<Popover
			open={abierto}
			onOpenChange={alCambiarApertura}
			trigger="click"
			placement="topRight"
			arrow={false}
			// Sin relleno propio: el picker ya trae el suyo y con el del Popover
			// puesto encima quedaba una doble caja alrededor del buscador.
			styles={{ content: { padding: 0 } }}
			content={
				<EmojiPicker
					onEmojiClick={alHacerClic}
					emojiStyle={EmojiStyle.NATIVE}
					theme={Theme.LIGHT}
					lazyLoadEmojis
					autoFocusSearch={false}
					searchPlaceHolder="Buscar emoji"
					previewConfig={{ showPreview: false }}
					width={320}
					height={380}
				/>
			}
		>
			{children}
		</Popover>
	)
}
