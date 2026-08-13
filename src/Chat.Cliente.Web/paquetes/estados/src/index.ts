// ============================================================================
// @paquetes/estados
// ============================================================================
// Almacenes de Zustand con el estado compartido de la aplicación.
//
//   almacenSesion → reflejo en React de la sesión que gestiona la API
//   almacenChat   → salas, mensajes, escritura, presencia y conexión
//   almacenUi     → preferencias y estado visual
//   selectores    → lecturas derivadas, con comparación superficial
// ============================================================================

export { useAlmacenChat } from "./almacenChat"
export { useAlmacenSesion } from "./almacenSesion"
export { useAlmacenUi } from "./almacenUi"

export {
	useEscribiendoEn,
	useEstadoConexion,
	useEstaEnLinea,
	useMensajes,
	useSalaActiva,
	useSalasOrdenadas,
	useTotalSinLeer,
} from "./selectores"
