using Chat.Aplicacion.Dtos;
using Spectre.Console;

namespace Chat.ClienteCli.Servicios;

/// <summary>Acción que el usuario pide desde la lista de conversaciones.</summary>
public enum AccionLista
{
    /// <summary>Abrir la conversación seleccionada.</summary>
    Abrir,

    /// <summary>Empezar una conversación privada con alguien.</summary>
    NuevoPrivado,

    /// <summary>Ver el catálogo de salas para unirse a una.</summary>
    ExplorarSalas,

    /// <summary>Crear una sala nueva.</summary>
    CrearSala,

    /// <summary>Ver la lista de usuarios.</summary>
    VerUsuarios,

    /// <summary>Cerrar el cliente.</summary>
    Salir
}

/// <summary>Resultado de una interacción con la lista de conversaciones.</summary>
/// <param name="Accion">Qué ha pedido el usuario.</param>
/// <param name="Sala">Conversación seleccionada, cuando la acción es abrirla.</param>
public sealed record ResultadoLista(AccionLista Accion, SalaDto? Sala = null);

/// <summary>
/// Pantalla principal del cliente: la lista de conversaciones, navegable con el
/// teclado y con refresco en vivo.
/// </summary>
/// <remarks>
/// <para>
/// No se usa el selector que trae Spectre.Console porque bloquea hasta que el usuario
/// elige, y entonces un mensaje que llegara mientras tanto no podría repintar la lista.
/// Aquí el bucle es propio: sondea el teclado con un intervalo corto y, entre pulsación
/// y pulsación, comprueba si algo ha cambiado y vuelve a dibujar.
/// </para>
/// <para>
/// De ahí sale el comportamiento que uno espera de una aplicación de mensajería: estar
/// mirando la lista y ver aparecer el mensaje nuevo, con su contador y su conversación
/// subiendo al principio, sin tocar nada.
/// </para>
/// </remarks>
public sealed class PantallaListaChats
{
    /// <summary>Intervalo de sondeo del teclado.</summary>
    private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromMilliseconds(80);

    /// <summary>Cada cuánto se vuelven a pedir las conversaciones aunque no llegue nada.</summary>
    private static readonly TimeSpan IntervaloRefresco = TimeSpan.FromSeconds(20);

    private readonly ClienteApi _api;
    private readonly ClienteTiempoReal _tiempoReal;

    /// <summary>Conversaciones que se están mostrando.</summary>
    private IReadOnlyList<SalaDto> _salas = [];

    private int _seleccionada;
    private string _estadoConexion = "conectando";

    /// <summary>Indica que hay que volver a pedir las conversaciones al servidor.</summary>
    private volatile bool _datosCaducados = true;

    /// <summary>Indica que hay que volver a pintar la pantalla.</summary>
    private volatile bool _pantallaCaducada = true;

    /// <summary>Crea la pantalla.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="tiempoReal">Cliente de SignalR.</param>
    public PantallaListaChats(ClienteApi api, ClienteTiempoReal tiempoReal)
    {
        _api = api;
        _tiempoReal = tiempoReal;
    }

    /// <summary>
    /// Muestra la lista y no vuelve hasta que el usuario elige algo.
    /// </summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task<ResultadoLista> MostrarAsync(SesionAlmacenada sesion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        var suscripcion = Suscribir();
        var ultimoRefresco = DateTimeOffset.UtcNow;

