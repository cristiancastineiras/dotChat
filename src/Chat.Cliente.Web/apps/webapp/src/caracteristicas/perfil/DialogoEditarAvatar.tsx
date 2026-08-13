// ============================================================================
// EDITOR DE LA FOTO DE PERFIL
// ============================================================================
// Elegir imagen, encuadrarla y subirla.
// ============================================================================

import { configuracion, mensajeDeError, olvidarAvatar, usuarios } from "@paquetes/api"
import { useAlmacenSesion } from "@paquetes/estados"
import { esImagen, validarTamano } from "@paquetes/utiles"
import { Button, Modal, Slider, Upload } from "antd"
import { ImagePlus, ZoomIn, ZoomOut } from "lucide-react"
import { useCallback, useEffect, useState } from "react"
import Cropper, { type Area } from "react-easy-crop"
import { toast } from "sonner"

import { recortarCuadrado } from "./recorte"

interface Propiedades {
	abierto: boolean
	alCerrar: () => void
}

export function DialogoEditarAvatar({ abierto, alCerrar }: Propiedades) {
	const usuarioId = useAlmacenSesion((estado) => estado.sesion?.usuarioId)
	const fijarPerfil = useAlmacenSesion((estado) => estado.fijarPerfil)

	const [origen, setOrigen] = useState<string | null>(null)
	const [posicion, setPosicion] = useState({ x: 0, y: 0 })
	const [zoom, setZoom] = useState(1)
	const [area, setArea] = useState<Area | null>(null)
	const [subiendo, setSubiendo] = useState(false)

	// La URL de objeto de la imagen elegida retiene el archivo en memoria hasta
	// que se revoca.
	useEffect(() => {
		return () => {
			if (origen) URL.revokeObjectURL(origen)
		}
	}, [origen])

	const reiniciar = useCallback(() => {
		setOrigen((anterior) => {
			if (anterior) URL.revokeObjectURL(anterior)
			return null
		})
		setPosicion({ x: 0, y: 0 })
		setZoom(1)
		setArea(null)
	}, [])

	function elegir(archivo: File): boolean {
		if (!esImagen(archivo)) {
			toast.error("La foto de perfil debe ser una imagen.")
			return false
		}

		const error = validarTamano(archivo, configuracion.maxAvatarMiB)
		if (error) {
			toast.error(error)
			return false
		}

		setOrigen((anterior) => {
			if (anterior) URL.revokeObjectURL(anterior)
			return URL.createObjectURL(archivo)
		})

		setPosicion({ x: 0, y: 0 })
		setZoom(1)

		return false
	}

	async function guardar() {
		if (!origen || !area) return

		setSubiendo(true)

		try {
			const recortada = await recortarCuadrado(origen, area)
			const perfil = await usuarios.subirAvatar(recortada)

			// La caché guarda las fotos por versión; la anterior ya no vale y hay que
			// soltarla para que no siga ocupando memoria.
			if (usuarioId) olvidarAvatar(usuarioId)

			fijarPerfil(perfil)
			toast.success("Foto de perfil actualizada.")

			reiniciar()
			alCerrar()
		} catch (error) {
			toast.error(mensajeDeError(error))
		} finally {
			setSubiendo(false)
		}
	}

	return (
		<Modal
			open={abierto}
			onCancel={() => {
				reiniciar()
				alCerrar()
			}}
			title="Cambiar foto de perfil"
			width={440}
			destroyOnHidden
			footer={
				<div className="flex justify-end gap-2">
					<Button
						onClick={() => {
							reiniciar()
							alCerrar()
						}}
						disabled={subiendo}
					>
						Cancelar
					</Button>
					<Button
						type="primary"
						onClick={() => void guardar()}
						loading={subiendo}
						disabled={!origen || !area}
					>
						Guardar
					</Button>
				</div>
			}
		>
			{origen ? (
				<div className="space-y-4 pt-2">
					{/* Encuadre. El recuadro es redondo porque así es como se va a ver
					    después en toda la aplicación. */}
					<div className="bg-tinta relative h-64 overflow-hidden rounded-lg">
						<Cropper
							image={origen}
							crop={posicion}
							zoom={zoom}
							aspect={1}
							cropShape="round"
							showGrid={false}
							onCropChange={setPosicion}
							onZoomChange={setZoom}
							onCropComplete={(_recorte, pixeles) => setArea(pixeles)}
							minZoom={1}
							maxZoom={4}
						/>
					</div>

					<div className="flex items-center gap-3">
						<ZoomOut className="text-tinta-tenue h-4 w-4 shrink-0" aria-hidden />
						<Slider
							min={1}
							max={4}
							step={0.05}
							value={zoom}
							onChange={setZoom}
							tooltip={{ formatter: null }}
							className="flex-1"
							aria-label="Ampliación"
						/>
						<ZoomIn className="text-tinta-tenue h-4 w-4 shrink-0" aria-hidden />
					</div>

					<div className="flex justify-center">
						<Button size="small" type="link" onClick={reiniciar} disabled={subiendo}>
							Elegir otra imagen
						</Button>
					</div>
				</div>
			) : (
				<Upload.Dragger
					accept="image/*"
					beforeUpload={elegir}
					showUploadList={false}
					maxCount={1}
					className="!mt-2"
				>
					<div className="py-6">
						<ImagePlus className="text-tinta-tenue mx-auto mb-3 h-8 w-8" aria-hidden />
						<p className="text-tinta text-sm font-medium">
							Arrastra una imagen o pulsa para elegirla
						</p>
						<p className="text-tinta-tenue mt-1 text-xs">
							JPG, PNG, WEBP o GIF. Hasta {configuracion.maxAvatarMiB} MiB.
						</p>
					</div>
				</Upload.Dragger>
			)}
		</Modal>
	)
}
