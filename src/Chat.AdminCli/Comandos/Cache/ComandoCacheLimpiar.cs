using Chat.AdminCli.Servicios;

namespace Chat.AdminCli.Comandos.Cache;

/// <summary>Comando <c>cache limpiar</c>: vacía por completo la caché del servidor.</summary>
public sealed class ComandoCacheLimpiar : ComandoAdminBase<OpcionesVacias>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoCacheLimpiar(ClienteAdminApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(OpcionesVacias opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Mantenimiento de caché en {Api.UrlServidor}");

        var resultado = await ConEsperaAsync(
            "Vaciando la caché...",
            () => Api.LimpiarCacheAsync(cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.Exito(resultado.Mensaje);
        return CodigoExito;
    }
}
