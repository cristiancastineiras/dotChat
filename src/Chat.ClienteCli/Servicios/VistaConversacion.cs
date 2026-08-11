using System.Globalization;
using System.Threading.Channels;
using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Conversación en vivo sobre una sala: pinta el historial, recibe los mensajes
/// nuevos por SignalR y lee lo que el usuario escribe.
/// </summary>
/// <remarks>
/// <para>
/// Es un servicio compartido porque tanto <c>unirse</c> (salas) como <c>privado</c>
/// (conversaciones directas) abren exactamente la misma pantalla. Las suscripciones
/// a los eventos del hub se retiran al cerrar: la conexión sobrevive a la orden en la
/// consola interactiva y, sin retirarlas, la siguiente conversación pintaría cada
/// mensaje tantas veces como salas se hubieran abierto.
/// </para>
/// <para>
/// Los mensajes no se pintan desde el manejador del hub, sino que pasan por una cola
/// que consume un único hilo. Dibujar una imagen exige descargarla, y hacerlo dentro
/// del manejador bloquearía la recepción; hacerlo en paralelo mezclaría las líneas de
/// unos mensajes con las de otros. Con la cola, la conversación se lee siempre en el
/// orden en que llegó.
/// </para>
/// </remarks>
public sealed class VistaConversacion
{
    /// <summary>Intervalo de sondeo del teclado para detectar que el usuario escribe.</summary>
    private static readonly TimeSpan IntervaloSondeoTeclado = TimeSpan.FromMilliseconds(150);

    /// <summary>Número de adjuntos recientes que quedan al alcance de «/ver» y «/descargar».</summary>
    private const int MaximoAdjuntosRecordados = 30;

    private readonly ClienteApi _api;
    private readonly ClienteTiempoReal _tiempoReal;
    private readonly RenderizadorImagenes _renderizador;
    private readonly OpcionesCliente _opciones;

    /// <summary>Crea la vista.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="tiempoReal">Cliente de SignalR.</param>
    /// <param name="renderizador">Dibujante de las imágenes adjuntas.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public VistaConversacion(
        ClienteApi api,
        ClienteTiempoReal tiempoReal,
        RenderizadorImagenes renderizador,
        IOptions<OpcionesCliente> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _api = api;
        _tiempoReal = tiempoReal;
        _renderizador = renderizador;
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

        var pantalla = new PantallaMensajes(_renderizador, sesion.UsuarioId, cancelacion);

        foreach (var mensaje in historial)
        {
            pantalla.Encolar(mensaje);
        }

