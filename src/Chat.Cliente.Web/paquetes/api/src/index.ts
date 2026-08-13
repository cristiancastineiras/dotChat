// ============================================================================
// @paquetes/api
// ============================================================================
// Todo lo que habla con el servidor de dotChat.
//
//   configuracion → variables de entorno ya tipadas
//   errores       → ProblemDetails convertido en errores presentables
//   sesion        → dónde vive el token y quién lo puede cambiar
//   cliente       → instancia de ky con renovación transparente de sesión
//   autenticacion → registro, inicio y cierre de sesión
//   salas         → catálogo, pertenencia, miembros e invitaciones
//   mensajes      → historial, envío y adjuntos
//   usuarios      → directorio, presencia y foto de perfil
//   avatares      → caché de fotos, que no se pueden enlazar directamente
//
// Los módulos de recursos se exponen con espacio de nombres para que en el
// código de la interfaz se lea `salas.listarPropias()` en lugar de un
// `listarPropias()` suelto del que no se sabe a qué recurso pertenece.
// ============================================================================

export * as autenticacion from "./autenticacion"
export * as mensajes from "./mensajes"
export * as salas from "./salas"
export * as usuarios from "./usuarios"

export { avatarEnCache, obtenerAvatar, olvidarAvatar, vaciarAvatares } from "./avatares"

export {
	alExpirarSesion,
	api,
	apiPublica,
	descargarBlob,
	ejecutar,
	obtenerTokenValido,
} from "./cliente"

export { MARGEN_RENOVACION_MS, configuracion } from "./configuracion"

export { ErrorApi, comoErrorApi, mensajeDeError } from "./errores"

export {
	comoSesion,
	esAdministrador,
	establecerSesion,
	obtenerSesion,
	obtenerToken,
	suscribirSesion,
} from "./sesion"
