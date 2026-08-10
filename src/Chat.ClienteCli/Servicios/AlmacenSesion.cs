using System.Runtime.InteropServices;
using System.Text.Json;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Guarda la sesión del usuario en el perfil local, de modo que no haya que
/// autenticarse en cada invocación del CLI.
/// </summary>
/// <remarks>
/// El fichero contiene tokens de acceso: se crea dentro del perfil del usuario y,
/// en sistemas tipo Unix, con permisos 600. Nunca se escribe en el repositorio.
/// </remarks>
public sealed class AlmacenSesion
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rutaFichero;

    /// <summary>Crea el almacén usando la carpeta por defecto del perfil del usuario.</summary>
    public AlmacenSesion() : this(RutaPorDefecto())
    {
    }

    /// <summary>Crea el almacén sobre una ruta concreta (útil en pruebas).</summary>
    /// <param name="rutaFichero">Ruta completa del fichero de sesión.</param>
    public AlmacenSesion(string rutaFichero) => _rutaFichero = rutaFichero;

    /// <summary>Ruta del fichero de sesión en uso.</summary>
    public string Ruta => _rutaFichero;

    /// <summary>Calcula la ruta por defecto: <c>~/.dotchat/sesion-cliente.json</c>.</summary>
    public static string RutaPorDefecto()
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotchat");

        return Path.Combine(carpeta, "sesion-cliente.json");
    }

    /// <summary>Lee la sesión almacenada, si existe y es legible.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La sesión o <c>null</c>.</returns>
    public async Task<SesionAlmacenada?> LeerAsync(CancellationToken cancelacion = default)
    {
        if (!File.Exists(_rutaFichero))
        {
            return null;
        }

        try
        {
            await using var flujo = File.OpenRead(_rutaFichero);
            return await JsonSerializer
                .DeserializeAsync<SesionAlmacenada>(flujo, OpcionesJson, cancelacion)
                .ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is JsonException or IOException)
        {
            // Un fichero corrupto equivale a no tener sesión: se fuerza un login nuevo.
            return null;
        }
    }

    /// <summary>Guarda la sesión, restringiendo los permisos del fichero.</summary>
    /// <param name="sesion">Sesión a persistir.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task GuardarAsync(SesionAlmacenada sesion, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        var carpeta = Path.GetDirectoryName(_rutaFichero);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        await using (var flujo = File.Create(_rutaFichero))
        {
            await JsonSerializer.SerializeAsync(flujo, sesion, OpcionesJson, cancelacion).ConfigureAwait(false);
        }

        RestringirPermisos(_rutaFichero);
    }

    /// <summary>Borra la sesión almacenada.</summary>
    public void Borrar()
    {
        if (File.Exists(_rutaFichero))
        {
            File.Delete(_rutaFichero);
        }
    }

    /// <summary>Deja el fichero accesible solo para su propietario en sistemas Unix.</summary>
    /// <param name="ruta">Ruta del fichero.</param>
    private static void RestringirPermisos(string ruta)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // En Windows el fichero hereda la ACL del perfil del usuario, que ya es privada.
            return;
        }

        File.SetUnixFileMode(ruta, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
