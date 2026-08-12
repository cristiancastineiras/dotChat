using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Chat.Infraestructura;
using Chat.Infraestructura.Cache;
using Chat.Infraestructura.Persistencia;
using Chat.Tests.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace Chat.Tests.Infraestructura;

/// <summary>
/// Pruebas del adaptador sobre ASP.NET Core Identity, ejecutadas contra el almacén de
/// verdad: se comprueba que la contraseña se guarda como hash, que el bloqueo por
/// intentos fallidos funciona y que los roles se crean al vuelo.
/// </summary>
public sealed class PruebasServicioIdentidad : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();
    private readonly ServiceProvider _proveedor;
    private readonly IServiceScope _ambito;

    public PruebasServicioIdentidad()
    {
        var servicios = new ServiceCollection();

        servicios.AddLogging(constructor => constructor.SetMinimumLevel(LogLevel.None));

        // El contexto es el mismo de las pruebas de persistencia: SQLite en memoria con
        // el esquema ya creado, incluidas las tablas de Identity.
        servicios.AddDbContext<ContextoChat>(opciones => opciones
            .UseSqlite(_bd.Contexto.Database.GetDbConnection())
            .ReplaceService<IModelCustomizer, PersonalizadorNeutro>());

        servicios.AgregarIdentidad();

        _proveedor = servicios.BuildServiceProvider();
        _ambito = _proveedor.CreateScope();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ambito.Dispose();
        _proveedor.Dispose();
        _bd.Dispose();
    }

    private IServicioIdentidad Servicio => _ambito.ServiceProvider.GetRequiredService<IServicioIdentidad>();

    [Fact]
    public async Task UnaCuentaSeCreaConLaContrasenaGuardadaSoloComoHash()
    {
        var usuario = Usuario();

        var resultado = await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.True(resultado.Exito);
        Assert.NotNull(usuario.PasswordHash);
        Assert.DoesNotContain("Clave-Larga-1!", usuario.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnaContrasenaQueNoCumpleLaPoliticaSeRechazaConSusMotivos()
    {
        var resultado = await Servicio.CrearUsuarioAsync(Usuario(), "corta");

        Assert.False(resultado.Exito);
        Assert.NotEmpty(resultado.Errores);
    }

    [Fact]
    public async Task LaContrasenaCorrectaSeVerificaYLaIncorrectaNo()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.True(await Servicio.VerificarClaveAsync(usuario, "Clave-Larga-1!"));
        Assert.False(await Servicio.VerificarClaveAsync(usuario, "Clave-Larga-2!"));
    }

    [Fact]
    public async Task LosIntentosFallidosAcabanBloqueandoLaCuenta()
    {
        // Es el freno a la fuerza bruta: tras cinco fallos, ni la contraseña correcta
        // abre la cuenta hasta que pase el bloqueo.
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        for (var intento = 0; intento < 5; intento++)
        {
            Assert.False(await Servicio.VerificarClaveAsync(usuario, "no-es-la-clave"));
        }

        Assert.False(await Servicio.VerificarClaveAsync(usuario, "Clave-Larga-1!"));
    }

    [Fact]
    public async Task UnAciertoBorraLosIntentosFallidosAcumulados()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        await Servicio.VerificarClaveAsync(usuario, "mal");
        await Servicio.VerificarClaveAsync(usuario, "mal");
        Assert.True(await Servicio.VerificarClaveAsync(usuario, "Clave-Larga-1!"));

        // Al reiniciarse el contador, vuelve a haber cinco intentos por delante.
        for (var intento = 0; intento < 4; intento++)
        {
            await Servicio.VerificarClaveAsync(usuario, "mal");
        }

        Assert.True(await Servicio.VerificarClaveAsync(usuario, "Clave-Larga-1!"));
    }

    [Fact]
    public async Task AsignarUnRolLoCreaSiNoExistiaYEsIdempotente()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.True((await Servicio.AsignarRolAsync(usuario, RolesDelSistema.Usuario)).Exito);
        Assert.True((await Servicio.AsignarRolAsync(usuario, RolesDelSistema.Usuario)).Exito);

        Assert.Equal([RolesDelSistema.Usuario], await Servicio.ObtenerRolesAsync(usuario));
    }

    [Fact]
    public async Task UnUsuarioPuedeTenerVariosRoles()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        await Servicio.AsignarRolAsync(usuario, RolesDelSistema.Usuario);
        await Servicio.AsignarRolAsync(usuario, RolesDelSistema.Administrador);

        var roles = await Servicio.ObtenerRolesAsync(usuario);

        Assert.Equal(2, roles.Count);
        Assert.Contains(RolesDelSistema.Administrador, roles);
    }

    [Fact]
    public async Task UnUsuarioReciénCreadoNoTieneRoles()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.Empty(await Servicio.ObtenerRolesAsync(usuario));
    }

    [Fact]
    public async Task ElCorreoYaRegistradoSeDetecta()
    {
        var usuario = Usuario("ana", "ana@dotchat.local");
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.True(await Servicio.ExisteEmailAsync("ana@dotchat.local"));
        Assert.False(await Servicio.ExisteEmailAsync("otra@dotchat.local"));
    }

    [Fact]
    public async Task EliminarUnaCuentaLaBorraDelAlmacen()
    {
        var usuario = Usuario();
        await Servicio.CrearUsuarioAsync(usuario, "Clave-Larga-1!");

        Assert.True((await Servicio.EliminarUsuarioAsync(usuario)).Exito);
        Assert.False(await Servicio.ExisteEmailAsync(usuario.Email!));
    }

    [Fact]
    public async Task UnaOperacionCanceladaNoLlegaAEjecutarse()
    {
        using var origen = new CancellationTokenSource();
        await origen.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Servicio.CrearUsuarioAsync(Usuario(), "Clave-Larga-1!", origen.Token));
    }

    /// <summary>Crea un usuario sin normalizar: de eso ya se encarga Identity.</summary>
    /// <param name="nombre">Nombre de usuario.</param>
    /// <param name="email">Correo electrónico.</param>
    private static Usuario Usuario(string nombre = "ana", string? email = null) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = nombre,
        Email = email ?? $"{nombre}-{Guid.NewGuid():N}@dotchat.local",
        FechaCreacion = Datos.Ahora
    };

    /// <summary>
    /// Personalizador que solo convierte las fechas para SQLite, igual que el de las
    /// pruebas de persistencia.
    /// </summary>
    /// <param name="dependencias">Dependencias que inyecta EF Core.</param>
    private sealed class PersonalizadorNeutro(ModelCustomizerDependencies dependencias)
        : RelationalModelCustomizer(dependencias)
    {
        /// <inheritdoc />
        public override void Customize(ModelBuilder constructor, DbContext contexto)
        {
            base.Customize(constructor, contexto);

            ArgumentNullException.ThrowIfNull(constructor);

            foreach (var entidad in constructor.Model.GetEntityTypes())
            {
                foreach (var propiedad in entidad.GetProperties())
                {
                    if (propiedad.ClrType == typeof(DateTimeOffset))
                    {
                        propiedad.SetValueConverter(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
                                valor => valor.UtcTicks,
                                valor => new DateTimeOffset(valor, TimeSpan.Zero)));
                    }
                    else if (propiedad.ClrType == typeof(DateTimeOffset?))
                    {
                        propiedad.SetValueConverter(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset?, long?>(
                                valor => valor.HasValue ? valor.Value.UtcTicks : null,
                                valor => valor.HasValue ? new DateTimeOffset(valor.Value, TimeSpan.Zero) : null));
                    }
                }
            }
        }
    }
}

