using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>Configuración del concentrador (hub) de SignalR y de sus límites operativos.</summary>
public sealed class SignalROptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "SignalR";

    /// <summary>Ruta relativa en la que se publica el hub de chat.</summary>
    [Required(ErrorMessage = "La ruta del hub es obligatoria.")]
    public string RutaHub { get; set; } = "/hubs/chat";

    /// <summary>Segundos sin actividad tras los que el servidor cierra la conexión.</summary>
    [Range(5, 600, ErrorMessage = "El tiempo de espera del cliente debe estar entre 5 y 600 segundos.")]
    public int SegundosTiempoEsperaCliente { get; set; } = 60;

    /// <summary>Intervalo, en segundos, entre latidos («keep-alive») enviados por el servidor.</summary>
    [Range(1, 300, ErrorMessage = "El intervalo de latido debe estar entre 1 y 300 segundos.")]
    public int SegundosIntervaloLatido { get; set; } = 15;

    /// <summary>Segundos máximos permitidos para completar la negociación inicial.</summary>
    [Range(1, 120, ErrorMessage = "El tiempo de negociación debe estar entre 1 y 120 segundos.")]
    public int SegundosTiempoNegociacion { get; set; } = 15;

    /// <summary>Tamaño máximo, en bytes, de un mensaje entrante del cliente.</summary>
    [Range(1024, 1048576, ErrorMessage = "El tamaño máximo de mensaje debe estar entre 1 KiB y 1 MiB.")]
    public int TamanoMaximoMensajeBytes { get; set; } = 32 * 1024;

    /// <summary>Incluye detalles de excepción en los errores enviados al cliente (solo desarrollo).</summary>
    public bool DetallarErrores { get; set; }

    /// <summary>Número máximo de mensajes que un usuario puede enviar por minuto.</summary>
    [Range(1, 600, ErrorMessage = "El límite de mensajes por minuto debe estar entre 1 y 600.")]
    public int MaximoMensajesPorMinuto { get; set; } = 60;
}
