using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Constantes;
using Chat.Infraestructura.Presencia;
using Chat.Infraestructura.Seguridad;
using Chat.Infraestructura.Tiempo;
using Chat.Tests.Comun;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Tests.Infraestructura;

/// <summary>Pruebas del generador de tokens de acceso y de refresco.</summary>
public sealed class PruebasGeneradorTokensJwt
{
    private readonly RelojFijo _reloj = new();

    private GeneradorTokensJwt Generador(int minutos = 30) => new(Opciones.De(Opciones.Jwt(minutos)), _reloj);

    [Fact]
    public async Task ElTokenEmitidoSuperaLaValidacionCompleta()
    {
        var usuario = Datos.Usuario();
        var token = Generador().GenerarTokenAcceso(usuario, [RolesDelSistema.Usuario]);

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Valor, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "dotchat-pruebas",
            ValidateAudience = true,
            ValidAudience = "dotchat-clientes",
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GeneradorTokensJwt.ConstruirClave(Opciones.ClaveFirmaBase64),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        });

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void ElTokenLlevaSujetoNombreCorreoYRoles()
    {
        var usuario = Datos.Usuario();

        var token = Generador().GenerarTokenAcceso(usuario, [RolesDelSistema.Administrador, RolesDelSistema.Usuario]);
        var leido = new JsonWebTokenHandler().ReadJsonWebToken(token.Valor);

        Assert.Equal(usuario.Id.ToString(), leido.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("ana", leido.GetClaim(JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal("ana@dotchat.local", leido.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(token.Identificador.ToString(), leido.GetClaim(JwtRegisteredClaimNames.Jti).Value);

        var roles = leido.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Contains(RolesDelSistema.Administrador, roles);
        Assert.Contains(RolesDelSistema.Usuario, roles);
    }

    [Fact]
    public void LaExpiracionSaleDelRelojYDeLaConfiguracion()
    {
        var token = Generador(minutos: 45).GenerarTokenAcceso(Datos.Usuario(), []);

        Assert.Equal(_reloj.Ahora.AddMinutes(45), token.ExpiraEn);
    }

    [Fact]
    public void CadaTokenLlevaSuPropioIdentificador()
    {
        var usuario = Datos.Usuario();
        var generador = Generador();

        Assert.NotEqual(
            generador.GenerarTokenAcceso(usuario, []).Identificador,
            generador.GenerarTokenAcceso(usuario, []).Identificador);
    }

    [Fact]
    public void UnUsuarioSinNombreNiCorreoNoImpideEmitirElToken()
    {
        var anonimo = new Chat.Dominio.Entidades.Usuario { Id = Guid.CreateVersion7() };

        var token = Generador().GenerarTokenAcceso(anonimo, []);

        Assert.False(string.IsNullOrEmpty(token.Valor));
    }

    [Fact]
    public void LosArgumentosNulosSeRechazan()
    {
        var generador = Generador();

        Assert.Throws<ArgumentNullException>(() => generador.GenerarTokenAcceso(null!, []));
        Assert.Throws<ArgumentNullException>(() => generador.GenerarTokenAcceso(Datos.Usuario(), null!));
    }

    [Fact]
    public void ElTokenDeRefrescoTieneEntropiaSuficienteYNoSeRepite()
    {
        var generador = Generador();
        var emitidos = Enumerable.Range(0, 100).Select(_ => generador.GenerarTokenRefresco()).ToArray();

        Assert.Equal(100, emitidos.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(32, Base64UrlEncoder.DecodeBytes(emitidos[0]).Length);
    }

    [Fact]
    public void ElHashDelRefrescoEsEstableYCoincideConSha256()
    {
        // Es lo que se persiste: tiene que ser reproducible para poder buscar el token.
        var generador = Generador();
        var esperado = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("un-token")));

        Assert.Equal(esperado, generador.CalcularHashRefresco("un-token"));
        Assert.Equal(esperado, generador.CalcularHashRefresco("un-token"));
        Assert.NotEqual(esperado, generador.CalcularHashRefresco("otro-token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnTokenVacioNoTieneHash(string? token)
        => Assert.ThrowsAny<ArgumentException>(() => Generador().CalcularHashRefresco(token!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinClaveDeFirmaNoSePuedeConstruirLaClave(string? clave)
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() => GeneradorTokensJwt.ConstruirClave(clave!));

        Assert.Contains("Jwt:ClaveFirmaBase64", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaClaveDeFirmaQueNoEsBase64SeRechaza()
        => Assert.Throws<InvalidOperationException>(() => GeneradorTokensJwt.ConstruirClave("no-base64-!!"));

    [Fact]
    public void UnaClaveDeFirmaCortaSeRechaza()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => GeneradorTokensJwt.ConstruirClave(Convert.ToBase64String(new byte[16])));

        Assert.Contains("256 bits", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LaClaveDeFirmaGeneradaEsValidaYDistintaCadaVez()
    {
        var clave = GeneradorTokensJwt.GenerarClaveFirmaBase64();

        Assert.Equal(256, GeneradorTokensJwt.ConstruirClave(clave).KeySize);
        Assert.NotEqual(clave, GeneradorTokensJwt.GenerarClaveFirmaBase64());
    }
}

/// <summary>Pruebas de la protección contra repetición.</summary>
public sealed class PruebasProtectorRepeticion
{
    private readonly CacheDePrueba _cache = new();

    private ProtectorRepeticion Protector => new(_cache, Opciones.De(Opciones.Cache()));

    [Fact]
    public async Task LaPrimeraVezUnIdentificadorEsNuevo()
        => Assert.True(await Protector.RegistrarSiEsNuevoAsync("mensaje", "abc"));

    [Fact]
    public async Task ElMismoIdentificadorNoSeAceptaDosVeces()
    {
        var protector = Protector;

        Assert.True(await protector.RegistrarSiEsNuevoAsync("mensaje", "abc"));
        Assert.False(await protector.RegistrarSiEsNuevoAsync("mensaje", "abc"));
        Assert.False(await protector.RegistrarSiEsNuevoAsync("mensaje", "abc"));
    }

    [Fact]
    public async Task LosAmbitosNoSePisanEntreSi()
    {
        var protector = Protector;

        Assert.True(await protector.RegistrarSiEsNuevoAsync("mensaje", "abc"));
        Assert.True(await protector.RegistrarSiEsNuevoAsync("adjunto", "abc"));
    }

    [Fact]
    public async Task ElRegistroSeGuardaBajoLaClaveDeRepeticion()
    {
        await Protector.RegistrarSiEsNuevoAsync("mensaje", "abc");

        Assert.True(_cache.Contiene(Chat.Aplicacion.Abstracciones.ClavesCache.Repeticion("mensaje", "abc")));
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("mensaje", "")]
    [InlineData("   ", "abc")]
    public async Task LosArgumentosVaciosSeRechazan(string ambito, string identificador)
        => await Assert.ThrowsAnyAsync<ArgumentException>(
            () => Protector.RegistrarSiEsNuevoAsync(ambito, identificador));
}

/// <summary>Pruebas del limitador de envíos local al proceso.</summary>
public sealed class PruebasLimitadorEnviosMemoria
{
    [Fact]
    public async Task DentroDelCupoSeAceptanTodosLosEnvios()
    {
        using var limitador = new LimitadorEnviosMemoria(Opciones.De(Opciones.SignalR(3)));
        var usuarioId = Guid.CreateVersion7();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(await limitador.IntentarConsumirAsync(usuarioId));
        }
    }

    [Fact]
    public async Task PasadoElCupoSeRechazanLosEnvios()
    {
        using var limitador = new LimitadorEnviosMemoria(Opciones.De(Opciones.SignalR(2)));
        var usuarioId = Guid.CreateVersion7();

        await limitador.IntentarConsumirAsync(usuarioId);
        await limitador.IntentarConsumirAsync(usuarioId);

        Assert.False(await limitador.IntentarConsumirAsync(usuarioId));
    }

    [Fact]
    public async Task ElCupoEsDeCadaUsuarioYNoGlobal()
    {
        using var limitador = new LimitadorEnviosMemoria(Opciones.De(Opciones.SignalR(1)));
        var ana = Guid.CreateVersion7();
        var eva = Guid.CreateVersion7();

        Assert.True(await limitador.IntentarConsumirAsync(ana));
        Assert.False(await limitador.IntentarConsumirAsync(ana));
        Assert.True(await limitador.IntentarConsumirAsync(eva));
    }

    [Fact]
    public async Task UnLimitadorLiberadoDejaDeAtender()
    {
        var limitador = new LimitadorEnviosMemoria(Opciones.De(Opciones.SignalR()));
        limitador.Dispose();
        limitador.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => limitador.IntentarConsumirAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public void SinOpcionesNoSePuedeConstruir()
        => Assert.Throws<ArgumentNullException>(() => new LimitadorEnviosMemoria(null!));
}

/// <summary>Pruebas del registro de conexiones y presencia local al proceso.</summary>
public sealed class PruebasRegistroConexionesMemoria
{
    private readonly RegistroConexionesMemoria _registro = new();
    private readonly Guid _ana = Guid.CreateVersion7();
    private readonly Guid _eva = Guid.CreateVersion7();

    [Fact]
    public async Task LaPrimeraConexionDeUnUsuarioSeAnunciaComoTal()
    {
        // De ese valor depende que se avise a los demás de que acaba de conectarse.
        Assert.True(await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora));
        Assert.False(await _registro.RegistrarAsync("c2", _ana, "ana", Datos.Ahora));
    }

    [Fact]
    public async Task CerrarUnaConexionDeVariasNoDesconectaAlUsuario()
    {
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.RegistrarAsync("c2", _ana, "ana", Datos.Ahora);

        var cerrada = await _registro.EliminarAsync("c1", Datos.Ahora.AddMinutes(1));

        Assert.NotNull(cerrada);
        Assert.False(cerrada.FueLaUltima);
        Assert.True(await _registro.EstaConectadoAsync(_ana));
    }

    [Fact]
    public async Task CerrarLaUltimaConexionDesconectaAlUsuario()
    {
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);

        var cerrada = await _registro.EliminarAsync("c1", Datos.Ahora.AddMinutes(1));

        Assert.NotNull(cerrada);
        Assert.True(cerrada.FueLaUltima);
        Assert.Equal(_ana, cerrada.UsuarioId);
        Assert.Equal("ana", cerrada.NombreUsuario);
        Assert.False(await _registro.EstaConectadoAsync(_ana));
    }

    [Fact]
    public async Task CerrarUnaConexionDesconocidaNoDevuelveNada()
        => Assert.Null(await _registro.EliminarAsync("no-existe", Datos.Ahora));

    [Fact]
    public async Task ElVistoPorUltimaVezSobreviveALaDesconexion()
    {
        var desconexion = Datos.Ahora.AddMinutes(30);

        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.EliminarAsync("c1", desconexion);

        var presencia = Assert.Single(await _registro.ListarPresenciaAsync());

        Assert.False(presencia.EnLinea);
        Assert.Equal(desconexion, presencia.UltimaVez);
        Assert.Equal(0, presencia.Conexiones);
    }

    [Fact]
    public async Task LasSalasSeAsocianYSeDesasocianDeCadaConexion()
    {
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.AgregarSalaAsync("c1", "General");
        await _registro.AgregarSalaAsync("c1", "Equipo");
        await _registro.QuitarSalaAsync("c1", "General");

        var conexion = Assert.Single(await _registro.ListarAsync());

        Assert.Equal(["Equipo"], conexion.Salas);
    }

    [Fact]
    public async Task TocarLasSalasDeUnaConexionDesconocidaNoRompeNada()
    {
        await _registro.AgregarSalaAsync("no-existe", "General");
        await _registro.QuitarSalaAsync("no-existe", "General");

        Assert.Empty(await _registro.ListarAsync());
    }

    [Fact]
    public async Task LasConexionesSeListanOrdenadasPorAntiguedadYConSusSalas()
    {
        await _registro.RegistrarAsync("c2", _eva, "eva", Datos.Ahora.AddMinutes(5));
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.AgregarSalaAsync("c1", "zeta");
        await _registro.AgregarSalaAsync("c1", "alfa");

        var conexiones = await _registro.ListarAsync();

        Assert.Equal(["c1", "c2"], conexiones.Select(c => c.ConexionId));
        Assert.Equal(["alfa", "zeta"], conexiones[0].Salas);
    }

    [Fact]
    public async Task LosConectadosSeFiltranEnUnaSolaLlamada()
    {
        var ausente = Guid.CreateVersion7();
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);

        var conectados = await _registro.FiltrarConectadosAsync([_ana, _eva, ausente]);

        Assert.Equal([_ana], conectados);
    }

    [Fact]
    public async Task FiltrarUnConjuntoVacioDevuelveVacio()
        => Assert.Empty(await _registro.FiltrarConectadosAsync([]));

    [Fact]
    public async Task UnConjuntoNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _registro.FiltrarConectadosAsync(null!));

    [Fact]
    public async Task LasConexionesDeUnUsuarioSeRecuperanParaSuscribirlasAUnaSalaNueva()
    {
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.RegistrarAsync("c2", _ana, "ana", Datos.Ahora);
        await _registro.RegistrarAsync("c3", _eva, "eva", Datos.Ahora);

        var conexiones = await _registro.ConexionesDeAsync(_ana);

        Assert.Equal(2, conexiones.Count);
        Assert.Contains("c1", conexiones);
        Assert.Contains("c2", conexiones);
    }

    [Fact]
    public async Task LosRecuentosDistinguenConexionesDeUsuarios()
    {
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);
        await _registro.RegistrarAsync("c2", _ana, "ana", Datos.Ahora);
        await _registro.RegistrarAsync("c3", _eva, "eva", Datos.Ahora);

        Assert.Equal(3, await _registro.ContarConexionesAsync());
        Assert.Equal(2, await _registro.ContarUsuariosConectadosAsync());

        await _registro.EliminarAsync("c3", Datos.Ahora);

        Assert.Equal(2, await _registro.ContarConexionesAsync());
        Assert.Equal(1, await _registro.ContarUsuariosConectadosAsync());
    }

    [Fact]
    public async Task LaPresenciaPoneDelanteAQuienEstaEnLinea()
    {
        await _registro.RegistrarAsync("c1", _ana, "zeta", Datos.Ahora);
        await _registro.RegistrarAsync("c2", _eva, "alfa", Datos.Ahora);
        await _registro.EliminarAsync("c1", Datos.Ahora);

        var presencias = await _registro.ListarPresenciaAsync();

        Assert.Equal(["alfa", "zeta"], presencias.Select(p => p.NombreUsuario));
        Assert.True(presencias[0].EnLinea);
        Assert.False(presencias[1].EnLinea);
    }

    [Fact]
    public async Task EnUnSoloProcesoNoHayConexionesFantasmaQueLimpiar()
    {
        // Si este proceso cae, se lleva consigo todo el estado: no queda nada que retirar.
        await _registro.RegistrarAsync("c1", _ana, "ana", Datos.Ahora);

        Assert.Empty(await _registro.LatirYLimpiarAsync(TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnaConexionSinIdentificadorSeRechaza(string conexionId)
        => await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _registro.RegistrarAsync(conexionId, _ana, "ana", Datos.Ahora));

    [Fact]
    public async Task VariasReconexionesSimultaneasDejanElRecuentoCoherente()
    {
        // El registro se protege con un cerrojo justamente para esto: el contador por
        // usuario solo es fiable si se actualiza a la vez que la conexión que lo provoca.
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            _registro.RegistrarAsync($"c{i}", _ana, "ana", Datos.Ahora)));

        Assert.Equal(50, await _registro.ContarConexionesAsync());
        Assert.Equal(1, await _registro.ContarUsuariosConectadosAsync());

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            _registro.EliminarAsync($"c{i}", Datos.Ahora)));

        Assert.Equal(0, await _registro.ContarConexionesAsync());
        Assert.False(await _registro.EstaConectadoAsync(_ana));
    }
}

