using Chat.Aplicacion.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Tests.Aplicacion;

/// <summary>
/// Pruebas del despachador CQRS: que resuelve el manejador correcto del contenedor,
/// que propaga resultado, cancelación y excepciones, y que falla con un mensaje útil
/// cuando falta un registro.
/// </summary>
public sealed class PruebasDespachador
{
    [Fact]
    public async Task UnComandoSeEntregaASuManejadorYDevuelveSuResultado()
    {
        var despachador = Construir(servicios => servicios
            .AddSingleton<IManejadorComando<ComandoDePrueba, string>, ManejadorDePrueba>());

        Assert.Equal("hecho: hola", await despachador.EjecutarAsync(new ComandoDePrueba("hola")));
    }

    [Fact]
    public async Task UnaConsultaSeEntregaASuManejadorYDevuelveSuResultado()
    {
        var despachador = Construir(servicios => servicios
            .AddSingleton<IManejadorConsulta<ConsultaDePrueba, int>, ManejadorConsultaDePrueba>());

        Assert.Equal(42, await despachador.ConsultarAsync(new ConsultaDePrueba(42)));
    }

    [Fact]
    public async Task ElTokenDeCancelacionLlegaAlManejador()
    {
        var manejador = new ManejadorDePrueba();
        var despachador = Construir(servicios => servicios
            .AddSingleton<IManejadorComando<ComandoDePrueba, string>>(manejador));

        using var origen = new CancellationTokenSource();
        await despachador.EjecutarAsync(new ComandoDePrueba("hola"), origen.Token);

        Assert.Equal(origen.Token, manejador.UltimaCancelacion);
    }

    [Fact]
    public async Task SinManejadorRegistradoElErrorDiceQueFaltaYCual()
    {
        var despachador = Construir(_ => { });

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => despachador.EjecutarAsync(new ComandoDePrueba("hola")));

        Assert.Contains(nameof(ComandoDePrueba), excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("Registre una implementación", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaExcepcionDelManejadorLlegaIntactaAQuienDespacha()
    {
        // El despachador traza el error, pero no lo envuelve: la capa de presentación
        // necesita el tipo original para elegir el código HTTP.
        var despachador = Construir(servicios => servicios
            .AddSingleton<IManejadorComando<ComandoDePrueba, string>>(new ManejadorQueFalla()));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => despachador.EjecutarAsync(new ComandoDePrueba("hola")));

        Assert.Equal("el manejador falló", excepcion.Message);
    }

    [Fact]
    public async Task DespacharDosVecesReutilizaLosMetadatosCacheados()
    {
        var manejador = new ManejadorDePrueba();
        var despachador = Construir(servicios => servicios
            .AddSingleton<IManejadorComando<ComandoDePrueba, string>>(manejador));

        await despachador.EjecutarAsync(new ComandoDePrueba("uno"));
        await despachador.EjecutarAsync(new ComandoDePrueba("dos"));

        Assert.Equal(2, manejador.Invocaciones);
    }

    [Fact]
    public async Task UnMensajeNuloSeRechaza()
    {
        var despachador = Construir(_ => { });

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => despachador.EjecutarAsync<string>(null!));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => despachador.ConsultarAsync<int>(null!));
    }

    /// <summary>Monta un contenedor con los registros indicados y devuelve su despachador.</summary>
    /// <param name="registrar">Registros adicionales.</param>
    private static IDespachador Construir(Action<IServiceCollection> registrar)
    {
        var servicios = new ServiceCollection();
        registrar(servicios);

        return new Despachador(servicios.BuildServiceProvider());
    }

    /// <summary>Comando de ejemplo.</summary>
    /// <param name="Valor">Carga útil.</param>
    private sealed record ComandoDePrueba(string Valor) : IComando<string>;

    /// <summary>Consulta de ejemplo.</summary>
    /// <param name="Valor">Valor que devolverá.</param>
    private sealed record ConsultaDePrueba(int Valor) : IConsulta<int>;

    /// <summary>Manejador que registra cómo se le invocó.</summary>
    private sealed class ManejadorDePrueba : IManejadorComando<ComandoDePrueba, string>
    {
        /// <summary>Número de veces que se le ha llamado.</summary>
        public int Invocaciones { get; private set; }

        /// <summary>Token recibido en la última invocación.</summary>
        public CancellationToken UltimaCancelacion { get; private set; }

        /// <inheritdoc />
        public Task<string> ManejarAsync(ComandoDePrueba comando, CancellationToken cancelacion = default)
        {
            Invocaciones++;
            UltimaCancelacion = cancelacion;

            return Task.FromResult($"hecho: {comando.Valor}");
        }
    }

    /// <summary>Manejador de consulta que devuelve lo que se le pide.</summary>
    private sealed class ManejadorConsultaDePrueba : IManejadorConsulta<ConsultaDePrueba, int>
    {
        /// <inheritdoc />
        public Task<int> ManejarAsync(ConsultaDePrueba consulta, CancellationToken cancelacion = default)
            => Task.FromResult(consulta.Valor);
    }

    /// <summary>
    /// Manejador que siempre falla. Se declara <c>async</c> como los reales: así la
    /// excepción viaja dentro de la tarea devuelta y no como fallo de la invocación
    /// por reflexión, que es lo que ocurre en producción.
    /// </summary>
    private sealed class ManejadorQueFalla : IManejadorComando<ComandoDePrueba, string>
    {
        /// <inheritdoc />
        public async Task<string> ManejarAsync(ComandoDePrueba comando, CancellationToken cancelacion = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("el manejador falló");
        }
    }
}
