using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Chat.Servidor.Observabilidad;

/// <summary>
/// Instrumentación propia de la plataforma: una fuente de actividades para las
/// trazas y un medidor con los contadores del negocio.
/// </summary>
/// <remarks>
/// Se expone como estático porque <see cref="ActivitySource"/> y <see cref="Meter"/>
/// están pensados para vivir tanto como el proceso y son seguros para uso concurrente.
/// Cuando no hay ningún receptor escuchando, <c>StartActivity</c> devuelve <c>null</c> y
/// los contadores no hacen nada: el coste de dejar la instrumentación puesta es nulo.
/// </remarks>
public static class MedidasChat
{
    /// <summary>Nombre de la fuente de actividades; es el que se registra en el proveedor de trazas.</summary>
    public const string NombreFuente = "Chat.Servidor";

    /// <summary>Nombre del medidor; es el que se registra en el proveedor de métricas.</summary>
    public const string NombreMedidor = "Chat.Servidor";

    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    private static readonly Meter Medidor = new(NombreMedidor, Version);

    private static readonly Counter<long> MensajesEnviados = Medidor.CreateCounter<long>(
        "chat.mensajes.enviados",
        unit: "{mensaje}",
        description: "Mensajes aceptados y persistidos.");

    private static readonly Counter<long> MensajesRechazados = Medidor.CreateCounter<long>(
        "chat.mensajes.rechazados",
        unit: "{mensaje}",
        description: "Mensajes rechazados, desglosados por motivo.");

    private static readonly Histogram<int> LongitudMensajes = Medidor.CreateHistogram<int>(
        "chat.mensajes.longitud",
        unit: "{carácter}",
        description: "Distribución de la longitud de los mensajes enviados.");

    private static readonly UpDownCounter<long> ConexionesActivas = Medidor.CreateUpDownCounter<long>(
        "chat.conexiones.activas",
        unit: "{conexión}",
        description: "Conexiones en tiempo real abiertas en este momento.");

    private static readonly UpDownCounter<long> UsuariosEnLinea = Medidor.CreateUpDownCounter<long>(
        "chat.usuarios.en_linea",
        unit: "{usuario}",
        description: "Usuarios distintos con al menos una conexión abierta.");

    /// <summary>Fuente de actividades de la aplicación.</summary>
    public static ActivitySource Fuente { get; } = new(NombreFuente, Version);

    /// <summary>Anota un mensaje publicado correctamente.</summary>
    /// <param name="longitud">Número de caracteres del mensaje en claro.</param>
    public static void RegistrarMensajeEnviado(int longitud)
    {
        MensajesEnviados.Add(1);
        LongitudMensajes.Record(longitud);
    }

    /// <summary>Anota un mensaje rechazado.</summary>
    /// <param name="motivo">Causa del rechazo, usada como etiqueta de la métrica.</param>
    public static void RegistrarMensajeRechazado(string motivo)
        => MensajesRechazados.Add(1, new KeyValuePair<string, object?>("motivo", motivo));

    /// <summary>Anota una conexión nueva.</summary>
    /// <param name="usuarioPasaAEnLinea">Indica si era la primera conexión del usuario.</param>
    public static void RegistrarConexion(bool usuarioPasaAEnLinea)
    {
        ConexionesActivas.Add(1);

        if (usuarioPasaAEnLinea)
        {
            UsuariosEnLinea.Add(1);
        }
    }

    /// <summary>Anota el cierre de una conexión.</summary>
    /// <param name="usuarioPasaADesconectado">Indica si era la última conexión del usuario.</param>
    public static void RegistrarDesconexion(bool usuarioPasaADesconectado)
    {
        ConexionesActivas.Add(-1);

        if (usuarioPasaADesconectado)
        {
            UsuariosEnLinea.Add(-1);
        }
    }
}
