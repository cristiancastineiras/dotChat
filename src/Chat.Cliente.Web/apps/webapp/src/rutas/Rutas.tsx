// ============================================================================
// ENRUTADO
// ============================================================================

import { alExpirarSesion } from "@paquetes/api"
import { Cargando } from "@paquetes/componentes"
import { useAlmacenSesion } from "@paquetes/estados"
import { lazy, Suspense, useEffect } from "react"
import { Navigate, Route, Routes, useNavigate } from "react-router-dom"

import { PaginaAcceso } from "../paginas/PaginaAcceso"
import { RutaProtegida } from "./RutaProtegida"

// La conversación arrastra antd, el hub y el editor de imágenes. Cargarla aparte
// hace que la pantalla de acceso —lo primero que ve alguien sin sesión— no tenga
// que esperar a descargar nada de eso.
const PaginaChat = lazy(async () => ({
	default: (await import("../paginas/PaginaChat")).PaginaChat,
}))

const PaginaPerfil = lazy(async () => ({
	default: (await import("../paginas/PaginaPerfil")).PaginaPerfil,
}))

export function Rutas() {
	const restaurar = useAlmacenSesion((estado) => estado.restaurar)
	const comprobando = useAlmacenSesion((estado) => estado.comprobando)
	const navegar = useNavigate()

	// Al arrancar se comprueba si la sesión guardada sigue valiendo. Sin esto, la
	// aplicación se pintaría como autenticada y empezaría a fallar petición a
	// petición hasta que alguna renovación cerrara la sesión.
	useEffect(() => {
		void restaurar()
	}, [restaurar])

	// Cuando el cliente HTTP se queda sin forma de renovar la sesión, avisa por
	// aquí. Es lo que devuelve al usuario a la pantalla de acceso desde cualquier
	// punto de la aplicación, incluido un componente que ya se ha desmontado.
	useEffect(() => {
		alExpirarSesion(() => {
			navegar("/acceso", { replace: true })
		})
	}, [navegar])

	if (comprobando) {
		return (
			<div className="bg-lienzo flex h-full items-center justify-center">
				<Cargando texto="Restaurando sesión…" />
			</div>
		)
	}

	return (
		<Suspense
			fallback={
				<div className="bg-lienzo flex h-full items-center justify-center">
					<Cargando />
				</div>
			}
		>
			<Routes>
				<Route path="/acceso" element={<PaginaAcceso />} />

				<Route element={<RutaProtegida />}>
					<Route path="/" element={<PaginaChat />} />
					<Route path="/sala/:salaId" element={<PaginaChat />} />
					<Route path="/perfil" element={<PaginaPerfil />} />
				</Route>

				{/* Cualquier otra ruta vuelve al principio en lugar de dejar la
				    pantalla en blanco. */}
				<Route path="*" element={<Navigate to="/" replace />} />
			</Routes>
		</Suspense>
	)
}
