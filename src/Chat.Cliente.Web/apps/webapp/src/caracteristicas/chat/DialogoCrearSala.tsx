// ============================================================================
// CREAR SALA
// ============================================================================

import { useAccionesSala } from "@paquetes/hooks"
import { Form, Input, Modal, Radio } from "antd"
import { useEffect } from "react"
import { toast } from "sonner"

import type { SolicitudCrearSala } from "@paquetes/modelos"

interface Propiedades {
	abierto: boolean
	alCerrar: () => void
}

/** Campos del formulario. */
interface Campos {
	nombre: string
	descripcion?: string
	visibilidad: "publica" | "privada"
}

export function DialogoCrearSala({ abierto, alCerrar }: Propiedades) {
	const [formulario] = Form.useForm<Campos>()

	const { crear, trabajando } = useAccionesSala({
		exito: (mensaje) => toast.success(mensaje),
		error: (mensaje) => toast.error(mensaje),
	})

	// El formulario se limpia al abrir y no al cerrar: así, si la creación falla y
	// el diálogo sigue abierto, no se pierde lo escrito.
	useEffect(() => {
		if (abierto) formulario.resetFields()
	}, [abierto, formulario])

	async function enviar(valores: Campos) {
		const solicitud: SolicitudCrearSala = {
			nombre: valores.nombre.trim(),
			descripcion: valores.descripcion?.trim() || null,
			privada: valores.visibilidad === "privada",
		}

		if (await crear(solicitud)) {
			alCerrar()
		}
	}

	return (
		<Modal
			open={abierto}
			onCancel={alCerrar}
			title="Crear una sala"
			okText="Crear"
			cancelText="Cancelar"
			confirmLoading={trabajando}
			onOk={() => formulario.submit()}
			destroyOnHidden
		>
			<Form<Campos>
				form={formulario}
				layout="vertical"
				onFinish={enviar}
				requiredMark={false}
				initialValues={{ visibilidad: "publica" }}
				className="pt-2"
			>
				<Form.Item
					name="nombre"
					label="Nombre"
					rules={[
						{ required: true, message: "La sala necesita un nombre." },
						{ min: 3, message: "Al menos 3 caracteres." },
						{ max: 64, message: "Como mucho 64 caracteres." },
					]}
				>
					<Input placeholder="general, proyecto-x, cafetería…" autoFocus />
				</Form.Item>

				<Form.Item
					name="descripcion"
					label="Descripción"
					rules={[{ max: 256, message: "Como mucho 256 caracteres." }]}
				>
					<Input.TextArea
						rows={2}
						placeholder="De qué se habla aquí (opcional)"
						maxLength={256}
						showCount
					/>
				</Form.Item>

				<Form.Item name="visibilidad" label="Visibilidad" className="!mb-0">
					<Radio.Group>
						<div className="space-y-2">
							<Radio value="publica">
								<span className="text-sm font-medium">Pública</span>
								<p className="text-tinta-tenue text-xs">
									Aparece en el catálogo y cualquiera puede unirse.
								</p>
							</Radio>
							<Radio value="privada">
								<span className="text-sm font-medium">Privada</span>
								<p className="text-tinta-tenue text-xs">
									Solo la ven sus miembros. Se entra por invitación.
								</p>
							</Radio>
						</div>
					</Radio.Group>
				</Form.Item>
			</Form>
		</Modal>
	)
}
