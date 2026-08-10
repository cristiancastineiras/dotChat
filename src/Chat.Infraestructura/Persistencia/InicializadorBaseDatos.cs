using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura.Persistencia;

/// <summary>
/// Prepara la base de datos al arrancar: aplica las migraciones pendientes y
/// siembra los datos mínimos (roles, administrador inicial y sala por defecto).
/// Todas las operaciones son idempotentes.
/// </summary>
public sealed class InicializadorBaseDatos
{
    private readonly ContextoChat _contexto;
    private readonly UserManager<Usuario> _gestorUsuarios;
    private readonly RoleManager<Rol> _gestorRoles;
    private readonly IProveedorFechaHora _reloj;
    private readonly AdministradorOptions _opciones;
    private readonly ILogger<InicializadorBaseDatos> _registro;

    /// <summary>Crea el inicializador.</summary>
    public InicializadorBaseDatos(
        ContextoChat contexto,
        UserManager<Usuario> gestorUsuarios,
        RoleManager<Rol> gestorRoles,
        IProveedorFechaHora reloj,
        IOptions<AdministradorOptions> opciones,
        ILogger<InicializadorBaseDatos> registro)
    {
        _contexto = contexto;
        _gestorUsuarios = gestorUsuarios;
        _gestorRoles = gestorRoles;
        _reloj = reloj;
        _opciones = opciones.Value;
        _registro = registro;
    }

    /// <summary>Ejecuta migraciones y siembra los datos base.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task InicializarAsync(CancellationToken cancelacion = default)
    {
        await AplicarMigracionesAsync(cancelacion).ConfigureAwait(false);
        await SembrarRolesAsync().ConfigureAwait(false);
        var administrador = await SembrarAdministradorAsync().ConfigureAwait(false);
        await SembrarSalaInicialAsync(administrador, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Aplica las migraciones pendientes de EF Core.</summary>
    private async Task AplicarMigracionesAsync(CancellationToken cancelacion)
    {
        var pendientes = (await _contexto.Database.GetPendingMigrationsAsync(cancelacion).ConfigureAwait(false)).ToList();

        if (pendientes.Count == 0)
        {
            _registro.LogInformation("La base de datos ya está actualizada; no hay migraciones pendientes.");
            return;
        }

        _registro.LogInformation(
            "Aplicando {Total} migración(es) pendiente(s): {Migraciones}",
            pendientes.Count,
            string.Join(", ", pendientes));

        await _contexto.Database.MigrateAsync(cancelacion).ConfigureAwait(false);
    }

    /// <summary>Crea los roles del sistema si no existen.</summary>
    private async Task SembrarRolesAsync()
    {
        foreach (var rol in RolesDelSistema.Todos)
        {
            if (await _gestorRoles.RoleExistsAsync(rol).ConfigureAwait(false))
            {
                continue;
            }

            var resultado = await _gestorRoles.CreateAsync(new Rol(rol)).ConfigureAwait(false);
            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el rol '{rol}': {string.Join(" ", resultado.Errors.Select(e => e.Description))}");
            }

            _registro.LogInformation("Rol creado durante la inicialización. Rol={Rol}", rol);
        }
    }

    /// <summary>Crea el administrador inicial si procede y devuelve la cuenta existente o nueva.</summary>
    private async Task<Usuario?> SembrarAdministradorAsync()
    {
        var existente = await _gestorUsuarios.FindByNameAsync(_opciones.NombreUsuario).ConfigureAwait(false);
        if (existente is not null)
        {
            return existente;
        }

        if (string.IsNullOrWhiteSpace(_opciones.Clave))
        {
            _registro.LogWarning(
                "No se ha creado la cuenta de administrador porque 'Administrador:Clave' está vacía. " +
                "Ejecute 'scripts/configurar-secretos.ps1' o defina la variable de entorno correspondiente.");
            return null;
        }

        var administrador = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = _opciones.NombreUsuario,
            Email = _opciones.Email,
            EmailConfirmed = true,
            FechaCreacion = _reloj.Ahora,
            Activo = true
        };

        var creacion = await _gestorUsuarios.CreateAsync(administrador, _opciones.Clave).ConfigureAwait(false);
        if (!creacion.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo crear la cuenta de administrador inicial: " +
                string.Join(" ", creacion.Errors.Select(e => e.Description)));
        }

        foreach (var rol in RolesDelSistema.Todos)
        {
            await _gestorUsuarios.AddToRoleAsync(administrador, rol).ConfigureAwait(false);
        }

        _registro.LogInformation(
            "Cuenta de administrador inicial creada. UsuarioId={UsuarioId} NombreUsuario={NombreUsuario}",
            administrador.Id,
            administrador.UserName);

        return administrador;
    }

    /// <summary>Crea la sala por defecto para que la plataforma sea utilizable de inmediato.</summary>
    /// <param name="administrador">Cuenta que figura como creadora; puede ser nula.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task SembrarSalaInicialAsync(Usuario? administrador, CancellationToken cancelacion)
    {
        if (await _contexto.Salas.AnyAsync(cancelacion).ConfigureAwait(false))
        {
            return;
        }

        var ahora = _reloj.Ahora;
        var sala = new Sala
        {
            Id = Guid.CreateVersion7(),
            Nombre = _opciones.SalaInicial,
            Descripcion = "Sala general creada durante la inicialización de la plataforma.",
            FechaCreacion = ahora,
            CreadorId = administrador?.Id
        };

        _contexto.Salas.Add(sala);

        if (administrador is not null)
        {
            _contexto.MiembrosSala.Add(new MiembroSala
            {
                SalaId = sala.Id,
                UsuarioId = administrador.Id,
                FechaUnion = ahora
            });
        }

        await _contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        _registro.LogInformation("Sala inicial creada. SalaId={SalaId} Nombre={Nombre}", sala.Id, sala.Nombre);
    }
}
