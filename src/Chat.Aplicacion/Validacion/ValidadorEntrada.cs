using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Chat.Dominio.Excepciones;

namespace Chat.Aplicacion.Validacion;

/// <summary>
/// Validación y saneamiento de las entradas de usuario.
/// Es la primera línea de defensa: normaliza, recorta y rechaza cualquier valor
/// que no cumpla el formato esperado antes de llegar al dominio o a la base de datos.
/// </summary>
public static partial class ValidadorEntrada
{
    /// <summary>Longitud mínima de un nombre de usuario.</summary>
    public const int LongitudMinimaNombreUsuario = 3;

    /// <summary>Longitud máxima de un nombre de usuario.</summary>
    public const int LongitudMaximaNombreUsuario = 32;

    /// <summary>Longitud mínima de una contraseña.</summary>
    public const int LongitudMinimaClave = 10;

    /// <summary>Longitud máxima de una contraseña (evita ataques de denegación por hashing costoso).</summary>
    public const int LongitudMaximaClave = 128;

    /// <summary>Longitud mínima del nombre de una sala.</summary>
    public const int LongitudMinimaNombreSala = 3;

    /// <summary>Longitud máxima del nombre de una sala.</summary>
    public const int LongitudMaximaNombreSala = 48;

    /// <summary>Longitud máxima de la descripción de una sala.</summary>
    public const int LongitudMaximaDescripcionSala = 256;

    /// <summary>Longitud máxima de un correo electrónico.</summary>
    public const int LongitudMaximaEmail = 254;

    /// <summary>Longitud máxima del nombre de un fichero adjunto.</summary>
    public const int LongitudMaximaNombreArchivo = 128;

    /// <summary>
    /// Unión de secuencia («zero width joiner»). Es el pegamento de los emojis
    /// compuestos: sin él, 👨‍👩‍👧 se descompone en tres personas sueltas.
    /// </summary>
    private const char UnionSecuencia = '\u200D';

