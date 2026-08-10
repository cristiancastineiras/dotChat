using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Conversación en vivo sobre una sala: pinta el historial, recibe los mensajes
/// nuevos por SignalR y lee lo que el usuario escribe.
/// </summary>
/// <remarks>
/// Es un servicio compartido porque tanto <c>unirse</c> (salas) como <c>privado</c>
/// (conversaciones directas) abren exactamente la misma pantalla. Las suscripciones
/// a los eventos del hub se retiran al cerrar: la conexión sobrevive a la orden en la
/// consola interactiva y, sin retirarlas, la siguiente conversación pintaría cada
/// mensaje tantas veces como salas se hubieran abierto.
/// </remarks>
public sealed class VistaConversacion
{
    /// <summary>Intervalo de sondeo del teclado para detectar que el usuario escribe.</summary>
    private static readonly TimeSpan IntervaloSondeoTeclado = TimeSpan.FromMilliseconds(150);

    private readonly ClienteApi _api;
    private readonly ClienteTiempoReal _tiempoReal;
    private readonly OpcionesCliente _opciones;

    /// <summary>Crea la vista.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="tiempoReal">Cliente de SignalR.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public VistaConversacion(ClienteApi api, ClienteTiempoReal tiempoReal, IOptions<OpcionesCliente> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _api = api;
        _tiempoReal = tiempoReal;
        _opciones = opciones.Value;
    }

    /// <summary>Abre la conversación y no vuelve hasta que el usuario la cierra.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="sala">Sala o conversación a abrir.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task AbrirAsync(SesionAlmacenada sesion, SalaDto sala, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(sala);

        var configuracion = await _api.ObtenerConfiguracionAsync(cancelacion).ConfigureAwait(false);

        // La conexión se establece antes de pintar nada para que el indicador de
        // espera no se solape con la conversación.
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
            .StartAsync(
                "Conectando en tiempo real...",
                async _ => await _tiempoReal.ConectarAsync(configuracion.RutaHub, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        var historial = await _api
            .ObtenerMensajesAsync(sala.Id, _opciones.MensajesHistorialInicial, cancelacion)
            .ConfigureAwait(false);

        Presentacion.LimpiarPantalla();
        Presentacion.CabeceraConversacion(sala, sesion.NombreUsuario);

        foreach (var mensaje in historial)
        {
            Presentacion.LineaMensaje(mensaje, mensaje.UsuarioId == sesion.UsuarioId);
        }

        Presentacion.LineaSistema("Escriba su mensaje y pulse Intro. '/ayuda' muestra las órdenes disponibles.");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));

        var suscripcion = Suscribir(sala, sesion);

        try
        {
            await _tiempoReal.MarcarLeidaAsync(sala.Id, cancelacion).ConfigureAwait(false);
            await BucleEntradaAsync(sesion, sala, cancelacion).ConfigureAwait(false);
        }
        finally
        {
            suscripcion.Retirar();
        }

        // Al salir, la conversación queda al día: lo leído en pantalla no debe
        // volver a contarse como pendiente.
        await MarcarLeidaSilenciosamenteAsync(sala.Id).ConfigureAwait(false);

        Presentacion.LineaSistema("Conversación cerrada.");
    }

    /// <summary>Registra los manejadores de los eventos del hub para esta conversación.</summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <returns>Objeto que retira las suscripciones al cerrarse la conversación.</returns>
    private Suscripciones Suscribir(SalaDto sala, SesionAlmacenada sesion)
    {
        void AlRecibirMensaje(MensajeDto mensaje)
        {
            // Solo se pintan los mensajes de la sala abierta; el usuario puede
            // pertenecer a varias y recibirlas todas por la misma conexión.
            if (mensaje.SalaId == sala.Id)
            {
                Presentacion.LineaMensaje(mensaje, mensaje.UsuarioId == sesion.UsuarioId);
            }
            else
            {
                Presentacion.LineaSistema($"Mensaje nuevo de {mensaje.NombreUsuario} en otra conversación.");
            }
        }

        void AlUnirse(string nombreSala, string usuario)
        {
            if (string.Equals(nombreSala, sala.Nombre, StringComparison.OrdinalIgnoreCase))
            {
                Presentacion.LineaSistema($"{usuario} se ha unido a la sala.");
            }
        }

        void AlSalir(string nombreSala, string usuario)
        {
            if (string.Equals(nombreSala, sala.Nombre, StringComparison.OrdinalIgnoreCase))
            {
                Presentacion.LineaSistema($"{usuario} ha salido de la sala.");
            }
        }

        void AlEscribir(Guid salaId, string usuario)
        {
            if (salaId == sala.Id)
            {
                Presentacion.LineaSistema($"{usuario} está escribiendo...");
            }
        }

        void AlCambiarPresencia(PresenciaDto presencia)
        {
            if (presencia.UsuarioId != sesion.UsuarioId)
            {
                Presentacion.LineaPresencia(presencia.NombreUsuario, presencia.EnLinea);
            }
        }

        void AlAbrirseSala(SalaDto nueva)
            => Presentacion.LineaSistema(
                nueva.EsDirecta
                    ? $"{nueva.Nombre} te ha abierto una conversación privada."
                    : $"Te han añadido a la sala '{nueva.Nombre}'.");

        void AlRecibirError(string mensaje) => Presentacion.Aviso(mensaje);

        void AlCambiarEstado(string estado)
        {
            if (estado is not "conectado")
            {
                Presentacion.LineaSistema($"Estado de la conexión: {estado}.");
            }
        }

        _tiempoReal.MensajeRecibido += AlRecibirMensaje;
        _tiempoReal.UsuarioUnido += AlUnirse;
        _tiempoReal.UsuarioSalido += AlSalir;
        _tiempoReal.UsuarioEscribiendo += AlEscribir;
        _tiempoReal.PresenciaCambiada += AlCambiarPresencia;
        _tiempoReal.SalaDisponible += AlAbrirseSala;
        _tiempoReal.ErrorRecibido += AlRecibirError;
        _tiempoReal.EstadoCambiado += AlCambiarEstado;

        return new Suscripciones(() =>
        {
            _tiempoReal.MensajeRecibido -= AlRecibirMensaje;
            _tiempoReal.UsuarioUnido -= AlUnirse;
            _tiempoReal.UsuarioSalido -= AlSalir;
            _tiempoReal.UsuarioEscribiendo -= AlEscribir;
            _tiempoReal.PresenciaCambiada -= AlCambiarPresencia;
            _tiempoReal.SalaDisponible -= AlAbrirseSala;
            _tiempoReal.ErrorRecibido -= AlRecibirError;
            _tiempoReal.EstadoCambiado -= AlCambiarEstado;
        });
    }

    /// <summary>Lee líneas de la consola y las envía hasta que el usuario escribe «/salir».</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task BucleEntradaAsync(SesionAlmacenada sesion, SalaDto sala, CancellationToken cancelacion)
    {
        while (!cancelacion.IsCancellationRequested)
        {
            var linea = await LeerLineaAsync(sala.Id, cancelacion).ConfigureAwait(false);

            if (linea is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            if (linea[0] == '/')
            {
                if (await ProcesarOrdenAsync(linea, sesion, sala, cancelacion).ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            try
            {
                await _tiempoReal.EnviarMensajeAsync(sala.Id, linea, cancelacion).ConfigureAwait(false);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                Presentacion.Aviso($"No se pudo enviar el mensaje: {excepcion.Message}");
            }
        }
    }

    /// <summary>
    /// Ejecuta una orden de barra dentro de la conversación.
    /// </summary>
    /// <param name="linea">Línea escrita por el usuario, que empieza por «/».</param>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si la orden cierra la conversación.</returns>
    private async Task<bool> ProcesarOrdenAsync(
        string linea,
        SesionAlmacenada sesion,
        SalaDto sala,
        CancellationToken cancelacion)
    {
        var separador = linea.IndexOf(' ', StringComparison.Ordinal);
        var orden = separador < 0 ? linea : linea[..separador];
        var argumento = separador < 0 ? string.Empty : linea[(separador + 1)..].Trim();

        switch (orden.ToLowerInvariant())
        {
            case "/salir":
                return true;

            case "/ayuda":
                Presentacion.AyudaConversacion();
                return false;

            case "/limpiar":
                Presentacion.LimpiarPantalla();
                Presentacion.CabeceraConversacion(sala, sesion.NombreUsuario);
                return false;

            case "/miembros":
                await MostrarMiembrosAsync(sala, cancelacion).ConfigureAwait(false);
                return false;

            case "/historial":
                await MostrarHistorialAsync(sesion, sala, cancelacion).ConfigureAwait(false);
                return false;

            case "/invitar":
                await InvitarAsync(sala, argumento, cancelacion).ConfigureAwait(false);
                return false;

            default:
                Presentacion.Aviso($"Orden desconocida: {orden}. Use '/ayuda' para ver las disponibles.");
                return false;
        }
    }

    /// <summary>Muestra los miembros de la sala y su estado de conexión.</summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task MostrarMiembrosAsync(SalaDto sala, CancellationToken cancelacion)
    {
        try
        {
            var miembros = await _tiempoReal.ListarMiembrosAsync(sala.Id, cancelacion).ConfigureAwait(false);
            Presentacion.TablaMiembros(miembros, sala.Nombre);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo consultar la lista de miembros: {excepcion.Message}");
        }
    }

    /// <summary>Vuelve a pintar el historial reciente de la sala.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task MostrarHistorialAsync(SesionAlmacenada sesion, SalaDto sala, CancellationToken cancelacion)
    {
        try
        {
            var mensajes = await _api
                .ObtenerMensajesAsync(sala.Id, _opciones.MensajesHistorialInicial, cancelacion)
                .ConfigureAwait(false);

            AnsiConsole.Write(new Rule("[grey]historial[/]").RuleStyle("grey"));

            foreach (var mensaje in mensajes)
            {
                Presentacion.LineaMensaje(mensaje, mensaje.UsuarioId == sesion.UsuarioId);
            }

            AnsiConsole.Write(new Rule().RuleStyle("grey"));
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo cargar el historial: {excepcion.Message}");
        }
    }

    /// <summary>Incorpora a otra persona a la sala abierta.</summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="nombreUsuario">Nombre del invitado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task InvitarAsync(SalaDto sala, string nombreUsuario, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario))
        {
            Presentacion.Aviso("Indique a quién quiere invitar: '/invitar <usuario>'.");
            return;
        }

        try
        {
            var resultado = await _api
                .InvitarASalaAsync(sala.Id, nombreUsuario, cancelacion)
                .ConfigureAwait(false);

            Presentacion.LineaSistema(resultado.Mensaje);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo invitar a '{nombreUsuario}': {excepcion.Message}");
        }
    }

    /// <summary>
    /// Lee una línea sin bloquear la recepción de mensajes y avisa a la sala en
    /// cuanto el usuario empieza a teclear.
    /// </summary>
    /// <param name="salaId">Sala en la que se escribe.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La línea escrita, o <c>null</c> si se cerró la entrada.</returns>
    private async Task<string?> LeerLineaAsync(Guid salaId, CancellationToken cancelacion)
    {
        await EsperarPrimeraTeclaAsync(salaId, cancelacion).ConfigureAwait(false);

        // La lectura se hace en un hilo del grupo para no bloquear las
        // notificaciones que llegan por SignalR.
        return await Task.Run(Console.ReadLine, cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Espera a que haya alguna tecla pulsada y avisa al resto de la sala de que el
    /// usuario está escribiendo.
    /// </summary>
    /// <remarks>
    /// Se sondea el búfer del teclado en lugar de leer las teclas una a una: así el
    /// aviso sale en cuanto se pulsa la primera tecla, pero la línea la sigue leyendo
    /// la consola, que ya sabe manejar el borrado, el pegado y el historial.
    /// Con la entrada redirigida (guiones, tuberías) no hay nada que sondear.
    /// </remarks>
    /// <param name="salaId">Sala en la que se escribe.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task EsperarPrimeraTeclaAsync(Guid salaId, CancellationToken cancelacion)
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        while (!cancelacion.IsCancellationRequested && !Console.KeyAvailable)
        {
            await Task.Delay(IntervaloSondeoTeclado, cancelacion).ConfigureAwait(false);
        }

        if (cancelacion.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await _tiempoReal.AvisarEscribiendoAsync(salaId, cancelacion).ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // El aviso de escritura es cosmético: si falla, no se interrumpe la escritura.
        }
    }

    /// <summary>Marca la sala como leída sin molestar al usuario si la llamada falla.</summary>
    /// <param name="salaId">Sala leída.</param>
    private async Task MarcarLeidaSilenciosamenteAsync(Guid salaId)
    {
        try
        {
            await _tiempoReal.MarcarLeidaAsync(salaId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // La conversación ya está cerrada: no hay a quién informar.
        }
    }

    /// <summary>Retira, al liberarse, las suscripciones registradas para una conversación.</summary>
    /// <param name="retirar">Acción que anula las suscripciones.</param>
    private sealed class Suscripciones(Action retirar)
    {
        /// <summary>Anula todas las suscripciones registradas.</summary>
        public void Retirar() => retirar();
    }
}