/// <summary>
/// Pruebas del adaptador de caché sobre FusionCache, con una instancia real en memoria.
/// </summary>
public sealed class PruebasServicioCacheFusion : IDisposable
{
    private readonly FusionCache _fusion = new(new FusionCacheOptions());
    private readonly ServicioCacheFusion _cache;

    public PruebasServicioCacheFusion()
        => _cache = new ServicioCacheFusion(_fusion, Opciones.De(new CacheOptions()));

    /// <inheritdoc />
    public void Dispose() => _fusion.Dispose();

    [Fact]
    public async Task ElValorSeGeneraUnaVezYLasSiguientesSalenDeLaCache()
    {
        var generaciones = 0;

        var primera = await _cache.ObtenerOCrearAsync("salas:lista", _ =>
        {
            generaciones++;
            return Task.FromResult("valor");
        });

        var segunda = await _cache.ObtenerOCrearAsync("salas:lista", _ =>
        {
            generaciones++;
            return Task.FromResult("otro");
        });

        Assert.Equal("valor", primera);
        Assert.Equal("valor", segunda);
        Assert.Equal(1, generaciones);
    }

    [Fact]
    public async Task LoGuardadoSeRecuperaYLoAusenteDevuelveElValorPorDefecto()
    {
        await _cache.EstablecerAsync("usuarios:ficha:1", 42);

        Assert.Equal(42, await _cache.ObtenerAsync<int>("usuarios:ficha:1"));
        Assert.Equal(0, await _cache.ObtenerAsync<int>("usuarios:ficha:2"));
        Assert.Null(await _cache.ObtenerAsync<string>("usuarios:ficha:3"));
    }

