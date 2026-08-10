using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Infraestructura;

/// <summary>
/// Puente entre el contenedor de dependencias de .NET y Spectre.Console.Cli,
/// para que los comandos de administración reciban sus servicios por constructor.
/// </summary>
public sealed class RegistradorTipos : ITypeRegistrar
{
    private readonly IServiceCollection _servicios;

    /// <summary>Crea el registrador sobre una colección de servicios existente.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    public RegistradorTipos(IServiceCollection servicios) => _servicios = servicios;

    /// <inheritdoc />
    public void Register(Type servicio, Type implementacion) => _servicios.AddSingleton(servicio, implementacion);

    /// <inheritdoc />
    public void RegisterInstance(Type servicio, object implementacion)
        => _servicios.AddSingleton(servicio, implementacion);

    /// <inheritdoc />
    public void RegisterLazy(Type servicio, Func<object> fabrica)
    {
        ArgumentNullException.ThrowIfNull(fabrica);
        _servicios.AddSingleton(servicio, _ => fabrica());
    }

    /// <inheritdoc />
    public ITypeResolver Build() => new ResolutorTipos(_servicios.BuildServiceProvider());
}

/// <summary>Resolutor de tipos que delega en el proveedor de servicios construido.</summary>
public sealed class ResolutorTipos : ITypeResolver, IDisposable
{
    private readonly ServiceProvider _proveedor;

    /// <summary>Crea el resolutor.</summary>
    /// <param name="proveedor">Proveedor de servicios ya construido.</param>
    public ResolutorTipos(ServiceProvider proveedor) => _proveedor = proveedor;

    /// <inheritdoc />
    public object? Resolve(Type? tipo) => tipo is null ? null : _proveedor.GetService(tipo);

    /// <inheritdoc />
    public void Dispose() => _proveedor.Dispose();
}
