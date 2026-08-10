using Chat.AdminCli.Servicios;

namespace Chat.AdminCli.Comandos.Salas;

/// <summary>
/// Comando <c>salas listar</c>: muestra todas las salas de la plataforma, incluidas
/// las privadas y las conversaciones directas.
/// </summary>
public sealed class ComandoSalasListar : ComandoAdminBase<OpcionesVacias>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoSalasListar(ClienteAdminApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(OpcionesVacias opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Salas de {Api.UrlServidor}");

        var salas = await ConEsperaAsync(
            "Consultando salas...",
            () => Api.ListarSalasAsync(cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.TablaSalas(salas);
        return CodigoExito;
    }
}