        try
        {
            while (!cancelacion.IsCancellationRequested)
            {
                if (_datosCaducados || DateTimeOffset.UtcNow - ultimoRefresco > IntervaloRefresco)
                {
                    await RecargarAsync(cancelacion).ConfigureAwait(false);
                    ultimoRefresco = DateTimeOffset.UtcNow;
                }

                if (_pantallaCaducada)
                {
                    Pintar(sesion);
                    _pantallaCaducada = false;
                }

                var tecla = await EsperarTeclaAsync(cancelacion).ConfigureAwait(false);

                if (tecla is null)
                {
                    continue;
                }

                var resultado = Interpretar(tecla.Value);

                if (resultado is not null)
                {
                    return resultado;
                }
            }

            return new ResultadoLista(AccionLista.Salir);
        }
        finally
        {
            suscripcion.Retirar();
        }
    }

    /// <summary>Marca los datos como caducados para que se recarguen en la próxima vuelta.</summary>
    public void Invalidar()
    {
        _datosCaducados = true;
        _pantallaCaducada = true;
    }

    /// <summary>Suscribe la pantalla a los eventos que obligan a repintarla.</summary>
    private Suscripcion Suscribir()
    {
        // Cualquiera de estos eventos cambia lo que se ve en la lista: un mensaje
        // nuevo mueve la conversación al principio y sube el contador, y un cambio de
        // presencia enciende o apaga el punto verde.
        void AlLlegarAlgo(MensajeDto _) => Invalidar();
        void AlCambiarPresencia(PresenciaDto _) => Invalidar();
        void AlAbrirseSala(SalaDto _) => Invalidar();

        void AlCambiarEstado(string estado)
        {
            _estadoConexion = estado;
            _pantallaCaducada = true;
        }

        _tiempoReal.MensajeRecibido += AlLlegarAlgo;
        _tiempoReal.PresenciaCambiada += AlCambiarPresencia;
        _tiempoReal.SalaDisponible += AlAbrirseSala;
        _tiempoReal.EstadoCambiado += AlCambiarEstado;

        return new Suscripcion(() =>
        {
            _tiempoReal.MensajeRecibido -= AlLlegarAlgo;
            _tiempoReal.PresenciaCambiada -= AlCambiarPresencia;
            _tiempoReal.SalaDisponible -= AlAbrirseSala;
            _tiempoReal.EstadoCambiado -= AlCambiarEstado;
        });
    }

    /// <summary>Vuelve a pedir las conversaciones y conserva la selección.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task RecargarAsync(CancellationToken cancelacion)
    {
        // La bandera se baja antes de pedir los datos: si llega un mensaje mientras
        // dura la llamada, la próxima vuelta recarga otra vez en lugar de perderlo.
        _datosCaducados = false;

        try
        {
            var salas = await _api.ObtenerSalasPropiasAsync(cancelacion).ConfigureAwait(false);
            var anterior = _seleccionada < _salas.Count ? _salas[_seleccionada].Id : (Guid?)null;

            _salas = [.. salas.OrderByDescending(sala =>
                sala.UltimoMensaje?.FechaEnvio ?? sala.FechaUltimaActividad ?? sala.FechaCreacion)];

            // Se mantiene señalada la misma conversación aunque haya cambiado de sitio;
            // que el cursor salte solo porque alguien ha escrito sería desconcertante.
            var indice = anterior is null ? -1 : IndiceDe(anterior.Value);
            _seleccionada = indice >= 0 ? indice : Math.Clamp(_seleccionada, 0, Math.Max(0, _salas.Count - 1));

            _pantallaCaducada = true;
        }
        catch (Exception excepcion) when (excepcion is ExcepcionApi or HttpRequestException)
        {
            // El servidor no responde: se conserva lo último que se pintó y se vuelve
            // a intentar en la siguiente vuelta.
            _estadoConexion = "sin conexión";
            _pantallaCaducada = true;
        }
    }

    /// <summary>Dibuja la pantalla completa.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    private void Pintar(SesionAlmacenada sesion)
    {
        Presentacion.LimpiarPantalla();

        PresentacionChats.Cabecera(
            sesion.NombreUsuario,
            sesion.UrlServidor,
            _estadoConexion,
            _salas.Sum(sala => sala.MensajesSinLeer));

        PresentacionChats.Lista(_salas, _seleccionada);
        PresentacionChats.Atajos();
    }

    /// <summary>Traduce una pulsación en una acción, o en un movimiento del cursor.</summary>
    /// <param name="tecla">Tecla pulsada.</param>
    /// <returns>La acción elegida, o <c>null</c> si la pulsación no cierra la pantalla.</returns>
    private ResultadoLista? Interpretar(ConsoleKeyInfo tecla)
    {
        switch (tecla.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.K:
                Mover(-1);
                return null;

            case ConsoleKey.DownArrow or ConsoleKey.J:
                Mover(1);
                return null;

            case ConsoleKey.Home:
                Mover(-_salas.Count);
                return null;

            case ConsoleKey.End:
                Mover(_salas.Count);
                return null;

            case ConsoleKey.Enter when _salas.Count > 0:
                return new ResultadoLista(AccionLista.Abrir, _salas[_seleccionada]);

            case ConsoleKey.N:
                return new ResultadoLista(AccionLista.NuevoPrivado);

            case ConsoleKey.S:
                return new ResultadoLista(AccionLista.ExplorarSalas);

            case ConsoleKey.C:
                return new ResultadoLista(AccionLista.CrearSala);

            case ConsoleKey.U:
                return new ResultadoLista(AccionLista.VerUsuarios);

            case ConsoleKey.R:
                Invalidar();
                return null;

            case ConsoleKey.Q or ConsoleKey.Escape:
                return new ResultadoLista(AccionLista.Salir);

            default:
                return null;
        }
    }

    /// <summary>Mueve el cursor dentro de los límites de la lista.</summary>
    /// <param name="desplazamiento">Posiciones a desplazar.</param>
    private void Mover(int desplazamiento)
    {
        if (_salas.Count == 0)
        {
            return;
        }

        _seleccionada = Math.Clamp(_seleccionada + desplazamiento, 0, _salas.Count - 1);
        _pantallaCaducada = true;
    }

    /// <summary>Localiza una conversación por identificador.</summary>
    /// <param name="salaId">Conversación buscada.</param>
    private int IndiceDe(Guid salaId)
    {
        for (var indice = 0; indice < _salas.Count; indice++)
        {
            if (_salas[indice].Id == salaId)
            {
                return indice;
            }
        }

        return -1;
    }

    /// <summary>
    /// Espera a que se pulse una tecla sin bloquear el hilo, para que el bucle pueda
    /// seguir repintando cuando llegan mensajes.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La tecla pulsada, o <c>null</c> si toca volver a comprobar el estado.</returns>
    private static async Task<ConsoleKeyInfo?> EsperarTeclaAsync(CancellationToken cancelacion)
    {
        if (Console.KeyAvailable)
        {
            return Console.ReadKey(intercept: true);
        }

        await Task.Delay(IntervaloSondeo, cancelacion).ConfigureAwait(false);
        return null;
    }

    /// <summary>Retira, al liberarse, las suscripciones registradas.</summary>
    /// <param name="retirar">Acción que anula las suscripciones.</param>
    private sealed class Suscripcion(Action retirar)
    {
        /// <summary>Anula todas las suscripciones registradas.</summary>
        public void Retirar() => retirar();
    }
}
