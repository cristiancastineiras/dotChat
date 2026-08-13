// ============================================================================
// TSDOWN
// ============================================================================
// Empaqueta la librería con tsdown (sobre Rolldown).
//
// `deps.neverBundle` deja fuera del bundle a React, a las librerías de terceros
// y a los demás paquetes del monorepo. Lo último hace falta explícitamente: el
// tsconfig mapea `@paquetes/*` a las carpetas `src` para que el typecheck no
// exija compilar antes, y sin esta regla tsdown seguiría ese mapeo e incrustaría
// el código fuente del paquete vecino —y el de sus dependencias— en cada
// bundle, duplicándolo tantas veces como paquetes lo importen.
// ============================================================================

import { defineConfig } from "tsdown"

export default defineConfig({
	entry: ["./src/index.ts"],
	format: ["esm"],
	platform: "neutral",
	dts: true,
	sourcemap: true,
	clean: true,
	treeshake: true,
	deps: {
		neverBundle: [
			/^react($|\/)/,
			/^react-dom($|\/)/,
			/^@paquetes\//,
			/^antd($|\/)/,
			/^@ant-design\//,
			/^@microsoft\/signalr/,
			/^@tanstack\//,
			/^lucide-react$/,
			/^zustand($|\/)/,
			/^clsx$/,
			/^ky$/,
		],
	},
})
