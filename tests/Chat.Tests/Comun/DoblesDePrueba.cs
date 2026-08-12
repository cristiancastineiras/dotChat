using System.Collections.Concurrent;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Options;

namespace Chat.Tests.Comun;

/// <summary>Reloj detenido en un instante concreto, para que nada dependa de la hora real.</summary>
public sealed class RelojFijo : IProveedorFechaHora
{
    /// <summary>Crea el reloj en el instante indicado.</summary>
    /// <param name="ahora">Instante que devolverá; por defecto, el de referencia de las pruebas.</param>
    public RelojFijo(DateTimeOffset? ahora = null) => Ahora = ahora ?? Datos.Ahora;

    /// <inheritdoc />
    public DateTimeOffset Ahora { get; set; }

    /// <summary>Adelanta el reloj.</summary>
    /// <param name="intervalo">Tiempo que avanza.</param>
    public void Avanzar(TimeSpan intervalo) => Ahora = Ahora.Add(intervalo);
}

/// <summary>
/// Caché real pero mínima: un diccionario con las mismas semánticas de etiquetado por
/// prefijo que el adaptador de producción. Permite comprobar que un manejador invalida
/// lo que debe sin montar FusionCache ni Valkey.
/// </summary>
public sealed class CacheDePrueba : IServicioCache
{
    private readonly ConcurrentDictionary<string, object?> _entradas = new(StringComparer.Ordinal);

    /// <summary>Número de veces que se ha ejecutado el generador de un valor ausente.</summary>
    public int Generaciones { get; private set; }

    /// <summary>Etiquetas invalidadas, en orden de invalidación.</summary>
    public List<string> EtiquetasInvalidadas { get; } = [];

    /// <summary>Claves invalidadas una a una.</summary>
    public List<string> ClavesInvalidadas { get; } = [];

    /// <summary>Número de veces que se ha vaciado la caché por completo.</summary>
    public int Vaciados { get; private set; }

    /// <summary>Indica si hay algo guardado bajo la clave indicada.</summary>
    /// <param name="clave">Clave de caché.</param>
    public bool Contiene(string clave) => _entradas.ContainsKey(clave);

    /// <inheritdoc />
    public async Task<T> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<T>> generador,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(generador);

        if (_entradas.TryGetValue(clave, out var guardado))
        {
            return (T)guardado!;
        }

        Generaciones++;
        var valor = await generador(cancelacion).ConfigureAwait(false);
        _entradas[clave] = valor;

