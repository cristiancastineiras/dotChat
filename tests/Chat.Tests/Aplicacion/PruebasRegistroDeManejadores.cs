using System.Reflection;
using Chat.Aplicacion;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Dominio.Abstracciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chat.Tests.Aplicacion;

/// <summary>
/// Comprueba que todos los comandos y consultas de la capa de aplicación tienen su
/// manejador registrado en el contenedor.
/// </summary>
/// <remarks>
/// Los registros se declaran uno a uno a mano. Un manejador nuevo que se olvide de
/// añadir compila igual de bien y solo falla en ejecución, con un «no hay ningún
/// manejador registrado» en la cara del usuario. Esta prueba recorre el ensamblado y
/// adelanta ese fallo a la compilación.
/// </remarks>
public sealed class PruebasRegistroDeManejadores
{
    /// <summary>Todos los comandos declarados en la capa de aplicación.</summary>
    public static TheoryData<Type> Comandos => Mensajes(typeof(IComando<>));

    /// <summary>Todas las consultas declaradas en la capa de aplicación.</summary>
    public static TheoryData<Type> Consultas => Mensajes(typeof(IConsulta<>));

    [Theory]
    [MemberData(nameof(Comandos))]
    public void CadaComandoTieneSuManejadorRegistrado(Type comando)
        => ComprobarRegistro(comando, typeof(IComando<>), typeof(IManejadorComando<,>));

    [Theory]
    [MemberData(nameof(Consultas))]
    public void CadaConsultaTieneSuManejadorRegistrado(Type consulta)
        => ComprobarRegistro(consulta, typeof(IConsulta<>), typeof(IManejadorConsulta<,>));

    [Fact]
    public void ElDespachadorYLosServiciosDeAplicacionSeResuelven()
    {
        using var ambito = Construir().CreateScope();

        Assert.NotNull(ambito.ServiceProvider.GetService<IDespachador>());
        Assert.NotNull(ambito.ServiceProvider.GetService<IEmisorSesiones>());
        Assert.NotNull(ambito.ServiceProvider.GetService<IServicioConfiguracionPlataforma>());
    }

    [Fact]
    public void HayComandosYConsultasQueComprobar()
    {
        // Red de seguridad de la propia prueba: si el descubrimiento por reflexión
        // dejara de encontrar nada, las teorías pasarían vacías sin comprobar nada.
        Assert.NotEmpty(Comandos);
        Assert.NotEmpty(Consultas);
    }

    /// <summary>Reúne los tipos del ensamblado que implementan la interfaz de mensaje indicada.</summary>
    /// <param name="interfazAbierta">Interfaz genérica abierta que marca el mensaje.</param>
    private static TheoryData<Type> Mensajes(Type interfazAbierta)
    {
        var datos = new TheoryData<Type>();

        var tipos = typeof(IDespachador).Assembly
            .GetTypes()
            .Where(tipo => tipo is { IsAbstract: false, IsInterface: false, IsPublic: true })
            .Where(tipo => tipo.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == interfazAbierta))
            .OrderBy(tipo => tipo.Name, StringComparer.Ordinal);

        foreach (var tipo in tipos)
        {
            datos.Add(tipo);
        }

        return datos;
    }

    /// <summary>Resuelve del contenedor el manejador que corresponde al mensaje.</summary>
    /// <param name="mensaje">Tipo del comando o consulta.</param>
    /// <param name="interfazAbierta">Interfaz que marca el mensaje.</param>
    /// <param name="manejadorAbierto">Interfaz genérica abierta del manejador.</param>
    private static void ComprobarRegistro(Type mensaje, Type interfazAbierta, Type manejadorAbierto)
    {
        var resultado = mensaje.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfazAbierta)
            .GetGenericArguments()[0];

        using var ambito = Construir().CreateScope();

        var manejador = ambito.ServiceProvider.GetService(manejadorAbierto.MakeGenericType(mensaje, resultado));

        Assert.True(
            manejador is not null,
            $"Falta registrar el manejador de '{mensaje.Name}' en AgregarAplicacion().");
    }

    /// <summary>
    /// Monta el contenedor con la capa de aplicación real y las dependencias de
    /// infraestructura sustituidas: lo que se comprueba es el registro, no lo que hay
    /// detrás de cada interfaz.
    /// </summary>
    private static ServiceProvider Construir()
    {
        var servicios = new ServiceCollection();

        servicios.AddLogging(constructor => constructor.SetMinimumLevel(LogLevel.None));
        servicios.AgregarAplicacion();

        servicios.AddSingleton(Substitute.For<IRepositorioUsuarios>());
        servicios.AddSingleton(Substitute.For<IRepositorioSalas>());
        servicios.AddSingleton(Substitute.For<IRepositorioMensajes>());
        servicios.AddSingleton(Substitute.For<IRepositorioAdjuntos>());
        servicios.AddSingleton(Substitute.For<IRepositorioTokensRefresco>());
        servicios.AddSingleton(Substitute.For<IUnidadDeTrabajo>());

        servicios.AddSingleton(Substitute.For<IServicioIdentidad>());
        servicios.AddSingleton(Substitute.For<IGeneradorTokens>());
        servicios.AddSingleton(Substitute.For<ICifradorMensajes>());
        servicios.AddSingleton(Substitute.For<ICifradorFlujo>());
        servicios.AddSingleton(Substitute.For<IAlmacenObjetos>());
        servicios.AddSingleton(Substitute.For<IProcesadorImagenes>());
        servicios.AddSingleton(Substitute.For<IProcesadorAudio>());
        servicios.AddSingleton(Substitute.For<INotificadorTiempoReal>());
        servicios.AddSingleton(Substitute.For<IRegistroConexiones>());
        servicios.AddSingleton(Substitute.For<IProtectorRepeticion>());
        servicios.AddSingleton(Substitute.For<ILimitadorEnvios>());
        servicios.AddSingleton<IServicioCache>(new CacheDePrueba());
        servicios.AddSingleton<IProveedorFechaHora>(new RelojFijo());

        servicios.AddSingleton(Opciones.De(Opciones.Cifrado()));
        servicios.AddSingleton(Opciones.De(Opciones.Jwt()));
        servicios.AddSingleton(Opciones.De(Opciones.Cache()));
        servicios.AddSingleton(Opciones.De(Opciones.SignalR()));
        servicios.AddSingleton(Opciones.De(Opciones.Adjuntos()));
        servicios.AddSingleton<IOptions<Chat.Aplicacion.Opciones.AdministradorOptions>>(
            Options.Create(new Chat.Aplicacion.Opciones.AdministradorOptions()));

        return servicios.BuildServiceProvider();
    }
}
