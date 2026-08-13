// ============================================================================
// TEMA DE ANT DESIGN
// ============================================================================
// Traduce los tokens de dotChat al lenguaje de antd, para que sus componentes y
// los propios se vean como una sola interfaz y no como dos librerías pegadas.
//
// Los valores están duplicados respecto a `index.css` por necesidad: antd
// resuelve su tema en JavaScript, antes de que exista ninguna hoja de estilos, y
// no puede leer variables CSS. La duplicación se acota declarándola aquí y solo
// aquí; cualquier cambio de paleta se hace en los dos sitios a la vez.
// ============================================================================

import type { ThemeConfig } from "antd"

/** Paleta compartida. Debe coincidir con el bloque `@theme` de index.css. */
export const paleta = {
	marca400: "#9370db",
	marca500: "#7e56c9",
	marca600: "#6b45b0",
	marca700: "#593a91",

	lienzo: "#f6f7f9",
	panel: "#ffffff",
	borde: "#e4e7ec",
	bordeFuerte: "#d3d8e0",

	tinta: "#10151f",
	tintaSuave: "#4a5464",
	tintaTenue: "#7b8595",

	exito: "#15803d",
	aviso: "#b45309",
	error: "#b91c1c",
	conectado: "#16a34a",
} as const

/** Familia tipográfica, igual que la del bloque `@theme`. */
const FUENTE =
	'"Uncut Sans", ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'

/**
 * Configuración del `ConfigProvider`.
 *
 * En antd 6 las variables CSS ya vienen activadas de serie, así que no hay que
 * pedirlas. `hashed: false` quita el sufijo aleatorio de los nombres de clase,
 * que solo hace falta cuando conviven dos versiones de antd en la misma página
 * y que, sin él, hace mucho más cómodo depurar estilos en el inspector.
 */
export const temaAntd: ThemeConfig = {
	hashed: false,

	token: {
		colorPrimary: paleta.marca600,
		colorInfo: paleta.marca600,
		colorSuccess: paleta.exito,
		colorWarning: paleta.aviso,
		colorError: paleta.error,

		colorText: paleta.tinta,
		colorTextSecondary: paleta.tintaSuave,
		colorTextTertiary: paleta.tintaTenue,
		colorTextQuaternary: paleta.tintaTenue,

		colorBgBase: paleta.panel,
		colorBgLayout: paleta.lienzo,
		colorBorder: paleta.bordeFuerte,
		colorBorderSecondary: paleta.borde,

		fontFamily: FUENTE,
		fontSize: 14,

		// Radios contenidos: una interfaz de trabajo con esquinas muy redondeadas
		// parece un juguete, y con esquinas vivas, un formulario de los noventa.
		borderRadius: 8,
		borderRadiusLG: 10,
		borderRadiusSM: 6,

		controlHeight: 36,
		wireframe: false,

		// Sombras discretas: aquí la jerarquía la marcan los bordes y el fondo, no
		// la profundidad.
		boxShadow: "0 1px 2px 0 rgb(16 21 31 / 0.05)",
		boxShadowSecondary: "0 4px 16px 0 rgb(16 21 31 / 0.08)",
	},

	components: {
		Button: {
			// Sin sombra en los botones primarios: es lo que los hace parecer de
			// otra época frente al resto de la interfaz.
			primaryShadow: "none",
			defaultShadow: "none",
			dangerShadow: "none",
			fontWeight: 500,
		},
		Input: {
			activeShadow: "none",
			paddingBlock: 7,
		},
		Modal: {
			titleFontSize: 17,
		},
		Tooltip: {
			colorBgSpotlight: paleta.tinta,
			fontSize: 13,
		},
		Dropdown: {
			paddingBlock: 5,
		},
		Segmented: {
			itemSelectedBg: paleta.panel,
		},
		Tabs: {
			horizontalItemPadding: "10px 0",
		},
		Form: {
			labelColor: paleta.tintaSuave,
			verticalLabelPadding: "0 0 6px",
			itemMarginBottom: 18,
		},
	},
}
