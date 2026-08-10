using Chat.AdminCli.Servicios;

namespace Chat.AdminCli.Comandos.Usuarios;

/// <summary>Comando <c>usuarios listar</c>: muestra todas las cuentas registradas.</summary>
public sealed class ComandoUsuariosListar : ComandoAdminBase<OpcionesVacias>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoUsuariosListar(ClienteAdminApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(OpcionesVacias opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Usuarios de {Api.UrlServidor}");

        var usuarios = await ConEsperaAsync(
            "Consultando usuarios...",
            () => Api.ListarUsuariosAsync(cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.TablaUsuarios(usuarios);
        return CodigoExito;
    }
}
