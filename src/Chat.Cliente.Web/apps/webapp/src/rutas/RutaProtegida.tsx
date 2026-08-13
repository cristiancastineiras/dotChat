import { useAlmacenSesion } from "@paquetes/estados"
import { Navigate, Outlet, useLocation } from "react-router-dom"

/**
 * Envoltorio de las rutas que exigen sesión.
 *
 * Guarda de dónde venía el usuario para devolverlo ahí tras entrar: si alguien
 * abre un enlace a una conversación concreta y tiene que autenticarse, debe
 * acabar en esa conversación y no en la portada.
 */
export function RutaProtegida() {
	const autenticado = useAlmacenSesion((estado) => estado.sesion !== null)
	const ubicacion = useLocation()

	if (!autenticado) {
		return <Navigate to="/acceso" replace state={{ desde: ubicacion.pathname }} />
	}

	return <Outlet />
}
