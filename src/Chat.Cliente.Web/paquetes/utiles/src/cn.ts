import clsx, { type ClassValue } from "clsx"

/**
 * Compone clases CSS condicionales.
 *
 * Es `clsx` con otro nombre, más corto, porque aparece en casi todos los
 * componentes. No deduplica clases de Tailwind en conflicto —para eso haría
 * falta `tailwind-merge`, que pesa bastante—: en este cliente los conflictos se
 * evitan escribiendo las variantes con condicionales excluyentes en lugar de
 * apilando clases y confiando en que gane la última.
 *
 * @param clases Clases, objetos `{ clase: condicion }` o listas anidadas.
 */
export function cn(...clases: ClassValue[]): string {
	return clsx(clases)
}
