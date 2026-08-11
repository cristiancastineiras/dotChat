using System.Collections.Frozen;
using System.Text;

namespace Chat.Aplicacion.Mensajeria;

/// <summary>Un emoji del catálogo, con su nombre canónico y su categoría.</summary>
/// <param name="Nombre">Nombre canónico, en minúsculas y sin acentos.</param>
/// <param name="Simbolo">Carácter o secuencia Unicode que representa al emoji.</param>
/// <param name="Categoria">Grupo con el que se presenta en la ayuda.</param>
/// <param name="Alias">Nombres alternativos aceptados, normalmente en inglés.</param>
public sealed record EntradaEmoji(string Nombre, string Simbolo, string Categoria, params string[] Alias);

/// <summary>
/// Traduce los atajos <c>:nombre:</c> que escribe el usuario al emoji correspondiente.
/// </summary>
/// <remarks>
/// <para>
/// La expansión se hace en el servidor, dentro del envío del mensaje, y no en cada
/// cliente. Así lo que se cifra y se guarda es ya el emoji: el historial se lee igual
/// desde cualquier terminal y dos clientes con catálogos distintos no muestran cosas
/// diferentes para el mismo mensaje.
/// </para>
/// <para>
/// Escribir el emoji directamente sigue funcionando; los atajos existen para los
/// teclados y terminales desde los que no es cómodo insertarlo.
/// </para>
/// </remarks>
public static class CatalogoEmojis
{
    /// <summary>Longitud máxima admitida para el nombre de un atajo.</summary>
    private const int LongitudMaximaNombre = 32;

