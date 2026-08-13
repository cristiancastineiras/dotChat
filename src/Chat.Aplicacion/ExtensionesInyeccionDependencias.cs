using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Administracion;
using Chat.Aplicacion.Comandos.Autenticacion;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Comandos.Usuarios;
using Chat.Aplicacion.Consultas.Administracion;
using Chat.Aplicacion.Consultas.Mensajes;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Aplicacion;

/// <summary>
/// Registro de la capa de aplicación en el contenedor de dependencias.
/// Los manejadores se declaran uno a uno, de forma explícita: es más verboso que
/// escanear ensamblados, pero el grafo de dependencias queda documentado y se
/// detecta en compilación cualquier manejador que falte.
/// </summary>
public static class ExtensionesInyeccionDependencias
{
    /// <summary>Registra despachador, servicios y manejadores de la capa de aplicación.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarAplicacion(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<IDespachador, Despachador>();
        servicios.AddScoped<IEmisorSesiones, EmisorSesiones>();
        servicios.AddScoped<IServicioConfiguracionPlataforma, ServicioConfiguracionPlataforma>();

        // Comandos.
        servicios.AddScoped<
            IManejadorComando<ComandoRegistrarUsuario, RespuestaAutenticacionDto>,
            ManejadorRegistrarUsuario>();
        servicios.AddScoped<
            IManejadorComando<ComandoIniciarSesion, RespuestaAutenticacionDto>,
            ManejadorIniciarSesion>();
        servicios.AddScoped<
            IManejadorComando<ComandoRefrescarSesion, RespuestaAutenticacionDto>,
            ManejadorRefrescarSesion>();
        servicios.AddScoped<IManejadorComando<ComandoCrearSala, SalaDto>, ManejadorCrearSala>();
        servicios.AddScoped<IManejadorComando<ComandoUnirseSala, SalaDto>, ManejadorUnirseSala>();
        servicios.AddScoped<IManejadorComando<ComandoSalirSala, ResultadoOperacionDto>, ManejadorSalirSala>();
        servicios.AddScoped<
            IManejadorComando<ComandoAbrirConversacionDirecta, SalaDto>,
            ManejadorAbrirConversacionDirecta>();
        servicios.AddScoped<IManejadorComando<ComandoInvitarASala, ResultadoOperacionDto>, ManejadorInvitarASala>();
        servicios.AddScoped<
            IManejadorComando<ComandoMarcarSalaLeida, ResultadoOperacionDto>,
            ManejadorMarcarSalaLeida>();
        servicios.AddScoped<IManejadorComando<ComandoEliminarSala, ResultadoOperacionDto>, ManejadorEliminarSala>();
        servicios.AddScoped<IManejadorComando<ComandoEnviarMensaje, MensajeDto>, ManejadorEnviarMensaje>();
        servicios.AddScoped<IManejadorComando<ComandoSubirAdjunto, AdjuntoDto>, ManejadorSubirAdjunto>();
        servicios.AddScoped<IManejadorComando<ComandoEliminarUsuario, ResultadoOperacionDto>, ManejadorEliminarUsuario>();
        servicios.AddScoped<IManejadorComando<ComandoActualizarAvatar, PerfilDto>, ManejadorActualizarAvatar>();
        servicios.AddScoped<IManejadorComando<ComandoEliminarAvatar, PerfilDto>, ManejadorEliminarAvatar>();
        servicios.AddScoped<IManejadorComando<ComandoLimpiarCache, ResultadoOperacionDto>, ManejadorLimpiarCache>();

        // Consultas.
        servicios.AddScoped<
            IManejadorConsulta<ConsultaListarUsuarios, IReadOnlyList<UsuarioDto>>,
            ManejadorListarUsuarios>();
        servicios.AddScoped<IManejadorConsulta<ConsultaPerfil, PerfilDto>, ManejadorPerfil>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaDescargarAvatar, ContenidoAdjuntoDto>,
            ManejadorDescargarAvatar>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaListarSalas, IReadOnlyList<SalaDto>>,
            ManejadorListarSalas>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaSalasDeUsuario, IReadOnlyList<SalaDto>>,
            ManejadorSalasDeUsuario>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaMiembrosSala, IReadOnlyList<MiembroSalaDto>>,
            ManejadorMiembrosSala>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaPresencia, IReadOnlyList<PresenciaDto>>,
            ManejadorPresencia>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaObtenerMensajes, IReadOnlyList<MensajeDto>>,
            ManejadorObtenerMensajes>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaDescargarAdjunto, ContenidoAdjuntoDto>,
            ManejadorDescargarAdjunto>();
        servicios.AddScoped<IManejadorConsulta<ConsultaEstadisticas, EstadisticasDto>, ManejadorEstadisticas>();
        servicios.AddScoped<
            IManejadorConsulta<ConsultaConexionesActivas, IReadOnlyList<ConexionActivaDto>>,
            ManejadorConexionesActivas>();

        return servicios;
    }
}