    [Fact]
    public async Task InvalidarUnaClaveRetiraSoloEsaEntrada()
    {
        await _cache.EstablecerAsync("salas:lista", "a");
        await _cache.EstablecerAsync("salas:ficha:1", "b");

        await _cache.InvalidarAsync("salas:lista");

        Assert.Null(await _cache.ObtenerAsync<string>("salas:lista"));
        Assert.Equal("b", await _cache.ObtenerAsync<string>("salas:ficha:1"));
    }

    [Fact]
    public async Task InvalidarPorEtiquetaSeLlevaTodoElAgregado()
    {
        // Las etiquetas se deducen del prefijo de la clave: por eso invalidar «salas»
        // basta para tirar el catálogo y todas las fichas de una vez.
        await _cache.EstablecerAsync("salas:lista", "a");
        await _cache.EstablecerAsync("salas:ficha:1", "b");
        await _cache.EstablecerAsync("usuarios:lista:activos", "c");

        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas);

        Assert.Null(await _cache.ObtenerAsync<string>("salas:lista"));
        Assert.Null(await _cache.ObtenerAsync<string>("salas:ficha:1"));
        Assert.Equal("c", await _cache.ObtenerAsync<string>("usuarios:lista:activos"));
    }

    [Fact]
    public async Task LimpiarLoVaciaTodo()
    {
        await _cache.EstablecerAsync("salas:lista", "a");
        await _cache.EstablecerAsync("usuarios:lista:activos", "c");

        await _cache.LimpiarTodoAsync();

        Assert.Null(await _cache.ObtenerAsync<string>("salas:lista"));
        Assert.Null(await _cache.ObtenerAsync<string>("usuarios:lista:activos"));
    }

    [Fact]
    public async Task UnaClaveSinAmbitoConocidoNoSeVeAfectadaPorLasEtiquetas()
    {
        await _cache.EstablecerAsync("otracosa:1", "a");

        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaUsuarios);

        Assert.Equal("a", await _cache.ObtenerAsync<string>("otracosa:1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LasClavesVaciasSeRechazan(string clave)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _cache.ObtenerOCrearAsync(clave, _ => Task.FromResult("x")));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.EstablecerAsync(clave, "x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.ObtenerAsync<string>(clave));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.InvalidarAsync(clave));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.InvalidarPorEtiquetaAsync(clave));
    }

    [Fact]
    public async Task UnGeneradorNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => _cache.ObtenerOCrearAsync<string>("salas:lista", null!));
}

/// <summary>Pruebas de la configuración del cliente de Valkey.</summary>
public sealed class PruebasExtensionesValkey
{
    [Fact]
    public void LaConfiguracionTomaLosTiemposDeEsperaDeLasOpciones()
    {
        var configuracion = ExtensionesValkey.ConstruirConfiguracion(new ValkeyOptions
        {
            Conexion = "valkey:6379",
            MilisegundosTiempoEspera = 1500
        });

        Assert.Equal(1500, configuracion.ConnectTimeout);
        Assert.Equal(1500, configuracion.SyncTimeout);
        Assert.Equal("dotchat", configuracion.ClientName);
    }

    [Fact]
    public void ArrancarSinValkeyLevantadaNoDebeImpedirArrancarElServidor()
    {
        // El cliente reintenta en segundo plano; las operaciones fallan mientras tanto,
        // que es un fallo mucho más manejable que no arrancar.
        var configuracion = ExtensionesValkey.ConstruirConfiguracion(new ValkeyOptions());

        Assert.False(configuracion.AbortOnConnectFail);
    }

    [Fact]
    public void LaCadenaDeConexionSeInterpretaTalCual()
    {
        var configuracion = ExtensionesValkey.ConstruirConfiguracion(new ValkeyOptions
        {
            Conexion = "servidor-a:6379,servidor-b:6380"
        });

        Assert.Equal(2, configuracion.EndPoints.Count);
    }

    [Fact]
    public void LasOpcionesNulasSeRechazan()
        => Assert.Throws<ArgumentNullException>(() => ExtensionesValkey.ConstruirConfiguracion(null!));

    [Fact]
    public void ElPrefijoNombraElCanalYLasClavesDelSegundoNivel()
    {
        var opciones = new ValkeyOptions { Prefijo = "entorno-pruebas" };

        Assert.Equal("entorno-pruebas:backplane", opciones.CanalRetropropagacion());
        Assert.Equal("entorno-pruebas:", opciones.PrefijoClaves());
    }

    [Fact]
    public void ElCanalDeRetropropagacionSeConstruyeComoCanalLiteral()
    {
        // Comprobación de tipo: si se compusiera como patrón, cada réplica recibiría
        // mensajes que no le corresponden.
        var canal = RedisChannel.Literal(new ValkeyOptions().CanalRetropropagacion());

        Assert.Equal("dotchat:backplane", canal.ToString());
    }
}