    /// <summary>Catálogo completo, en el orden en que se presenta al usuario.</summary>
    public static readonly IReadOnlyList<EntradaEmoji> Entradas =
    [
        // Caras y gestos
        new("sonrisa", "\U0001F604", "Caras", "smile"),
        new("risa", "\U0001F602", "Caras", "joy", "lol"),
        new("carcajada", "\U0001F923", "Caras", "rofl"),
        new("guino", "\U0001F609", "Caras", "wink"),
        new("sonrojo", "\U0001F60A", "Caras", "blush"),
        new("gafas", "\U0001F60E", "Caras", "cool", "sunglasses"),
        new("pensando", "\U0001F914", "Caras", "thinking"),
        new("triste", "\U0001F614", "Caras", "sad", "pensive"),
        new("llorando", "\U0001F622", "Caras", "cry"),
        new("enfadado", "\U0001F620", "Caras", "angry"),
        new("sorpresa", "\U0001F62E", "Caras", "wow", "open_mouth"),
        new("miedo", "\U0001F631", "Caras", "scream"),
        new("sudor", "\U0001F605", "Caras", "sweat_smile"),
        new("silencio", "\U0001F910", "Caras", "zipper"),
        new("dormido", "\U0001F634", "Caras", "sleeping"),
        new("beso", "\U0001F618", "Caras", "kiss"),
        new("lengua", "\U0001F61B", "Caras", "tongue"),
        new("neutral", "\U0001F610", "Caras", "poker_face"),
        new("ojos", "\U0001F440", "Caras", "eyes"),
        new("guay", "\U0001F973", "Caras", "party_face"),

        // Manos y personas
        new("pulgar", "\U0001F44D", "Manos", "+1", "thumbsup", "ok"),
        new("pulgarabajo", "\U0001F44E", "Manos", "-1", "thumbsdown"),
        new("aplauso", "\U0001F44F", "Manos", "clap"),
        new("saludo", "\U0001F44B", "Manos", "wave", "hola"),
        new("choque", "\U0001F91D", "Manos", "handshake"),
        new("fuerza", "\U0001F4AA", "Manos", "muscle"),
        new("rezar", "\U0001F64F", "Manos", "pray", "gracias"),
        new("victoria", "✌️", "Manos", "v"),
        new("vale", "\U0001F44C", "Manos", "okhand"),
        new("senalar", "\U0001F449", "Manos", "point"),

        // Corazones y símbolos
        new("corazon", "❤️", "Símbolos", "heart"),
        new("corazonroto", "\U0001F494", "Símbolos", "broken_heart"),
        new("chispa", "✨", "Símbolos", "sparkles"),
        new("estrella", "⭐", "Símbolos", "star"),
        new("fuego", "\U0001F525", "Símbolos", "fire"),
        new("rayo", "⚡", "Símbolos", "zap"),
        new("bomba", "\U0001F4A3", "Símbolos", "bomb"),
        new("aviso", "⚠️", "Símbolos", "warning"),
        new("prohibido", "⛔", "Símbolos", "no_entry"),
        new("comprobado", "✅", "Símbolos", "check", "hecho"),
        new("cruz", "❌", "Símbolos", "x"),
        new("pregunta", "❓", "Símbolos", "question"),
        new("exclamacion", "❗", "Símbolos", "exclamation"),
        new("cien", "\U0001F4AF", "Símbolos", "100"),
        new("campana", "\U0001F514", "Símbolos", "bell"),
        new("candado", "\U0001F512", "Símbolos", "lock"),
        new("llave", "\U0001F511", "Símbolos", "key"),
        new("reloj", "⏰", "Símbolos", "alarm"),
        new("diana", "\U0001F3AF", "Símbolos", "dart", "objetivo"),
        new("trofeo", "\U0001F3C6", "Símbolos", "trophy"),

        // Animales y naturaleza
        new("perro", "\U0001F436", "Naturaleza", "dog"),
        new("gato", "\U0001F431", "Naturaleza", "cat"),
        new("raton", "\U0001F42D", "Naturaleza", "mouse"),
        new("zorro", "\U0001F98A", "Naturaleza", "fox"),
        new("oso", "\U0001F43B", "Naturaleza", "bear"),
        new("panda", "\U0001F43C", "Naturaleza"),
        new("unicornio", "\U0001F984", "Naturaleza", "unicorn"),
        new("bicho", "\U0001F41B", "Naturaleza", "bug"),
        new("arbol", "\U0001F333", "Naturaleza", "tree"),
        new("flor", "\U0001F338", "Naturaleza", "flower"),
        new("sol", "☀️", "Naturaleza", "sun"),
        new("luna", "\U0001F319", "Naturaleza", "moon"),
        new("nube", "☁️", "Naturaleza", "cloud"),
        new("lluvia", "\U0001F327️", "Naturaleza", "rain"),
        new("nieve", "❄️", "Naturaleza", "snow"),
        new("ola", "\U0001F30A", "Naturaleza", "wave_sea"),

        // Comida y bebida
        new("cafe", "☕", "Comida", "coffee"),
        new("cerveza", "\U0001F37A", "Comida", "beer"),
        new("brindis", "\U0001F942", "Comida", "cheers"),
        new("pizza", "\U0001F355", "Comida"),
        new("hamburguesa", "\U0001F354", "Comida", "burger"),
        new("tarta", "\U0001F370", "Comida", "cake"),
        new("manzana", "\U0001F34E", "Comida", "apple"),
        new("palomitas", "\U0001F37F", "Comida", "popcorn"),

        // Actividades y objetos
        new("fiesta", "\U0001F389", "Objetos", "tada", "party"),
        new("regalo", "\U0001F381", "Objetos", "gift"),
        new("musica", "\U0001F3B5", "Objetos", "music"),
        new("libro", "\U0001F4DA", "Objetos", "books"),
        new("lapiz", "✏️", "Objetos", "pencil"),
        new("carpeta", "\U0001F4C1", "Objetos", "folder"),
        new("grafico", "\U0001F4C8", "Objetos", "chart"),
        new("camara", "\U0001F4F7", "Objetos", "camera"),
        new("imagen", "\U0001F5BC️", "Objetos", "picture", "foto"),
        new("telefono", "\U0001F4F1", "Objetos", "phone"),
        new("correo", "\U0001F4E7", "Objetos", "mail"),
        new("cohete", "\U0001F680", "Objetos", "rocket"),
        new("avion", "✈️", "Objetos", "airplane"),
        new("coche", "\U0001F697", "Objetos", "car"),
        new("casa", "\U0001F3E0", "Objetos", "house"),
        new("reloj_arena", "⌛", "Objetos", "hourglass"),

        // Trabajo y desarrollo
        new("ordenador", "\U0001F4BB", "Desarrollo", "laptop", "pc"),
        new("teclado", "⌨️", "Desarrollo", "keyboard"),
        new("disquete", "\U0001F4BE", "Desarrollo", "floppy", "guardar"),
        new("engranaje", "⚙️", "Desarrollo", "gear", "config"),
        new("herramientas", "\U0001F6E0️", "Desarrollo", "tools"),
        new("martillo", "\U0001F528", "Desarrollo", "hammer"),
        new("lupa", "\U0001F50D", "Desarrollo", "search", "buscar"),
        new("bombilla", "\U0001F4A1", "Desarrollo", "idea", "bulb"),
        new("robot", "\U0001F916", "Desarrollo"),
        new("satelite", "\U0001F4E1", "Desarrollo", "satellite"),
        new("basura", "\U0001F5D1️", "Desarrollo", "trash"),
        new("etiqueta", "\U0001F3F7️", "Desarrollo", "label", "tag"),
        new("enlace", "\U0001F517", "Desarrollo", "link"),
        new("recargar", "\U0001F504", "Desarrollo", "refresh"),
        new("verde", "\U0001F7E2", "Desarrollo", "green"),
        new("rojo", "\U0001F534", "Desarrollo", "red"),
        new("amarillo", "\U0001F7E1", "Desarrollo", "yellow")
    ];

    /// <summary>Índice de nombre y alias a símbolo. Se construye una sola vez.</summary>
    private static readonly FrozenDictionary<string, string> Indice = ConstruirIndice();

