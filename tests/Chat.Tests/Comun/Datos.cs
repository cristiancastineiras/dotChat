using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;

namespace Chat.Tests.Comun;

/// <summary>
/// Constructores de entidades y DTO para las pruebas. Cada uno deja el objeto en un
/// estado válido y admite sobrescribir solo lo que la prueba concreta necesita, de
/// modo que en el cuerpo del test se lea únicamente lo que importa.
/// </summary>
public static class Datos
{
    /// <summary>Instante de referencia fijo, para que nada dependa del reloj real.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 3, 14, 10, 30, 0, TimeSpan.Zero);

    /// <summary>Crea un usuario con los campos normalizados que espera Identity.</summary>
    /// <param name="id">Identificador; se genera uno si no se indica.</param>
    /// <param name="nombre">Nombre de usuario.</param>
    /// <param name="email">Correo electrónico; se deriva del nombre si no se indica.</param>
    /// <param name="activo">Indica si la cuenta está habilitada.</param>
    public static Usuario Usuario(
        Guid? id = null,
        string nombre = "ana",
        string? email = null,
        bool activo = true) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            UserName = nombre,
            NormalizedUserName = nombre.ToUpperInvariant(),
            Email = email ?? $"{nombre}@dotchat.local",
            NormalizedEmail = (email ?? $"{nombre}@dotchat.local").ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            FechaCreacion = Ahora,
            Activo = activo
        };

    /// <summary>Crea una sala.</summary>
    /// <param name="id">Identificador; se genera uno si no se indica.</param>
    /// <param name="nombre">Nombre de la sala.</param>
    /// <param name="tipo">Naturaleza de la sala.</param>
    /// <param name="creadorId">Usuario que la creó.</param>
    /// <param name="claveDirecta">Clave canónica, solo en las conversaciones directas.</param>
    public static Sala Sala(
        Guid? id = null,
        string nombre = "General",
        TipoSala tipo = TipoSala.Publica,
        Guid? creadorId = null,
        string? claveDirecta = null) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Nombre = nombre,
            Tipo = tipo,
            ClaveDirecta = claveDirecta,
            FechaCreacion = Ahora,
            CreadorId = creadorId
        };

    /// <summary>Crea una conversación directa ya formada entre dos usuarios.</summary>
    /// <param name="primero">Un participante.</param>
    /// <param name="segundo">El otro participante.</param>
    public static Sala SalaDirecta(Usuario primero, Usuario segundo)
    {
        ArgumentNullException.ThrowIfNull(primero);
        ArgumentNullException.ThrowIfNull(segundo);

        var clave = Chat.Dominio.Entidades.Sala.ConstruirClaveDirecta(primero.Id, segundo.Id);

        var sala = Sala(
            nombre: Chat.Dominio.Entidades.Sala.ConstruirNombreDirecto(clave),
            tipo: TipoSala.Directa,
            creadorId: primero.Id,
            claveDirecta: clave);

        sala.Miembros.Add(Membresia(sala.Id, primero.Id, primero));
        sala.Miembros.Add(Membresia(sala.Id, segundo.Id, segundo));

        return sala;
    }

    /// <summary>Crea una membresía de sala.</summary>
    /// <param name="salaId">Sala.</param>
    /// <param name="usuarioId">Usuario.</param>
    /// <param name="usuario">Usuario cargado, cuando la proyección lo necesita.</param>
    /// <param name="ultimaLectura">Marca de lectura.</param>
    public static MiembroSala Membresia(
        Guid salaId,
        Guid usuarioId,
        Usuario? usuario = null,
        DateTimeOffset? ultimaLectura = null) => new()
        {
            SalaId = salaId,
            UsuarioId = usuarioId,
            Usuario = usuario,
            FechaUnion = Ahora,
            FechaUltimaLectura = ultimaLectura
        };

    /// <summary>Crea un mensaje.</summary>
    /// <param name="salaId">Sala en la que se publicó.</param>
    /// <param name="usuarioId">Autor.</param>
    /// <param name="textoCifrado">Criptograma del texto; nulo si el mensaje era solo un archivo.</param>
    /// <param name="fechaEnvio">Fecha de envío.</param>
    /// <param name="adjuntoId">Adjunto publicado con el mensaje.</param>
    public static Mensaje Mensaje(
        Guid salaId,
        Guid usuarioId,
        string? textoCifrado = "cifrado",
        DateTimeOffset? fechaEnvio = null,
        Guid? adjuntoId = null) => new()
        {
            Id = Guid.CreateVersion7(),
            SalaId = salaId,
            UsuarioId = usuarioId,
            TextoCifrado = textoCifrado,
            AdjuntoId = adjuntoId,
            FechaEnvio = fechaEnvio ?? Ahora
        };

    /// <summary>Crea la ficha de un adjunto.</summary>
    /// <param name="salaId">Sala para la que se subió.</param>
    /// <param name="usuarioId">Quien lo subió.</param>
    /// <param name="id">Identificador; se genera uno si no se indica.</param>
    /// <param name="nombre">Nombre del fichero.</param>
    /// <param name="tipo">Naturaleza del contenido.</param>
    /// <param name="fechaCreacion">Fecha de subida.</param>
    public static Adjunto Adjunto(
        Guid salaId,
        Guid usuarioId,
        Guid? id = null,
        string nombre = "foto.png",
        TipoAdjunto tipo = TipoAdjunto.Imagen,
        DateTimeOffset? fechaCreacion = null)
    {
        var adjuntoId = id ?? Guid.CreateVersion7();
        var fecha = fechaCreacion ?? Ahora;

        return new Adjunto
        {
            Id = adjuntoId,
            SalaId = salaId,
            UsuarioId = usuarioId,
            NombreArchivo = nombre,
            TipoMime = tipo == TipoAdjunto.Imagen ? "image/png" : "application/octet-stream",
            Tipo = tipo,
            ClaveObjeto = Chat.Dominio.Entidades.Adjunto.ConstruirClave(salaId, adjuntoId, fecha),
            Ancho = tipo == TipoAdjunto.Imagen ? 640 : null,
            Alto = tipo == TipoAdjunto.Imagen ? 480 : null,
            TamanoBytes = 1024,
            Huella = new string('a', 64),
            FechaCreacion = fecha
        };
    }

    /// <summary>Crea un token de refresco.</summary>
    /// <param name="usuarioId">Propietario.</param>
    /// <param name="hash">Hash del token entregado al cliente.</param>
    /// <param name="expiracion">Fecha de caducidad.</param>
    /// <param name="revocacion">Fecha de revocación, si ya se usó.</param>
    public static TokenRefresco TokenRefresco(
        Guid usuarioId,
        string hash = "hash",
        DateTimeOffset? expiracion = null,
        DateTimeOffset? revocacion = null) => new()
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            HashToken = hash,
            FechaCreacion = Ahora,
            FechaExpiracion = expiracion ?? Ahora.AddDays(7),
            FechaRevocacion = revocacion
        };

    /// <summary>Crea la solicitud de envío de un mensaje.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="texto">Contenido.</param>
    /// <param name="adjuntoId">Adjunto que se publica.</param>
    /// <param name="identificadorEnvio">Identificador antirrepetición.</param>
    public static SolicitudEnviarMensajeDto SolicitudMensaje(
        Guid salaId,
        string texto = "hola",
        Guid? adjuntoId = null,
        Guid? identificadorEnvio = null)
        => new(salaId, texto, identificadorEnvio ?? Guid.CreateVersion7(), adjuntoId);
}
