// ============================================================================
// @paquetes/hooks
// ============================================================================
// Comportamiento con estado, envuelto en hooks de React.
//
//   conexionHub      → conexión de SignalR, fuera de React
//   useConexionChat  → engancha el hub a los almacenes
//   useHistorial     → primera página y paginación hacia atrás
//   useEnviarMensaje → envío optimista, adjuntos y reintentos
//   useEscribiendo   → aviso de escritura, limitado en frecuencia
//   useAccionesSala  → crear, unirse, invitar, salir
//   useConsultas     → listas de solo lectura con React Query
//   useAvatar        → foto de perfil desde la caché de blobs
//   useInterfaz      → medios, retardo, desplazamiento, notificaciones, atajos
// ============================================================================

export { estaConectado, hub, type ManejadoresHub } from "./conexionHub"

export { useAccionesSala } from "./useAccionesSala"
export { useAvatar } from "./useAvatar"
export { useConexionChat } from "./useConexionChat"
export { useEnviarMensaje, type EnvioMensaje } from "./useEnviarMensaje"
export { useEscribiendo } from "./useEscribiendo"
export { useHistorial } from "./useHistorial"

export {
	clavesConsulta,
	useCatalogoSalas,
	useDirectorio,
	useInvalidar,
	useMiembros,
	useUsuarios,
} from "./useConsultas"

export {
	useAtajoTeclado,
	useConsultaMedios,
	useDesplazamientoAlFinal,
	useEsEscritorio,
	useNotificaciones,
	useRetardo,
} from "./useInterfaz"
