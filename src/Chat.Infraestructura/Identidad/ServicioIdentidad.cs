using Chat.Aplicacion.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.AspNetCore.Identity;

namespace Chat.Infraestructura.Identidad;

/// <summary>
/// Adaptador sobre <see cref="UserManager{TUser}"/> y <see cref="RoleManager{TRole}"/>.
/// Concentra aquí toda la dependencia de ASP.NET Core Identity para que la capa de
/// aplicación trabaje contra <see cref="IServicioIdentidad"/>.
/// </summary>
public sealed class ServicioIdentidad : IServicioIdentidad
{
    private readonly UserManager<Usuario> _gestorUsuarios;
    private readonly RoleManager<Rol> _gestorRoles;

    /// <summary>Crea el servicio.</summary>
    /// <param name="gestorUsuarios">Gestor de usuarios de Identity.</param>
    /// <param name="gestorRoles">Gestor de roles de Identity.</param>
    public ServicioIdentidad(UserManager<Usuario> gestorUsuarios, RoleManager<Rol> gestorRoles)
    {
        _gestorUsuarios = gestorUsuarios;
        _gestorRoles = gestorRoles;
    }

    /// <inheritdoc />
    public async Task<ResultadoIdentidad> CrearUsuarioAsync(
        Usuario usuario,
        string clave,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();

        // Identity calcula y almacena el hash de la contraseña (PBKDF2 con sal
        // aleatoria); el valor en claro nunca se persiste.
        var resultado = await _gestorUsuarios.CreateAsync(usuario, clave).ConfigureAwait(false);
        return Traducir(resultado);
    }

    /// <inheritdoc />
    public async Task<ResultadoIdentidad> AsignarRolAsync(
        Usuario usuario,
        string rol,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();

        if (!await _gestorRoles.RoleExistsAsync(rol).ConfigureAwait(false))
        {
            var creacion = await _gestorRoles.CreateAsync(new Rol(rol)).ConfigureAwait(false);
            if (!creacion.Succeeded)
            {
                return Traducir(creacion);
            }
        }

        if (await _gestorUsuarios.IsInRoleAsync(usuario, rol).ConfigureAwait(false))
        {
            return ResultadoIdentidad.Correcto;
        }

        return Traducir(await _gestorUsuarios.AddToRoleAsync(usuario, rol).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<bool> VerificarClaveAsync(
        Usuario usuario,
        string clave,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();

        if (await _gestorUsuarios.IsLockedOutAsync(usuario).ConfigureAwait(false))
        {
            return false;
        }

        if (await _gestorUsuarios.CheckPasswordAsync(usuario, clave).ConfigureAwait(false))
        {
            await _gestorUsuarios.ResetAccessFailedCountAsync(usuario).ConfigureAwait(false);
            return true;
        }

        // Cada fallo incrementa el contador; al alcanzar el máximo, Identity bloquea
        // la cuenta temporalmente y frena los ataques por fuerza bruta.
        await _gestorUsuarios.AccessFailedAsync(usuario).ConfigureAwait(false);
        return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ObtenerRolesAsync(
        Usuario usuario,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        return [.. await _gestorUsuarios.GetRolesAsync(usuario).ConfigureAwait(false)];
    }

    /// <inheritdoc />
    public async Task<bool> ExisteEmailAsync(string email, CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        return await _gestorUsuarios.FindByEmailAsync(email).ConfigureAwait(false) is not null;
    }

    /// <inheritdoc />
    public async Task<ResultadoIdentidad> EliminarUsuarioAsync(
        Usuario usuario,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        return Traducir(await _gestorUsuarios.DeleteAsync(usuario).ConfigureAwait(false));
    }

    /// <summary>Convierte un <see cref="IdentityResult"/> al resultado de la capa de aplicación.</summary>
    /// <param name="resultado">Resultado devuelto por Identity.</param>
    private static ResultadoIdentidad Traducir(IdentityResult resultado)
        => resultado.Succeeded
            ? ResultadoIdentidad.Correcto
            : new ResultadoIdentidad(false, [.. resultado.Errors.Select(e => e.Description)]);
}
