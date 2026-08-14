// ============================================================================
// RAÍZ DE LA APLICACIÓN
// ============================================================================
// Composición de proveedores y enrutado. El orden importa:
//
//   ErrorBoundary  → por fuera de todo, para atrapar también fallos de los
//                    propios proveedores
//   ConfigProvider → tema e idioma de antd, que necesitan los componentes
//   App de antd    → contexto de sus avisos y diálogos
//   QueryClient    → caché de las consultas
//   BrowserRouter  → navegación
// ============================================================================

import { ErrorApi } from "@paquetes/api"
import { temaAntd } from "@paquetes/componentes"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { App as AppAntd, ConfigProvider } from "antd"
import esES from "antd/locale/es_ES"
import { ErrorBoundary } from "react-error-boundary"
import { BrowserRouter } from "react-router-dom"
import { Toaster } from "sonner"

import { PantallaFallo } from "./componentes/PantallaFallo"
import { Rutas } from "./rutas/Rutas"

/**
 * Cliente de React Query.
 *
 * Se crea fuera del componente para que sobreviva a los remontajes: dentro, el
 * modo estricto de desarrollo lo recrearía y tiraría la caché en cada montaje.
 */
const clienteConsultas = new QueryClient({
	defaultOptions: {
		queries: {
			// Un minuto de vigencia. Estos datos —usuarios, catálogo de salas— no son
			// los que cambian rápido; lo que sí lo hace llega por el hub.
			staleTime: 60_000,
			gcTime: 5 * 60_000,

			// Recuperar el foco no debería disparar una tanda de peticiones: el
			// contenido en vivo ya viene empujado por la conexión en tiempo real.
			refetchOnWindowFocus: false,
			refetchOnReconnect: true,

			retry: (intentos, error) => {
				// No tiene sentido insistir con lo que ya se sabe que va a fallar:
				// permisos, recursos que no existen o datos mal formados.
				if (error instanceof ErrorApi && !error.esTransitorio) return false
				return intentos < 2
			},
		},
	},
})

export function App() {
	return (
		<ErrorBoundary FallbackComponent={PantallaFallo}>
			<ConfigProvider theme={temaAntd} locale={esES}>
				{/* Sin `className`, el `<div class="ant-app">` que antd interpone aquí no
				    lleva altura ni flex propios: rompe la cadena de `height:100%` que baja
				    desde `#root` y todo lo de dentro colapsa a su altura de contenido, que
				    es lo que recortaba la ventana de chat por abajo. */}
				<AppAntd className="flex h-full min-h-0 flex-col">
					<QueryClientProvider client={clienteConsultas}>
						<BrowserRouter>
							<Rutas />
						</BrowserRouter>

						<Toaster
							position="bottom-right"
							closeButton
							richColors
							// Discretos y con la tipografía de la aplicación: los avisos no
							// deberían parecer de otra librería.
							toastOptions={{
								style: {
									fontFamily: "inherit",
									fontSize: "13px",
								},
							}}
						/>
					</QueryClientProvider>
				</AppAntd>
			</ConfigProvider>
		</ErrorBoundary>
	)
}
