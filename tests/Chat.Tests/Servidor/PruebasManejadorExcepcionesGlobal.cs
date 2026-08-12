using System.Security.Claims;
using System.Text.Json;
using Chat.Dominio.Constantes;
using Chat.Dominio.Excepciones;
using Chat.Servidor.Configuracion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chat.Tests.Servidor;

/// <summary>
/// Pruebas de la traducción de excepciones a respuestas <c>ProblemDetails</c>.
/// </summary>
/// <remarks>
/// Es el contrato de errores de toda la API: de aquí depende que el cliente sepa
/// distinguir «no tienes permiso» de «no existe» o de «vuelve a autenticarte».
/// </remarks>
public sealed class PruebasManejadorExcepcionesGlobal
{
    [Theory]
    [InlineData(typeof(ExcepcionValidacion), StatusCodes.Status400BadRequest, "Datos no válidos")]
    [InlineData(typeof(ExcepcionAutenticacion), StatusCodes.Status401Unauthorized, "No autenticado")]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, "No autenticado")]
    [InlineData(typeof(ExcepcionAutorizacion), StatusCodes.Status403Forbidden, "Acceso denegado")]
    [InlineData(typeof(ExcepcionNoEncontrado), StatusCodes.Status404NotFound, "Recurso no encontrado")]
    [InlineData(typeof(ExcepcionConflicto), StatusCodes.Status409Conflict, "Conflicto")]
    [InlineData(typeof(OperationCanceledException), 499, "Petición cancelada")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, "Error interno")]
    public async Task CadaExcepcionSeTraduceASuCodigoYSuTitulo(Type tipo, int estado, string titulo)
    {
        var excepcion = Construir(tipo);
        var (contexto, cuerpo) = Contexto();

        Assert.True(await Manejador().TryHandleAsync(contexto, excepcion, CancellationToken.None));

        Assert.Equal(estado, contexto.Response.StatusCode);

        var problema = Leer(cuerpo);
        Assert.Equal(estado, problema.GetProperty("status").GetInt32());
        Assert.Equal(titulo, problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnErrorDeValidacionLlevaElDetallePorCampo()
    {
        var errores = new Dictionary<string, string[]>
        {
            ["clave"] = ["Falta un dígito."],
            ["email"] = ["No es válido."]
        };

        var (contexto, cuerpo) = Contexto();
        await Manejador().TryHandleAsync(contexto, new ExcepcionValidacion(errores), CancellationToken.None);

        var detalle = Leer(cuerpo).GetProperty("errores");

        Assert.Equal("Falta un dígito.", detalle.GetProperty("clave")[0].GetString());
        Assert.Equal("No es válido.", detalle.GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task UnErrorEsperadoConservaSuMensajeParaElUsuario()
    {
        var (contexto, cuerpo) = Contexto();

        await Manejador().TryHandleAsync(
            contexto,
            new ExcepcionAutorizacion("Debes unirte a la sala antes de escribir en ella."),
            CancellationToken.None);

        Assert.Equal(
            "Debes unirte a la sala antes de escribir en ella.",
            Leer(cuerpo).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnErrorInesperadoNoFiltraDetallesInternos()
    {
        // El mensaje original va al registro del servidor, no a la respuesta.
        var (contexto, cuerpo) = Contexto();

        await Manejador().TryHandleAsync(
            contexto,
            new InvalidOperationException("la cadena de conexión es Host=10.0.0.5;Password=secreto"),
            CancellationToken.None);

        var detalle = Leer(cuerpo).GetProperty("detail").GetString();

        Assert.DoesNotContain("secreto", detalle, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0.0.5", detalle, StringComparison.Ordinal);
        Assert.Contains("identificador de traza", detalle!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaRespuestaLlevaLaRutaYElIdentificadorDeTraza()
    {
        var (contexto, cuerpo) = Contexto("/api/salas/abc");
        contexto.TraceIdentifier = "traza-42";

        await Manejador().TryHandleAsync(contexto, new ExcepcionNoEncontrado("no existe"), CancellationToken.None);

        var problema = Leer(cuerpo);

        Assert.Equal("/api/salas/abc", problema.GetProperty("instance").GetString());
        Assert.Equal("traza-42", problema.GetProperty("trazaId").GetString());
    }

    [Fact]
    public async Task UnErrorSinCamposNoIncluyeElBloqueDeErrores()
    {
        var (contexto, cuerpo) = Contexto();

        await Manejador().TryHandleAsync(contexto, new ExcepcionConflicto("duplicado"), CancellationToken.None);

        Assert.False(Leer(cuerpo).TryGetProperty("errores", out _));
    }

    [Fact]
    public async Task LosArgumentosNulosSeRechazan()
    {
        var (contexto, _) = Contexto();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Manejador().TryHandleAsync(null!, new ExcepcionConflicto("x"), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Manejador().TryHandleAsync(contexto, null!, CancellationToken.None));
    }

    /// <summary>Crea el manejador con un registro nulo y el servicio real de problemas.</summary>
    private static ManejadorExcepcionesGlobal Manejador()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddProblemDetails();

        return new ManejadorExcepcionesGlobal(
            NullLogger<ManejadorExcepcionesGlobal>.Instance,
            servicios.BuildServiceProvider().GetRequiredService<IProblemDetailsService>());
    }

    /// <summary>Monta un contexto HTTP con el cuerpo de respuesta capturable.</summary>
    /// <param name="ruta">Ruta de la petición.</param>
    private static (HttpContext Contexto, MemoryStream Cuerpo) Contexto(string ruta = "/api/prueba")
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddProblemDetails();

        var cuerpo = new MemoryStream();
        var contexto = new DefaultHttpContext
        {
            RequestServices = servicios.BuildServiceProvider()
        };

        contexto.Request.Path = ruta;
        contexto.Request.Method = "GET";
        contexto.Response.Body = cuerpo;

        // Sin esta característica, el servicio de problemas no sabe negociar el tipo
        // de contenido y no escribe nada.
        contexto.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(cuerpo));

        return (contexto, cuerpo);
    }

    /// <summary>Lee el cuerpo escrito como JSON.</summary>
    /// <param name="cuerpo">Flujo con la respuesta.</param>
    private static JsonElement Leer(MemoryStream cuerpo)
    {
        cuerpo.Position = 0;
        return JsonDocument.Parse(cuerpo).RootElement;
    }

    /// <summary>Construye una excepción del tipo indicado con un mensaje cualquiera.</summary>
    /// <param name="tipo">Tipo de excepción.</param>
    private static Exception Construir(Type tipo) => tipo switch
    {
        _ when tipo == typeof(ExcepcionValidacion) => new ExcepcionValidacion("campo", "no vale"),
        _ when tipo == typeof(ExcepcionAutenticacion) => new ExcepcionAutenticacion("credenciales"),
        _ when tipo == typeof(UnauthorizedAccessException) => new UnauthorizedAccessException(),
        _ when tipo == typeof(ExcepcionAutorizacion) => new ExcepcionAutorizacion("prohibido"),
        _ when tipo == typeof(ExcepcionNoEncontrado) => new ExcepcionNoEncontrado("no existe"),
        _ when tipo == typeof(ExcepcionConflicto) => new ExcepcionConflicto("duplicado"),
        _ when tipo == typeof(OperationCanceledException) => new OperationCanceledException(),
        _ => new InvalidOperationException("vaya")
    };
}

/// <summary>
/// Pruebas de la lectura de identidad desde los claims. Toda la autorización de la
/// plataforma se apoya en estas tres funciones.
/// </summary>
public sealed class PruebasIdentidadDesdeClaims
{
    [Fact]
    public void ElUsuarioSeIdentificaPorElSujetoDelToken()
    {
        var id = Guid.CreateVersion7();
        var principal = Construir(new Claim(JwtRegisteredClaimNames.Sub, id.ToString()));

        Assert.Equal(id, principal.ObtenerUsuarioId());
    }

    [Fact]
    public void SinSujetoSeCaeAlIdentificadorDeNombre()
    {
        var id = Guid.CreateVersion7();
        var principal = Construir(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        Assert.Equal(id, principal.ObtenerUsuarioId());
    }

    [Fact]
    public void ElSujetoTienePrioridadSobreElIdentificadorDeNombre()
    {
        var sujeto = Guid.CreateVersion7();
        var otro = Guid.CreateVersion7();

        var principal = Construir(
            new Claim(JwtRegisteredClaimNames.Sub, sujeto.ToString()),
            new Claim(ClaimTypes.NameIdentifier, otro.ToString()));

        Assert.Equal(sujeto, principal.ObtenerUsuarioId());
    }

    [Fact]
    public void UnTokenSinSujetoValidoSeRechaza()
    {
        Assert.Throws<UnauthorizedAccessException>(() => Construir().ObtenerUsuarioId());
        Assert.Throws<UnauthorizedAccessException>(
            () => Construir(new Claim(JwtRegisteredClaimNames.Sub, "no-es-un-guid")).ObtenerUsuarioId());
    }

    [Fact]
    public void ElNombreSaleDelClaimDeNombreYSiNoDelNombreUnico()
    {
        Assert.Equal("ana", Construir(new Claim(ClaimTypes.Name, "ana")).ObtenerNombreUsuario());
        Assert.Equal("eva", Construir(new Claim(JwtRegisteredClaimNames.UniqueName, "eva")).ObtenerNombreUsuario());
        Assert.Equal("(desconocido)", Construir().ObtenerNombreUsuario());
    }

    [Fact]
    public void ElRolDeAdministradorSeReconoceComoTal()
    {
        Assert.True(Construir(new Claim(ClaimTypes.Role, RolesDelSistema.Administrador)).EsAdministrador());
        Assert.False(Construir(new Claim(ClaimTypes.Role, RolesDelSistema.Usuario)).EsAdministrador());
        Assert.False(Construir().EsAdministrador());
    }

    [Fact]
    public void UnaIdentidadNulaSeRechaza()
    {
        Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).ObtenerUsuarioId());
        Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).ObtenerNombreUsuario());
        Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).EsAdministrador());
    }

    [Fact]
    public void LaClaveDeFirmaSeValidaAlArrancar()
    {
        ExtensionesAutenticacion.ValidarClaveFirma(Comun.Opciones.ClaveFirmaBase64);

        Assert.Throws<InvalidOperationException>(
            () => ExtensionesAutenticacion.ValidarClaveFirma(Convert.ToBase64String(new byte[16])));
    }

    /// <summary>Construye una identidad autenticada con los claims indicados.</summary>
    /// <param name="claims">Claims del token.</param>
    private static ClaimsPrincipal Construir(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Prueba", ClaimTypes.Name, ClaimTypes.Role));
}
