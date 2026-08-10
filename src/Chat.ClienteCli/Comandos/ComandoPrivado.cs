using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Abre una conversación privada con otra persona. Si ya existe se retoma la misma:
/// nunca se crean dos conversaciones entre los mismos interlocutores.
/// </summary>
public sealed class ComandoPrivado : ComandoBase<ComandoPrivado.Opciones>
{
    private readonly ClienteApi _api;
    private readonly VistaConversacion _vista;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="vista">Vista de conversación en vivo.</param>
    public ComandoPrivado(ClienteApi api, VistaConversacion vista)
    {
        _api = api;
        _vista = vista;
    }

    /// <summary>Opciones del comando <c>privado</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre del interlocutor.</summary>
        [CommandArgument(0, "<usuario>")]
        [Description("Nombre de la persona con la que se quiere hablar.")]
        public string Usuario { get; init; } = string.Empty;

        /// <summary>Envía un único mensaje sin abrir la conversación en vivo.</summary>
        [CommandOption("-m|--mensaje")]
        [Description("Envía este mensaje y termina, sin abrir la conversación en vivo.")]
        public string? Mensaje { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        var sesion = await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);

        var conversacion = await _api
            .AbrirConversacionDirectaAsync(opciones.Usuario, cancelacion)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(opciones.Mensaje))
        {
            await _api.EnviarMensajeAsync(conversacion.Id, opciones.Mensaje, cancelacion).ConfigureAwait(false);
            Presentacion.Exito($"Mensaje enviado a {conversacion.Nombre}.");
            return CodigoExito;
        }

        await _vista.AbrirAsync(sesion, conversacion, cancelacion).ConfigureAwait(false);
        return CodigoExito;
    }
}