        Presentacion.LineaSistema(
            "Escriba su mensaje y pulse Intro. '/ayuda' muestra las órdenes disponibles, " +
            "'/emojis' los atajos y '/imagen <ruta>' comparte una foto.");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));

        var suscripcion = Suscribir(sala, sesion, pantalla);

        try
        {
            await _tiempoReal.MarcarLeidaAsync(sala.Id, cancelacion).ConfigureAwait(false);
            await BucleEntradaAsync(sesion, sala, pantalla, cancelacion).ConfigureAwait(false);
        }
        finally
        {
            suscripcion.Retirar();
            await pantalla.CerrarAsync().ConfigureAwait(false);
        }

        // Al salir, la conversación queda al día: lo leído en pantalla no debe
        // volver a contarse como pendiente.
        await MarcarLeidaSilenciosamenteAsync(sala.Id).ConfigureAwait(false);

        Presentacion.LineaSistema("Conversación cerrada.");
    }

    /// <summary>Registra los manejadores de los eventos del hub para esta conversación.</summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="pantalla">Cola por la que salen los mensajes a la consola.</param>
    /// <returns>Objeto que retira las suscripciones al cerrarse la conversación.</returns>
    private Suscripciones Suscribir(SalaDto sala, SesionAlmacenada sesion, PantallaMensajes pantalla)
    {
        void AlRecibirMensaje(MensajeDto mensaje)
        {
            // Solo se pintan los mensajes de la sala abierta; el usuario puede
            // pertenecer a varias y recibirlas todas por la misma conexión.
            if (mensaje.SalaId == sala.Id)
            {
                pantalla.Encolar(mensaje);
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
    /// <param name="pantalla">Cola de mensajes de la conversación.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task BucleEntradaAsync(
        SesionAlmacenada sesion,
        SalaDto sala,
        PantallaMensajes pantalla,
        CancellationToken cancelacion)
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
                if (await ProcesarOrdenAsync(linea, sesion, sala, pantalla, cancelacion).ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            try
            {
                await _tiempoReal.EnviarMensajeAsync(sala.Id, linea, cancelacion: cancelacion).ConfigureAwait(false);
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
    /// <param name="pantalla">Cola de mensajes de la conversación.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si la orden cierra la conversación.</returns>
    private async Task<bool> ProcesarOrdenAsync(
        string linea,
        SesionAlmacenada sesion,
        SalaDto sala,
        PantallaMensajes pantalla,
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

            case "/imagen" or "/archivo" or "/enviar":
                await EnviarArchivoAsync(sala, argumento, cancelacion).ConfigureAwait(false);
                return false;

            case "/ver":
                await RedibujarImagenAsync(pantalla, argumento, cancelacion).ConfigureAwait(false);
                return false;

            case "/descargar":
                await DescargarAsync(pantalla, argumento, cancelacion).ConfigureAwait(false);
                return false;

            case "/adjuntos":
                Presentacion.TablaAdjuntos(pantalla.Adjuntos());
                return false;

            case "/emojis":
                Presentacion.TablaEmojis();
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

            // El historial se repinta sin dibujar las imágenes: se listan sus fichas y
            // el usuario decide cuál quiere ver con «/ver».
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
    /// Sube un archivo y lo publica en la sala. El texto que lo acompaña es opcional y
    /// va detrás de la ruta.
    /// </summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="argumento">Ruta del fichero y, opcionalmente, el texto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task EnviarArchivoAsync(SalaDto sala, string argumento, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(argumento))
        {
            Presentacion.Aviso("Indique el archivo que quiere enviar: '/archivo <ruta> [texto]'.");
            return;
        }

        var (ruta, texto) = SepararRutaYPie(argumento);

        try
        {
            var adjunto = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
                .StartAsync(
                    "Subiendo el archivo...",
                    async _ => await _api.SubirAdjuntoAsync(sala.Id, ruta, cancelacion).ConfigureAwait(false))
                .ConfigureAwait(false);

            // El mensaje se publica por el hub, igual que el texto: así llega a los
            // demás al instante y con el mismo camino de difusión. Por el hub solo
            // viaja el identificador; los bytes ya están en el servidor.
            await _tiempoReal
                .EnviarMensajeAsync(sala.Id, texto, adjunto.Id, cancelacion)
                .ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo enviar el archivo: {excepcion.Message}");
        }
    }

    /// <summary>Descarga a disco uno de los archivos recibidos en la conversación.</summary>
    /// <param name="pantalla">Cola de mensajes, que recuerda los adjuntos vistos.</param>
    /// <param name="argumento">Posición pedida; 1 es el más reciente.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task DescargarAsync(
        PantallaMensajes pantalla,
        string argumento,
        CancellationToken cancelacion)
    {
        var posicion = 1;

        if (!string.IsNullOrWhiteSpace(argumento)
            && !int.TryParse(argumento, CultureInfo.InvariantCulture, out posicion))
        {
            Presentacion.Aviso("Indique el número del archivo: '/descargar <n>'. Use '/adjuntos' para verlos.");
            return;
        }

        var adjunto = pantalla.AdjuntoReciente(posicion);

        if (adjunto is null)
        {
            Presentacion.Aviso("No hay ningún archivo en esa posición dentro de esta conversación.");
            return;
        }

        try
        {
            var destino = ResolverDestino(adjunto.NombreArchivo);

            var bytes = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
                .StartAsync(
                    $"Descargando '{adjunto.NombreArchivo}'...",
                    async _ => await _api
                        .DescargarAdjuntoAArchivoAsync(adjunto.Id, destino, cancelacion)
                        .ConfigureAwait(false))
                .ConfigureAwait(false);

            Presentacion.Exito($"Guardado en {destino} ({Presentacion.FormatearTamano(bytes)}).");
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo descargar '{adjunto.NombreArchivo}': {excepcion.Message}");
        }
    }

    /// <summary>
    /// Decide dónde guardar una descarga y evita pisar un fichero que ya exista
    /// añadiendo un número al nombre.
    /// </summary>
    /// <param name="nombreArchivo">Nombre propuesto por el servidor, ya saneado.</param>
    private static string ResolverDestino(string nombreArchivo)
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        // Si no existe la carpeta habitual de descargas —hay sistemas donde no la
        // hay—, se usa el directorio de trabajo, que siempre está.
        if (!Directory.Exists(carpeta))
        {
            carpeta = Directory.GetCurrentDirectory();
        }

        var destino = Path.Combine(carpeta, nombreArchivo);

        if (!File.Exists(destino))
        {
            return destino;
        }

        var raiz = Path.GetFileNameWithoutExtension(nombreArchivo);
        var extension = Path.GetExtension(nombreArchivo);

        for (var intento = 1; intento < 1000; intento++)
        {
            var candidato = Path.Combine(carpeta, $"{raiz} ({intento}){extension}");

            if (!File.Exists(candidato))
            {
                return candidato;
            }
        }

        return Path.Combine(carpeta, $"{raiz}-{Guid.CreateVersion7():N}{extension}");
    }

    /// <summary>Vuelve a dibujar una de las imágenes recibidas en la conversación.</summary>
    /// <param name="pantalla">Cola de mensajes, que recuerda las imágenes vistas.</param>
    /// <param name="argumento">Posición pedida; 1 es la más reciente.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task RedibujarImagenAsync(
        PantallaMensajes pantalla,
        string argumento,
        CancellationToken cancelacion)
    {
        var posicion = 1;

        if (!string.IsNullOrWhiteSpace(argumento)
            && !int.TryParse(argumento, CultureInfo.InvariantCulture, out posicion))
        {
            Presentacion.Aviso("Indique el número de la imagen: '/ver <n>', donde 1 es la más reciente.");
            return;
        }

        var adjunto = pantalla.ImagenReciente(posicion);

        if (adjunto is null)
        {
            Presentacion.Aviso("No hay ninguna imagen en esa posición dentro de esta conversación.");
            return;
        }

        await _renderizador.DibujarAsync(adjunto, cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Separa la ruta del pie de foto. Se admiten comillas para las rutas con espacios,
    /// que en Windows son la norma más que la excepción.
    /// </summary>
    /// <param name="argumento">Todo lo escrito detrás de «/imagen».</param>
    private static (string Ruta, string Pie) SepararRutaYPie(string argumento)
    {
        if (argumento[0] is '"' or '\'')
        {
            var comilla = argumento[0];
            var cierre = argumento.IndexOf(comilla, 1);

            if (cierre > 0)
            {
                return (argumento[1..cierre], argumento[(cierre + 1)..].Trim());
            }
        }

        var espacio = argumento.IndexOf(' ', StringComparison.Ordinal);

        return espacio < 0
            ? (argumento, string.Empty)
            : (argumento[..espacio], argumento[(espacio + 1)..].Trim());
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

    /// <summary>
    /// Cola de salida de la conversación: recibe los mensajes desde el hub y los pinta
    /// uno tras otro en un único hilo, descargando por el camino las imágenes.
    /// </summary>
    private sealed class PantallaMensajes
    {
        private readonly RenderizadorImagenes _renderizador;
        private readonly Guid _usuarioId;
        private readonly Channel<MensajeDto> _cola;
        private readonly Task _consumidor;

        /// <summary>Adjuntos vistos en la conversación, en orden de llegada.</summary>
        private readonly List<AdjuntoDto> _adjuntos = [];

        private readonly Lock _cerrojoAdjuntos = new();

        /// <summary>Crea la cola y arranca el consumidor.</summary>
        /// <param name="renderizador">Dibujante de imágenes.</param>
        /// <param name="usuarioId">Usuario conectado, para distinguir sus propios mensajes.</param>
        /// <param name="cancelacion">Token de cancelación de la conversación.</param>
        public PantallaMensajes(RenderizadorImagenes renderizador, Guid usuarioId, CancellationToken cancelacion)
        {
            _renderizador = renderizador;
            _usuarioId = usuarioId;

            // Un solo lector: es lo que garantiza que las líneas salgan en orden.
            _cola = Channel.CreateUnbounded<MensajeDto>(new UnboundedChannelOptions
            {
                SingleReader = true
            });

            _consumidor = ConsumirAsync(cancelacion);
        }

        /// <summary>Añade un mensaje a la cola de pintado.</summary>
        /// <param name="mensaje">Mensaje recibido o cargado del historial.</param>
        public void Encolar(MensajeDto mensaje) => _cola.Writer.TryWrite(mensaje);

        /// <summary>Devuelve una de las imágenes vistas en la conversación.</summary>
        /// <param name="posicion">Posición contando desde la más reciente; 1 es la última.</param>
        /// <returns>La imagen pedida, o <c>null</c> si no hay tantas.</returns>
        public AdjuntoDto? ImagenReciente(int posicion)
            => Reciente(posicion, adjunto => adjunto.EsImagen);

        /// <summary>Devuelve uno de los archivos vistos en la conversación.</summary>
        /// <param name="posicion">Posición contando desde el más reciente; 1 es el último.</param>
        /// <returns>El adjunto pedido, o <c>null</c> si no hay tantos.</returns>
        public AdjuntoDto? AdjuntoReciente(int posicion) => Reciente(posicion, _ => true);

        /// <summary>Devuelve los adjuntos vistos, del más reciente al más antiguo.</summary>
        public IReadOnlyList<AdjuntoDto> Adjuntos()
        {
            lock (_cerrojoAdjuntos)
            {
                return [.. Enumerable.Reverse(_adjuntos)];
            }
        }

        /// <summary>Localiza el enésimo adjunto contando desde el final.</summary>
        /// <param name="posicion">Posición pedida; 1 es el más reciente.</param>
        /// <param name="filtro">Condición que debe cumplir el adjunto.</param>
        private AdjuntoDto? Reciente(int posicion, Func<AdjuntoDto, bool> filtro)
        {
            if (posicion < 1)
            {
                return null;
            }

            lock (_cerrojoAdjuntos)
            {
                var vistos = 0;

                for (var indice = _adjuntos.Count - 1; indice >= 0; indice--)
                {
                    if (filtro(_adjuntos[indice]) && ++vistos == posicion)
                    {
                        return _adjuntos[indice];
                    }
                }

                return null;
            }
        }

        /// <summary>Cierra la cola y espera a que se pinte lo que quedaba pendiente.</summary>
        public async Task CerrarAsync()
        {
            _cola.Writer.TryComplete();

            try
            {
                await _consumidor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // La conversación se cerró mientras quedaban mensajes por pintar.
            }
        }

        /// <summary>Pinta los mensajes de la cola hasta que se cierra.</summary>
        /// <param name="cancelacion">Token de cancelación de la conversación.</param>
        private async Task ConsumirAsync(CancellationToken cancelacion)
        {
            await foreach (var mensaje in _cola.Reader.ReadAllAsync(cancelacion).ConfigureAwait(false))
            {
                Presentacion.LineaMensaje(mensaje, mensaje.UsuarioId == _usuarioId);

                if (mensaje.Adjunto is not { } adjunto)
                {
                    continue;
                }

                lock (_cerrojoAdjuntos)
                {
                    _adjuntos.Add(adjunto);

                    if (_adjuntos.Count > MaximoAdjuntosRecordados)
                    {
                        _adjuntos.RemoveAt(0);
                    }
                }

                // Solo las imágenes se dibujan; de un archivo cualquiera basta con la
                // ficha que ya se ha impreso, y el usuario decide si lo descarga.
                if (adjunto.EsImagen && _renderizador.DibujaAlRecibir)
                {
                    await _renderizador.DibujarAsync(adjunto, cancelacion).ConfigureAwait(false);
                }
            }
        }
    }
}
