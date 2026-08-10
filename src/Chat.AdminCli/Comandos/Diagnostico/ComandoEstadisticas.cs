using Chat.AdminCli.Servicios;

namespace Chat.AdminCli.Comandos.Diagnostico;

/// <summary>Comando <c>estadisticas</c>: muestra un resumen de actividad de la plataforma.</summary>
public sealed class ComandoEstadisticas : ComandoAdminBase<OpcionesVacias>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoEstadisticas(ClienteAdminApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(OpcionesVacias opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Resumen de {Api.UrlServidor}");

        // Las dos consultas se piden a la vez bajo un único indicador de espera.
        var (estadisticas, presencia) = await ConEsperaAsync(
            "Recopilando métricas...",
            async () =>
            {
                var resumen = Api.ObtenerEstadisticasAsync(cancelacion);
                var conectados = Api.ObtenerPresenciaAsync(cancelacion);

                return (await resumen.ConfigureAwait(false), await conectados.ConfigureAwait(false));
            }).ConfigureAwait(false);

        PresentacionAdmin.PanelEstadisticas(estadisticas);
        PresentacionAdmin.TablaPresencia(presencia);

        return CodigoExito;
    }
}