    [GeneratedRegex(@"^[a-zA-Z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronNombreUsuario();

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9 _-]*[a-zA-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronNombreSala();

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronEmail();

    /// <summary>
    /// Valida y normaliza un nombre de usuario: solo letras ASCII, dígitos, punto,
    /// guion y guion bajo, para evitar suplantaciones por caracteres homógrafos.
    /// </summary>
    /// <param name="valor">Valor recibido del cliente.</param>
    /// <returns>Nombre de usuario recortado.</returns>
    /// <exception cref="ExcepcionValidacion">Si el valor no cumple el formato.</exception>
    public static string ValidarNombreUsuario(string? valor)
    {
        var limpio = Sanear(valor);

        if (limpio.Length is < LongitudMinimaNombreUsuario or > LongitudMaximaNombreUsuario)
        {
            throw new ExcepcionValidacion(
                "nombreUsuario",
                $"El nombre de usuario debe tener entre {LongitudMinimaNombreUsuario} y {LongitudMaximaNombreUsuario} caracteres.");
        }

        if (!PatronNombreUsuario().IsMatch(limpio))
        {
            throw new ExcepcionValidacion(
                "nombreUsuario",
                "El nombre de usuario solo admite letras, dígitos, punto, guion y guion bajo.");
        }

        return limpio;
    }

    /// <summary>Valida y normaliza un correo electrónico.</summary>
    /// <param name="valor">Valor recibido del cliente.</param>
    /// <returns>Correo en minúsculas invariantes.</returns>
    /// <exception cref="ExcepcionValidacion">Si el valor no cumple el formato.</exception>
    public static string ValidarEmail(string? valor)
    {
        var limpio = Sanear(valor);

        if (limpio.Length is 0 or > LongitudMaximaEmail || !PatronEmail().IsMatch(limpio))
        {
            throw new ExcepcionValidacion("email", "El correo electrónico no tiene un formato válido.");
        }

        return limpio.ToLowerInvariant();
    }

    /// <summary>
    /// Valida una contraseña. Las reglas de complejidad detalladas las aplica Identity;
    /// aquí solo se comprueban longitud y ausencia de caracteres de control.
    /// </summary>
    /// <param name="valor">Contraseña en claro.</param>
    /// <returns>La misma contraseña, sin recortar (los espacios pueden ser intencionados).</returns>
    /// <exception cref="ExcepcionValidacion">Si el valor no cumple las reglas.</exception>
    public static string ValidarClave(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            throw new ExcepcionValidacion("clave", "La contraseña es obligatoria.");
        }

        if (valor.Length is < LongitudMinimaClave or > LongitudMaximaClave)
        {
            throw new ExcepcionValidacion(
                "clave",
                $"La contraseña debe tener entre {LongitudMinimaClave} y {LongitudMaximaClave} caracteres.");
        }

        if (valor.Any(char.IsControl))
        {
            throw new ExcepcionValidacion("clave", "La contraseña no puede contener caracteres de control.");
        }

        return valor;
    }

    /// <summary>Valida y normaliza el nombre de una sala.</summary>
    /// <param name="valor">Valor recibido del cliente.</param>
    /// <returns>Nombre saneado.</returns>
    /// <exception cref="ExcepcionValidacion">Si el valor no cumple el formato.</exception>
    public static string ValidarNombreSala(string? valor)
    {
        var limpio = Sanear(valor);

        if (limpio.Length is < LongitudMinimaNombreSala or > LongitudMaximaNombreSala)
        {
            throw new ExcepcionValidacion(
                "nombre",
                $"El nombre de la sala debe tener entre {LongitudMinimaNombreSala} y {LongitudMaximaNombreSala} caracteres.");
        }

        if (!PatronNombreSala().IsMatch(limpio))
        {
            throw new ExcepcionValidacion(
                "nombre",
                "El nombre de la sala solo admite letras, dígitos, espacios, guion y guion bajo, y no puede empezar ni terminar por separador.");
        }

        return limpio;
    }

    /// <summary>Valida y normaliza la descripción opcional de una sala.</summary>
    /// <param name="valor">Valor recibido del cliente.</param>
    /// <returns>Descripción saneada o <c>null</c> si venía vacía.</returns>
    /// <exception cref="ExcepcionValidacion">Si excede la longitud máxima.</exception>
    public static string? ValidarDescripcionSala(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpio = Sanear(valor);

        if (limpio.Length > LongitudMaximaDescripcionSala)
        {
            throw new ExcepcionValidacion(
                "descripcion",
                $"La descripción no puede superar {LongitudMaximaDescripcionSala} caracteres.");
        }

        return limpio;
    }

    /// <summary>Valida y sanea el texto de un mensaje.</summary>
    /// <param name="valor">Texto recibido del cliente.</param>
    /// <param name="longitudMaxima">Longitud máxima permitida según la configuración.</param>
    /// <returns>Texto saneado.</returns>
    /// <exception cref="ExcepcionValidacion">Si está vacío o excede la longitud máxima.</exception>
    public static string ValidarTextoMensaje(string? valor, int longitudMaxima)
    {
        var limpio = Sanear(valor, permitirSecuenciasEmoji: true);

        if (limpio.Length == 0)
        {
            throw new ExcepcionValidacion("texto", "El mensaje no puede estar vacío.");
        }

        if (limpio.Length > longitudMaxima)
        {
            throw new ExcepcionValidacion(
                "texto",
                $"El mensaje no puede superar {longitudMaxima} caracteres.");
        }

        return limpio;
    }

    /// <summary>
    /// Valida el texto opcional que acompaña a una imagen. A diferencia de
    /// <see cref="ValidarTextoMensaje"/>, admite que no haya nada que escribir.
    /// </summary>
    /// <param name="valor">Texto recibido del cliente.</param>
    /// <param name="longitudMaxima">Longitud máxima permitida según la configuración.</param>
    /// <returns>Texto saneado, o <c>null</c> si venía vacío.</returns>
    /// <exception cref="ExcepcionValidacion">Si excede la longitud máxima.</exception>
    public static string? ValidarPieDeFoto(string? valor, int longitudMaxima)
    {
        var limpio = Sanear(valor, permitirSecuenciasEmoji: true);

        if (limpio.Length == 0)
        {
            return null;
        }

        if (limpio.Length > longitudMaxima)
        {
            throw new ExcepcionValidacion(
                "texto",
                $"El texto que acompaña a la imagen no puede superar {longitudMaxima} caracteres.");
        }

        return limpio;
    }

    /// <summary>
    /// Valida y sanea el nombre de un fichero adjunto. Solo se conserva el nombre
    /// propiamente dicho: se descartan rutas y caracteres que confundirían al sistema
    /// de archivos de quien lo descargue.
    /// </summary>
    /// <param name="valor">Nombre recibido del cliente.</param>
    /// <returns>Nombre saneado; nunca vacío.</returns>
    public static string ValidarNombreArchivo(string? valor)
    {
        // El cliente puede mandar «..\..\etc\passwd» o una ruta absoluta: nos quedamos
        // solo con el último tramo y, aun así, lo saneamos carácter a carácter.
        var soloNombre = Sanear(valor);
        var separador = soloNombre.LastIndexOfAny(['/', '\\', ':']);

        if (separador >= 0)
        {
            soloNombre = soloNombre[(separador + 1)..];
        }

        var constructor = new StringBuilder(soloNombre.Length);

        foreach (var caracter in soloNombre)
        {
            constructor.Append(
                char.IsAsciiLetterOrDigit(caracter) || caracter is '.' or '-' or '_' or ' '
                    ? caracter
                    : '_');
        }

        var limpio = constructor.ToString().Trim(' ', '.');

        if (limpio.Length == 0)
        {
            return "imagen";
        }

        return limpio.Length > LongitudMaximaNombreArchivo
            ? limpio[..LongitudMaximaNombreArchivo]
            : limpio;
    }

    /// <summary>Valida que un identificador no sea el valor vacío.</summary>
    /// <param name="valor">Identificador recibido.</param>
    /// <param name="campo">Nombre del campo, usado en el mensaje de error.</param>
    /// <returns>El mismo identificador.</returns>
    /// <exception cref="ExcepcionValidacion">Si el identificador es <see cref="Guid.Empty"/>.</exception>
    public static Guid ValidarIdentificador(Guid valor, string campo)
    {
        if (valor == Guid.Empty)
        {
            throw new ExcepcionValidacion(campo, $"El campo '{campo}' debe ser un identificador válido.");
        }

        return valor;
    }

    /// <summary>Ajusta un tamaño de página al rango permitido.</summary>
    /// <param name="cantidad">Cantidad solicitada.</param>
    /// <param name="maximo">Máximo permitido.</param>
    /// <param name="porDefecto">Valor a usar si la cantidad no es positiva.</param>
    public static int NormalizarCantidad(int cantidad, int maximo = 200, int porDefecto = 50)
        => cantidad switch
        {
            <= 0 => porDefecto,
            _ when cantidad > maximo => maximo,
            _ => cantidad
        };

    /// <summary>
    /// Sanea una cadena: normaliza a forma canónica Unicode, recorta espacios,
    /// elimina caracteres de control y colapsa espacios consecutivos.
    /// Evita inyección de secuencias de escape ANSI en las consolas y de
    /// caracteres invisibles usados para suplantar identidades.
    /// </summary>
    /// <param name="valor">Cadena original.</param>
    /// <param name="permitirSecuenciasEmoji">
    /// Conserva la unión de secuencia, el único carácter invisible que hace falta para
    /// que los emojis compuestos —familias, profesiones, banderas— lleguen enteros.
    /// Solo se activa para el cuerpo de los mensajes: en los nombres de usuario y de
    /// sala, donde un carácter invisible sirve para suplantar a alguien, se sigue
    /// descartando todo.
    /// </param>
    /// <returns>Cadena saneada (nunca <c>null</c>).</returns>
    public static string Sanear(string? valor, bool permitirSecuenciasEmoji = false)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var normalizado = valor.Normalize(NormalizationForm.FormC);
        var constructor = new StringBuilder(normalizado.Length);
        var espacioPendiente = false;

        foreach (var caracter in normalizado)
        {
            // La unión de secuencia es el único formateador que se conserva, y solo
            // si une algo: al principio de la cadena no tendría nada que unir.
            var esUnionUtil = permitirSecuenciasEmoji
                && caracter == UnionSecuencia
                && constructor.Length > 0;

            // Del resto de controles y formateadores invisibles (categoría Cf: anulación
            // de dirección de escritura, marcas de orden de bytes...) no se conserva ninguno.
            if (!esUnionUtil
                && (char.IsControl(caracter) || CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.Format))
            {
                espacioPendiente = constructor.Length > 0;
                continue;
            }

            if (char.IsWhiteSpace(caracter))
            {
                espacioPendiente = constructor.Length > 0;
                continue;
            }

            if (espacioPendiente)
            {
                constructor.Append(' ');
                espacioPendiente = false;
            }

            constructor.Append(caracter);
        }

        return constructor.ToString();
    }
}