/// <summary>Pruebas del proveedor de fecha y hora y de la identidad de la réplica.</summary>
public sealed class PruebasTiempoEIdentidad
{
    [Fact]
    public void ElProveedorDevuelveLaHoraDelRelojQueSeLePase()
    {
        var instante = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var proveedor = new ProveedorFechaHoraSistema(new TimeProviderFijo(instante));

        Assert.Equal(instante, proveedor.Ahora);
    }

    [Fact]
    public void ElProveedorDelSistemaDevuelveLaHoraEnUtc()
    {
        var ahora = new ProveedorFechaHoraSistema().Ahora;

        Assert.Equal(TimeSpan.Zero, ahora.Offset);
        Assert.True(Math.Abs((DateTimeOffset.UtcNow - ahora).TotalMinutes) < 1);
    }

    [Fact]
    public void LaIdentidadDeLaReplicaSeTomaDelEntornoCuandoEstaDefinida()
    {
        var anterior = Environment.GetEnvironmentVariable(IdentidadReplica.VariableEntorno);

        try
        {
            Environment.SetEnvironmentVariable(IdentidadReplica.VariableEntorno, "replica-uno");

            var identidad = new IdentidadReplica();

            Assert.Equal("replica-uno", identidad.Nombre);
            Assert.StartsWith("replica-uno-", identidad.Id, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(IdentidadReplica.VariableEntorno, anterior);
        }
    }

    [Fact]
    public void SinVariableDeEntornoSeUsaElNombreDeLaMaquina()
    {
        var anterior = Environment.GetEnvironmentVariable(IdentidadReplica.VariableEntorno);

        try
        {
            Environment.SetEnvironmentVariable(IdentidadReplica.VariableEntorno, null);

            Assert.Equal(Environment.MachineName, new IdentidadReplica().Nombre);
        }
        finally
        {
            Environment.SetEnvironmentVariable(IdentidadReplica.VariableEntorno, anterior);
        }
    }

    [Fact]
    public void ElIdentificadorDeLaReplicaLlevaElProcesoParaNoHeredarConexionesFantasma()
    {
        // Dos arranques del mismo contenedor no deben compartir identidad: si la
        // compartieran, el proceso nuevo heredaría las conexiones muertas del anterior.
        var identidad = new IdentidadReplica();

        Assert.EndsWith($"-{Environment.ProcessId}", identidad.Id, StringComparison.Ordinal);
    }

    /// <summary>Proveedor de tiempo detenido, para comprobar que no se usa el reloj real.</summary>
    /// <param name="instante">Hora que devuelve siempre.</param>
    private sealed class TimeProviderFijo(DateTimeOffset instante) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
