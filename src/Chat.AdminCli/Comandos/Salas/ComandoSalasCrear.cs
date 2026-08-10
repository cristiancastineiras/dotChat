using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Salas;

/// <summary>Comando <c>salas crear</c>: da de alta una sala nueva.</summary>
public sealed class ComandoSalasCrear : ComandoAdminBase<ComandoSalasCrear.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoSalasCrear(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de la sala.</summary>
        [CommandArgument(0, "<nombre>")]
        [Description("Nombre de la sala (3-48 caracteres).")]
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Descripción opcional.</summary>
        [CommandOption("-d|--descripcion")]
        [Description("Descripción de la sala.")]
        public string? Descripcion { get; init; }

        /// <summary>Crea la sala como privada.</summary>
        [CommandOption("-p|--privada")]
        [Description("Crea la sala como privada: solo la verán quienes sean invitados.")]
        public bool Privada { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Creación de sala en {Api.UrlServidor}");

        var sala = await ConEsperaAsync(
            $"Creando la sala '{opciones.Nombre}'...",
            () => Api.CrearSalaAsync(opciones.Nombre, opciones.Descripcion, opciones.Privada, cancelacion))
            .ConfigureAwait(false);

        PresentacionAdmin.Exito($"Sala '{sala.Nombre}' creada con identificador {sala.Id}.");
        return CodigoExito;
    }
}
