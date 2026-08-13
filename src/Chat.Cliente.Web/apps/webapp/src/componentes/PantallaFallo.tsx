// ============================================================================
// PANTALLA DE FALLO
// ============================================================================
// Último recurso: lo que se ve cuando un error escapa de todos los manejadores
// y React desmonta el árbol. Se escribe sin antd ni Tailwind a propósito —solo
// estilos en línea—, porque un fallo al cargar la hoja de estilos o la propia
// librería de componentes es precisamente uno de los casos que puede traer
// hasta aquí.
// ============================================================================

import type { FallbackProps } from "react-error-boundary"

export function PantallaFallo({ error, resetErrorBoundary }: FallbackProps) {
	const mensaje = error instanceof Error ? error.message : String(error)

	return (
		<div
			role="alert"
			style={{
				display: "flex",
				minHeight: "100vh",
				alignItems: "center",
				justifyContent: "center",
				padding: "2rem",
				backgroundColor: "#f6f7f9",
				fontFamily: "system-ui, sans-serif",
				color: "#10151f",
			}}
		>
			<div style={{ maxWidth: "28rem", textAlign: "center" }}>
				<h1 style={{ margin: "0 0 0.75rem", fontSize: "1.25rem", fontWeight: 600 }}>
					La aplicación se ha detenido
				</h1>

				<p style={{ margin: "0 0 1rem", fontSize: "0.875rem", color: "#4a5464" }}>
					Ha ocurrido un error inesperado del que no se ha podido recuperar.
				</p>

				<pre
					style={{
						margin: "0 0 1.5rem",
						overflow: "auto",
						borderRadius: "0.5rem",
						border: "1px solid #e4e7ec",
						backgroundColor: "#ffffff",
						padding: "0.75rem",
						textAlign: "left",
						fontSize: "0.75rem",
						color: "#b91c1c",
					}}
				>
					{mensaje}
				</pre>

				<button
					type="button"
					onClick={resetErrorBoundary}
					style={{
						cursor: "pointer",
						borderRadius: "0.5rem",
						border: "none",
						backgroundColor: "#6b45b0",
						padding: "0.5rem 1.25rem",
						fontSize: "0.875rem",
						fontWeight: 500,
						color: "#ffffff",
					}}
				>
					Reintentar
				</button>
			</div>
		</div>
	)
}
