using System.ComponentModel.DataAnnotations;

namespace Chat.ClienteCli.Servicios;

/// <summary>Configuración del cliente CLI.</summary>
public sealed class OpcionesCliente
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Cliente";

    /// <summary>Dirección base del servidor de mensajería.</summary>
    [Required(ErrorMessage = "La dirección del servidor es obligatoria.")]
    public string UrlServidor { get; set; } = "https://localhost:7150";

    /// <summary>Segundos de espera máximos para una llamada HTTP.</summary>
    [Range(1, 300, ErrorMessage = "El tiempo de espera debe estar entre 1 y 300 segundos.")]
    public int SegundosTiempoEspera { get; set; } = 30;

    /// <summary>Número de mensajes que se cargan al abrir una sala.</summary>
    [Range(1, 200, ErrorMessage = "El historial inicial debe estar entre 1 y 200 mensajes.")]
    public int MensajesHistorialInicial { get; set; } = 30;

    /// <summary>
    /// Acepta certificados TLS no verificables. Solo debe activarse en desarrollo
    /// local contra un certificado autofirmado que no esté en el almacén de confianza.
    /// </summary>
    public bool AceptarCertificadosNoConfiables { get; set; }
}
