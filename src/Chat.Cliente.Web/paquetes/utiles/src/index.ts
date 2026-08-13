// ============================================================================
// @paquetes/utiles
// ============================================================================
// Funciones sin estado ni dependencias de React, compartidas por toda la app.
//
//   cn             → composición de clases CSS
//   fechas         → formato de instantes en la zona del navegador
//   texto          → iniciales, colores estables, recortes y búsqueda
//   emojis         → catálogo espejo del servidor y autocompletado
//   archivos       → tamaños, extensiones, validación y descarga
//   almacenamiento → localStorage a prueba de navegación privada
// ============================================================================

export * from "./almacenamiento"
export * from "./archivos"
export * from "./cn"
export * from "./emojis"
export * from "./fechas"
export * from "./texto"
