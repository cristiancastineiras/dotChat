using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Administracion;
using Chat.Aplicacion.Comandos.Autenticacion;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Comandos.Usuarios;
using Chat.Aplicacion.Consultas.Mensajes;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using NSubstitute;

namespace Chat.Tests.Servidor;

/// <summary>
/// Pruebas de la capa HTTP de punta a punta contra un servidor en memoria: rutas,
/// autenticación, autorización, códigos de estado y traducción de errores.
/// </summary>
public sealed class PruebasEndpoints : IAsyncLifetime
{
    private ServidorDePrueba _servidor = null!;

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _servidor = new ServidorDePrueba();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync() => _servidor.DisposeAsync().AsTask();

    // -----------------------------------------------------------------------
    // Diagnóstico
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElEstadoEsPublicoYRespondeConLaHoraDelServidor()
    {
        using var cliente = _servidor.Anonimo();

        var respuesta = await cliente.GetAsync(new Uri("/api/estado", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await Leer(respuesta);
        Assert.Equal("activo", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(_servidor.Reloj.Ahora, cuerpo.GetProperty("fechaServidor").GetDateTimeOffset());
    }

    [Fact]
    public async Task LaConfiguracionPublicaSeSirveSinAutenticar()
    {
        _servidor.Configuracion
            .ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new ConfiguracionPlataformaDto("/hubs/chat", 2000, 30, 60));

        using var cliente = _servidor.Anonimo();

        var respuesta = await cliente.GetAsync(new Uri("/api/configuracion", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("/hubs/chat", (await Leer(respuesta)).GetProperty("rutaHub").GetString());
    }

    // -----------------------------------------------------------------------
    // Autenticación
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElRegistroDevuelveCreadoConLaSesionIniciada()
    {
        Programar<RespuestaAutenticacionDto>(Sesion());

        using var cliente = _servidor.Anonimo();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/auth/registrar", UriKind.Relative),
            new SolicitudRegistroDto("ana", "ana@dotchat.local", "clave-larga-1"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("token", (await Leer(respuesta)).GetProperty("tokenAcceso").GetString());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Any<ComandoRegistrarUsuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElLoginDevuelveLaSesion()
    {
        Programar<RespuestaAutenticacionDto>(Sesion());

        using var cliente = _servidor.Anonimo();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new SolicitudLoginDto("ana", "clave-larga-1"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Any<ComandoIniciarSesion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnLoginFallidoSeTraduceANoAutorizado()
    {
        Fallar<RespuestaAutenticacionDto>(new ExcepcionAutenticacion("Credenciales no válidas."));

        using var cliente = _servidor.Anonimo();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new SolicitudLoginDto("ana", "clave-larga-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Credenciales no válidas.", (await Leer(respuesta)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RefrescarRenuevaLaSesion()
    {
        Programar<RespuestaAutenticacionDto>(Sesion());

        using var cliente = _servidor.Anonimo();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/auth/refrescar", UriKind.Relative),
            new SolicitudRefrescoDto("un-token"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Any<ComandoRefrescarSesion>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Autorización
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/api/usuarios")]
    [InlineData("/api/salas")]
    [InlineData("/api/salas/mias")]
    [InlineData("/api/mensajes?salaId=00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/admin/estadisticas")]
    public async Task SinTokenLasRutasProtegidasResponden401(string ruta)
    {
        using var cliente = _servidor.Anonimo();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(new Uri(ruta, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task UnTokenInventadoNoAbreLasRutasProtegidas()
    {
        using var cliente = _servidor.Anonimo();
        cliente.DefaultRequestHeaders.Add("Authorization", "Bearer esto.no.es-un-token");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await cliente.GetAsync(new Uri("/api/usuarios", UriKind.Relative))).StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/estadisticas")]
    [InlineData("/api/admin/conexiones")]
    [InlineData("/api/admin/salas")]
    public async Task UnUsuarioCorrienteNoEntraEnLaConsolaDeAdministracion(string ruta)
    {
        using var cliente = _servidor.ComoUsuario();

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(new Uri(ruta, UriKind.Relative))).StatusCode);
    }

    // -----------------------------------------------------------------------
    // Usuarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LaIdentidadPropiaSaleDelTokenPresentado()
    {
        using var cliente = _servidor.ComoUsuario();

        var cuerpo = await Leer(await cliente.GetAsync(new Uri("/api/usuarios/yo", UriKind.Relative)));

        Assert.Equal(_servidor.UsuarioId, cuerpo.GetProperty("id").GetGuid());
        Assert.Equal("ana", cuerpo.GetProperty("nombreUsuario").GetString());
        Assert.False(cuerpo.GetProperty("esAdministrador").GetBoolean());
    }

    [Fact]
    public async Task UnAdministradorSeReconoceComoTal()
    {
        using var cliente = _servidor.ComoAdministrador();

        var cuerpo = await Leer(await cliente.GetAsync(new Uri("/api/usuarios/yo", UriKind.Relative)));

        Assert.True(cuerpo.GetProperty("esAdministrador").GetBoolean());
    }

    [Fact]
    public async Task UnUsuarioCorrienteNoPuedeVerLasCuentasDesactivadas()
    {
        // El parámetro se acepta pero se ignora: solo un administrador lo activa.
        Programar<IReadOnlyList<UsuarioDto>>([]);

        using var cliente = _servidor.ComoUsuario();
        await cliente.GetAsync(new Uri("/api/usuarios?incluirInactivos=true", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaListarUsuarios>(c => !c.IncluirInactivos),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnAdministradorSiPuedeVerLasCuentasDesactivadas()
    {
        Programar<IReadOnlyList<UsuarioDto>>([]);

        using var cliente = _servidor.ComoAdministrador();
        await cliente.GetAsync(new Uri("/api/usuarios?incluirInactivos=true", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaListarUsuarios>(c => c.IncluirInactivos),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaPresenciaSeConsultaConElUsuarioAutenticado()
    {
        Programar<IReadOnlyList<PresenciaDto>>(
            [new PresenciaDto(_servidor.UsuarioId, "ana", true, Datos.Ahora, 1)]);

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.GetAsync(new Uri("/api/usuarios/presencia", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("ana", (await Leer(respuesta))[0].GetProperty("nombreUsuario").GetString());
    }

    // -----------------------------------------------------------------------
    // Salas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CrearUnaSalaDevuelveCreadoConSuUbicacion()
    {
        var sala = SalaDto();
        Programar(sala);

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/salas", UriKind.Relative),
            new SolicitudCrearSalaDto("Equipo", null));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal($"/api/salas/{sala.Id}", respuesta.Headers.Location?.ToString());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoCrearSala>(c => c.CreadorId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnNombreDeSalaDuplicadoSeTraduceAConflicto()
    {
        Fallar<SalaDto>(new ExcepcionConflicto("Ya existe una sala llamada 'Equipo'."));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/salas", UriKind.Relative),
            new SolicitudCrearSalaDto("Equipo", null));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnNombreDeSalaInvalidoSeTraduceAPeticionIncorrectaConSusCampos()
    {
        Fallar<SalaDto>(new ExcepcionValidacion("nombre", "El nombre de la sala no es válido."));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/salas", UriKind.Relative),
            new SolicitudCrearSalaDto("--", null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var errores = (await Leer(respuesta)).GetProperty("errores");
        Assert.Equal("El nombre de la sala no es válido.", errores.GetProperty("nombre")[0].GetString());
    }

    [Fact]
    public async Task ElCatalogoDeSalasDistingueSiQuienPreguntaEsAdministrador()
    {
        Programar<IReadOnlyList<SalaDto>>([]);

        using var usuario = _servidor.ComoUsuario();
        await usuario.GetAsync(new Uri("/api/salas", UriKind.Relative));

        using var administrador = _servidor.ComoAdministrador();
        await administrador.GetAsync(new Uri("/api/salas", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaListarSalas>(c => !c.IncluirTodas), Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaListarSalas>(c => c.IncluirTodas), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaBandejaPropiaSeConsultaConLaIdentidadDelToken()
    {
        Programar<IReadOnlyList<SalaDto>>([SalaDto()]);

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.GetAsync(new Uri("/api/salas/mias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaSalasDeUsuario>(c => c.UsuarioId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbrirUnaConversacionDirectaDevuelveLaSala()
    {
        Programar(SalaDto("eva", TipoSala.Directa));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/salas/directas", UriKind.Relative),
            new SolicitudConversacionDirectaDto("eva"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("eva", (await Leer(respuesta)).GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task LosMiembrosDeUnaSalaSeConsultanConLaMarcaDeAdministrador()
    {
        var salaId = Guid.CreateVersion7();
        Programar<IReadOnlyList<MiembroSalaDto>>([]);

        using var cliente = _servidor.ComoAdministrador();
        await cliente.GetAsync(new Uri($"/api/salas/{salaId}/miembros", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaMiembrosSala>(c => c.SalaId == salaId && c.OmitirComprobacionMembresia),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnirseInvitarLeerYSalirLleganASuComandoConLaSalaYLaIdentidad()
    {
        var salaId = Guid.CreateVersion7();
        Programar(SalaDto());
        Programar(new ResultadoOperacionDto(true, "hecho"));

        using var cliente = _servidor.ComoUsuario();
        var raiz = new Uri($"/api/salas/{salaId}", UriKind.Relative);

        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync(new Uri($"{raiz}/unirse", UriKind.Relative), null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync(new Uri($"{raiz}/invitar", UriKind.Relative), new SolicitudInvitarDto("eva"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync(new Uri($"{raiz}/leida", UriKind.Relative), null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync(new Uri($"{raiz}/salir", UriKind.Relative), null)).StatusCode);

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoUnirseSala>(c => c.SalaId == salaId && c.UsuarioId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoInvitarASala>(c => c.SalaId == salaId && c.AnfitrionId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoMarcarSalaLeida>(c => c.SalaId == salaId && c.UsuarioId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoSalirSala>(c => c.SalaId == salaId && c.UsuarioId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaSalaInexistenteSeTraduceANoEncontrado()
    {
        Fallar<SalaDto>(ExcepcionNoEncontrado.Para("La sala", Guid.CreateVersion7()));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsync(
            new Uri($"/api/salas/{Guid.CreateVersion7()}/unirse", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaSalaPrivadaAjenaSeTraduceAAccesoDenegado()
    {
        Fallar<SalaDto>(new ExcepcionAutorizacion("La sala es privada."));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsync(
            new Uri($"/api/salas/{Guid.CreateVersion7()}/unirse", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Mensajes
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElHistorialSeConsultaConLaSalaLaCantidadYLaPaginacion()
    {
        var salaId = Guid.CreateVersion7();
        var limite = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Programar<IReadOnlyList<MensajeDto>>([]);

        using var cliente = _servidor.ComoUsuario();
        var ruta = $"/api/mensajes?salaId={salaId}&cantidad=25&anteriorA={Uri.EscapeDataString(limite.ToString("O"))}";

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(new Uri(ruta, UriKind.Relative))).StatusCode);

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaObtenerMensajes>(c =>
                c.SalaId == salaId
                && c.SolicitanteId == _servidor.UsuarioId
                && c.Cantidad == 25
                && c.AnteriorA == limite
                && !c.OmitirComprobacionMembresia),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnAdministradorPuedeAuditarElHistorialDeCualquierSala()
    {
        Programar<IReadOnlyList<MensajeDto>>([]);

        using var cliente = _servidor.ComoAdministrador();
        await cliente.GetAsync(new Uri($"/api/mensajes?salaId={Guid.CreateVersion7()}", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaObtenerMensajes>(c => c.OmitirComprobacionMembresia),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublicarPorHttpDevuelveCreadoConLaUbicacionDelMensaje()
    {
        var salaId = Guid.CreateVersion7();
        var mensaje = new MensajeDto(
            Guid.CreateVersion7(), salaId, "General", _servidor.UsuarioId, "ana", "hola", Datos.Ahora);

        Programar(mensaje);

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/mensajes", UriKind.Relative),
            new SolicitudEnviarMensajeDto(salaId, "hola", Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal($"/api/mensajes/{mensaje.Id}", respuesta.Headers.Location?.ToString());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoEnviarMensaje>(c => c.UsuarioId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnEnvioRepetidoSeTraduceAConflicto()
    {
        Fallar<MensajeDto>(new ExcepcionConflicto("El mensaje ya se había enviado."));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/mensajes", UriKind.Relative),
            new SolicitudEnviarMensajeDto(Guid.CreateVersion7(), "hola", Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Adjuntos
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SubirUnArchivoDevuelveCreadoConSuFicha()
    {
        var salaId = Guid.CreateVersion7();
        var adjunto = new AdjuntoDto(Guid.CreateVersion7(), "notas.txt", "text/plain", TipoAdjunto.Archivo, 12);
        Programar(adjunto);

        using var cliente = _servidor.ComoUsuario();
        using var formulario = Formulario("notas.txt", "contenido!!!"u8.ToArray());

        var respuesta = await cliente.PostAsync(
            new Uri($"/api/adjuntos?salaId={salaId}", UriKind.Relative), formulario);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal($"/api/adjuntos/{adjunto.Id}", respuesta.Headers.Location?.ToString());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoSubirAdjunto>(c =>
                c.SalaId == salaId
                && c.UsuarioId == _servidor.UsuarioId
                && c.NombreArchivo == "notas.txt"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaSubidaQueNoEsUnFormularioSeRechaza()
    {
        using var cliente = _servidor.ComoUsuario();

        var respuesta = await cliente.PostAsJsonAsync(
            new Uri($"/api/adjuntos?salaId={Guid.CreateVersion7()}", UriKind.Relative),
            new { archivo = "esto no es un fichero" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaSubidaSinFicheroSeRechaza()
    {
        using var cliente = _servidor.ComoUsuario();
        using var formulario = new MultipartFormDataContent { { new StringContent("valor"), "campo" } };

        var respuesta = await cliente.PostAsync(
            new Uri($"/api/adjuntos?salaId={Guid.CreateVersion7()}", UriKind.Relative), formulario);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaDescargaSirveElContenidoComoAdjuntoYSinCache()
    {
        var contenido = "contenido descargado"u8.ToArray();
        var adjuntoId = Guid.CreateVersion7();

        _servidor.Despachador
            .ConsultarAsync(Arg.Any<IConsulta<ContenidoAdjuntoDto>>(), Arg.Any<CancellationToken>())
            .Returns(new ContenidoAdjuntoDto(
                new MemoryStream(contenido, writable: false), "text/plain", "notas.txt", contenido.Length));

        using var cliente = _servidor.ComoUsuario();
        var respuesta = await cliente.GetAsync(new Uri($"/api/adjuntos/{adjuntoId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(contenido, await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal("text/plain", respuesta.Content.Headers.ContentType?.MediaType);

        // Se fuerza la descarga y no se deja cachear: el contenido es privado de la
        // conversación y la autorización se comprueba en cada petición.
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("notas.txt", respuesta.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        Assert.True(respuesta.Headers.CacheControl?.Private);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task UnAdministradorDescargaSinComprobarLaMembresia()
    {
        _servidor.Despachador
            .ConsultarAsync(Arg.Any<IConsulta<ContenidoAdjuntoDto>>(), Arg.Any<CancellationToken>())
            .Returns(new ContenidoAdjuntoDto(new MemoryStream([1]), "text/plain", "n.txt", 1));

        using var cliente = _servidor.ComoAdministrador();
        await cliente.GetAsync(new Uri($"/api/adjuntos/{Guid.CreateVersion7()}", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaDescargarAdjunto>(c => c.OmitirComprobacionMembresia),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Administración
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElAdministradorAccedeAlResumenDeActividad()
    {
        Programar(new EstadisticasDto(3, 2, 10, 1, 2048, 4, 2, Datos.Ahora));

        using var cliente = _servidor.ComoAdministrador();
        var respuesta = await cliente.GetAsync(new Uri("/api/admin/estadisticas", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(3, (await Leer(respuesta)).GetProperty("totalUsuarios").GetInt32());
    }

    [Fact]
    public async Task ElAdministradorConsultaLasConexionesActivas()
    {
        Programar<IReadOnlyList<ConexionActivaDto>>(
            [new ConexionActivaDto("c1", _servidor.UsuarioId, "ana", Datos.Ahora, ["General"])]);

        using var cliente = _servidor.ComoAdministrador();
        var respuesta = await cliente.GetAsync(new Uri("/api/admin/conexiones", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("c1", (await Leer(respuesta))[0].GetProperty("conexionId").GetString());
    }

    [Fact]
    public async Task ElAdministradorVeTodasLasSalasSinFiltroDeVisibilidad()
    {
        Programar<IReadOnlyList<SalaDto>>([]);

        using var cliente = _servidor.ComoAdministrador();
        await cliente.GetAsync(new Uri("/api/admin/salas", UriKind.Relative));

        await _servidor.Despachador.Received(1).ConsultarAsync(
            Arg.Is<ConsultaListarSalas>(c => c.IncluirTodas), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElAdministradorEliminaCuentasYSalasYVaciaLaCache()
    {
        var usuarioId = Guid.CreateVersion7();
        var salaId = Guid.CreateVersion7();
        Programar(new ResultadoOperacionDto(true, "hecho"));

        using var cliente = _servidor.ComoAdministrador();

        Assert.Equal(HttpStatusCode.OK, (await cliente.DeleteAsync(new Uri($"/api/admin/usuarios/{usuarioId}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.DeleteAsync(new Uri($"/api/admin/salas/{salaId}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync(new Uri("/api/admin/cache/limpiar", UriKind.Relative), null)).StatusCode);

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoEliminarUsuario>(c => c.UsuarioId == usuarioId && c.SolicitanteId == _servidor.UsuarioId),
            Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoEliminarSala>(c => c.SalaId == salaId), Arg.Any<CancellationToken>());

        await _servidor.Despachador.Received(1).EjecutarAsync(
            Arg.Is<ComandoLimpiarCache>(c => c.SolicitanteId == _servidor.UsuarioId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnErrorInesperadoSeDevuelveComoErrorInternoSinDetalles()
    {
        _servidor.Despachador
            .ConsultarAsync(Arg.Any<IConsulta<EstadisticasDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<EstadisticasDto>>(_ => throw new InvalidOperationException("Password=secreto"));

        using var cliente = _servidor.ComoAdministrador();
        var respuesta = await cliente.GetAsync(new Uri("/api/admin/estadisticas", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var detalle = (await Leer(respuesta)).GetProperty("detail").GetString();
        Assert.DoesNotContain("secreto", detalle, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Utilidades
    // -----------------------------------------------------------------------

    /// <summary>Programa el resultado de cualquier comando que devuelva ese tipo.</summary>
    /// <typeparam name="T">Tipo del resultado.</typeparam>
    /// <param name="resultado">Valor a devolver.</param>
    private void Programar<T>(T resultado)
    {
        _servidor.Despachador
            .EjecutarAsync(Arg.Any<IComando<T>>(), Arg.Any<CancellationToken>())
            .Returns(resultado);

        _servidor.Despachador
            .ConsultarAsync(Arg.Any<IConsulta<T>>(), Arg.Any<CancellationToken>())
            .Returns(resultado);
    }

    /// <summary>Programa un fallo para cualquier operación que devuelva ese tipo.</summary>
    /// <typeparam name="T">Tipo del resultado.</typeparam>
    /// <param name="excepcion">Excepción que lanzará.</param>
    private void Fallar<T>(Exception excepcion)
    {
        _servidor.Despachador
            .EjecutarAsync(Arg.Any<IComando<T>>(), Arg.Any<CancellationToken>())
            .Returns<Task<T>>(_ => throw excepcion);

        _servidor.Despachador
            .ConsultarAsync(Arg.Any<IConsulta<T>>(), Arg.Any<CancellationToken>())
            .Returns<Task<T>>(_ => throw excepcion);
    }

    /// <summary>Construye un formulario multiparte con un fichero.</summary>
    /// <param name="nombre">Nombre del fichero.</param>
    /// <param name="contenido">Bytes del fichero.</param>
    private static MultipartFormDataContent Formulario(string nombre, byte[] contenido)
        => new() { { new ByteArrayContent(contenido), "archivo", nombre } };

    /// <summary>Lee el cuerpo de la respuesta como JSON.</summary>
    /// <param name="respuesta">Respuesta HTTP.</param>
    private static async Task<JsonElement> Leer(HttpResponseMessage respuesta)
        => JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Construye la proyección de una sala.</summary>
    /// <param name="nombre">Nombre visible.</param>
    /// <param name="tipo">Naturaleza de la sala.</param>
    private static SalaDto SalaDto(string nombre = "Equipo", TipoSala tipo = TipoSala.Publica)
        => new(Guid.CreateVersion7(), nombre, null, tipo, Datos.Ahora, null, 1, EsMiembro: true);

    /// <summary>Construye una sesión de ejemplo.</summary>
    private RespuestaAutenticacionDto Sesion()
        => new(_servidor.UsuarioId, "ana", "token", Datos.Ahora.AddMinutes(30), "refresco", ["Usuario"]);
}
