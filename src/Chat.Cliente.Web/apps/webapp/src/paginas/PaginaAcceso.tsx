// ============================================================================
// ACCESO
// ============================================================================
// Inicio de sesión y registro en una sola pantalla, alternando con pestañas.
// ============================================================================

import { ErrorApi, configuracion, mensajeDeError } from "@paquetes/api"
import { useAlmacenSesion } from "@paquetes/estados"
import { Alert, Button, Form, Input, Tabs } from "antd"
import { useState } from "react"
import { Navigate, useLocation, useNavigate } from "react-router-dom"

/** Campos del formulario de inicio de sesión. */
interface CamposLogin {
	nombreUsuario: string
	clave: string
}

/** Campos del formulario de registro. */
interface CamposRegistro extends CamposLogin {
	email: string
	claveRepetida: string
}

export function PaginaAcceso() {
	const autenticado = useAlmacenSesion((estado) => estado.sesion !== null)
	const entrar = useAlmacenSesion((estado) => estado.entrar)
	const registrarse = useAlmacenSesion((estado) => estado.registrarse)

	const [pestana, setPestana] = useState("entrar")
	const [error, setError] = useState<string | null>(null)
	const [enviando, setEnviando] = useState(false)

	const navegar = useNavigate()
	const ubicacion = useLocation()

	// A dónde volver tras entrar: la ruta que se intentó abrir sin sesión.
	const destino = (ubicacion.state as { desde?: string } | null)?.desde ?? "/"

	if (autenticado) {
		return <Navigate to={destino} replace />
	}

	/** Ejecuta una acción de acceso y traduce el fallo a un aviso legible. */
	async function intentar(accion: () => Promise<void>): Promise<void> {
		setEnviando(true)
		setError(null)

		try {
			await accion()
			navegar(destino, { replace: true })
		} catch (fallo) {
			// El 401 del inicio de sesión llega como «credenciales no válidas», que
			// ya es el mensaje correcto. Los demás se muestran tal cual los envía el
			// servidor: son los que explican qué regla de validación se incumplió.
			setError(
				fallo instanceof ErrorApi && fallo.estado === 0
					? "No se ha podido contactar con el servidor. Comprueba que está levantado."
					: mensajeDeError(fallo),
			)
		} finally {
			setEnviando(false)
		}
	}

	return (
		<div className="bg-lienzo flex h-full items-center justify-center overflow-y-auto p-4">
			<div className="w-full max-w-sm">
				{/* Identidad del proyecto */}
				<div className="mb-8 text-center">
					<img src="/dotChat_.svg" alt="" className="mx-auto mb-4 h-14 w-auto" aria-hidden />
					<h1 className="text-tinta text-xl font-semibold tracking-tight">
						{configuracion.nombreApp}
					</h1>
					<p className="text-tinta-tenue mt-1 text-sm">
						Mensajería cifrada, en tu propio servidor.
					</p>
				</div>

				<div className="border-borde bg-panel rounded-xl border p-6 shadow-sm">
					<Tabs
						activeKey={pestana}
						onChange={(clave) => {
							setPestana(clave)
							setError(null)
						}}
						items={[
							{
								key: "entrar",
								label: "Iniciar sesión",
								children: (
									<FormularioLogin
										enviando={enviando}
										alEnviar={(valores) => intentar(() => entrar(valores))}
									/>
								),
							},
							{
								key: "registrar",
								label: "Crear cuenta",
								children: (
									<FormularioRegistro
										enviando={enviando}
										alEnviar={(valores) =>
											intentar(() =>
												registrarse({
													nombreUsuario: valores.nombreUsuario,
													email: valores.email,
													clave: valores.clave,
												}),
											)
										}
									/>
								),
							},
						]}
					/>

					{error && (
						<Alert
							type="error"
							message={error}
							showIcon
							className="mt-2"
							closable
							onClose={() => setError(null)}
						/>
					)}
				</div>

				<p className="text-tinta-tenue mt-6 text-center text-xs">
					Tus mensajes se cifran antes de tocar el disco. Ni el servidor los lee.
				</p>
			</div>
		</div>
	)
}

// ---------------------------------------------------------------------------
// Inicio de sesión
// ---------------------------------------------------------------------------

function FormularioLogin({
	enviando,
	alEnviar,
}: {
	enviando: boolean
	alEnviar: (valores: CamposLogin) => void
}) {
	return (
		<Form<CamposLogin>
			layout="vertical"
			onFinish={alEnviar}
			requiredMark={false}
			disabled={enviando}
			autoComplete="on"
		>
			<Form.Item
				name="nombreUsuario"
				label="Nombre de usuario"
				rules={[{ required: true, message: "Escribe tu nombre de usuario." }]}
			>
				<Input size="large" autoComplete="username" autoFocus placeholder="tu-usuario" />
			</Form.Item>

			<Form.Item
				name="clave"
				label="Contraseña"
				rules={[{ required: true, message: "Escribe tu contraseña." }]}
			>
				<Input.Password size="large" autoComplete="current-password" placeholder="••••••••" />
			</Form.Item>

			<Button type="primary" htmlType="submit" size="large" block loading={enviando}>
				Entrar
			</Button>
		</Form>
	)
}

// ---------------------------------------------------------------------------
// Registro
// ---------------------------------------------------------------------------

function FormularioRegistro({
	enviando,
	alEnviar,
}: {
	enviando: boolean
	alEnviar: (valores: CamposRegistro) => void
}) {
	return (
		<Form<CamposRegistro>
			layout="vertical"
			onFinish={alEnviar}
			requiredMark={false}
			disabled={enviando}
		>
			<Form.Item
				name="nombreUsuario"
				label="Nombre de usuario"
				rules={[
					{ required: true, message: "Elige un nombre de usuario." },
					{ min: 3, message: "Al menos 3 caracteres." },
					{ max: 32, message: "Como mucho 32 caracteres." },
					{
						// Se adelanta a la validación del servidor para dar el aviso
						// mientras se escribe, no después de enviar.
						pattern: /^[a-zA-Z0-9._-]+$/u,
						message: "Solo letras, números, punto, guion y guion bajo.",
					},
				]}
			>
				<Input size="large" autoComplete="username" placeholder="tu-usuario" />
			</Form.Item>

			<Form.Item
				name="email"
				label="Correo electrónico"
				rules={[
					{ required: true, message: "Escribe tu correo." },
					{ type: "email", message: "Ese correo no parece válido." },
				]}
			>
				<Input size="large" autoComplete="email" placeholder="tu@correo.com" />
			</Form.Item>

			<Form.Item
				name="clave"
				label="Contraseña"
				rules={[
					{ required: true, message: "Elige una contraseña." },
					{ min: 8, message: "Al menos 8 caracteres." },
				]}
			>
				<Input.Password size="large" autoComplete="new-password" placeholder="••••••••" />
			</Form.Item>

			<Form.Item
				name="claveRepetida"
				label="Repite la contraseña"
				dependencies={["clave"]}
				rules={[
					{ required: true, message: "Repite la contraseña." },
					({ getFieldValue }) => ({
						validator: (_regla, valor: string) =>
							!valor || getFieldValue("clave") === valor
								? Promise.resolve()
								: Promise.reject(new Error("Las contraseñas no coinciden.")),
					}),
				]}
			>
				<Input.Password size="large" autoComplete="new-password" placeholder="••••••••" />
			</Form.Item>

			<Button type="primary" htmlType="submit" size="large" block loading={enviando}>
				Crear cuenta
			</Button>
		</Form>
	)
}