        return valor;
    }

    /// <inheritdoc />
    public Task EstablecerAsync<T>(
        string clave,
        T valor,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default)
    {
        _entradas[clave] = valor;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<T?> ObtenerAsync<T>(string clave, CancellationToken cancelacion = default)
        => Task.FromResult(_entradas.TryGetValue(clave, out var valor) ? (T?)valor : default);

    /// <inheritdoc />
    public Task InvalidarAsync(string clave, CancellationToken cancelacion = default)
    {
        ClavesInvalidadas.Add(clave);
        _entradas.TryRemove(clave, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InvalidarPorEtiquetaAsync(string etiqueta, CancellationToken cancelacion = default)
    {
        EtiquetasInvalidadas.Add(etiqueta);

        foreach (var clave in _entradas.Keys)
        {
            if (clave.StartsWith(etiqueta + ':', StringComparison.Ordinal))
            {
                _entradas.TryRemove(clave, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task LimpiarTodoAsync(CancellationToken cancelacion = default)
    {
        Vaciados++;
        _entradas.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>Almacén de objetos respaldado por un diccionario en memoria.</summary>
public sealed class AlmacenObjetosDePrueba : IAlmacenObjetos
{
    private readonly ConcurrentDictionary<string, byte[]> _objetos = new(StringComparer.Ordinal);

    /// <summary>Claves eliminadas, para comprobar la limpieza tras un fallo.</summary>
    public List<string> Eliminados { get; } = [];

    /// <summary>Excepción que lanzará la próxima escritura, si se ha programado una.</summary>
    public Exception? FalloAlGuardar { get; set; }

    /// <summary>Número de objetos almacenados.</summary>
    public int Total => _objetos.Count;

    /// <summary>Devuelve el contenido almacenado bajo una clave.</summary>
    /// <param name="clave">Clave del objeto.</param>
    public byte[] Contenido(string clave) => _objetos[clave];

    /// <inheritdoc />
    public Task PrepararAsync(CancellationToken cancelacion = default) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task GuardarAsync(
        string clave,
        Stream contenido,
        long tamano,
        string tipoMime,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (FalloAlGuardar is not null)
        {
            throw FalloAlGuardar;
        }

        using var destino = new MemoryStream();
        await contenido.CopyToAsync(destino, cancelacion).ConfigureAwait(false);

        _objetos[clave] = destino.ToArray();
    }

    /// <inheritdoc />
    public Task<Stream> AbrirAsync(string clave, CancellationToken cancelacion = default)
        => _objetos.TryGetValue(clave, out var contenido)
            ? Task.FromResult<Stream>(new MemoryStream(contenido, writable: false))
            : throw ExcepcionNoEncontrado.Para("El objeto", clave);

    /// <inheritdoc />
    public Task EliminarAsync(string clave, CancellationToken cancelacion = default)
    {
        Eliminados.Add(clave);
        _objetos.TryRemove(clave, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EliminarVariosAsync(IReadOnlyCollection<string> claves, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(claves);

        foreach (var clave in claves)
        {
            Eliminados.Add(clave);
            _objetos.TryRemove(clave, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RespondeAsync(CancellationToken cancelacion = default) => Task.FromResult(true);
}

/// <summary>Atajos para construir las opciones que reciben los servicios bajo prueba.</summary>
public static class Opciones
{
    /// <summary>Clave AES de 256 bits fija («clave-de-cifrado-de-32-bytes!!!!»).</summary>
    public const string ClaveCifradoBase64 = "Y2xhdmUtZGUtY2lmcmFkby1kZS0zMi1ieXRlcyEhISE=";

    /// <summary>Clave de firma JWT fija («clave-de-firma-jwt-de-32-bytes!!»).</summary>
    public const string ClaveFirmaBase64 = "Y2xhdmUtZGUtZmlybWEtand0LWRlLTMyLWJ5dGVzISE=";

    /// <summary>Envuelve un valor en <see cref="IOptions{TOptions}"/>.</summary>
    /// <typeparam name="T">Tipo de las opciones.</typeparam>
    /// <param name="valor">Instancia configurada.</param>
    public static IOptions<T> De<T>(T valor) where T : class => Microsoft.Extensions.Options.Options.Create(valor);

    /// <summary>Opciones de cifrado con la clave de pruebas.</summary>
    /// <param name="longitudMaximaMensaje">Longitud máxima de un mensaje.</param>
    public static CifradoOptions Cifrado(int longitudMaximaMensaje = 2000) => new()
    {
        ClaveBase64 = ClaveCifradoBase64,
        ContextoAsociado = "dotchat:prueba:v1",
        LongitudMaximaMensaje = longitudMaximaMensaje
    };

    /// <summary>Opciones JWT con la clave de pruebas.</summary>
    /// <param name="minutosAcceso">Vigencia del token de acceso.</param>
    /// <param name="diasRefresco">Vigencia del token de refresco.</param>
    public static JwtOptions Jwt(int minutosAcceso = 30, int diasRefresco = 7) => new()
    {
        Emisor = "dotchat-pruebas",
        Audiencia = "dotchat-clientes",
        ClaveFirmaBase64 = ClaveFirmaBase64,
        MinutosVigenciaAcceso = minutosAcceso,
        DiasVigenciaRefresco = diasRefresco
    };

    /// <summary>Opciones de adjuntos.</summary>
    /// <param name="activados">Permite adjuntar archivos.</param>
    /// <param name="tamanoMaximoBytes">Tamaño máximo de un archivo.</param>
    public static AdjuntosOptions Adjuntos(bool activados = true, long tamanoMaximoBytes = 25L * 1024 * 1024) => new()
    {
        Activados = activados,
        TamanoMaximoBytes = tamanoMaximoBytes
    };

    /// <summary>Opciones de caché.</summary>
    /// <param name="segundosVentanaAntiRepeticion">Ventana antirrepetición.</param>
    public static CacheOptions Cache(int segundosVentanaAntiRepeticion = 120) => new()
    {
        SegundosVentanaAntiRepeticion = segundosVentanaAntiRepeticion
    };

    /// <summary>Opciones de SignalR.</summary>
    /// <param name="maximoMensajesPorMinuto">Cupo de envíos por usuario y minuto.</param>
    public static SignalROptions SignalR(int maximoMensajesPorMinuto = 60) => new()
    {
        MaximoMensajesPorMinuto = maximoMensajesPorMinuto
    };
}
