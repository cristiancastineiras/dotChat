using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Servicios;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Chat.Tests.Comun;
using NSubstitute;

namespace Chat.Tests.Aplicacion;

/// <summary>
/// Pruebas de las proyecciones a DTO. Se hacen a mano y en un único sitio justamente
/// para garantizar que no se escape ningún campo sensible.
/// </summary>
public sealed class PruebasProyecciones
{
    [Fact]
    public void UnUsuarioSeProyectaSinHashesNiSellos()
    {
        var usuario = Datos.Usuario();
        usuario.PasswordHash = "hash-que-no-debe-salir";
        usuario.FechaUltimoAcceso = Datos.Ahora;

        var dto = usuario.ADto(enLinea: true);

        Assert.Equal(usuario.Id, dto.Id);
        Assert.Equal("ana", dto.NombreUsuario);
        Assert.Equal("ana@dotchat.local", dto.Email);
        Assert.Equal(Datos.Ahora, dto.FechaUltimoAcceso);
        Assert.True(dto.Activo);
        Assert.True(dto.EnLinea);

        // El DTO solo tiene los campos declarados: no hay forma de que el hash viaje.
        Assert.DoesNotContain("hash-que-no-debe-salir", dto.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnUsuarioSinNombreNiCorreoSeProyectaConCadenasVacias()
    {
        var dto = new Usuario { UserName = null, Email = null }.ADto();

        Assert.Equal(string.Empty, dto.NombreUsuario);
        Assert.Equal(string.Empty, dto.Email);
        Assert.False(dto.EnLinea);
    }

    [Fact]
    public void UnaSalaSeProyectaConSuNombreSalvoQueSeIndiqueOtro()
    {
        var sala = Datos.Sala(nombre: "Equipo");

        Assert.Equal("Equipo", sala.ADto(3).Nombre);
        Assert.Equal("eva", sala.ADto(2, nombreVisible: "eva").Nombre);
    }

    [Fact]
    public void UnaSalaDirectaSePresentaConElNombreDelOtroParticipante()
    {
        var ana = Datos.Usuario(nombre: "ana");
        var eva = Datos.Usuario(nombre: "eva");
        var directa = Datos.SalaDirecta(ana, eva);

        Assert.Equal("eva", directa.NombreVisiblePara(ana.Id));
        Assert.Equal("ana", directa.NombreVisiblePara(eva.Id));
    }

    [Fact]
    public void UnaSalaNormalSePresentaIgualParaTodos()
    {
        var sala = Datos.Sala(nombre: "Equipo");

        Assert.Equal("Equipo", sala.NombreVisiblePara(Guid.CreateVersion7()));
    }

    [Fact]
    public void UnaDirectaConElInterlocutorBorradoSePresentaComoDesconocida()
    {
        var ana = Datos.Usuario(nombre: "ana");
        var directa = Datos.Sala(tipo: TipoSala.Directa, claveDirecta: "x:y");
        directa.Miembros.Add(Datos.Membresia(directa.Id, ana.Id, ana));

        Assert.Equal(Proyecciones.NombreDesconocido, directa.NombreVisiblePara(ana.Id));
    }

    [Fact]
    public void ElNombreDeSalaDeUnMensajeOcultaElIdentificadorInternoDeUnaDirecta()
    {
        // El nombre almacenado de una directa es un identificador interno; sacarlo
        // filtraría los identificadores de ambos participantes.
        var directa = Datos.Sala(nombre: "directa:x:y", tipo: TipoSala.Directa, claveDirecta: "x:y");
        var normal = Datos.Sala(nombre: "Equipo");

        Assert.Equal("ana", Proyecciones.NombreSalaEnMensaje(directa, "ana"));
        Assert.Equal("Equipo", Proyecciones.NombreSalaEnMensaje(normal, "ana"));
    }

    [Fact]
    public void UnMensajeSeProyectaConSuAdjuntoPeroNuncaConSuBinario()
    {
        var salaId = Guid.CreateVersion7();
        var usuarioId = Guid.CreateVersion7();
        var adjunto = Datos.Adjunto(salaId, usuarioId);
        var mensaje = Datos.Mensaje(salaId, usuarioId, adjuntoId: adjunto.Id);
        mensaje.Adjunto = adjunto;

        var dto = mensaje.ADto("hola", "General", "ana");

        Assert.Equal("hola", dto.Texto);
        Assert.Equal("General", dto.SalaNombre);
        Assert.Equal("ana", dto.NombreUsuario);
        Assert.NotNull(dto.Adjunto);
        Assert.Equal(adjunto.Id, dto.Adjunto.Id);
        Assert.Equal(640, dto.Adjunto.Ancho);
        Assert.True(dto.Adjunto.EsImagen);
    }

    [Fact]
    public void UnMensajeSinAdjuntoSeProyectaSinFicha()
    {
        var mensaje = Datos.Mensaje(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Null(mensaje.ADto("hola", "General", "ana").Adjunto);
    }

    [Fact]
    public void UnArchivoGenericoSeProyectaSinDimensiones()
    {
        var adjunto = Datos.Adjunto(Guid.CreateVersion7(), Guid.CreateVersion7(), tipo: TipoAdjunto.Archivo);

        var dto = adjunto.ADto();

        Assert.False(dto.EsImagen);
        Assert.Null(dto.Ancho);
        Assert.Null(dto.Alto);
    }

    [Fact]
    public void UnMiembroSinUsuarioCargadoSeProyectaComoDesconocido()
    {
        var miembro = Datos.Membresia(Guid.CreateVersion7(), Guid.CreateVersion7());

        var dto = miembro.ADto(enLinea: true, esCreador: false);

        Assert.Equal(Proyecciones.NombreDesconocido, dto.NombreUsuario);
        Assert.True(dto.EnLinea);
        Assert.False(dto.EsCreador);
    }

    [Fact]
    public void LasProyeccionesRechazanEntidadesNulas()
    {
        Assert.Throws<ArgumentNullException>(() => ((Mensaje)null!).ADto("x", "y", "z"));
        Assert.Throws<ArgumentNullException>(() => ((Adjunto)null!).ADto());
        Assert.Throws<ArgumentNullException>(() => ((Sala)null!).NombreVisiblePara(Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => Proyecciones.NombreSalaEnMensaje(null!, "ana"));
    }
}

/// <summary>Pruebas del emisor de sesiones.</summary>
public sealed class PruebasEmisorSesiones
{
    private readonly IGeneradorTokens _generador = Substitute.For<IGeneradorTokens>();
    private readonly IServicioIdentidad _identidad = Substitute.For<IServicioIdentidad>();
    private readonly IRepositorioTokensRefresco _tokens = Substitute.For<IRepositorioTokensRefresco>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly RelojFijo _reloj = new();

    private readonly Usuario _usuario = Datos.Usuario();

    public PruebasEmisorSesiones()
    {
        _identidad.ObtenerRolesAsync(_usuario, Arg.Any<CancellationToken>()).Returns([RolesDelSistema.Usuario]);
        _generador
            .GenerarTokenAcceso(_usuario, Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new TokenAcceso("jwt", _reloj.Ahora.AddMinutes(30), Guid.CreateVersion7()));
        _generador.GenerarTokenRefresco().Returns("refresco-en-claro");
        _generador.CalcularHashRefresco("refresco-en-claro").Returns("hash-del-refresco");
    }

    private EmisorSesiones Emisor => new(
        _generador,
        _identidad,
        _tokens,
        _unidadDeTrabajo,
        _reloj,
        Opciones.De(Opciones.Jwt()));

    [Fact]
    public async Task LaSesionLlevaElAccesoElRefrescoYLosRoles()
    {
        var sesion = await Emisor.EmitirAsync(_usuario);

        Assert.Equal(_usuario.Id, sesion.UsuarioId);
        Assert.Equal("ana", sesion.NombreUsuario);
        Assert.Equal("jwt", sesion.TokenAcceso);
        Assert.Equal("refresco-en-claro", sesion.TokenRefresco);
        Assert.Equal([RolesDelSistema.Usuario], sesion.Roles);
    }

    [Fact]
    public async Task DelTokenDeRefrescoSoloSePersisteSuHash()
    {
        // Es lo que hace que una filtración de la base de datos no permita reutilizarlo.
        await Emisor.EmitirAsync(_usuario);

        await _tokens.Received(1).AgregarAsync(
            Arg.Is<TokenRefresco>(t =>
                t.UsuarioId == _usuario.Id
                && t.HashToken == "hash-del-refresco"
                && t.FechaCreacion == _reloj.Ahora
                && t.FechaExpiracion == _reloj.Ahora.AddDays(7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaVigenciaDelRefrescoSaleDeLaConfiguracion()
    {
        var emisor = new EmisorSesiones(
            _generador, _identidad, _tokens, _unidadDeTrabajo, _reloj, Opciones.De(Opciones.Jwt(diasRefresco: 30)));

        await emisor.EmitirAsync(_usuario);

        await _tokens.Received(1).AgregarAsync(
            Arg.Is<TokenRefresco>(t => t.FechaExpiracion == _reloj.Ahora.AddDays(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirDejaConstanciaDelUltimoAccesoYConfirmaLosCambios()
    {
        Assert.Null(_usuario.FechaUltimoAcceso);

        await Emisor.EmitirAsync(_usuario);

        Assert.Equal(_reloj.Ahora, _usuario.FechaUltimoAcceso);
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LosRolesDelUsuarioViajanAlTokenDeAcceso()
    {
        _identidad
            .ObtenerRolesAsync(_usuario, Arg.Any<CancellationToken>())
            .Returns([RolesDelSistema.Administrador, RolesDelSistema.Usuario]);

        await Emisor.EmitirAsync(_usuario);

        _generador.Received(1).GenerarTokenAcceso(
            _usuario,
            Arg.Is<IReadOnlyCollection<string>>(roles => roles.Contains(RolesDelSistema.Administrador)));
    }

    [Fact]
    public async Task UnUsuarioNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Emisor.EmitirAsync(null!));
}

/// <summary>Pruebas de la configuración pública que se sirve a los clientes.</summary>
public sealed class PruebasServicioConfiguracionPlataforma
{
    private readonly CacheDePrueba _cache = new();

    [Fact]
    public async Task LaConfiguracionReuneLosValoresDeCadaSeccion()
    {
        var servicio = Construir();

        var configuracion = await servicio.ObtenerAsync();

        Assert.Equal("/hubs/chat", configuracion.RutaHub);
        Assert.Equal(2000, configuracion.LongitudMaximaMensaje);
        Assert.Equal(30, configuracion.MinutosVigenciaAcceso);
        Assert.Equal(60, configuracion.MaximoMensajesPorMinuto);
    }

    [Fact]
    public async Task LaConfiguracionSeSirveDeLaCacheAPartirDeLaSegundaVez()
    {
        // La consultan todos los clientes al arrancar y cambia con muy poca frecuencia.
        var servicio = Construir();

        await servicio.ObtenerAsync();
        await servicio.ObtenerAsync();

        Assert.Equal(1, _cache.Generaciones);
        Assert.True(_cache.Contiene(ClavesCache.ConfiguracionPlataforma));
    }

    /// <summary>Monta el servicio con las opciones de prueba.</summary>
    private ServicioConfiguracionPlataforma Construir() => new(
        _cache,
        Opciones.De(Opciones.SignalR()),
        Opciones.De(Opciones.Cifrado()),
        Opciones.De(Opciones.Jwt()),
        Opciones.De(Opciones.Cache()));
}
