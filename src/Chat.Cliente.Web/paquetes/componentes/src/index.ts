// ============================================================================
// @paquetes/componentes
// ============================================================================
// Piezas visuales reutilizables y el tema compartido con Ant Design.
//
// Aquí solo vive lo que se usa en más de un sitio y no depende de la lógica de
// una pantalla concreta. Los componentes propios de la conversación —burbujas,
// redactor, lista de chats— están en la aplicación, junto a lo que los usa.
// ============================================================================

export { Avatar, type TamanoAvatar } from "./Avatar"
export { BarraConexion } from "./BarraConexion"
export { PuntoPresencia } from "./PuntoPresencia"

export { Cargando, EsqueletoConversaciones, EstadoError, EstadoVacio } from "./Estados"

export { AvisoEscribiendo, ContadorSinLeer, IconoSala, SeparadorFecha } from "./Indicadores"

export { paleta, temaAntd } from "./tema"