    /// <summary>Número de atajos reconocidos, contando alias.</summary>
    public static int TotalAtajos => Indice.Count;

    /// <summary>
    /// Sustituye los atajos <c>:nombre:</c> por su emoji. Lo que no figura en el
    /// catálogo se deja intacto, de modo que un texto como <c>12:30:45</c> o una ruta
    /// de Windows no se ven alterados.
    /// </summary>
    /// <param name="texto">Texto escrito por el usuario.</param>
    /// <returns>El texto con los atajos ya sustituidos.</returns>
    public static string Expandir(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || !texto.Contains(':', StringComparison.Ordinal))
        {
            return texto ?? string.Empty;
        }

        StringBuilder? resultado = null;

        // Dos cursores distintos, y ahí está la sutileza: «copiado» marca hasta dónde
        // se ha volcado ya el original y solo avanza al sustituir, mientras que
        // «busqueda» recorre los delimitadores y avanza también cuando un candidato se
        // descarta. Con un único cursor, un «12:30 :fuego:» perdería el «12:» al
        // saltarse el primer signo de dos puntos.
        var copiado = 0;
        var busqueda = 0;

        while (busqueda < texto.Length)
        {
            var apertura = texto.IndexOf(':', busqueda);

            if (apertura < 0)
            {
                break;
            }

            var cierre = texto.IndexOf(':', apertura + 1);

            if (cierre < 0)
            {
                break;
            }

            var nombre = texto.AsSpan(apertura + 1, cierre - apertura - 1);

            if (!EsNombreDeAtajo(nombre)
                || !Indice.TryGetValue(nombre.ToString().ToLowerInvariant(), out var simbolo))
            {
                // Este signo de dos puntos no abría un atajo, pero el siguiente aún
                // puede hacerlo: se retoma desde ahí en vez de descartar el resto.
                busqueda = apertura + 1;
                continue;
            }

            resultado ??= new StringBuilder(texto.Length);
            resultado.Append(texto, copiado, apertura - copiado).Append(simbolo);

            copiado = cierre + 1;
            busqueda = copiado;
        }

        if (resultado is null)
        {
            return texto;
        }

        return resultado.Append(texto, copiado, texto.Length - copiado).ToString();
    }

    /// <summary>Busca el emoji asociado a un nombre o alias.</summary>
    /// <param name="nombre">Nombre del atajo, sin los dos puntos.</param>
    /// <param name="simbolo">Emoji encontrado.</param>
    /// <returns><c>true</c> si el nombre está en el catálogo.</returns>
    public static bool TryObtener(string nombre, out string? simbolo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            simbolo = null;
            return false;
        }

        return Indice.TryGetValue(nombre.Trim().ToLowerInvariant(), out simbolo);
    }

    /// <summary>Devuelve el catálogo agrupado por categoría, en orden de presentación.</summary>
    public static IEnumerable<IGrouping<string, EntradaEmoji>> PorCategoria()
        => Entradas.GroupBy(entrada => entrada.Categoria);

    /// <summary>
    /// Comprueba que lo que hay entre dos signos de dos puntos tiene forma de atajo:
    /// corto y compuesto solo de letras ASCII, dígitos y separadores sencillos.
    /// </summary>
    /// <param name="nombre">Texto encontrado entre los delimitadores.</param>
    private static bool EsNombreDeAtajo(ReadOnlySpan<char> nombre)
    {
        if (nombre.Length is 0 or > LongitudMaximaNombre)
        {
            return false;
        }

        foreach (var caracter in nombre)
        {
            var admitido = char.IsAsciiLetterOrDigit(caracter)
                || caracter is '_' or '-' or '+';

            if (!admitido)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Construye el índice de nombres y alias a partir del catálogo.</summary>
    /// <exception cref="InvalidOperationException">Si dos entradas comparten nombre o alias.</exception>
    private static FrozenDictionary<string, string> ConstruirIndice()
    {
        var indice = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entrada in Entradas)
        {
            Registrar(indice, entrada.Nombre, entrada.Simbolo);

            foreach (var alias in entrada.Alias)
            {
                Registrar(indice, alias, entrada.Simbolo);
            }
        }

        return indice.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Añade una clave al índice comprobando que no estuviera ya ocupada.</summary>
    /// <param name="indice">Índice en construcción.</param>
    /// <param name="clave">Nombre o alias.</param>
    /// <param name="simbolo">Emoji asociado.</param>
    private static void Registrar(Dictionary<string, string> indice, string clave, string simbolo)
    {
        // Un duplicado haría que un atajo dependiera del orden de la lista: se detecta
        // al cargar el tipo y no en producción, cuando alguien lo escriba.
        if (!indice.TryAdd(clave.ToLowerInvariant(), simbolo))
        {
            throw new InvalidOperationException(
                $"El catálogo de emojis define dos veces el atajo ':{clave}:'.");
        }
    }
}
