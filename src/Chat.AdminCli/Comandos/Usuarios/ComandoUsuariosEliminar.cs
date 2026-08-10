using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Usuarios;

/// <summary>
/// Comando <c>usuarios eliminar</c>: borra definitivamente una cuenta y, en cascada,
/// sus mensajes, membresías y tokens. Pide confirmación salvo que se indique <c>--si</c>.
/// </summary>
public sealed class ComandoUsuariosEliminar : ComandoAdminBase<ComandoUsuariosEliminar.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoUsuariosEliminar(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre o identificador del usuario.</summary>
        [CommandArgument(0, "<usuario>")]
        [Description("Nombre de usuario o identificador (GUID) de la cuenta a eliminar.")]
        public string Usuario { get; init; } = string.Empty;

        /// <summary>Omite la confirmación interactiva.</summary>
        [CommandOption("-y|--si")]
        [Description("No pide confirmación (para uso en guiones).")]
        public bool SinConfirmar { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Eliminación de cuenta en {Api.UrlServidor}");

        var (usuarioId, nombre) = Guid.TryParse(opciones.Usuario, out var identificador)
            ? (identificador, opciones.Usuario)
            : await ResolverPorNombreAsync(opciones.Usuario, cancelacion).ConfigureAwait(false);

        // La confirmación se pide antes de arrancar el indicador de espera: dos
        // pantallas interactivas simultáneas abortarían la orden.
        if (!opciones.SinConfirmar
            && !AnsiConsole.Confirm(
                $"[red]¿Eliminar definitivamente la cuenta '{PresentacionAdmin.Escapar(nombre)}' y todos sus mensajes?[/]",
                false))
        {
            PresentacionAdmin.Aviso("Operación cancelada por el usuario.");
            return CodigoError;
        }

        var resultado = await ConEsperaAsync(
            "Eliminando la cuenta...",
            () => Api.EliminarUsuarioAsync(usuarioId, cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.Exito(resultado.Mensaje);
        return CodigoExito;
    }

    /// <summary>Resuelve el identificador de un usuario a partir de su nombre.</summary>
    /// <param name="nombre">Nombre buscado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<(Guid Id, string Nombre)> ResolverPorNombreAsync(string nombre, CancellationToken cancelacion)
    {
        var usuario = await Api.ResolverUsuarioAsync(nombre, cancelacion).ConfigureAwait(false);
        return (usuario.Id, usuario.NombreUsuario);
    }
}
