using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Administracion;
using Chat.Aplicacion.Comandos.Usuarios;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Comandos;

/// <summary>Pruebas de las operaciones administrativas sobre cuentas y caché.</summary>
public sealed class PruebasAdministracion
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IServicioIdentidad _identidad = Substitute.For<IServicioIdentidad>();
    private readonly CacheDePrueba _cache = new();

    private readonly Guid _administradorId = Guid.CreateVersion7();

    private ManejadorEliminarUsuario ManejadorBorrado => new(
        _usuarios,
        _identidad,
        _cache,
        NullLogger<ManejadorEliminarUsuario>.Instance);

    private ManejadorLimpiarCache ManejadorCache => new(_cache, NullLogger<ManejadorLimpiarCache>.Instance);

    [Fact]
    public async Task EliminarUnaCuentaLaBorraEInvalidaUsuariosYSalas()
    {
        // Hay que tirar también el catálogo de salas: sus membresías se van con la cuenta.
        var usuario = Datos.Usuario(nombre: "eva");
        _usuarios.ObtenerPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);
        _identidad.EliminarUsuarioAsync(usuario, Arg.Any<CancellationToken>()).Returns(ResultadoIdentidad.Correcto);

        var resultado = await ManejadorBorrado.ManejarAsync(
            new ComandoEliminarUsuario(usuario.Id, _administradorId));

        Assert.True(resultado.Exito);
        Assert.Contains("eva", resultado.Mensaje, StringComparison.Ordinal);
        Assert.Contains(ClavesCache.EtiquetaUsuarios, _cache.EtiquetasInvalidadas);
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeBorrarseASiMismo()
    {
        // Evita quedarse sin ninguna cuenta capaz de administrar la plataforma.
        await Assert.ThrowsAsync<ExcepcionConflicto>(() => ManejadorBorrado.ManejarAsync(
            new ComandoEliminarUsuario(_administradorId, _administradorId)));

        await _identidad.DidNotReceiveWithAnyArgs().EliminarUsuarioAsync(default!);
    }

    [Fact]
    public async Task NoSePuedeBorrarUnaCuentaQueNoExiste()
    {
        var usuarioId = Guid.CreateVersion7();
        _usuarios.ObtenerPorIdAsync(usuarioId, Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => ManejadorBorrado.ManejarAsync(
            new ComandoEliminarUsuario(usuarioId, _administradorId)));
    }

    [Fact]
    public async Task UnFalloDeIdentitySeTraduceEnConflicto()
    {
        var usuario = Datos.Usuario();
        _usuarios.ObtenerPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);
        _identidad
            .EliminarUsuarioAsync(usuario, Arg.Any<CancellationToken>())
            .Returns(ResultadoIdentidad.Fallido("Hay datos que dependen de la cuenta."));

        var excepcion = await Assert.ThrowsAsync<ExcepcionConflicto>(() => ManejadorBorrado.ManejarAsync(
            new ComandoEliminarUsuario(usuario.Id, _administradorId)));

        Assert.Contains("Hay datos que dependen", excepcion.Message, StringComparison.Ordinal);
        Assert.Empty(_cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task UnIdentificadorVacioSeRechazaAlBorrarUnaCuenta()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => ManejadorBorrado.ManejarAsync(
            new ComandoEliminarUsuario(Guid.Empty, _administradorId)));

    [Fact]
    public async Task LimpiarLaCacheLaVaciaPorCompleto()
    {
        await _cache.EstablecerAsync("salas:lista", "algo");

        var resultado = await ManejadorCache.ManejarAsync(new ComandoLimpiarCache(_administradorId));

        Assert.True(resultado.Exito);
        Assert.Equal(1, _cache.Vaciados);
        Assert.False(_cache.Contiene("salas:lista"));
    }

    [Fact]
    public async Task UnComandoNuloSeRechaza()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ManejadorCache.ManejarAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ManejadorBorrado.ManejarAsync(null!));
    }
}
