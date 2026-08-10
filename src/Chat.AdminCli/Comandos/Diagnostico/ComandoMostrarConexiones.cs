using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Diagnostico;

/// <summary>
/// Comando <c>mostrar-conexiones</c>: lista las conexiones SignalR abiertas.
/// Con <c>--seguir</c> refresca la tabla periódicamente hasta pulsar Ctrl+C.
/// </summary>
public sealed class ComandoMostrarConexiones : ComandoAdminBase<ComandoMostrarConexiones.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoMostrarConexiones(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Refresca la vista de forma continua.</summary>
        [CommandOption("-s|--seguir")]
        [Description("Refresca la lista periódicamente hasta pulsar Ctrl+C.")]
        public bool Seguir { get; init; }

        /// <summary>Segundos entre refrescos.</summary>
        [CommandOption("-i|--intervalo")]
        [Description("Segundos entre refrescos cuando se usa --seguir.")]
        [DefaultValue(3)]
        public int Intervalo { get; init; } = 3;
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        if (!opciones.Seguir)
        {
            PresentacionAdmin.Cabecera($"Conexiones activas en {Api.UrlServidor}");

            var conexiones = await ConEsperaAsync(
                "Consultando conexiones...",
                () => Api.ObtenerConexionesAsync(cancelacion)).ConfigureAwait(false);

            PresentacionAdmin.TablaConexiones(conexiones);
            return CodigoExito;
        }

        return await SeguirAsync(Math.Clamp(opciones.Intervalo, 1, 60), cancelacion).ConfigureAwait(false);
    }

    /// <summary>Refresca la tabla de conexiones hasta que se cancele la operación.</summary>
    /// <param name="intervalo">Segundos entre refrescos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<int> SeguirAsync(int intervalo, CancellationToken cancelacion)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(intervalo));

        do
        {
            // Sin indicador de espera: el propio refresco periódico ya informa de
            // que la vista está viva, y encadenar pantallas activas daría problemas.
            var conexiones = await Api.ObtenerConexionesAsync(cancelacion).ConfigureAwait(false);

            PresentacionAdmin.LimpiarPantalla();
            PresentacionAdmin.Cabecera(
                $"Conexiones activas en {Api.UrlServidor} · refresco cada {intervalo}s · Ctrl+C para salir");
            PresentacionAdmin.TablaConexiones(conexiones);
        }
        while (await temporizador.WaitForNextTickAsync(cancelacion).ConfigureAwait(false));

        return CodigoExito;
    }
}
